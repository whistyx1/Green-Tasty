using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Repositories.GuestSessions;
using Moq;

namespace APIHandler.Tests.Repositories;

[Collection("Group preorder repository")]
public class GuestSessionRepositoryTests : IDisposable
{
    private readonly string? _originalTable = Environment.GetEnvironmentVariable("guestSessionTable");
    private readonly Mock<IAmazonDynamoDB> _client = new();
    private readonly IDynamoDBContext _serializer;

    public GuestSessionRepositoryTests()
    {
        Environment.SetEnvironmentVariable("guestSessionTable", "test-sessions");
        _serializer = new DynamoDBContextBuilder().WithDynamoDBClient(() => _client.Object)
            .ConfigureContext(config => config.DisableFetchingTableMetadata = true).Build();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("guestSessionTable", _originalTable);
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
    public async Task Save_UsesConditionalUpdateSoDeletedSessionCannotBeRecreated()
    {
        var guest = Session("guest");
        guest.Dishes = [new OrderItem { DishId = "salad", Quantity = 2 }];
        var context = new Mock<IDynamoDBContext>();
        context.Setup(value => value.ToDocument(guest, It.IsAny<ToDocumentConfig>()))
            .Returns(_serializer.ToDocument(guest, new ToDocumentConfig { OverrideTableName = "test-sessions" }));
        UpdateItemRequest? captured = null;
        _client.Setup(value => value.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateItemRequest, CancellationToken>((request, _) => captured = request)
            .ThrowsAsync(new ConditionalCheckFailedException("deleted"));
        var repository = new GuestSessionRepository(context.Object, _client.Object);

        await Assert.ThrowsAsync<GuestSessionNotFoundException>(() => repository.SaveAsync(guest));

        Assert.NotNull(captured);
        Assert.Equal("test-sessions", captured.TableName);
        Assert.Equal("attribute_exists(#id) AND #group = :group", captured.ConditionExpression);
        Assert.Equal("SET #dishes = :dishes, #submitted = :submitted", captured.UpdateExpression);
        Assert.Equal("guest", captured.Key["guestSessionId"].S);
        Assert.Equal("group", captured.ExpressionAttributeValues[":group"].S);
        Assert.Equal("salad", captured.ExpressionAttributeValues[":dishes"].L[0].M["dishId"].S);
        Assert.Equal("2", captured.ExpressionAttributeValues[":dishes"].L[0].M["quantity"].N);
        context.Verify(value => value.SaveAsync(It.IsAny<GuestSession>(), It.IsAny<SaveConfig>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static GuestSession Session(string id) => new()
    {
        Id = id, GroupReservationId = "group", GuestName = "Guest", Dishes = [],
    };
}
