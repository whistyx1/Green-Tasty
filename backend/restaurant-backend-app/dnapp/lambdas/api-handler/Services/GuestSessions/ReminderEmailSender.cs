using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using System;
using System.Threading.Tasks;

namespace APIHandler.Services.GuestSessions;

public class ReminderEmailSender(IAmazonSimpleEmailService ses) : IReminderEmailSender
{
    public async Task SendAsync(string to, string guestName)
    {
        var from = Environment.GetEnvironmentVariable("EmailFrom");
        if (string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException("Reminder sender is not configured.");

        await ses.SendEmailAsync(new SendEmailRequest
        {
            Source = from,
            Destination = new Destination { ToAddresses = [to] },
            Message = new Message
            {
                Subject = new Content { Data = "Green Tasty pre-order reminder", Charset = "UTF-8" },
                Body = new Body { Text = new Content
                {
                    Data = $"Hello {guestName}, your online group pre-order is still pending. Please open your existing Green Tasty invitation link and submit it before ordering closes.",
                    Charset = "UTF-8"
                } }
            }
        });
    }
}
