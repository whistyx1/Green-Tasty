using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Exceptions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace APIHandler.Repositories.GuestSessions;

public class ReminderRepository(IDynamoDBContext context, IAmazonDynamoDB client) : IReminderRepository
{
    public const string GuestRecipientPrefix = "guest-session:";

    public async Task SetEmailAsync(GuestSession session, string? email, bool consent)
    {
        var values = new Dictionary<string, AttributeValue>
        {
            [":group"] = new() { S = session.GroupReservationId },
            [":revision"] = Number(session.Revision),
            [":zero"] = Number(0),
            [":one"] = Number(1),
            [":false"] = Number(0),
            [":consent"] = Number(consent ? 1 : 0)
        };
        if (email is not null) values[":email"] = new AttributeValue { S = email };

        try
        {
            await client.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = ResourceNames.GuestSessionTableName,
                Key = new() { ["guestSessionId"] = new AttributeValue { S = session.Id } },
                UpdateExpression = email is null
                    ? "SET reminderEmailConsent = :consent, #rev = if_not_exists(#rev, :zero) + :one REMOVE reminderEmail"
                    : "SET reminderEmail = :email, reminderEmailConsent = :consent, #rev = if_not_exists(#rev, :zero) + :one",
                ConditionExpression = "groupReservationId = :group AND (attribute_not_exists(isOwnerSession) OR isOwnerSession = :false) AND " + RevisionCondition(session.Revision),
                ExpressionAttributeNames = new() { ["#rev"] = "revision" },
                ExpressionAttributeValues = values
            });
        }
        catch (ConditionalCheckFailedException)
        {
            throw new GroupPreOrderConflictException("Guest details changed; reload before trying again.");
        }
    }

    public async Task ClaimAsync(GuestSession session, GroupReservation group, Reservation reservation,
        string ownerId, long nowUnixMs, string attemptId, Notification? notification)
    {
        var reservationItem = context.ToDocument(reservation,
            new ToDocumentConfig { OverrideTableName = ResourceNames.ReservationsTableName }).ToAttributeMap();
        var cutoff = nowUnixMs - (long)TimeSpan.FromMinutes(15).TotalMilliseconds;
        var writes = new List<TransactWriteItem>
        {
            new() { Update = new Update
            {
                TableName = ResourceNames.GuestSessionTableName,
                Key = new() { ["guestSessionId"] = new AttributeValue { S = session.Id } },
                UpdateExpression = "SET lastReminderAtUnixMs = :now, reminderAttemptId = :attempt, #rev = if_not_exists(#rev, :zero) + :one",
                ConditionExpression = "groupReservationId = :group AND (attribute_not_exists(isOwnerSession) OR isOwnerSession = :false) " +
                    "AND (attribute_not_exists(isSubmitted) OR isSubmitted = :false) " +
                    "AND (attribute_not_exists(orderingPreference) OR orderingPreference = :online) " +
                    "AND (attribute_not_exists(lastReminderAtUnixMs) OR lastReminderAtUnixMs <= :cutoff) AND " + RevisionCondition(session.Revision),
                ExpressionAttributeNames = new() { ["#rev"] = "revision" },
                ExpressionAttributeValues = new()
                {
                    [":group"] = new AttributeValue { S = group.GroupReservationId },
                    [":false"] = Number(0), [":online"] = new AttributeValue { S = "Online" },
                    [":now"] = Number(nowUnixMs), [":cutoff"] = Number(cutoff),
                    [":attempt"] = new AttributeValue { S = attemptId },
                    [":revision"] = Number(session.Revision), [":zero"] = Number(0), [":one"] = Number(1)
                }
            } },
            new() { Update = new Update
            {
                TableName = ResourceNames.GroupReservationsTableName,
                Key = new() { ["groupReservationId"] = new AttributeValue { S = group.GroupReservationId } },
                UpdateExpression = "SET #participantRevision = if_not_exists(#participantRevision, :zero) + :one",
                ConditionExpression = "#status = :open AND reservationId = :reservation AND (attribute_not_exists(onlinePreOrderClosed) OR onlinePreOrderClosed = :false)",
                ExpressionAttributeNames = new() { ["#status"] = "status", ["#participantRevision"] = "participantRevision" },
                ExpressionAttributeValues = new()
                {
                    [":open"] = Number((int)GroupReservationStatus.Open),
                    [":reservation"] = new AttributeValue { S = reservation.Id },
                    [":false"] = Number(0), [":zero"] = Number(0), [":one"] = Number(1)
                }
            } },
            new() { ConditionCheck = new ConditionCheck
            {
                TableName = ResourceNames.ReservationsTableName,
                Key = new() { ["id"] = reservationItem["id"] },
                ConditionExpression = "#status = :reserved AND customerId = :owner AND startTime = :start",
                ExpressionAttributeNames = new() { ["#status"] = "status" },
                ExpressionAttributeValues = new()
                {
                    [":reserved"] = reservationItem["status"], [":owner"] = new AttributeValue { S = ownerId },
                    [":start"] = reservationItem["startTime"]
                }
            } }
        };
        if (notification is not null)
        {
            var item = context.ToDocument(notification,
                new ToDocumentConfig { OverrideTableName = ResourceNames.NotificationsTableName }).ToAttributeMap();
            writes.Add(new TransactWriteItem { Put = new Put
            {
                TableName = ResourceNames.NotificationsTableName, Item = item,
                ConditionExpression = "attribute_not_exists(id)"
            } });
        }

        try
        {
            await client.TransactWriteItemsAsync(new TransactWriteItemsRequest { TransactItems = writes });
        }
        catch (TransactionCanceledException ex) when (ex.CancellationReasons?.Any(r => r.Code is "ConditionalCheckFailed" or "TransactionConflict") == true)
        {
            throw new GroupPreOrderConflictException("The guest or group changed, or the reminder cooldown is active. Reload and try again.");
        }
        catch (TransactionConflictException)
        {
            throw new GroupPreOrderConflictException("The guest or group is changing. Reload and try again.");
        }
    }

    public async Task<IReadOnlyList<Notification>> GetNotificationsAsync(string sessionId)
    {
        var candidates = await context.QueryAsync<Notification>(GuestRecipientPrefix + sessionId, new QueryConfig
        {
            OverrideTableName = ResourceNames.NotificationsTableName,
            IndexName = ResourceNames.NotificationsUserIdIndex
        }).GetRemainingAsync();
        return candidates.Where(n => n.UserId == GuestRecipientPrefix + sessionId)
            .OrderByDescending(n => n.CreatedAt).ToArray();
    }

    private static AttributeValue Number(long value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };

    private static string RevisionCondition(long revision) => revision == 0
        ? "(attribute_not_exists(#rev) OR #rev = :revision)"
        : "#rev = :revision";
}
