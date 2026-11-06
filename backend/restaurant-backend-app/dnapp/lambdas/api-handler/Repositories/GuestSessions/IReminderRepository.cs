using APIHandler.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APIHandler.Repositories.GuestSessions;

public interface IReminderRepository
{
    Task SetEmailAsync(GuestSession session, string? email, bool consent);
    Task ClaimAsync(GuestSession session, GroupReservation group, Reservation reservation,
        string ownerId, long nowUnixMs, string attemptId, Notification? notification);
    Task<IReadOnlyList<Notification>> GetNotificationsAsync(string sessionId);
}
