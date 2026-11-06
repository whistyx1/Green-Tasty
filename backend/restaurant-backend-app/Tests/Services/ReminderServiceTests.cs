using APIHandler.Entities;
using APIHandler.Constants;
using APIHandler.Exceptions;
using APIHandler.Models.GuestSessions;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Reservations;
using APIHandler.Services.GuestSessions;
using Moq;

namespace APIHandler.Tests.Services;

public class ReminderServiceTests
{
    private readonly DateTimeOffset _now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    private readonly Mock<IGuestSessionRepository> _sessions = new();
    private readonly Mock<IGroupReservationRepository> _groups = new();
    private readonly Mock<IReservationRepository> _reservations = new();
    private readonly Mock<IReminderRepository> _reminders = new();
    private readonly Mock<IReminderEmailSender> _sender = new();
    private readonly GuestSession _session = new()
    {
        Id = "guest", GroupReservationId = "group", GuestName = "Guest", Dishes = []
    };

    private ReminderService Service(bool emailEnabled = true) => new(_sessions.Object, _groups.Object, _reservations.Object,
        _reminders.Object, _sender.Object, new FixedClock(_now), emailEnabled);
    private ReminderService Service(TimeProvider clock) => new(_sessions.Object, _groups.Object, _reservations.Object,
        _reminders.Object, _sender.Object, clock, true);

    private void Arrange(int minutesUntilStart = 45)
    {
        _groups.Setup(x => x.GetGroupReservationByIdAsync("group")).ReturnsAsync(new GroupReservation
        {
            GroupReservationId = "group", ReservationId = "reservation", ReservationToken = "token",
            Status = GroupReservationStatus.Open
        });
        _reservations.Setup(x => x.GetReservationByIdAsync("reservation")).ReturnsAsync(new Reservation
        {
            Id = "reservation", TableId = "table", WaiterId = "waiter", CustomerId = "owner",
            GuestCount = 2, StartTime = _now.AddMinutes(minutesUntilStart).UtcDateTime,
            EndTime = _now.AddMinutes(minutesUntilStart + 90).UtcDateTime, Status = ReservationStatus.Reserved
        });
        _sessions.Setup(x => x.GetByGuestSessionId("guest")).ReturnsAsync(_session);
    }

    [Theory]
    [InlineData(60, true)]
    [InlineData(45, true)]
    [InlineData(30, false)]
    [InlineData(61, false)]
    public async Task Send_UsesFinalSixtyToThirtyMinuteWindow(int minutesUntilStart, bool allowed)
    {
        Arrange(minutesUntilStart);
        if (allowed)
        {
            var result = await Service().SendAsync("owner", "group", "guest");
            Assert.Equal("InApp", result.Channel);
            Assert.Equal(_now.AddMinutes(15).UtcDateTime, result.NextAllowedAt);
            _reminders.Verify(x => x.ClaimAsync(_session, It.IsAny<GroupReservation>(),
                It.IsAny<Reservation>(), "owner", _now.ToUnixTimeMilliseconds(),
                It.IsAny<string>(), It.Is<Notification>(n => n.UserId == "guest-session:guest")), Times.Once);
        }
        else
        {
            await Assert.ThrowsAsync<GroupPreOrderConflictException>(() => Service().SendAsync("owner", "group", "guest"));
            _reminders.Verify(x => x.ClaimAsync(It.IsAny<GuestSession>(), It.IsAny<GroupReservation>(),
                It.IsAny<Reservation>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<Notification?>()), Times.Never);
        }
    }

