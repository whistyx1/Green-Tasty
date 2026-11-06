using System;

namespace APIHandler.Models.GuestSessions;

public record ReminderEmailRequest(string? Email, bool Consent);
public record ReminderEmailResponse(string? Email, bool Consent, long Revision);
public record ReminderResponse(string Channel, DateTime NextAllowedAt);
public record GuestReminderNotification(string Id, string Message, DateTime CreatedAt, bool IsRead);
