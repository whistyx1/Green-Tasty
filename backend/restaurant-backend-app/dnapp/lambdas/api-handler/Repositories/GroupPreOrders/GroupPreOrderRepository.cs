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
namespace APIHandler.Repositories.GroupPreOrders;

public class GroupPreOrderRepository(IAmazonDynamoDB client, IDynamoDBContext context) : IGroupPreOrderRepository
{
    public async Task SetOwnerPreferenceAsync(GuestSession owner, GroupReservation group, Reservation reservation,
        Order? order, string preference)
    {
        var writes = OwnerBaseWrites(owner, group, reservation,
            "SET #pref = :preference, #rev = if_not_exists(#rev, :zero) + :one",
            new() { [":preference"] = new AttributeValue { S = preference } });
        if (order != null)
        {
            var orderItem = ToOrderAttributes(order);
            writes.Add(new() { Update = new Update {
                TableName = ResourceNames.OrdersTableName, Key = OrderKey(orderItem),
                UpdateExpression = "SET #ready = if_not_exists(#ready, :false)",
                ConditionExpression = "#status = :draft AND reservationId = :reservation AND (attribute_not_exists(#ready) OR #ready = :false) AND " + RevisionCondition(order.Revision),
                ExpressionAttributeNames = new() { ["#ready"] = "ownerGroupReady", ["#status"] = "status", ["#rev"] = "revision" },
                ExpressionAttributeValues = new() { [":false"] = new AttributeValue { N = "0" }, [":draft"] = orderItem["status"],
                    [":reservation"] = orderItem["reservationId"], [":expected"] = orderItem["revision"] }
            } });
        }
        await WriteOwnerAsync(writes);
    }

    public async Task SubmitOwnerAsync(GuestSession owner, GroupReservation group, Reservation reservation, Order order)
    {
        var writes = OwnerBaseWrites(owner, group, reservation,
            "SET #submitted = :true, #rev = if_not_exists(#rev, :zero) + :one",
            new() { [":true"] = new AttributeValue { N = "1" } }, requireOnline: true);
        var orderItem = ToOrderAttributes(order);
        writes.Add(new() { Update = new Update {
            TableName = ResourceNames.OrdersTableName, Key = OrderKey(orderItem),
            UpdateExpression = "SET #ready = :true, #rev = if_not_exists(#rev, :zero) + :one",
            ConditionExpression = "#status = :draft AND reservationId = :reservation AND dishes = :dishes AND (attribute_not_exists(#ready) OR #ready = :false) AND (attribute_not_exists(#finalized) OR #finalized = :false) AND " + RevisionCondition(order.Revision),
            ExpressionAttributeNames = new() { ["#ready"] = "ownerGroupReady", ["#status"] = "status", ["#rev"] = "revision", ["#finalized"] = "groupFinalized" },
            ExpressionAttributeValues = new() { [":true"] = new AttributeValue { N = "1" }, [":false"] = new AttributeValue { N = "0" },
                [":draft"] = orderItem["status"], [":reservation"] = orderItem["reservationId"],
                [":dishes"] = orderItem["dishes"], [":expected"] = orderItem["revision"], [":zero"] = new AttributeValue { N = "0" }, [":one"] = new AttributeValue { N = "1" } }
        } });
        await WriteOwnerAsync(writes);
    }

