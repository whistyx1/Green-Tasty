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
        TransactWriteItemsRequest? saved = null;
        _client.Setup(c => c.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((request, _) => saved = request)
            .ReturnsAsync(new TransactWriteItemsResponse());
        var owner = await _repository.EnsureOwnerSessionAsync("group", "owner");
        Assert.Equal("owner-group", owner.Id);
        Assert.Null(owner.SessionTokenHash);
        Assert.Equal(2, saved!.TransactItems.Count);
        Assert.Equal("attribute_not_exists(guestSessionId)", saved.TransactItems[0].Put.ConditionExpression);
        Assert.Equal("owner-group", saved.TransactItems[0].Put.Item["guestSessionId"].S);
        // Match the application's default V1 conversion: booleans are numeric 1/0.
        Assert.Equal("1", saved.TransactItems[0].Put.Item["isOwnerSession"].N);
        Assert.Empty(saved.TransactItems[0].Put.Item["dishes"].L);
        Assert.Equal("0", saved.TransactItems[0].Put.Item["isSubmitted"].N);
        Assert.Contains("participantRevision", saved.TransactItems[1].Update.ExpressionAttributeNames.Values);
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
        Assert.Contains("attribute_not_exists(#rev)", order.ConditionExpression);
        Assert.Contains("#rev = if_not_exists(#rev, :zero) + :one", order.UpdateExpression);
        Assert.Contains("#finalized = :true", order.UpdateExpression);
        Assert.Contains("attribute_not_exists(#finalized) OR #finalized = :false", order.ConditionExpression);
        Assert.Equal("1", order.ExpressionAttributeValues[":true"].N);
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
    public async Task CloseWithoutOrder_ChecksEveryParticipantAndMembershipRevisionWithoutWritingOrders()
    {
        TransactWriteItemsRequest? saved = null;
        _client.Setup(c => c.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((request, _) => saved = request)
            .ReturnsAsync(new TransactWriteItemsResponse());
        var (_, group, reservation, _) = OwnerContext();
        group.ParticipantRevision = 4;
        var owner = new GuestSession { Id = "owner-group", GroupReservationId = "group", GuestName = "Owner",
            IsOwnerSession = true, OwnerUserId = "owner", OrderingPreference = "InRestaurant", Dishes = [] };
        var guest = new GuestSession { Id = "guest", GroupReservationId = "group", GuestName = "Guest",
            OrderingPreference = "InRestaurant", Dishes = [new OrderItem { DishId = "salad", Quantity = 1 }] };

        await _repository.CloseWithoutOrderAsync(group, reservation, [owner, guest]);

        Assert.Equal(4, saved!.TransactItems.Count);
        var groupWrite = saved.TransactItems[0].Update;
        Assert.Contains("onlinePreOrderClosed = :true", groupWrite.UpdateExpression);
        Assert.Contains("#revision = :expectedRevision", groupWrite.ConditionExpression);
        Assert.Equal("4", groupWrite.ExpressionAttributeValues[":expectedRevision"].N);
        Assert.All(saved.TransactItems.Skip(2), item =>
            Assert.Equal("InRestaurant", item.ConditionCheck.ExpressionAttributeValues[":preference"].S));
        Assert.DoesNotContain(saved.TransactItems, item => item.Update?.TableName == "test-orders");
    }

    [Fact]
    public async Task Submit_RejectsChangedMembershipRevisionWithinTransaction()
    {
        TransactWriteItemsRequest? saved = null;
        _client.Setup(c => c.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((request, _) => saved = request)
            .ThrowsAsync(new TransactionCanceledException("membership changed") {
                CancellationReasons = [new CancellationReason { Code = "ConditionalCheckFailed" }] });

        await Assert.ThrowsAsync<GroupPreOrderConflictException>(() => Submit());

        Assert.Contains("attribute_not_exists(#revision)", saved!.TransactItems[1].Update.ConditionExpression);
        Assert.Equal("0", saved.TransactItems[1].Update.ExpressionAttributeValues[":expectedRevision"].N);
    }

    [Fact]
    public async Task Submit_ConditionChecksInRestaurantParticipantsAsWellAsSubmittedGuests()
    {
        TransactWriteItemsRequest? saved = null;
        _client.Setup(c => c.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((request, _) => saved = request)
            .ReturnsAsync(new TransactWriteItemsResponse());
        var (_, group, reservation, order) = OwnerContext();
        group.ParticipantRevision = 2;
        var online = new GuestSession { Id = "online", GroupReservationId = "group", GuestName = "Online",
            IsSubmitted = true, Dishes = [new OrderItem { DishId = "salad", Quantity = 1 }] };
        var inRestaurant = new GuestSession { Id = "restaurant", GroupReservationId = "group", GuestName = "Restaurant",
            OrderingPreference = "InRestaurant", Dishes = [], Revision = 3 };

        await _repository.SubmitAsync(order, group, reservation,
            [new OrderItem { DishId = "salad", Quantity = 1 }], [online, inRestaurant]);

        Assert.Equal(5, saved!.TransactItems.Count);
        Assert.Equal("2", saved.TransactItems[1].Update.ExpressionAttributeValues[":expectedRevision"].N);
        Assert.Contains("attribute_not_exists(orderingPreference)", saved.TransactItems[3].ConditionCheck.ConditionExpression);
        Assert.Equal("InRestaurant", saved.TransactItems[4].ConditionCheck.ExpressionAttributeValues[":preference"].S);
        Assert.Contains("revision = :revision", saved.TransactItems[4].ConditionCheck.ConditionExpression);
    }

    [Fact]
    public async Task EnsureOwner_ConcurrentCreationReturnsStronglyConsistentExistingOwner()
    {
        var context = new Mock<IDynamoDBContext>();
        context.Setup(c => c.ToDocument(It.IsAny<GuestSession>(), It.IsAny<ToDocumentConfig>())).Returns(_context.ToDocument(new GuestSession { Id = "owner-group", GuestName = "Owner", Dishes = [] }, new ToDocumentConfig { OverrideTableName = "test-sessions" }));
        _client.Setup(c => c.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TransactionCanceledException("already created") { CancellationReasons = [new CancellationReason { Code = "ConditionalCheckFailed" }] });
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
            new GuestSession { Id = "owner", GroupReservationId = "group", GuestName = "Owner", IsOwnerSession = true, Dishes = [] },
            new GuestSession { Id = "guest", GroupReservationId = "group", GuestName = "Guest", Dishes = [] }
        ]);
        context.Setup(c => c.QueryAsync<GuestSession>("group", It.IsAny<QueryConfig>())).Returns(search.Object);
        context.Setup(c => c.LoadAsync<GuestSession?>("owner", It.Is<LoadConfig>(config => config.ConsistentRead == true)))
            .ReturnsAsync(new GuestSession { Id = "owner", GroupReservationId = "group", GuestName = "Owner", IsOwnerSession = true, Dishes = [] });
        context.Setup(c => c.LoadAsync<GuestSession?>("guest", It.Is<LoadConfig>(config => config.ConsistentRead == true)))
            .ReturnsAsync(new GuestSession { Id = "guest", GroupReservationId = "group", GuestName = "Guest", Dishes = [] });
        Assert.Equal(1, await new APIHandler.Repositories.GuestSessions.GuestSessionRepository(context.Object, _client.Object).CountByGroupReservationId("group"));
    }

    [Fact]
    public async Task OwnerReadiness_AtomicallyLocksSessionAndExistingOrder()
    {
        TransactWriteItemsRequest? saved = null;
        _client.Setup(c => c.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((request, _) => saved = request)
            .ReturnsAsync(new TransactWriteItemsResponse());
        var (owner, group, reservation, order) = OwnerContext();

        await _repository.SubmitOwnerAsync(owner, group, reservation, order);

        Assert.Equal(4, saved!.TransactItems.Count);
        Assert.Contains("#submitted = :true", saved.TransactItems[0].Update.UpdateExpression);
        Assert.Contains("attribute_not_exists(#pref) OR #pref = :online", saved.TransactItems[0].Update.ConditionExpression);
        Assert.Equal("1", saved.TransactItems[0].Update.ExpressionAttributeValues[":true"].N);
        Assert.Equal("test-groups", saved.TransactItems[1].Update.TableName);
        Assert.Equal("test-reservations", saved.TransactItems[2].ConditionCheck.TableName);
        Assert.Contains("#ready = :true", saved.TransactItems[3].Update.UpdateExpression);
        Assert.Contains("dishes = :dishes", saved.TransactItems[3].Update.ConditionExpression);
        Assert.Contains("attribute_not_exists(#rev) OR #rev = :expected", saved.TransactItems[3].Update.ConditionExpression);
        Assert.Contains("#rev = if_not_exists(#rev, :zero) + :one", saved.TransactItems[3].Update.UpdateExpression);
        Assert.Contains("attribute_not_exists(#rev) OR #rev = :expected", saved.TransactItems[0].Update.ConditionExpression);
        Assert.Equal("1", saved.TransactItems[3].Update.ExpressionAttributeValues[":true"].N);
        _client.Verify(c => c.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OwnerPreference_ConditionChecksDraftOrderWithoutChangingDishes()
    {
        TransactWriteItemsRequest? saved = null;
        _client.Setup(c => c.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((request, _) => saved = request)
            .ReturnsAsync(new TransactWriteItemsResponse());
        var (owner, group, reservation, order) = OwnerContext();

        await _repository.SetOwnerPreferenceAsync(owner, group, reservation, order, "InRestaurant");

        Assert.Equal(4, saved!.TransactItems.Count);
        Assert.Equal("InRestaurant", saved.TransactItems[0].Update.ExpressionAttributeValues[":preference"].S);
        Assert.DoesNotContain("dishes", saved.TransactItems[0].Update.UpdateExpression);
        Assert.Contains("#ready = if_not_exists(#ready, :false)", saved.TransactItems[3].Update.UpdateExpression);
        Assert.DoesNotContain("dishes", saved.TransactItems[3].Update.UpdateExpression);
        Assert.Equal("0", saved.TransactItems[3].Update.ExpressionAttributeValues[":false"].N);
    }

    private static (GuestSession Owner, GroupReservation Group, Reservation Reservation, Order Order) OwnerContext() => (
        new GuestSession { Id = "owner-group", GroupReservationId = "group", GuestName = "Owner", Dishes = [],
            IsOwnerSession = true, OwnerUserId = "owner" },
        new GroupReservation { GroupReservationId = "group", ReservationId = "reservation", ReservationToken = "token" },
        new Reservation { Id = "reservation", CustomerId = "owner", WaiterId = "waiter", TableId = "table",
            StartTime = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc) },
        new Order { OrderId = "order", CustomerId = "owner", ReservationId = "reservation", ReservationAddress = "address",
            ReservationTime = "time", Dishes = [new OrderItem { DishId = "salad", Quantity = 1 }] });

    private Task Submit() => _repository.SubmitAsync(
        new Order { OrderId = "order", CustomerId = "owner", ReservationId = "reservation", ReservationAddress = "address", ReservationTime = "time", Dishes = [] },
        new GroupReservation { GroupReservationId = "group", ReservationId = "reservation", ReservationToken = "token" },
        new Reservation { Id = "reservation", CustomerId = "owner", WaiterId = "waiter", TableId = "table", StartTime = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc) },
        [new OrderItem { DishId = "dish", Quantity = 2 }],
        [new GuestSession { Id = "guest", GroupReservationId = "group", GuestName = "Guest", IsSubmitted = true, Dishes = [new OrderItem { DishId = "dish", Quantity = 2 }] }]);
}
