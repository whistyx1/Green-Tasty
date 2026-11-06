using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GuestSessions;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Reservations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;

namespace APIHandler.Services.GuestSessions;

public class ReminderService(
    IGuestSessionRepository sessions,
    IGroupReservationRepository groups,
    IReservationRepository reservations,
    IReminderRepository reminders,
    IReminderEmailSender emailSender,
    TimeProvider? clock = null,
    bool? emailDeliveryEnabled = null) : IReminderService
{
    private static readonly TimeSpan WindowOpens = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan WindowCloses = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(15);
    private readonly bool _emailDeliveryEnabled = emailDeliveryEnabled ??
        string.Equals(Environment.GetEnvironmentVariable("ReminderEmailEnabled"), "true", StringComparison.OrdinalIgnoreCase);

    public async Task<ReminderResponse> SendAsync(string ownerId, string groupId, string sessionId)
    {
        var group = await groups.GetGroupReservationByIdAsync(groupId)
            ?? throw new GroupReservationNotFoundException();
        var reservation = await reservations.GetReservationByIdAsync(group.ReservationId)
            ?? throw new ReservationNotFoundException("Reservation was not found.");

        if (string.IsNullOrWhiteSpace(ownerId) || !string.Equals(reservation.CustomerId, ownerId, StringComparison.Ordinal))
            throw new ActionForbiddenException("Only the reservation owner can send reminders.");

        var now = (clock ?? TimeProvider.System).GetUtcNow();
        var start = new DateTimeOffset(reservation.StartTime.ToUniversalTime());
        if (reservation.Status != ReservationStatus.Reserved || group.Status != GroupReservationStatus.Open ||
            group.OnlinePreOrderClosed || !InWindow(now, start))
            throw new GroupPreOrderConflictException("Reminders are available only during the active pre-order reminder window.");

        var session = await sessions.GetByGuestSessionId(sessionId)
            ?? throw new GuestSessionNotFoundException("Guest session was not found.");
        if (session.GroupReservationId != groupId || session.IsOwnerSession || session.IsSubmitted ||
            OrderingPreference.Effective(session.OrderingPreference) != OrderingPreference.Online)
            throw new GroupPreOrderConflictException("This participant cannot receive a pre-order reminder.");

        now = (clock ?? TimeProvider.System).GetUtcNow();
        if (!InWindow(now, start))
            throw new GroupPreOrderConflictException("The pre-order reminder window has closed.");
        var nowUnixMs = now.ToUnixTimeMilliseconds();
        if (session.LastReminderAtUnixMs > nowUnixMs - (long)Cooldown.TotalMilliseconds)
            throw new GroupPreOrderConflictException("A reminder was sent recently. Try again after the cooldown.");

        var usableEmail = _emailDeliveryEnabled && session.ReminderEmailConsent && IsUsableEmail(session.ReminderEmail)
            ? session.ReminderEmail : null;
        var channel = usableEmail is null ? "InApp" : "Email";
        var attemptId = Guid.NewGuid().ToString("N");
        var notification = usableEmail is null ? new Notification
        {
            Id = Guid.NewGuid().ToString("N"),
            UserId = ReminderRepository.GuestRecipientPrefix + session.Id,
            Message = "Your online group pre-order is still pending. Please submit it before ordering closes.",
            ReservationId = reservation.Id,
            CreatedAt = now.UtcDateTime
        } : null;

        await reminders.ClaimAsync(session, group, reservation, ownerId, nowUnixMs, attemptId, notification);
        if (usableEmail is not null)
        {
            var latest = await sessions.GetByGuestSessionId(sessionId);
            var latestGroup = await groups.GetGroupReservationByIdAsync(groupId);
            var latestReservation = await reservations.GetReservationByIdAsync(reservation.Id);
            if (latest is null || latest.ReminderAttemptId != attemptId || !latest.ReminderEmailConsent ||
                !string.Equals(latest.ReminderEmail, usableEmail, StringComparison.Ordinal) || latest.IsSubmitted ||
                OrderingPreference.Effective(latest.OrderingPreference) != OrderingPreference.Online ||
                latestGroup?.Status != GroupReservationStatus.Open || latestGroup.OnlinePreOrderClosed ||
                latestGroup.ReservationId != reservation.Id ||
                latestReservation?.Status != ReservationStatus.Reserved ||
                latestReservation.StartTime.ToUniversalTime() != reservation.StartTime.ToUniversalTime() ||
                !InWindow((clock ?? TimeProvider.System).GetUtcNow(), start))
                throw new GroupPreOrderConflictException("The guest or reservation changed; no email was sent.");
            try
            {
                await emailSender.SendAsync(usableEmail, session.GuestName);
            }
            catch (Exception)
            {
                // An SES timeout can occur after acceptance. Retain the claim so
                // a retry cannot silently send a duplicate message.
                throw new ReminderDeliveryException();
            }
        }

        return new ReminderResponse(channel, now.Add(Cooldown).UtcDateTime);
    }

    public async Task<ReminderEmailResponse> SetEmailAsync(string sessionId, ReminderEmailRequest request)
    {
        if (!_emailDeliveryEnabled)
            throw new GroupPreOrderConflictException("Email reminders are not available yet.");
        if (!request.Consent || !IsUsableEmail(request.Email))
            throw new ValidationException("A valid email and explicit reminder consent are required.");
        var session = await LoadGuestAsync(sessionId);
        var normalized = request.Email!.Trim();
        await reminders.SetEmailAsync(session, normalized, true);
        return new ReminderEmailResponse(normalized, true, session.Revision + 1);
    }

    public async Task<ReminderEmailResponse> RemoveEmailAsync(string sessionId)
    {
        var session = await LoadGuestAsync(sessionId);
        await reminders.SetEmailAsync(session, null, false);
        return new ReminderEmailResponse(null, false, session.Revision + 1);
    }

    public async Task<IReadOnlyList<GuestReminderNotification>> GetNotificationsAsync(string sessionId)
    {
        await LoadGuestAsync(sessionId);
        var notifications = await reminders.GetNotificationsAsync(sessionId);
        return notifications.Select(n => new GuestReminderNotification(n.Id, n.Message, n.CreatedAt, n.IsRead)).ToArray();
    }

    private async Task<GuestSession> LoadGuestAsync(string sessionId)
    {
        var session = await sessions.GetByGuestSessionId(sessionId)
            ?? throw new GuestSessionNotFoundException("Guest session was not found.");
        if (session.IsOwnerSession) throw new ActionForbiddenException("This action is for invited guests only.");
        return session;
    }

    private static bool IsUsableEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 254 || !MailAddress.TryCreate(value.Trim(), out var address))
            return false;
        return string.Equals(address.Address, value.Trim(), StringComparison.Ordinal);
    }

    private static bool InWindow(DateTimeOffset now, DateTimeOffset start) =>
        now >= start - WindowOpens && now < start - WindowCloses;
}

public class ReminderDeliveryException : Exception
{
    public ReminderDeliveryException() : base("The reminder email could not be confirmed as accepted. Please try again after the cooldown.") { }
}
