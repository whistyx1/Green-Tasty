using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GuestSessions;
using APIHandler.Repositories.GuestSessions;
using Moq;

namespace APIHandler.Tests.Repositories;

[Collection("Group preorder repository")]
public class GuestSessionRepositoryTests : IDisposable
{
    private readonly string? _originalTable = Environment.GetEnvironmentVariable("guestSessionTable");
    private readonly string? _originalGroupTable = Environment.GetEnvironmentVariable("groupReservationsTable");
    private readonly Mock<IAmazonDynamoDB> _client = new();
    private readonly IDynamoDBContext _serializer;

    public GuestSessionRepositoryTests()
    {
        Environment.SetEnvironmentVariable("guestSessionTable", "test-sessions");
        Environment.SetEnvironmentVariable("groupReservationsTable", "test-groups");
        _serializer = new DynamoDBContextBuilder().WithDynamoDBClient(() => _client.Object)
            .ConfigureContext(config => config.DisableFetchingTableMetadata = true).Build();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("guestSessionTable", _originalTable);
        Environment.SetEnvironmentVariable("groupReservationsTable", _originalGroupTable);
        _serializer.Dispose();
    }

    [Fact]
    public async Task GetAll_StaleDeletedIndexEntryDoesNotRestoreRemovedParticipant()
    {
        var active = Session("active");
        var deleted = Session("deleted");
        var context = new Mock<IDynamoDBContext>();
        var search = new Mock<AsyncSearch<GuestSession>>();
        search.Setup(value => value.GetRemainingAsync(It.IsAny<CancellationToken>())).ReturnsAsync([active, deleted]);
        context.Setup(value => value.QueryAsync<GuestSession>("group", It.IsAny<QueryConfig>())).Returns(search.Object);
        context.Setup(value => value.LoadAsync<GuestSession?>("active", It.Is<LoadConfig>(config => config.ConsistentRead == true)))
            .ReturnsAsync(active);
        context.Setup(value => value.LoadAsync<GuestSession?>("deleted", It.Is<LoadConfig>(config => config.ConsistentRead == true)))
            .ReturnsAsync((GuestSession?)null);
        var repository = new GuestSessionRepository(context.Object, _client.Object);

        var result = await repository.GetAllByGroupReservationIdAsync("group");

        Assert.Equal("active", Assert.Single(result).Id);
        Assert.Equal(1, await repository.CountByGroupReservationId("group"));
        context.Verify(value => value.LoadAsync<GuestSession?>("deleted", It.Is<LoadConfig>(config => config.ConsistentRead == true)), Times.Exactly(2));
    }

    [Fact]
    public async Task GetByTokenHash_StaleIndexEntryCannotAuthorizeRemovedGuest()
    {
        var deleted = Session("deleted");
        deleted.SessionTokenHash = "old-hash";
        var context = new Mock<IDynamoDBContext>();
        var search = new Mock<AsyncSearch<GuestSession>>();
        search.Setup(value => value.GetRemainingAsync(It.IsAny<CancellationToken>())).ReturnsAsync([deleted]);
        context.Setup(value => value.QueryAsync<GuestSession>("old-hash", It.IsAny<QueryConfig>())).Returns(search.Object);
        context.Setup(value => value.LoadAsync<GuestSession?>("deleted", It.Is<LoadConfig>(config => config.ConsistentRead == true)))
            .ReturnsAsync((GuestSession?)null);
        var repository = new GuestSessionRepository(context.Object, _client.Object);

        Assert.Null(await repository.GetByTokenHash("old-hash"));
        context.Verify(value => value.LoadAsync<GuestSession?>("deleted", It.Is<LoadConfig>(config => config.ConsistentRead == true)), Times.Once);
    }

    [Fact]
    public async Task GetAll_UsesCurrentSubmissionStatusInsteadOfStaleIndexStatus()
    {
        var stale = Session("guest");
        stale.IsSubmitted = false;
        var current = Session("guest");
        current.IsSubmitted = true;
        var context = new Mock<IDynamoDBContext>();
        var search = new Mock<AsyncSearch<GuestSession>>();
        search.Setup(value => value.GetRemainingAsync(It.IsAny<CancellationToken>())).ReturnsAsync([stale]);
        context.Setup(value => value.QueryAsync<GuestSession>("group", It.IsAny<QueryConfig>())).Returns(search.Object);
        context.Setup(value => value.LoadAsync<GuestSession?>("guest", It.Is<LoadConfig>(config => config.ConsistentRead == true)))
            .ReturnsAsync(current);
        var repository = new GuestSessionRepository(context.Object, _client.Object);

        var result = await repository.GetAllByGroupReservationIdAsync("group");

        Assert.False(stale.IsSubmitted);
        Assert.Same(current, Assert.Single(result));
        Assert.True(result[0].IsSubmitted);
        context.Verify(value => value.LoadAsync<GuestSession?>("guest", It.Is<LoadConfig>(config => config.ConsistentRead == true)), Times.Once);
    }

