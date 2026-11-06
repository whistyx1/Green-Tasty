using System.Threading.Tasks;

namespace APIHandler.Services.GuestSessions;

public interface IReminderEmailSender
{
    Task SendAsync(string to, string guestName);
}