    private List<TransactWriteItem> OwnerBaseWrites(GuestSession owner, GroupReservation group, Reservation reservation,
        string updateExpression, Dictionary<string, AttributeValue> extraValues, bool requireOnline = false)
    {
        var ownerItem = context.ToDocument(owner, new ToDocumentConfig { OverrideTableName = ResourceNames.GuestSessionTableName }).ToAttributeMap();
        var reservationItem = context.ToDocument(reservation, new ToDocumentConfig { OverrideTableName = ResourceNames.ReservationsTableName }).ToAttributeMap();
        var values = new Dictionary<string, AttributeValue> {
            [":group"] = new AttributeValue { S = group.GroupReservationId }, [":owner"] = new AttributeValue { S = reservation.CustomerId },
            [":false"] = new AttributeValue { N = "0" },
            [":expected"] = new AttributeValue { N = owner.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture) },
            [":zero"] = new AttributeValue { N = "0" }, [":one"] = new AttributeValue { N = "1" }
        };
        foreach (var (key, value) in extraValues) values[key] = value;
        var condition = "isOwnerSession = :ownerSession AND ownerUserId = :owner AND groupReservationId = :group AND (attribute_not_exists(#submitted) OR #submitted = :false) AND " +
            (owner.Revision == 0 ? "(attribute_not_exists(#rev) OR #rev = :expected)" : "#rev = :expected");
        values[":ownerSession"] = new AttributeValue { N = "1" };
        if (requireOnline) {
            condition += " AND (attribute_not_exists(#pref) OR #pref = :online)";
            values[":online"] = new AttributeValue { S = OrderingPreference.Online };
        }
        return new() {
            new() { Update = new Update { TableName = ResourceNames.GuestSessionTableName,
                Key = new() { ["guestSessionId"] = ownerItem["guestSessionId"] },
                UpdateExpression = updateExpression, ConditionExpression = condition,
                ExpressionAttributeNames = new() { ["#pref"] = "orderingPreference", ["#rev"] = "revision", ["#submitted"] = "isSubmitted" },
                ExpressionAttributeValues = values } },
            new() { Update = new Update { TableName = ResourceNames.GroupReservationsTableName,
                Key = new() { ["groupReservationId"] = new AttributeValue { S = group.GroupReservationId } },
                UpdateExpression = "SET #status = :open", ConditionExpression = "#status = :open AND reservationId = :reservation",
                ExpressionAttributeNames = new() { ["#status"] = "status" },
                ExpressionAttributeValues = new() { [":open"] = new AttributeValue { N = "0" }, [":reservation"] = new AttributeValue { S = reservation.Id } } } },
            new() { ConditionCheck = new ConditionCheck { TableName = ResourceNames.ReservationsTableName,
                Key = new() { ["id"] = reservationItem["id"] },
                ConditionExpression = "#status = :reserved AND customerId = :owner AND startTime = :start",
                ExpressionAttributeNames = new() { ["#status"] = "status" },
                ExpressionAttributeValues = new() { [":reserved"] = reservationItem["status"],
                    [":owner"] = reservationItem["customerId"], [":start"] = reservationItem["startTime"] } } }
        };
    }

    private async Task WriteOwnerAsync(List<TransactWriteItem> writes)
    {
        try { await client.TransactWriteItemsAsync(new TransactWriteItemsRequest { TransactItems = writes }); }
        catch (TransactionCanceledException ex) when (ex.CancellationReasons?.Any(r => r.Code is "ConditionalCheckFailed" or "TransactionConflict") == true)
        { throw new GroupPreOrderConflictException("Owner pre-order changed; reload before trying again."); }
        catch (TransactionConflictException) { throw new GroupPreOrderConflictException("Owner pre-order is being changed; reload before trying again."); }
    }

    private Dictionary<string, AttributeValue> ToOrderAttributes(Order order) =>
        context.ToDocument(order, new ToDocumentConfig { OverrideTableName = ResourceNames.OrdersTableName }).ToAttributeMap();
    private static Dictionary<string, AttributeValue> OrderKey(Dictionary<string, AttributeValue> item) => new() {
        ["orderId"] = item["orderId"], ["customerId"] = item["customerId"] };
    private static string RevisionCondition(long revision) => revision == 0
        ? "(attribute_not_exists(#rev) OR #rev = :expected)" : "#rev = :expected";
    public async Task<GuestSession> EnsureOwnerSessionAsync(string groupReservationId, string userId)
    {
        // Group IDs are globally unique; stable keys make concurrent legacy backfills safe.
        var session = new GuestSession { Id = "owner-" + groupReservationId, GroupReservationId = groupReservationId,
            GuestName = "Owner", Dishes = [], IsOwnerSession = true, OwnerUserId = userId };
        try
        {
            await client.PutItemAsync(new PutItemRequest {
                TableName = ResourceNames.GuestSessionTableName,
                Item = context.ToDocument(session, new ToDocumentConfig { OverrideTableName = ResourceNames.GuestSessionTableName }).ToAttributeMap(),
                ConditionExpression = "attribute_not_exists(guestSessionId)"
            });
            return session;
        }
        catch (ConditionalCheckFailedException)
        {
            var existing = await context.LoadAsync<GuestSession>(session.Id, new LoadConfig {
                OverrideTableName = ResourceNames.GuestSessionTableName, ConsistentRead = true });
            if (existing == null || !existing.IsOwnerSession || existing.OwnerUserId != userId || existing.GroupReservationId != groupReservationId)
                throw new GroupPreOrderConflictException("The owner session changed; reload the group.");
            return existing;
        }
    }

    public async Task SubmitAsync(Order order, GroupReservation group, Reservation reservation,
        List<OrderItem> dishes, IReadOnlyList<GuestSession> submittedSessions)
    {
        var originalOrder = context.ToDocument(order, new ToDocumentConfig { OverrideTableName = ResourceNames.OrdersTableName }).ToAttributeMap();
        var originalGroup = context.ToDocument(group, new ToDocumentConfig { OverrideTableName = ResourceNames.GroupReservationsTableName }).ToAttributeMap();
        var originalReservation = context.ToDocument(reservation, new ToDocumentConfig { OverrideTableName = ResourceNames.ReservationsTableName }).ToAttributeMap();
        var updatedOrder = new Order { OrderId = order.OrderId, CustomerId = order.CustomerId,
            ReservationId = order.ReservationId, ReservationAddress = order.ReservationAddress,
            ReservationTime = order.ReservationTime, Dishes = dishes, Status = OrderStatus.SubmittedByCustomer };
        var newOrder = context.ToDocument(updatedOrder, new ToDocumentConfig { OverrideTableName = ResourceNames.OrdersTableName }).ToAttributeMap();
        var updatedGroup = new GroupReservation { GroupReservationId = group.GroupReservationId,
            ReservationId = group.ReservationId, ReservationToken = group.ReservationToken, Status = GroupReservationStatus.Submitted };
        var newGroup = context.ToDocument(updatedGroup, new ToDocumentConfig { OverrideTableName = ResourceNames.GroupReservationsTableName }).ToAttributeMap();
        var writes = new List<TransactWriteItem> {
            new() { Update = new Update {
                TableName = ResourceNames.OrdersTableName,
                Key = new() { ["orderId"] = originalOrder["orderId"], ["customerId"] = originalOrder["customerId"] },
                UpdateExpression = "SET #s = :newStatus, dishes = :newDishes, #rev = if_not_exists(#rev, :zero) + :one, #finalized = :true",
                ConditionExpression = "#s = :oldStatus AND reservationId = :reservation AND dishes = :oldDishes AND (attribute_not_exists(#finalized) OR #finalized = :false) AND " +
                    (order.Revision == 0 ? "(attribute_not_exists(#rev) OR #rev = :expectedRevision)" : "#rev = :expectedRevision"),
                ExpressionAttributeNames = new() { ["#s"] = "status", ["#rev"] = "revision", ["#finalized"] = "groupFinalized" },
                ExpressionAttributeValues = new() { [":oldStatus"] = originalOrder["status"], [":newStatus"] = newOrder["status"],
                    [":oldDishes"] = originalOrder["dishes"], [":newDishes"] = newOrder["dishes"], [":reservation"] = originalOrder["reservationId"],
                    [":expectedRevision"] = originalOrder["revision"], [":zero"] = new AttributeValue { N = "0" }, [":one"] = new AttributeValue { N = "1" },
                    [":true"] = new AttributeValue { N = "1" }, [":false"] = new AttributeValue { N = "0" } }
            } },
            new() { Update = new Update {
                TableName = ResourceNames.GroupReservationsTableName,
                Key = new() { ["groupReservationId"] = originalGroup["groupReservationId"] },
                UpdateExpression = "SET #s = :newStatus",
                ConditionExpression = "#s = :oldStatus AND reservationId = :reservation",
                ExpressionAttributeNames = new() { ["#s"] = "status" },
                ExpressionAttributeValues = new() { [":oldStatus"] = originalGroup["status"], [":newStatus"] = newGroup["status"], [":reservation"] = originalGroup["reservationId"] }
            } },
            new() { ConditionCheck = new ConditionCheck {
                TableName = ResourceNames.ReservationsTableName, Key = new() { ["id"] = originalReservation["id"] },
                ConditionExpression = "#s = :status AND customerId = :owner AND startTime = :startTime",
                ExpressionAttributeNames = new() { ["#s"] = "status" },
                ExpressionAttributeValues = new() { [":status"] = originalReservation["status"], [":owner"] = originalReservation["customerId"], [":startTime"] = originalReservation["startTime"] }
            } }
        };
        foreach (var session in submittedSessions)
        {
            var snapshot = context.ToDocument(session, new ToDocumentConfig { OverrideTableName = ResourceNames.GuestSessionTableName }).ToAttributeMap();
            writes.Add(new TransactWriteItem { ConditionCheck = new ConditionCheck {
                TableName = ResourceNames.GuestSessionTableName, Key = new() { ["guestSessionId"] = snapshot["guestSessionId"] },
                ConditionExpression = "isSubmitted = :submitted AND groupReservationId = :group AND dishes = :dishes AND " +
                    "(attribute_not_exists(orderingPreference) OR orderingPreference = :online) AND " +
                    (session.Revision == 0 ? "(attribute_not_exists(revision) OR revision = :revision)" : "revision = :revision"),
                ExpressionAttributeValues = new() { [":submitted"] = snapshot["isSubmitted"], [":group"] = snapshot["groupReservationId"],
                    [":dishes"] = snapshot["dishes"], [":online"] = new AttributeValue { S = OrderingPreference.Online },
                    [":revision"] = new AttributeValue { N = session.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture) } }
            } });
        }
        try { await client.TransactWriteItemsAsync(new TransactWriteItemsRequest { TransactItems = writes }); }
        catch (TransactionCanceledException ex) when (ex.CancellationReasons?.Any(r => r.Code == "ConditionalCheckFailed" || r.Code == "TransactionConflict") == true)
        { throw new GroupPreOrderConflictException("The group order changed; reload before submitting again."); }
        catch (TransactionConflictException)
        { throw new GroupPreOrderConflictException("The group order is being changed; reload before submitting again."); }
    }
}
