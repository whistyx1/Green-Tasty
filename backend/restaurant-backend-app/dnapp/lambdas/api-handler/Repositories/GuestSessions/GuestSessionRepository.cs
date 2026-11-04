using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GuestSessions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIHandler.Repositories.GuestSessions;

public class GuestSessionRepository(IDynamoDBContext context, IAmazonDynamoDB client) : IGuestSessionRepository
{
    public async Task<GuestSession?> GetByGuestSessionId(string id) => await context.LoadAsync<GuestSession?>(id, new LoadConfig()
    {
        OverrideTableName = ResourceNames.GuestSessionTableName,
        ConsistentRead = true
    });

    public async Task<GuestSession?> GetByGroupReservationId(string groupReservationId)
    {
        return (await GetAllByGroupReservationIdAsync(groupReservationId)).FirstOrDefault();
    }

    public async Task<GuestSession?> GetByTokenHash(string tokenHash)
    {
        List<GuestSession> candidates = await context.QueryAsync<GuestSession>(tokenHash, new QueryConfig
        {
            OverrideTableName = ResourceNames.GuestSessionTableName,
            IndexName = GlobalSecondaryIndexes.GuestSession.SessionTokenHash
        }).GetRemainingAsync();

        foreach (var candidate in candidates)
        {
            var current = await GetByGuestSessionId(candidate.Id);
            if (current?.SessionTokenHash == tokenHash)
                return current;
        }
        return null;
    }

    public async Task<List<GuestSession>> GetAllByGroupReservationIdAsync(string groupReservationId)
    {
        var candidates = await context.QueryAsync<GuestSession>(groupReservationId, new QueryConfig
        {
            OverrideTableName = ResourceNames.GuestSessionTableName,
            IndexName = GlobalSecondaryIndexes.GuestSession.GroupReservationId
        }).GetRemainingAsync();
        // The index can briefly retain a deleted guest. Check the base table
        // before showing a participant or counting them toward capacity.
        var current = await Task.WhenAll(candidates.Select(candidate => GetByGuestSessionId(candidate.Id)));
        return current.Where(session => session is not null && session.GroupReservationId == groupReservationId)
            .Select(session => session!).ToList();
    }

    public async Task<List<GuestSession>> GetAllByGroupReservationIdConsistentAsync(string groupReservationId)
    {
        var result = new List<GuestSession>();
        Dictionary<string, AttributeValue>? cursor = null;
        do
        {
            var page = await client.ScanAsync(new ScanRequest
            {
                TableName = ResourceNames.GuestSessionTableName,
                ConsistentRead = true,
                FilterExpression = "groupReservationId = :group",
                ProjectionExpression = "guestSessionId",
                ExpressionAttributeValues = new() { [":group"] = new AttributeValue { S = groupReservationId } },
                ExclusiveStartKey = cursor
            });
            foreach (var item in page.Items)
            {
                var session = await GetByGuestSessionId(item["guestSessionId"].S);
                if (session?.GroupReservationId == groupReservationId) result.Add(session);
            }
            cursor = page.LastEvaluatedKey;
        } while (cursor is { Count: > 0 });
        return result;
    }

    public async Task CreateAsync(GuestSession guestSession)
    {
        var item = context.ToDocument(guestSession,
            new ToDocumentConfig { OverrideTableName = ResourceNames.GuestSessionTableName }).ToAttributeMap();
        try
        {
            await client.TransactWriteItemsAsync(new TransactWriteItemsRequest { TransactItems = new()
            {
                new() { Put = new Put { TableName = ResourceNames.GuestSessionTableName, Item = item,
                    ConditionExpression = "attribute_not_exists(guestSessionId)" } },
                new() { Update = new Update { TableName = ResourceNames.GroupReservationsTableName,
                    Key = new() { ["groupReservationId"] = item["groupReservationId"] },
                    UpdateExpression = "SET #revision = if_not_exists(#revision, :zero) + :one",
                    ConditionExpression = "#status = :open",
                    ExpressionAttributeNames = new() { ["#revision"] = "participantRevision", ["#status"] = "status" },
                    ExpressionAttributeValues = new() { [":zero"] = new AttributeValue { N = "0" },
                        [":one"] = new AttributeValue { N = "1" }, [":open"] = new AttributeValue { N = "0" } } } }
            } });
        }
        catch (TransactionCanceledException ex) when (IsConflict(ex))
        { throw new GroupPreOrderConflictException("The group changed; reload before joining."); }
        catch (TransactionConflictException)
        { throw new GroupPreOrderConflictException("The group is being changed; retry joining."); }
    }

