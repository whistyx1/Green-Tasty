using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Repositories.GuestSessions;
using Moq;

namespace APIHandler.Tests.Repositories;

[Collection("Group preorder repository")]
public class ReminderRepositoryTests : IDisposable
{
    private readonly Dictionary<string, string?> _old = new[]
        { "guestSessionTable", "groupReservationsTable", "reservationsTable", "notificationsTable" }
        .ToDictionary(key => key, Environment.GetEnvironmentVariable);
    private readonly Mock<IAmazonDynamoDB> _client = new();
    private readonly IDynamoDBContext _context;
    private readonly ReminderRepository _repository;

    public ReminderRepositoryTests()
    {
        Environment.SetEnvironmentVariable("guestSessionTable", "test-sessions");
        Environment.SetEnvironmentVariable("groupReservationsTable", "test-groups");
        Environment.SetEnvironmentVariable("reservationsTable", "test-reservations");
        Environment.SetEnvironmentVariable("notificationsTable", "test-notifications");
        _context = new DynamoDBContextBuilder().WithDynamoDBClient(() => _client.Object)
            .ConfigureContext(config => config.DisableFetchingTableMetadata = true).Build();
        _repository = new ReminderRepository(_context, _client.Object);
    }

    public void Dispose()
    {
        foreach (var entry in _old) Environment.SetEnvironmentVariable(entry.Key, entry.Value);
        _context.Dispose();
    }

    [Fact]
    public async Task Claim_InAppReminderIsAtomicWithCooldownAndLifecycleConditions()
    {
        TransactWriteItemsRequest? request = null;
        _client.Setup(c => c.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((value, _) => request = value)
            .ReturnsAsync(new TransactWriteItemsResponse());
        var (session, group, reservation) = Data();
        var sentAt = new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var notification = new Notification { Id = "notification", UserId = "guest-session:guest",
            Message = "Reminder", ReservationId = reservation.Id, CreatedAt = DateTime.UtcNow };

        await _repository.ClaimAsync(session, group, reservation, "owner", sentAt, "attempt", notification);

        Assert.NotNull(request);
        Assert.Equal(4, request.TransactItems.Count);
        var guestWrite = request.TransactItems[0].Update;
        Assert.Equal("test-sessions", guestWrite.TableName);
        Assert.Contains("lastReminderAtUnixMs <= :cutoff", guestWrite.ConditionExpression);
        Assert.Contains("isSubmitted = :false", guestWrite.ConditionExpression);
        Assert.Contains("orderingPreference = :online", guestWrite.ConditionExpression);
        Assert.Contains("#rev = :revision", guestWrite.ConditionExpression);
        Assert.Equal((sentAt - 900_000).ToString(), guestWrite.ExpressionAttributeValues[":cutoff"].N);
        Assert.Equal("attempt", guestWrite.ExpressionAttributeValues[":attempt"].S);
        var groupWrite = request.TransactItems[1].Update;
        Assert.Equal("test-groups", groupWrite.TableName);
        Assert.Contains("#status = :open", groupWrite.ConditionExpression);
        Assert.Contains("#participantRevision", groupWrite.UpdateExpression);
        var reservationCheck = request.TransactItems[2].ConditionCheck;
        Assert.Equal("test-reservations", reservationCheck.TableName);
        Assert.Contains("customerId = :owner", reservationCheck.ConditionExpression);
        Assert.Contains("startTime = :start", reservationCheck.ConditionExpression);
        Assert.Equal("owner", reservationCheck.ExpressionAttributeValues[":owner"].S);
        var notificationPut = request.TransactItems[3].Put;
        Assert.Equal("test-notifications", notificationPut.TableName);
        Assert.Equal("guest-session:guest", notificationPut.Item["userId"].S);
        Assert.Equal("attribute_not_exists(id)", notificationPut.ConditionExpression);
    }

    [Fact]
    public async Task Claim_ConcurrentConditionalFailureReturnsConflictWithoutFallbackWrite()
    {
        _client.Setup(c => c.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TransactionCanceledException("changed")
            { CancellationReasons = [new CancellationReason { Code = "ConditionalCheckFailed" }] });
        var (session, group, reservation) = Data();
        await Assert.ThrowsAsync<GroupPreOrderConflictException>(() =>
            _repository.ClaimAsync(session, group, reservation, "owner", 1_000_000, "attempt", null));
        _client.Verify(c => c.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _client.Verify(c => c.UpdateItemAsync(It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (GuestSession, GroupReservation, Reservation) Data() =>
        (new GuestSession { Id = "guest", GroupReservationId = "group", GuestName = "Guest", Dishes = [], Revision = 2 },
         new GroupReservation { GroupReservationId = "group", ReservationId = "reservation", ReservationToken = "token",
             Status = GroupReservationStatus.Open },
         new Reservation { Id = "reservation", TableId = "table", WaiterId = "waiter", CustomerId = "owner",
             StartTime = new DateTime(2026, 11, 5, 12, 45, 0, DateTimeKind.Utc),
             EndTime = new DateTime(2026, 11, 5, 14, 0, 0, DateTimeKind.Utc),
             GuestCount = 2, Status = APIHandler.Constants.ReservationStatus.Reserved });
}
