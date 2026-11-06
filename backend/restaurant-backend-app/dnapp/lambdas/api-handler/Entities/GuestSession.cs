using Amazon.DynamoDBv2.DataModel;
using APIHandler.Constants;
using System.Collections.Generic;

namespace APIHandler.Entities;

public class GuestSession
{
    [DynamoDBHashKey("guestSessionId")]
    public required string Id { get; set; }

    [DynamoDBProperty("groupReservationId")]
    [DynamoDBGlobalSecondaryIndexHashKey(GlobalSecondaryIndexes.GuestSession.GroupReservationId)]
    public string GroupReservationId { get; set; } = string.Empty;

    [DynamoDBProperty("guestName")]
    public required string GuestName { get; set; }

    [DynamoDBProperty("dishes")]
    public required List<OrderItem> Dishes { get; set; }

    [DynamoDBProperty("guestSessionTokenHash")]
    [DynamoDBGlobalSecondaryIndexHashKey(GlobalSecondaryIndexes.GuestSession.SessionTokenHash)]
    public string? SessionTokenHash { get; set; }

    [DynamoDBProperty("isOwnerSession")]
    public bool IsOwnerSession { get; set; }

    [DynamoDBProperty("ownerUserId")]
    public string? OwnerUserId { get; set; }

    [DynamoDBProperty("isSubmitted")]
    public bool IsSubmitted { get; set; }

    [DynamoDBProperty("orderingPreference")]
    public string? OrderingPreference { get; set; }

    [DynamoDBProperty("revision")]
    public long Revision { get; set; }

    [DynamoDBProperty("reminderEmail")]
    public string? ReminderEmail { get; set; }

    [DynamoDBProperty("reminderEmailConsent")]
    public bool ReminderEmailConsent { get; set; }

    [DynamoDBProperty("lastReminderAtUnixMs")]
    public long LastReminderAtUnixMs { get; set; }

    [DynamoDBProperty("reminderAttemptId")]
    public string? ReminderAttemptId { get; set; }
}