    public async Task SaveAsync(GuestSession guestSession)
    {
        var item = context.ToDocument(guestSession,
            new ToDocumentConfig { OverrideTableName = ResourceNames.GuestSessionTableName }).ToAttributeMap();
        try
        {
            var names = SessionNames();
            var values = SessionValues(item, guestSession.Revision);
            values[":dishes"] = item["dishes"];
            values[":submitted"] = new AttributeValue { N = guestSession.IsSubmitted ? "1" : "0" };
            values[":online"] = new AttributeValue { S = OrderingPreference.Online };
            await WriteWithOpenGroupAsync(item, guestSession.GroupReservationId,
                "SET #dishes = :dishes, #submitted = :submitted, #pref = if_not_exists(#pref, :online), #rev = if_not_exists(#rev, :zero) + :one",
                BaseCondition(guestSession.Revision) +
                (guestSession.IsSubmitted ? " AND (attribute_not_exists(#pref) OR #pref = :online) AND attribute_exists(#dishes)" : ""),
                names, values);
        }
        catch (TransactionCanceledException ex) when (IsConflict(ex))
        {
            throw new GroupPreOrderConflictException("Guest session or group changed; reload before trying again.");
        }
        catch (TransactionConflictException) { throw new GroupPreOrderConflictException("Guest session is being changed; retry after reloading."); }
    }

    public async Task SetPreferenceAsync(GuestSession session, string preference)
    {
        var item = context.ToDocument(session,
            new ToDocumentConfig { OverrideTableName = ResourceNames.GuestSessionTableName }).ToAttributeMap();
        var names = SessionNames();
        names.Remove("#dishes");
        var values = SessionValues(item, session.Revision);
        values[":preference"] = new AttributeValue { S = preference };
        try
        {
            await WriteWithOpenGroupAsync(item, session.GroupReservationId,
                "SET #pref = :preference, #rev = if_not_exists(#rev, :zero) + :one",
                BaseCondition(session.Revision), names, values);
        }
        catch (TransactionCanceledException ex) when (IsConflict(ex)) { throw new GroupPreOrderConflictException("Guest session or group changed; reload before trying again."); }
        catch (TransactionConflictException) { throw new GroupPreOrderConflictException("Guest session is being changed; retry after reloading."); }
    }

    private static Dictionary<string, string> SessionNames() => new()
    {
        ["#id"] = "guestSessionId", ["#group"] = "groupReservationId", ["#dishes"] = "dishes",
        ["#submitted"] = "isSubmitted", ["#rev"] = "revision", ["#pref"] = "orderingPreference"
    };

    private static Dictionary<string, AttributeValue> SessionValues(Dictionary<string, AttributeValue> item, long revision) => new()
    {
        [":group"] = item["groupReservationId"], [":false"] = new AttributeValue { N = "0" },
        [":expected"] = new AttributeValue { N = revision.ToString(System.Globalization.CultureInfo.InvariantCulture) },
        [":zero"] = new AttributeValue { N = "0" }, [":one"] = new AttributeValue { N = "1" }
    };

    private static string BaseCondition(long revision) =>
        "attribute_exists(#id) AND #group = :group AND (attribute_not_exists(#submitted) OR #submitted = :false) AND " +
        (revision == 0 ? "(attribute_not_exists(#rev) OR #rev = :expected)" : "#rev = :expected");

    private static bool IsConflict(TransactionCanceledException ex) =>
        ex.CancellationReasons?.Any(reason => reason.Code is "ConditionalCheckFailed" or "TransactionConflict") == true;

    private async Task WriteWithOpenGroupAsync(Dictionary<string, AttributeValue> item, string groupId,
        string updateExpression, string condition, Dictionary<string, string> names,
        Dictionary<string, AttributeValue> values)
    {
        await client.TransactWriteItemsAsync(new TransactWriteItemsRequest { TransactItems = new()
        {
            new() { Update = new Update { TableName = ResourceNames.GuestSessionTableName,
                Key = new() { ["guestSessionId"] = item["guestSessionId"] },
                UpdateExpression = updateExpression, ConditionExpression = condition,
                ExpressionAttributeNames = names, ExpressionAttributeValues = values } },
            new() { Update = new Update { TableName = ResourceNames.GroupReservationsTableName,
                Key = new() { ["groupReservationId"] = new AttributeValue { S = groupId } },
                UpdateExpression = "SET #status = :open",
                ConditionExpression = "#status = :open",
                ExpressionAttributeNames = new() { ["#status"] = "status" },
                ExpressionAttributeValues = new() { [":open"] = new AttributeValue { N = "0" } } } }
        } });
    }

    public async Task<int> CountByGroupReservationId(string groupReservationId)
    {
        var result = await GetAllByGroupReservationIdAsync(groupReservationId);
        return result.Count(session => !session.IsOwnerSession);
    }

    public async Task DeleteAsync(GuestSession session)
    {
        await context.DeleteAsync(session, DynamoConfig.DeleteConfig(ResourceNames.GuestSessionTableName));
    }
}
