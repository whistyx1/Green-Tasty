using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Exceptions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace APIHandler.Repositories.GroupPreOrders;

public class GroupPreOrderRepository(IAmazonDynamoDB client, IDynamoDBContext context) : IGroupPreOrderRepository
{
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
                UpdateExpression = "SET #s = :newStatus, dishes = :newDishes",
                ConditionExpression = "#s = :oldStatus AND reservationId = :reservation AND dishes = :oldDishes",
                ExpressionAttributeNames = new() { ["#s"] = "status" },
                ExpressionAttributeValues = new() { [":oldStatus"] = originalOrder["status"], [":newStatus"] = newOrder["status"],
                    [":oldDishes"] = originalOrder["dishes"], [":newDishes"] = newOrder["dishes"], [":reservation"] = originalOrder["reservationId"] }
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
                ConditionExpression = "isSubmitted = :submitted AND groupReservationId = :group AND dishes = :dishes",
                ExpressionAttributeValues = new() { [":submitted"] = snapshot["isSubmitted"], [":group"] = snapshot["groupReservationId"], [":dishes"] = snapshot["dishes"] }
            } });
        }
        try { await client.TransactWriteItemsAsync(new TransactWriteItemsRequest { TransactItems = writes }); }
        catch (TransactionCanceledException ex) when (ex.CancellationReasons?.Any(r => r.Code == "ConditionalCheckFailed" || r.Code == "TransactionConflict") == true)
        { throw new GroupPreOrderConflictException("The group order changed; reload before submitting again."); }
        catch (TransactionConflictException)
        { throw new GroupPreOrderConflictException("The group order is being changed; reload before submitting again."); }
    }
}
