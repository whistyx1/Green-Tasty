using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Repositories.GroupPreOrders;
using Moq;

namespace APIHandler.Tests.Repositories;

[CollectionDefinition("Group preorder repository", DisableParallelization = true)]
public class GroupPreOrderRepositoryCollection;

[Collection("Group preorder repository")]
public class GroupPreOrderRepositoryTests : IDisposable
{
    private readonly Dictionary<string, string?> _originalEnvironment = new[] { "ordersTable", "groupReservationsTable", "reservationsTable", "guestSessionTable" }.ToDictionary(key => key, Environment.GetEnvironmentVariable);
    public void Dispose() { foreach (var pair in _originalEnvironment) Environment.SetEnvironmentVariable(pair.Key, pair.Value); _context.Dispose(); }
    private readonly Mock<IAmazonDynamoDB> _client = new();
    private readonly IDynamoDBContext _context;
    private readonly GroupPreOrderRepository _repository;
    public GroupPreOrderRepositoryTests()
    {
        // Fake resource names only. No credentials, network, or AWS resources are used.
        Environment.SetEnvironmentVariable("ordersTable", "test-orders");
        Environment.SetEnvironmentVariable("groupReservationsTable", "test-groups");
        Environment.SetEnvironmentVariable("reservationsTable", "test-reservations");
        Environment.SetEnvironmentVariable("guestSessionTable", "test-sessions");
        _context = new DynamoDBContextBuilder().WithDynamoDBClient(() => _client.Object)
            .ConfigureContext(config => config.DisableFetchingTableMetadata = true).Build();
        _repository = new(_client.Object, _context);
    }

    [Fact]
    public async Task EnsureOwner_UsesStableConditionalKeyAndRealEmptyCartSerialization()
    {
        PutItemRequest? saved = null;
        _client.Setup(c => c.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutItemRequest, CancellationToken>((request, _) => saved = request).ReturnsAsync(new PutItemResponse());
        var owner = await _repository.EnsureOwnerSessionAsync("group", "owner");
        Assert.Equal("owner-group", owner.Id);
        Assert.Null(owner.SessionTokenHash);
        Assert.Equal("attribute_not_exists(guestSessionId)", saved!.ConditionExpression);
        Assert.Equal("owner-group", saved.Item["guestSessionId"].S);
        // Match the application's default V1 conversion: booleans are numeric 1/0.
        Assert.Equal("1", saved.Item["isOwnerSession"].N);
        Assert.Empty(saved.Item["dishes"].L);
        Assert.Equal("0", saved.Item["isSubmitted"].N);
    }

    [Fact]
    public async Task Submit_SendsOneAtomicWriteWithRealEnumAndSnapshotSerialization()
    {
        TransactWriteItemsRequest? saved = null;
        _client.Setup(c => c.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((request, _) => saved = request).ReturnsAsync(new TransactWriteItemsResponse());
        await Submit();
        Assert.Equal(4, saved!.TransactItems.Count);
        var order = saved.TransactItems[0].Update;
        Assert.Equal("test-orders", order.TableName);
        Assert.Equal("order", order.Key["orderId"].S);
        Assert.Equal("owner", order.Key["customerId"].S);
        Assert.Equal(((int)OrderStatus.SubmittedByCustomer).ToString(), order.ExpressionAttributeValues[":newStatus"].N);
        Assert.Empty(order.ExpressionAttributeValues[":oldDishes"].L);
        Assert.Single(order.ExpressionAttributeValues[":newDishes"].L);
        Assert.Contains("dishes = :oldDishes", order.ConditionExpression);
        Assert.Equal("test-groups", saved.TransactItems[1].Update.TableName);
        Assert.Contains("startTime = :startTime", saved.TransactItems[2].ConditionCheck.ConditionExpression);
        Assert.Equal("test-sessions", saved.TransactItems[3].ConditionCheck.TableName);
        Assert.Equal("1", saved.TransactItems[3].ConditionCheck.ExpressionAttributeValues[":submitted"].N);
        _client.Verify(c => c.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _client.Verify(c => c.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Submit_ConditionalCancellationBecomesConflictWithoutFallbackWrites()
    {
        _client.Setup(c => c.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TransactionCanceledException("changed") { CancellationReasons = [new CancellationReason { Code = "ConditionalCheckFailed" }] });
        await Assert.ThrowsAsync<GroupPreOrderConflictException>(() => Submit());
        _client.Verify(c => c.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureOwner_ConcurrentCreationReturnsStronglyConsistentExistingOwner()
    {
        var context = new Mock<IDynamoDBContext>();
        context.Setup(c => c.ToDocument(It.IsAny<GuestSession>(), It.IsAny<ToDocumentConfig>())).Returns(_context.ToDocument(new GuestSession { Id = "owner-group", GuestName = "Owner", Dishes = [] }, new ToDocumentConfig { OverrideTableName = "test-sessions" }));
        _client.Setup(c => c.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(new ConditionalCheckFailedException("already created"));
        var existing = new GuestSession { Id = "owner-group", GroupReservationId = "group", GuestName = "Owner", Dishes = [], IsOwnerSession = true, OwnerUserId = "owner" };
        context.Setup(c => c.LoadAsync<GuestSession>("owner-group", It.Is<LoadConfig>(config => config.ConsistentRead == true), It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        Assert.Same(existing, await new GroupPreOrderRepository(_client.Object, context.Object).EnsureOwnerSessionAsync("group", "owner"));
    }

    [Fact]
    public async Task GuestCount_ExcludesStoredOwnerSession()
    {
        var context = new Mock<IDynamoDBContext>();
        var search = new Mock<AsyncSearch<GuestSession>>();
        search.Setup(s => s.GetRemainingAsync(It.IsAny<CancellationToken>())).ReturnsAsync([
            new GuestSession { Id = "owner", GuestName = "Owner", IsOwnerSession = true, Dishes = [] },
            new GuestSession { Id = "guest", GuestName = "Guest", Dishes = [] }
        ]);
        context.Setup(c => c.QueryAsync<GuestSession>("group", It.IsAny<QueryConfig>())).Returns(search.Object);
        Assert.Equal(1, await new APIHandler.Repositories.GuestSessions.GuestSessionRepository(context.Object).CountByGroupReservationId("group"));
    }

    private Task Submit() => _repository.SubmitAsync(
        new Order { OrderId = "order", CustomerId = "owner", ReservationId = "reservation", ReservationAddress = "address", ReservationTime = "time", Dishes = [] },
        new GroupReservation { GroupReservationId = "group", ReservationId = "reservation", ReservationToken = "token" },
        new Reservation { Id = "reservation", CustomerId = "owner", WaiterId = "waiter", TableId = "table", StartTime = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc) },
        [new OrderItem { DishId = "dish", Quantity = 2 }],
        [new GuestSession { Id = "guest", GroupReservationId = "group", GuestName = "Guest", IsSubmitted = true, Dishes = [new OrderItem { DishId = "dish", Quantity = 2 }] }]);
}
