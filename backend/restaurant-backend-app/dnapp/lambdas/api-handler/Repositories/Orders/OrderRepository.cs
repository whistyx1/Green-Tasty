using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIHandler.Repositories.Orders;

public class OrderRepository : IOrderRepository
{
    private readonly string OrdersTableName = ResourceNames.OrdersTableName;
    private readonly string CustomerIdGlobalIndex = ResourceNames.OrdersTableCustomerIdGlobalIndex;
    private readonly string ReservationIdGlobalIndex = ResourceNames.OrdersTableReservationIdGlobalIndex;
    private const string GuardCustomerId = "__reservation_order_guard__";

    private readonly IDynamoDBContext _dynamoDbContext;
    private readonly IAmazonDynamoDB _client;

    public OrderRepository(IDynamoDBContext dynamoDbContext, IAmazonDynamoDB client)
    {
        _dynamoDbContext = dynamoDbContext ?? throw new ArgumentNullException(nameof(dynamoDbContext));
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<Order?> GetOrderByIdAsync(string orderId, string customerId)
    {
        return await _dynamoDbContext.LoadAsync<Order>(orderId, customerId, DynamoConfig.LoadConfig(OrdersTableName));
    }

    public async Task<IEnumerable<Order>> GetOrdersByCustomerIdAsync(string customerId)
    {
        var config = new QueryConfig
        {
            OverrideTableName = OrdersTableName,
            IndexName = CustomerIdGlobalIndex,
        };

        var query = _dynamoDbContext.QueryAsync<Order>(customerId, config);

        return await query.GetRemainingAsync();
    }

    public async Task<Order?> GetOrderByReservationIdAsync(string reservationId)
    {
        var config = new QueryConfig
        {
            OverrideTableName = OrdersTableName,
            IndexName = ReservationIdGlobalIndex,
        };

        var results = await _dynamoDbContext.QueryAsync<Order>(reservationId, config).GetRemainingAsync();
        var active = results.Where(o => o.Status != OrderStatus.Cancelled).Take(2).ToList();
        if (active.Count > 1)
            throw new ValidationException("Multiple active pre-orders exist for this reservation; contact support.");
        return active.SingleOrDefault();
    }

    public async Task SaveOrderAsync(Order order)
    {
        // The GSI cannot enforce uniqueness. A consistent scan detects legacy rows that predate guards;
        // the conditional guard put serializes all new creations, even with different order IDs.
        if (await CountLegacyActiveOrdersAsync(order.ReservationId) > 0)
            throw new ValidationException("An active pre-order already exists for this reservation.");
        var guardKey = GuardKey(order.ReservationId);
        try
        {
            await _client.TransactWriteItemsAsync(new TransactWriteItemsRequest { TransactItems = new() {
                new() { Put = new Put { TableName = OrdersTableName, Item = ToAttributes(order),
                    ConditionExpression = "attribute_not_exists(orderId)" } },
                new() { Put = new Put { TableName = OrdersTableName, Item = new() {
                    ["orderId"] = guardKey["orderId"], ["customerId"] = guardKey["customerId"],
                    ["activeOrderId"] = new AttributeValue { S = order.OrderId }
                }, ConditionExpression = "attribute_not_exists(orderId)" } }
            } });
        }
        catch (TransactionCanceledException ex) when (IsConflict(ex))
        {
            throw new ValidationException("An active pre-order already exists or changed; reload before trying again.");
        }
        catch (TransactionConflictException)
        {
            throw new ValidationException("Pre-order is being created; try again.");
        }
    }

    private async Task<int> CountLegacyActiveOrdersAsync(string reservationId)
    {
        Dictionary<string, AttributeValue>? lastKey = null;
        var active = 0;
        do
        {
            var page = await _client.ScanAsync(new ScanRequest {
                TableName = OrdersTableName, ConsistentRead = true,
                FilterExpression = "reservationId = :reservation",
                ExpressionAttributeValues = new() { [":reservation"] = new AttributeValue { S = reservationId } },
                ExclusiveStartKey = lastKey
            });
            foreach (var item in page.Items ?? [])
            {
                if (!item.TryGetValue("status", out var status) ||
                    !int.TryParse(status.N, out var value) || value != (int)OrderStatus.Cancelled)
                    active++;
            }
            lastKey = page.LastEvaluatedKey;
        } while (lastKey is { Count: > 0 });
        return active;
    }

    private static Dictionary<string, AttributeValue> GuardKey(string reservationId) => new() {
        ["orderId"] = new AttributeValue { S = "reservation-order#" + reservationId },
        ["customerId"] = new AttributeValue { S = GuardCustomerId }
    };

    private static bool IsConflict(TransactionCanceledException ex) => ex.CancellationReasons?.Any(reason =>
        reason.Code is "ConditionalCheckFailed" or "TransactionConflict") == true;

    public Task SaveOrderIfUnchangedAsync(Order order, Order previous) =>
        SaveOrderIfUnchangedAsync(order, previous, protectFinalizedGroup: false);

    public async Task SaveOrderIfUnchangedAsync(Order order, Order previous, bool protectFinalizedGroup)
    {
        if (order.OrderId != previous.OrderId || order.CustomerId != previous.CustomerId ||
            order.ReservationId != previous.ReservationId)
            throw new ValidationException("Pre-order identity changed; reload it before trying again.");
        var oldItem = ToAttributes(previous);
        var newItem = ToAttributes(order);
        try
        {
            var request = new UpdateItemRequest {
                TableName = OrdersTableName,
                Key = new() { ["orderId"] = oldItem["orderId"], ["customerId"] = oldItem["customerId"] },
                UpdateExpression = "SET #s = :newStatus, dishes = :newDishes, #rev = if_not_exists(#rev, :zero) + :one",
                ConditionExpression = "#s = :oldStatus AND dishes = :oldDishes AND reservationId = :reservation AND " + RevisionCondition(previous.Revision) +
                    (protectFinalizedGroup ? " AND (attribute_not_exists(#finalized) OR #finalized = :false) AND (attribute_not_exists(#ownerReady) OR #ownerReady = :false)" : ""),
                ExpressionAttributeNames = new() { ["#s"] = "status", ["#rev"] = "revision" },
                ExpressionAttributeValues = new() {
                    [":oldStatus"] = oldItem["status"], [":oldDishes"] = oldItem["dishes"],
                    [":newStatus"] = newItem["status"], [":newDishes"] = newItem["dishes"],
                    [":reservation"] = oldItem["reservationId"], [":expectedRevision"] = oldItem["revision"],
                    [":zero"] = new AttributeValue { N = "0" }, [":one"] = new AttributeValue { N = "1" }
                }
            };
            if (protectFinalizedGroup)
            {
                request.ExpressionAttributeNames["#finalized"] = "groupFinalized";
                request.ExpressionAttributeNames["#ownerReady"] = "ownerGroupReady";
                request.ExpressionAttributeValues[":false"] = new AttributeValue { N = "0" };
            }
            if (order.Status == OrderStatus.Cancelled && previous.Status != OrderStatus.Cancelled)
            {
                var guardKey = GuardKey(order.ReservationId);
                await _client.TransactWriteItemsAsync(new TransactWriteItemsRequest { TransactItems = new() {
                    new() { Update = new Update { TableName = request.TableName, Key = request.Key,
                        UpdateExpression = request.UpdateExpression, ConditionExpression = request.ConditionExpression,
                        ExpressionAttributeNames = request.ExpressionAttributeNames,
                        ExpressionAttributeValues = request.ExpressionAttributeValues } },
                    new() { Delete = new Delete { TableName = OrdersTableName, Key = guardKey,
                        ConditionExpression = "attribute_not_exists(orderId) OR activeOrderId = :orderId",
                        ExpressionAttributeValues = new() { [":orderId"] = new AttributeValue { S = order.OrderId } } } }
                } });
            }
            else
                await _client.UpdateItemAsync(request);
        }
        catch (ConditionalCheckFailedException)
        {
            throw new ValidationException("Pre-order changed; reload it before trying again.");
        }
        catch (TransactionConflictException)
        {
            throw new ValidationException("Pre-order is being changed; try again.");
        }
        catch (TransactionCanceledException ex) when (IsConflict(ex))
        {
            throw new ValidationException("Pre-order changed; reload it before trying again.");
        }
    }

    public async Task SubmitOrderIfEligibleAsync(Order order, Reservation reservation)
    {
        var oldOrder = ToAttributes(order);
        var reservationItem = _dynamoDbContext.ToDocument(reservation,
            new ToDocumentConfig { OverrideTableName = ResourceNames.ReservationsTableName }).ToAttributeMap();
        var submittedOrder = new Order {
            OrderId = order.OrderId, CustomerId = order.CustomerId, ReservationId = order.ReservationId,
            ReservationAddress = order.ReservationAddress, ReservationTime = order.ReservationTime,
            Dishes = order.Dishes, Status = OrderStatus.SubmittedByCustomer
        };
        var submitted = ToAttributes(submittedOrder)["status"];
        try
        {
            await _client.TransactWriteItemsAsync(new TransactWriteItemsRequest { TransactItems = new() {
                new() { Update = new Update {
                    TableName = OrdersTableName,
                    Key = new() { ["orderId"] = oldOrder["orderId"], ["customerId"] = oldOrder["customerId"] },
                    UpdateExpression = "SET #s = :submitted, #rev = if_not_exists(#rev, :zero) + :one",
                    ConditionExpression = "#s = :draft AND dishes = :dishes AND reservationId = :reservation AND " +
                        "(attribute_not_exists(#ownerReady) OR #ownerReady = :false) AND " +
                        "(attribute_not_exists(#finalized) OR #finalized = :false) AND " + RevisionCondition(order.Revision),
                    ExpressionAttributeNames = new() { ["#s"] = "status", ["#rev"] = "revision",
                        ["#ownerReady"] = "ownerGroupReady", ["#finalized"] = "groupFinalized" },
                    ExpressionAttributeValues = new() {
                        [":submitted"] = submitted, [":draft"] = oldOrder["status"],
                        [":dishes"] = oldOrder["dishes"], [":reservation"] = oldOrder["reservationId"],
                        [":expectedRevision"] = oldOrder["revision"],
                        [":zero"] = new AttributeValue { N = "0" }, [":one"] = new AttributeValue { N = "1" },
                        [":false"] = new AttributeValue { N = "0" }
                    }
                } },
                new() { ConditionCheck = new ConditionCheck {
                    TableName = ResourceNames.ReservationsTableName,
                    Key = new() { ["id"] = reservationItem["id"] },
                    ConditionExpression = "#s = :reserved AND customerId = :owner AND startTime = :startTime",
                    ExpressionAttributeNames = new() { ["#s"] = "status" },
                    ExpressionAttributeValues = new() {
                        [":reserved"] = reservationItem["status"], [":owner"] = reservationItem["customerId"],
                        [":startTime"] = reservationItem["startTime"]
                    }
                } }
            } });
        }
        catch (TransactionCanceledException ex) when (ex.CancellationReasons?.Any(reason =>
            reason.Code is "ConditionalCheckFailed" or "TransactionConflict") == true)
        {
            throw new ValidationException("Reservation or pre-order changed; reload before submitting.");
        }
        catch (TransactionConflictException)
        {
            throw new ValidationException("Reservation or pre-order is being changed; try again.");
        }
    }

    private Dictionary<string, AttributeValue> ToAttributes(Order order) =>
        _dynamoDbContext.ToDocument(order,
            new ToDocumentConfig { OverrideTableName = OrdersTableName }).ToAttributeMap();

    private static string RevisionCondition(long revision) => revision == 0
        ? "(attribute_not_exists(#rev) OR #rev = :expectedRevision)"
        : "#rev = :expectedRevision";
}