    [Fact]
    public async Task Send_RejectsNonOwnerBeforeClaim()
    {
        Arrange();
        await Assert.ThrowsAsync<ActionForbiddenException>(() => Service().SendAsync("other", "group", "guest"));
        _reminders.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, "InRestaurant")]
    public async Task Send_RejectsSubmittedOrInRestaurantGuest(bool submitted, string? preference)
    {
        Arrange();
        _session.IsSubmitted = submitted;
        _session.OrderingPreference = preference;
        await Assert.ThrowsAsync<GroupPreOrderConflictException>(() => Service().SendAsync("owner", "group", "guest"));
        _reminders.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Send_EnforcesFifteenMinuteCooldown()
    {
        Arrange();
        _session.LastReminderAtUnixMs = _now.AddMinutes(-14).ToUnixTimeMilliseconds();
        await Assert.ThrowsAsync<GroupPreOrderConflictException>(() => Service().SendAsync("owner", "group", "guest"));
        _reminders.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Send_WithConsentedEmail_UsesEmailAndReportsFailureWithoutSuccess()
    {
        Arrange();
        _session.ReminderEmail = "guest@example.com";
        _session.ReminderEmailConsent = true;
        _reminders.Setup(x => x.ClaimAsync(_session, It.IsAny<GroupReservation>(), It.IsAny<Reservation>(),
            "owner", It.IsAny<long>(), It.IsAny<string>(), null))
            .Callback<GuestSession, GroupReservation, Reservation, string, long, string, Notification?>
                ((session, _, _, _, _, attemptId, _) => session.ReminderAttemptId = attemptId)
            .Returns(Task.CompletedTask);
        _sender.Setup(x => x.SendAsync("guest@example.com", "Guest"))
            .ThrowsAsync(new InvalidOperationException("SES unavailable"));

        await Assert.ThrowsAsync<ReminderDeliveryException>(() => Service().SendAsync("owner", "group", "guest"));
        _reminders.Verify(x => x.ClaimAsync(_session, It.IsAny<GroupReservation>(),
            It.IsAny<Reservation>(), "owner", It.IsAny<long>(), It.IsAny<string>(), null), Times.Once);
        _sender.Verify(x => x.SendAsync("guest@example.com", "Guest"), Times.Once);
    }

    [Fact]
    public async Task Send_EmailDisabled_CreatesInAppNotificationEvenForConsentedAddress()
    {
        Arrange();
        _session.ReminderEmail = "guest@example.com";
        _session.ReminderEmailConsent = true;

        var result = await Service(emailEnabled: false).SendAsync("owner", "group", "guest");

        Assert.Equal("InApp", result.Channel);
        _reminders.Verify(x => x.ClaimAsync(_session, It.IsAny<GroupReservation>(),
            It.IsAny<Reservation>(), "owner", It.IsAny<long>(), It.IsAny<string>(),
            It.Is<Notification>(n => n.UserId == "guest-session:guest")), Times.Once);
        _sender.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SetEmail_EmailDisabled_RejectsAddressCollection()
    {
        await Assert.ThrowsAsync<GroupPreOrderConflictException>(() =>
            Service(emailEnabled: false).SetEmailAsync("guest", new ReminderEmailRequest("guest@example.com", true)));
        _reminders.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Send_ConsentRemovedAfterClaim_DoesNotCallSes()
    {
        Arrange();
        _session.ReminderEmail = "guest@example.com";
        _session.ReminderEmailConsent = true;
        _reminders.Setup(x => x.ClaimAsync(_session, It.IsAny<GroupReservation>(), It.IsAny<Reservation>(),
            "owner", It.IsAny<long>(), It.IsAny<string>(), null))
            .Callback<GuestSession, GroupReservation, Reservation, string, long, string, Notification?>
                ((session, group, reservation, owner, time, attemptId, notification) =>
                {
                    session.ReminderAttemptId = attemptId;
                    session.ReminderEmailConsent = false;
                })
            .Returns(Task.CompletedTask);

        await Assert.ThrowsAsync<GroupPreOrderConflictException>(() => Service().SendAsync("owner", "group", "guest"));
        _sender.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Send_WindowClosesBeforeClaim_DoesNotPersistReminder()
    {
        Arrange();
        var clock = new SequenceClock(_now, _now.AddMinutes(15));
        await Assert.ThrowsAsync<GroupPreOrderConflictException>(() => Service(clock).SendAsync("owner", "group", "guest"));
        _reminders.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Send_WindowClosesAfterEmailClaim_DoesNotCallSes()
    {
        Arrange();
        _session.ReminderEmail = "guest@example.com";
        _session.ReminderEmailConsent = true;
        _reminders.Setup(x => x.ClaimAsync(_session, It.IsAny<GroupReservation>(), It.IsAny<Reservation>(),
            "owner", It.IsAny<long>(), It.IsAny<string>(), null))
            .Callback<GuestSession, GroupReservation, Reservation, string, long, string, Notification?>
                ((session, group, reservation, owner, time, attemptId, notification) => session.ReminderAttemptId = attemptId)
            .Returns(Task.CompletedTask);
        var clock = new SequenceClock(_now, _now, _now.AddMinutes(15));
        await Assert.ThrowsAsync<GroupPreOrderConflictException>(() => Service(clock).SendAsync("owner", "group", "guest"));
        _sender.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Send_ReservationTimeEditedAfterClaim_DoesNotCallSes()
    {
        Arrange();
        _session.ReminderEmail = "guest@example.com";
        _session.ReminderEmailConsent = true;
        var reads = 0;
        _reservations.Setup(x => x.GetReservationByIdAsync("reservation"))
            .ReturnsAsync(() => new Reservation
            {
                Id = "reservation", TableId = "table", WaiterId = "waiter", CustomerId = "owner",
                GuestCount = 2, StartTime = _now.AddMinutes(++reads == 1 ? 45 : 90).UtcDateTime,
                EndTime = _now.AddMinutes(180).UtcDateTime, Status = ReservationStatus.Reserved
            });
        _reminders.Setup(x => x.ClaimAsync(_session, It.IsAny<GroupReservation>(), It.IsAny<Reservation>(),
            "owner", It.IsAny<long>(), It.IsAny<string>(), null))
            .Callback<GuestSession, GroupReservation, Reservation, string, long, string, Notification?>
                ((session, group, reservation, owner, time, attemptId, notification) => session.ReminderAttemptId = attemptId)
            .Returns(Task.CompletedTask);

        await Assert.ThrowsAsync<GroupPreOrderConflictException>(() => Service().SendAsync("owner", "group", "guest"));
        _sender.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Email_CanBeUpdatedAndRemovedWithExplicitConsent()
    {
        _sessions.Setup(x => x.GetByGuestSessionId("guest")).ReturnsAsync(_session);
        var service = Service();
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.SetEmailAsync("guest", new ReminderEmailRequest("guest@example.com", false)));
        var set = await service.SetEmailAsync("guest", new ReminderEmailRequest("guest@example.com", true));
        var removed = await service.RemoveEmailAsync("guest");
        Assert.Equal("guest@example.com", set.Email);
        Assert.True(set.Consent);
        Assert.Equal(1, set.Revision);
        Assert.Null(removed.Email);
        Assert.False(removed.Consent);
        Assert.Equal(1, removed.Revision);
        _reminders.Verify(x => x.SetEmailAsync(_session, "guest@example.com", true), Times.Once);
        _reminders.Verify(x => x.SetEmailAsync(_session, null, false), Times.Once);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class SequenceClock(params DateTimeOffset[] times) : TimeProvider
    {
        private int _index;
        public override DateTimeOffset GetUtcNow() => times[Math.Min(Interlocked.Increment(ref _index) - 1, times.Length - 1)];
    }
}
