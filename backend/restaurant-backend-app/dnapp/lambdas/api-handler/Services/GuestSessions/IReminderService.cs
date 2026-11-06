using APIHandler.Models.GuestSessions;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APIHandler.Services.GuestSessions;

public interface IReminderService
{
    Task<ReminderResponse> SendAsync(string ownerId, string groupId, string sessionId);
    Task<ReminderEmailResponse> SetEmailAsync(string sessionId, ReminderEmailRequest request);
    Task<ReminderEmailResponse> RemoveEmailAsync(string sessionId);
    Task<IReadOnlyList<GuestReminderNotification>> GetNotificationsAsync(string sessionId);
}