    [Fact]
    public async Task GetAllConsistent_PaginatesBaseTableAndReloadsCurrentSessions()
    {
        var context = new Mock<IDynamoDBContext>();
        context.Setup(value => value.LoadAsync<GuestSession?>("first", It.Is<LoadConfig>(config => config.ConsistentRead == true)))
            .ReturnsAsync(Session("first"));
        context.Setup(value => value.LoadAsync<GuestSession?>("second", It.Is<LoadConfig>(config => config.ConsistentRead == true)))
            .ReturnsAsync(Session("second"));
        var cursor = new Dictionary<string, AttributeValue> { ["guestSessionId"] = new() { S = "first" } };
        var requests = new List<ScanRequest>();
        var pages = new Queue<ScanResponse>([
            new() { Items = [new() { ["guestSessionId"] = new AttributeValue { S = "first" } }], LastEvaluatedKey = cursor },
            new() { Items = [new() { ["guestSessionId"] = new AttributeValue { S = "second" } }], LastEvaluatedKey = [] }
        ]);
        _client.Setup(value => value.ScanAsync(It.IsAny<ScanRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ScanRequest, CancellationToken>((request, _) => requests.Add(request))
            .ReturnsAsync(() => pages.Dequeue());

        var result = await new GuestSessionRepository(context.Object, _client.Object)
            .GetAllByGroupReservationIdConsistentAsync("group");

        Assert.Equal(["first", "second"], result.Select(session => session.Id));
        Assert.Equal(2, requests.Count);
        Assert.All(requests, request => Assert.True(request.ConsistentRead));
        Assert.Equal("first", requests[1].ExclusiveStartKey["guestSessionId"].S);
    }

    [Fact]
    public async Task Create_AddsSessionAndIncrementsMembershipRevisionAtomically()
    {
        var session = Session("new");
        TransactWriteItemsRequest? request = null;
        _client.Setup(value => value.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((value, _) => request = value)
            .ReturnsAsync(new TransactWriteItemsResponse());

        await new GuestSessionRepository(_serializer, _client.Object).CreateAsync(session);

        Assert.Equal(2, request!.TransactItems.Count);
        Assert.Equal("new", request.TransactItems[0].Put.Item["guestSessionId"].S);
        Assert.Contains("participantRevision", request.TransactItems[1].Update.ExpressionAttributeNames.Values);
        Assert.Contains("#status = :open", request.TransactItems[1].Update.ConditionExpression);
    }

    [Fact]
    public async Task Save_UsesRevisionAndOpenGroupTransaction()
    {
        var guest = Session("guest");
        guest.Dishes = [new OrderItem { DishId = "salad", Quantity = 2 }];
        var context = new Mock<IDynamoDBContext>();
        context.Setup(value => value.ToDocument(guest, It.IsAny<ToDocumentConfig>()))
            .Returns(_serializer.ToDocument(guest, new ToDocumentConfig { OverrideTableName = "test-sessions" }));
        TransactWriteItemsRequest? captured = null;
        _client.Setup(value => value.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new TransactWriteItemsResponse());
        var repository = new GuestSessionRepository(context.Object, _client.Object);

        await repository.SaveAsync(guest);

        Assert.NotNull(captured);
        Assert.Equal(2, captured.TransactItems.Count);
        var update = captured.TransactItems[0].Update;
        Assert.Equal("test-sessions", update.TableName);
        Assert.Contains("attribute_exists(#id)", update.ConditionExpression);
        Assert.Contains("attribute_not_exists(#rev)", update.ConditionExpression);
        Assert.Contains("attribute_not_exists(#submitted)", update.ConditionExpression);
        Assert.Contains("#rev = if_not_exists(#rev, :zero) + :one", update.UpdateExpression);
        Assert.Equal("guest", update.Key["guestSessionId"].S);
        Assert.Equal("group", update.ExpressionAttributeValues[":group"].S);
        Assert.Equal("salad", update.ExpressionAttributeValues[":dishes"].L[0].M["dishId"].S);
        Assert.Equal("2", update.ExpressionAttributeValues[":dishes"].L[0].M["quantity"].N);
        Assert.Equal("0", captured.TransactItems[1].Update.ExpressionAttributeValues[":open"].N);
        context.Verify(value => value.SaveAsync(It.IsAny<GuestSession>(), It.IsAny<SaveConfig>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetPreference_PreservesDraftAndUsesLegacyRevisionCondition()
    {
        var guest = Session("guest");
        guest.Dishes = [new OrderItem { DishId = "salad", Quantity = 2 }];
        var context = new Mock<IDynamoDBContext>();
        context.Setup(value => value.ToDocument(guest, It.IsAny<ToDocumentConfig>()))
            .Returns(_serializer.ToDocument(guest, new ToDocumentConfig { OverrideTableName = "test-sessions" }));
        TransactWriteItemsRequest? captured = null;
        _client.Setup(value => value.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new TransactWriteItemsResponse());

        await new GuestSessionRepository(context.Object, _client.Object).SetPreferenceAsync(guest, OrderingPreference.InRestaurant);

        Assert.Equal(2, guest.Dishes[0].Quantity);
        Assert.Null(guest.OrderingPreference);
        var update = captured!.TransactItems[0].Update;
        Assert.Contains("attribute_not_exists(#rev)", update.ConditionExpression);
        Assert.Contains("attribute_not_exists(#submitted)", update.ConditionExpression);
        Assert.DoesNotContain("#dishes =", update.UpdateExpression);
        Assert.Equal(OrderingPreference.InRestaurant, update.ExpressionAttributeValues[":preference"].S);
        Assert.Equal("0", update.ExpressionAttributeValues[":false"].N);
        Assert.Equal("test-groups", captured.TransactItems[1].Update.TableName);
    }

    private static GuestSession Session(string id) => new()
    {
        Id = id, GroupReservationId = "group", GuestName = "Guest", Dishes = [],
    };
}
