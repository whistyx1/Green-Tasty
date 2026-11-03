using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Exceptions;
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

    public async Task CreateAsync(GuestSession guestSession) => await context.SaveAsync(guestSession, new SaveConfig
    {
        OverrideTableName = ResourceNames.GuestSessionTableName
    });

    public async Task SaveAsync(GuestSession guestSession)
    {
        var item = context.ToDocument(guestSession,
            new ToDocumentConfig { OverrideTableName = ResourceNames.GuestSessionTableName }).ToAttributeMap();
        try
        {
            await client.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = ResourceNames.GuestSessionTableName,
                Key = new() { ["guestSessionId"] = item["guestSessionId"] },
                UpdateExpression = "SET #dishes = :dishes, #submitted = :submitted",
                ConditionExpression = "attribute_exists(#id) AND #group = :group",
                ExpressionAttributeNames = new()
                {
                    ["#id"] = "guestSessionId", ["#group"] = "groupReservationId",
                    ["#dishes"] = "dishes", ["#submitted"] = "isSubmitted",
                },
                ExpressionAttributeValues = new()
                {
                    [":group"] = item["groupReservationId"],
                    [":dishes"] = item["dishes"],
                    [":submitted"] = item["isSubmitted"],
                },
            });
        }
        catch (ConditionalCheckFailedException)
        {
            throw new GuestSessionNotFoundException("Guest session was removed; reload the invitation.");
        }
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
