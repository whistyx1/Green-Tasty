using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Repositories.Orders;
using Moq;

namespace APIHandler.Tests.Repositories;

[Collection("Group preorder repository")]
public class OrderRepositoryConcurrencyTests : IDisposable
{
    private readonly Dictionary<string, string?> _environment = new[] { "ordersTable", "reservationsTable" }
        .ToDictionary(name => name, Environment.GetEnvironmentVariable);
    private readonly Mock<IAmazonDynamoDB> _client = new();
    private readonly IDynamoDBContext _context;
    private readonly OrderRepository _repository;

    public OrderRepositoryConcurrencyTests()
    {
        Environment.SetEnvironmentVariable("ordersTable", "test-orders");
        Environment.SetEnvironmentVariable("reservationsTable", "test-reservations");
        _context = new DynamoDBContextBuilder().WithDynamoDBClient(() => _client.Object)
            .ConfigureContext(config => config.DisableFetchingTableMetadata = true).Build();
        _client.Setup(client => client.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScanResponse { Items = [] });
        _repository = new OrderRepository(_context, _client.Object);
    }

    public void Dispose()
    {
        foreach (var item in _environment) Environment.SetEnvironmentVariable(item.Key, item.Value);
        _context.Dispose();
    }

    [Fact]
    public async Task Create_AtomicallyPutsOrderAndReservationGuard()
    {
        TransactWriteItemsRequest? captured = null;
        _client.Setup(client => client.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new TransactWriteItemsResponse());
        var (order, _) = ValidItems();

        await _repository.SaveOrderAsync(order);

        Assert.NotNull(captured);
        Assert.Equal(2, captured.TransactItems.Count);
        Assert.All(captured.TransactItems, item => Assert.Equal("attribute_not_exists(orderId)", item.Put.ConditionExpression));
        Assert.Equal("order", captured.TransactItems[0].Put.Item["orderId"].S);
        Assert.Equal("owner", captured.TransactItems[0].Put.Item["customerId"].S);
        Assert.Equal("0", captured.TransactItems[0].Put.Item["revision"].N);
        Assert.Equal("reservation-order#reservation", captured.TransactItems[1].Put.Item["orderId"].S);
        Assert.Equal("__reservation_order_guard__", captured.TransactItems[1].Put.Item["customerId"].S);
        Assert.Equal("order", captured.TransactItems[1].Put.Item["activeOrderId"].S);
        Assert.False(captured.TransactItems[1].Put.Item.ContainsKey("reservationId"));
        _client.Verify(client => client.ScanAsync(It.Is<ScanRequest>(request => request.ConsistentRead == true &&
            request.ExpressionAttributeValues[":reservation"].S == "reservation"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WhenOrderKeyAlreadyExists_RejectsWithoutOverwriting()
    {
        _client.Setup(client => client.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TransactionCanceledException("existing guard") { CancellationReasons = [
                new CancellationReason { Code = "None" }, new CancellationReason { Code = "ConditionalCheckFailed" }] });
        var (order, _) = ValidItems();

        await Assert.ThrowsAsync<ValidationException>(() => _repository.SaveOrderAsync(order));
    }

    [Fact]
    public async Task ConcurrentCreate_DifferentOrderIdsCompeteForSameReservationGuard()
    {
        var guarded = false;
        _client.Setup(client => client.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Returns<TransactWriteItemsRequest, CancellationToken>((request, _) => {
                lock (_client) {
                    Assert.Equal("reservation-order#reservation", request.TransactItems[1].Put.Item["orderId"].S);
                    if (guarded) throw new TransactionCanceledException("guard exists") { CancellationReasons = [
                        new CancellationReason { Code = "None" }, new CancellationReason { Code = "ConditionalCheckFailed" }] };
                    guarded = true;
                    return Task.FromResult(new TransactWriteItemsResponse());
                }
            });
        var (first, _) = ValidItems();
        var second = new Order { OrderId = "different-order", CustomerId = first.CustomerId,
            ReservationId = first.ReservationId, ReservationAddress = first.ReservationAddress,
            ReservationTime = first.ReservationTime, Dishes = first.Dishes };

        var results = await Task.WhenAll(new[] { first, second }.Select(async order => {
            try { await _repository.SaveOrderAsync(order); return true; }
            catch (ValidationException) { return false; }
        }));

        Assert.Single(results.Where(result => result));
        Assert.Single(results.Where(result => !result));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Create_RejectsLegacyActiveOrdersWithoutGuard(int activeCount)
    {
        _client.Setup(client => client.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScanResponse { Items = Enumerable.Range(0, activeCount).Select(index =>
                new Dictionary<string, AttributeValue> { ["status"] = new() { N = "0" },
                    ["orderId"] = new() { S = "legacy-" + index } }).ToList() });
        var (order, _) = ValidItems();

        await Assert.ThrowsAsync<ValidationException>(() => _repository.SaveOrderAsync(order));
        _client.Verify(client => client.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_AfterLegacyCancellation_AllowsNewGuardedOrder()
    {
        _client.Setup(client => client.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScanResponse { Items = [new() { ["status"] = new AttributeValue { N = "2" } }] });
        _client.Setup(client => client.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(new TransactWriteItemsResponse());
        var (order, _) = ValidItems();

        await _repository.SaveOrderAsync(order);

        _client.Verify(client => client.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_ScansPastEmptyFilteredPageToFindLegacyActiveOrder()
    {
        _client.SetupSequence(client => client.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ScanResponse { Items = [], LastEvaluatedKey = new() {
                ["orderId"] = new AttributeValue { S = "previous-page" },
                ["customerId"] = new AttributeValue { S = "owner" }
            } })
            .ReturnsAsync(new ScanResponse { Items = [new() { ["status"] = new AttributeValue { N = "0" } }] });
        var (order, _) = ValidItems();

        await Assert.ThrowsAsync<ValidationException>(() => _repository.SaveOrderAsync(order));

        _client.Verify(client => client.ScanAsync(It.Is<ScanRequest>(request => request.ConsistentRead == true),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        _client.Verify(client => client.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Lookup_RejectsExistingDuplicateActiveOrdersInsteadOfChoosingOne()
    {
        var context = new Mock<IDynamoDBContext>();
        var search = new Mock<AsyncSearch<Order>>();
        var (first, _) = ValidItems();
        var second = new Order { OrderId = "second", CustomerId = first.CustomerId,
            ReservationId = first.ReservationId, ReservationAddress = first.ReservationAddress,
            ReservationTime = first.ReservationTime, Dishes = first.Dishes };
        search.Setup(result => result.GetRemainingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([first, second]);
        context.Setup(item => item.QueryAsync<Order>(first.ReservationId, It.IsAny<QueryConfig>()))
            .Returns(search.Object);
        var repository = new OrderRepository(context.Object, _client.Object);

        await Assert.ThrowsAsync<ValidationException>(() => repository.GetOrderByReservationIdAsync(first.ReservationId));
    }

    [Fact]
    public async Task Submit_AtomicallyChecksReservationAndExpectedDraftSnapshot()
    {
        TransactWriteItemsRequest? captured = null;
        _client.Setup(client => client.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new TransactWriteItemsResponse());
        var (order, reservation) = ValidItems();

        await _repository.SubmitOrderIfEligibleAsync(order, reservation);

        Assert.NotNull(captured);
        Assert.Equal(2, captured.TransactItems.Count);
        var update = captured.TransactItems[0].Update;
        Assert.Equal("test-orders", update.TableName);
        Assert.Equal("order", update.Key["orderId"].S);
        Assert.Contains("#s = :draft", update.ConditionExpression);
        Assert.Contains("dishes = :dishes", update.ConditionExpression);
        Assert.Contains("reservationId = :reservation", update.ConditionExpression);
        Assert.Contains("attribute_not_exists(#rev)", update.ConditionExpression);
        Assert.Contains("#rev = if_not_exists(#rev, :zero) + :one", update.UpdateExpression);
        Assert.Equal("0", update.ExpressionAttributeValues[":expectedRevision"].N);
        Assert.Equal("0", update.ExpressionAttributeValues[":draft"].N);
        Assert.Equal("3", update.ExpressionAttributeValues[":submitted"].N);
        Assert.Single(update.ExpressionAttributeValues[":dishes"].L);
        var check = captured.TransactItems[1].ConditionCheck;
        Assert.Equal("test-reservations", check.TableName);
        Assert.Equal("reservation", check.Key["id"].S);
        Assert.Contains("#s = :reserved", check.ConditionExpression);
        Assert.Contains("customerId = :owner", check.ConditionExpression);
        Assert.Contains("startTime = :startTime", check.ConditionExpression);
        Assert.Equal("0", check.ExpressionAttributeValues[":reserved"].N);
        Assert.Equal("owner", check.ExpressionAttributeValues[":owner"].S);
    }

    [Fact]
    public async Task Submit_WhenReservationCancellationWins_RejectsEntireTransaction()
    {
        _client.Setup(client => client.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TransactionCanceledException("reservation changed") {
                CancellationReasons = [new CancellationReason { Code = "None" },
                    new CancellationReason { Code = "ConditionalCheckFailed" }]
            });
        var (order, reservation) = ValidItems();

        await Assert.ThrowsAsync<ValidationException>(() => _repository.SubmitOrderIfEligibleAsync(order, reservation));
        _client.Verify(client => client.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Submit_WhenConcurrentEditOrCancelWins_RejectsEntireTransaction()
    {
        _client.Setup(client => client.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TransactionCanceledException("order changed") {
                CancellationReasons = [new CancellationReason { Code = "ConditionalCheckFailed" },
                    new CancellationReason { Code = "None" }]
            });
        var (order, reservation) = ValidItems();

        await Assert.ThrowsAsync<ValidationException>(() => _repository.SubmitOrderIfEligibleAsync(order, reservation));
    }

    [Theory]
    [InlineData(OrderStatus.Created)]
    public async Task EditOrCancel_UsesExpectedStatusAndDishesSnapshot(OrderStatus newStatus)
    {
        UpdateItemRequest? captured = null;
        _client.Setup(client => client.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateItemRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new UpdateItemResponse());
        var (previous, _) = ValidItems();
        var changed = new Order { OrderId = previous.OrderId, CustomerId = previous.CustomerId,
            ReservationId = previous.ReservationId, ReservationAddress = previous.ReservationAddress,
            ReservationTime = previous.ReservationTime, Status = newStatus,
            Dishes = [new OrderItem { DishId = "new-dish", Quantity = 2 }] };

        await _repository.SaveOrderIfUnchangedAsync(changed, previous);

        Assert.NotNull(captured);
        Assert.Equal("0", captured.ExpressionAttributeValues[":oldStatus"].N);
        Assert.Equal(((int)newStatus).ToString(), captured.ExpressionAttributeValues[":newStatus"].N);
        Assert.Contains("#s = :oldStatus", captured.ConditionExpression);
        Assert.Contains("dishes = :oldDishes", captured.ConditionExpression);
        Assert.Contains("attribute_not_exists(#rev)", captured.ConditionExpression);
        Assert.Contains("#rev = if_not_exists(#rev, :zero) + :one", captured.UpdateExpression);
        Assert.Equal("dish", captured.ExpressionAttributeValues[":oldDishes"].L[0].M["dishId"].S);
        Assert.Equal("new-dish", captured.ExpressionAttributeValues[":newDishes"].L[0].M["dishId"].S);
    }

    [Theory]
    [InlineData(OrderStatus.Created)]
    public async Task ConcurrentEditOrCancel_AfterSubmitWins_RejectsStaleWrite(OrderStatus newStatus)
    {
        _client.Setup(client => client.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConditionalCheckFailedException("order changed"));
        var (previous, _) = ValidItems();
        var changed = new Order { OrderId = previous.OrderId, CustomerId = previous.CustomerId,
            ReservationId = previous.ReservationId, ReservationAddress = previous.ReservationAddress,
            ReservationTime = previous.ReservationTime, Status = newStatus, Dishes = previous.Dishes };

        await Assert.ThrowsAsync<ValidationException>(() => _repository.SaveOrderIfUnchangedAsync(changed, previous));
    }

    [Fact]
    public async Task Cancel_AtomicallyChangesOrderAndReleasesMatchingReservationGuard()
    {
        TransactWriteItemsRequest? captured = null;
        _client.Setup(client => client.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new TransactWriteItemsResponse());
        var (previous, _) = ValidItems();
        var cancelled = new Order { OrderId = previous.OrderId, CustomerId = previous.CustomerId,
            ReservationId = previous.ReservationId, ReservationAddress = previous.ReservationAddress,
            ReservationTime = previous.ReservationTime, Status = OrderStatus.Cancelled, Dishes = previous.Dishes };

        await _repository.SaveOrderIfUnchangedAsync(cancelled, previous, protectFinalizedGroup: true);

        Assert.NotNull(captured);
        Assert.Equal(2, captured.TransactItems.Count);
        Assert.Contains("#rev = if_not_exists", captured.TransactItems[0].Update.UpdateExpression);
        Assert.Contains("#finalized = :false", captured.TransactItems[0].Update.ConditionExpression);
        Assert.Equal("reservation-order#reservation", captured.TransactItems[1].Delete.Key["orderId"].S);
        Assert.Equal("order", captured.TransactItems[1].Delete.ExpressionAttributeValues[":orderId"].S);
    }

    [Fact]
    public async Task Cancel_WhenGroupFinalizationWins_RejectsWithoutReleasingGuard()
    {
        _client.Setup(client => client.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TransactionCanceledException("finalized") { CancellationReasons = [
                new CancellationReason { Code = "ConditionalCheckFailed" }, new CancellationReason { Code = "None" }] });
        var (previous, _) = ValidItems();
        var cancelled = new Order { OrderId = previous.OrderId, CustomerId = previous.CustomerId,
            ReservationId = previous.ReservationId, ReservationAddress = previous.ReservationAddress,
            ReservationTime = previous.ReservationTime, Status = OrderStatus.Cancelled, Dishes = previous.Dishes };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _repository.SaveOrderIfUnchangedAsync(cancelled, previous, protectFinalizedGroup: true));
    }

    [Theory]
    [InlineData(OrderStatus.Created)]
    [InlineData(OrderStatus.SubmittedByCustomer)]
    public async Task CustomerEditOrUnsubmit_RequiresGroupNotFinalized(OrderStatus newStatus)
    {
        UpdateItemRequest? captured = null;
        _client.Setup(client => client.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateItemRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new UpdateItemResponse());
        var (previous, _) = ValidItems();
        var changed = new Order { OrderId = previous.OrderId, CustomerId = previous.CustomerId,
            ReservationId = previous.ReservationId, ReservationAddress = previous.ReservationAddress,
            ReservationTime = previous.ReservationTime, Status = newStatus, Dishes = previous.Dishes };

        await _repository.SaveOrderIfUnchangedAsync(changed, previous, protectFinalizedGroup: true);

        Assert.NotNull(captured);
        Assert.Contains("attribute_not_exists(#finalized) OR #finalized = :false", captured.ConditionExpression);
        Assert.Equal("0", captured.ExpressionAttributeValues[":false"].N);
    }

    [Theory]
    [InlineData(OrderStatus.Created)]
    [InlineData(OrderStatus.SubmittedByCustomer)]
    public async Task CustomerEditOrUnsubmit_AfterGroupFinalizationWins_RejectsStaleWrite(OrderStatus newStatus)
    {
        _client.Setup(client => client.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConditionalCheckFailedException("group finalized"));
        var (previous, _) = ValidItems();
        var changed = new Order { OrderId = previous.OrderId, CustomerId = previous.CustomerId,
            ReservationId = previous.ReservationId, ReservationAddress = previous.ReservationAddress,
            ReservationTime = previous.ReservationTime, Status = newStatus, Dishes = previous.Dishes };

        await Assert.ThrowsAsync<ValidationException>(() =>
            _repository.SaveOrderIfUnchangedAsync(changed, previous, protectFinalizedGroup: true));
    }

    [Fact]
    public async Task WaiterDishProcessing_KeepsRevisionGuardWithoutBlockingFinalizedGroup()
    {
        UpdateItemRequest? captured = null;
        _client.Setup(client => client.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateItemRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new UpdateItemResponse());
        var (order, _) = ValidItems();
        order.GroupFinalized = true;
        var previous = new Order { OrderId = order.OrderId, CustomerId = order.CustomerId,
            ReservationId = order.ReservationId, ReservationAddress = order.ReservationAddress,
            ReservationTime = order.ReservationTime, Status = order.Status, Dishes = order.Dishes,
            GroupFinalized = true, Revision = order.Revision };
        order.Status = OrderStatus.Submitted;

        await _repository.SaveOrderIfUnchangedAsync(order, previous);

        Assert.NotNull(captured);
        Assert.Contains("#rev", captured.ConditionExpression);
        Assert.DoesNotContain("#finalized", captured.ConditionExpression);
    }

    [Fact]
    public async Task Unsubmit_WhenAnotherOperationWins_RejectsStaleWrite()
    {
        _client.Setup(client => client.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConditionalCheckFailedException("order changed"));
        var (previous, _) = ValidItems();
        previous.Status = OrderStatus.SubmittedByCustomer;
        var changed = new Order { OrderId = previous.OrderId, CustomerId = previous.CustomerId,
            ReservationId = previous.ReservationId, ReservationAddress = previous.ReservationAddress,
            ReservationTime = previous.ReservationTime, Status = OrderStatus.Created, Dishes = previous.Dishes };

        await Assert.ThrowsAsync<ValidationException>(() => _repository.SaveOrderIfUnchangedAsync(changed, previous));
    }

    [Fact]
    public async Task StaleUnsubmitAfterStatusReturnsToSameValue_RequiresNewerRevision()
    {
        UpdateItemRequest? captured = null;
        _client.Setup(client => client.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateItemRequest, CancellationToken>((request, _) => captured = request)
            .ThrowsAsync(new ConditionalCheckFailedException("revision advanced"));
        var (previous, _) = ValidItems();
        previous.Status = OrderStatus.SubmittedByCustomer;
        previous.Revision = 1;
        var changed = new Order { OrderId = previous.OrderId, CustomerId = previous.CustomerId,
            ReservationId = previous.ReservationId, ReservationAddress = previous.ReservationAddress,
            ReservationTime = previous.ReservationTime, Status = OrderStatus.Created, Dishes = previous.Dishes };

        await Assert.ThrowsAsync<ValidationException>(() => _repository.SaveOrderIfUnchangedAsync(changed, previous));
        Assert.NotNull(captured);
        Assert.Contains("#rev = :expectedRevision", captured.ConditionExpression);
        Assert.DoesNotContain("attribute_not_exists(#rev)", captured.ConditionExpression);
        Assert.Equal("1", captured.ExpressionAttributeValues[":expectedRevision"].N);
    }

    private static (Order, Reservation) ValidItems()
    {
        var order = new Order { OrderId = "order", CustomerId = "owner", ReservationId = "reservation",
            ReservationAddress = "Test", ReservationTime = "Later", Status = OrderStatus.Created,
            Dishes = [new OrderItem { DishId = "dish", Quantity = 1 }] };
        var reservation = new Reservation { Id = "reservation", CustomerId = "owner", WaiterId = "waiter",
            TableId = "table", StartTime = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            Status = ReservationStatus.Reserved };
        return (order, reservation);
    }
}
