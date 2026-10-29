using Amazon.CognitoIdentityProvider.Model;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GuestSessions;
using APIHandler.Repositories.Auth;
using APIHandler.Repositories.Dishes;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.GuestSessions;
using Moq;

namespace APIHandler.Tests.Services;

public class GetGroupSessionsTests
{
    private const string OwnerId = "owner-user";
    private const string GroupId = "group-1";
    private readonly Mock<IGroupReservationRepository> _groups = new();
    private readonly Mock<IGuestSessionRepository> _sessions = new();
    private readonly Mock<IDishRepository> _dishes = new();
    private readonly Mock<IReservationRepository> _reservations = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly GuestSessionService _service;

    public GetGroupSessionsTests()
    {
        _service = new GuestSessionService(_groups.Object, _sessions.Object, _dishes.Object,
            _reservations.Object, _users.Object, Mock.Of<ITableRepository>());
        _groups.Setup(repository => repository.GetGroupReservationByIdAsync(GroupId))
            .ReturnsAsync(new GroupReservation
            {
                GroupReservationId = GroupId,
                ReservationId = "reservation-1",
                ReservationToken = "invite-token"
            });
        _reservations.Setup(repository => repository.GetReservationByIdAsync("reservation-1"))
            .ReturnsAsync(new Reservation
            {
                Id = "reservation-1", TableId = "table-1", WaiterId = "waiter-1", CustomerId = OwnerId
            });
    }

    [Fact]
    public async Task GetAllSessionsAsync_ReturnsAllCartsWithDecimalTotalsAndSubmissionStatus()
    {
        var sessions = new List<GuestSession>
        {
            Session("andrii", "Andrii", true, new OrderItem { DishId = "salad", Quantity = 2 }),
            Session("olena", "Olena", false, new OrderItem { DishId = "soup", Quantity = 1 })
        };
        _sessions.Setup(repository => repository.GetAllByGroupReservationIdAsync(GroupId)).ReturnsAsync(sessions);
        _dishes.Setup(repository => repository.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([Dish("salad", "Salad", 180.25m), Dish("soup", "Soup", 220.10m)]);

        var result = await _service.GetAllSessionsAsync(OwnerId, GroupId);
        var participants = result.Participants.ToArray();

        Assert.Equal(2, participants.Length);
        Assert.Equal("andrii", participants[0].Id);
        Assert.Equal("Andrii", participants[0].Name);
        Assert.Equal(ParticipantType.Guest, participants[0].Type);
        Assert.True(participants[0].IsSubmitted);
        var salad = Assert.Single(participants[0].Items);
        Assert.Equal("salad", salad.Dish.Id);
        Assert.Equal("Salad", salad.Dish.Name);
        Assert.Equal(180.25m, salad.Dish.Price);
        Assert.Equal(2, salad.Quantity);
        Assert.Equal(360.50m, participants[0].Total);
        Assert.Equal("olena", participants[1].Id);
        Assert.False(participants[1].IsSubmitted);
        Assert.Equal(220.10m, participants[1].Total);
        Assert.Equal(580.60m, result.Total);
        _sessions.Verify(repository => repository.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
        _sessions.Verify(repository => repository.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task GetAllSessionsAsync_ExistingOwnerSessionUsesOwnerTypeAndCognitoName()
    {
        var ownerSession = Session("owner-session", "Owner", false);
        ownerSession.IsOwnerSession = true;
        ownerSession.OwnerUserId = OwnerId;
        _sessions.Setup(repository => repository.GetAllByGroupReservationIdAsync(GroupId)).ReturnsAsync([ownerSession]);
        _users.Setup(repository => repository.GetUserByIdAsync(OwnerId))
            .ReturnsAsync(new UserType { Attributes = [new AttributeType { Name = "given_name", Value = "Yevhenii" }] });

        var result = await _service.GetAllSessionsAsync(OwnerId, GroupId);

        var owner = Assert.Single(result.Participants);
        Assert.Equal("owner-session", owner.Id);
        Assert.Equal(ParticipantType.Owner, owner.Type);
        Assert.Equal("Yevhenii", owner.Name);
        Assert.Empty(owner.Items);
        Assert.Equal(0m, owner.Total);
        Assert.Equal(0m, result.Total);
    }

    [Fact]
    public async Task GetAllSessionsAsync_MissingOwnerProfileUsesStoredName()
    {
        var ownerSession = Session("owner-session", "Stored owner", false);
        ownerSession.IsOwnerSession = true;
        _sessions.Setup(repository => repository.GetAllByGroupReservationIdAsync(GroupId)).ReturnsAsync([ownerSession]);
        _users.Setup(repository => repository.GetUserByIdAsync(OwnerId)).ReturnsAsync((UserType?)null);

        var result = await _service.GetAllSessionsAsync(OwnerId, GroupId);

        Assert.Equal("Stored owner", Assert.Single(result.Participants).Name);
    }

    [Fact]
    public async Task GetAllSessionsAsync_EmptyGroupReturnsEmptySnapshotWithoutInventingOwnerSession()
    {
        _sessions.Setup(repository => repository.GetAllByGroupReservationIdAsync(GroupId)).ReturnsAsync([]);

        var result = await _service.GetAllSessionsAsync(OwnerId, GroupId);

        Assert.Empty(result.Participants);
        Assert.Equal(0m, result.Total);
        _dishes.Verify(repository => repository.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()), Times.Never);
        _users.Verify(repository => repository.GetUserByIdAsync(It.IsAny<string>()), Times.Never);
        _sessions.Verify(repository => repository.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task GetAllSessionsAsync_NullLegacyCartReturnsEmptyItemsAndZeroTotal()
    {
        var session = Session("guest-1", "Guest", false);
        session.Dishes = null!; // A legacy DynamoDB record may omit the collection.
        _sessions.Setup(repository => repository.GetAllByGroupReservationIdAsync(GroupId)).ReturnsAsync([session]);

        var result = await _service.GetAllSessionsAsync(OwnerId, GroupId);

        Assert.Empty(Assert.Single(result.Participants).Items);
        Assert.Equal(0m, result.Total);
    }

    [Fact]
    public async Task GetAllSessionsAsync_SharedDishIsFetchedOnceAndPricedPerParticipant()
    {
        _sessions.Setup(repository => repository.GetAllByGroupReservationIdAsync(GroupId))
            .ReturnsAsync([
                Session("one", "One", true, new OrderItem { DishId = "salad", Quantity = 2 }),
                Session("two", "Two", false, new OrderItem { DishId = "salad", Quantity = 3 })
            ]);
        _dishes.Setup(repository => repository.GetDishesByIdsAsync(It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { "salad" }))))
            .ReturnsAsync([Dish("salad", "Salad", 2.50m)]);

        var result = await _service.GetAllSessionsAsync(OwnerId, GroupId);

        Assert.Equal(new[] { 5m, 7.50m }, result.Participants.Select(participant => participant.Total));
        Assert.Equal(12.50m, result.Total);
        _dishes.Verify(repository => repository.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()), Times.Once);
    }

    [Fact]
    public async Task GetAllSessionsAsync_NonOwnerIsRejectedBeforeReadingSessionsOrDishes()
    {
        await Assert.ThrowsAsync<ActionForbiddenException>(() => _service.GetAllSessionsAsync("another-user", GroupId));

        _sessions.Verify(repository => repository.GetAllByGroupReservationIdAsync(It.IsAny<string>()), Times.Never);
        _dishes.Verify(repository => repository.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()), Times.Never);
        _users.Verify(repository => repository.GetUserByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetAllSessionsAsync_MissingGroupThrowsNotFound()
    {
        _groups.Setup(repository => repository.GetGroupReservationByIdAsync(GroupId)).ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _service.GetAllSessionsAsync(OwnerId, GroupId));

        _reservations.Verify(repository => repository.GetReservationByIdAsync(It.IsAny<string>()), Times.Never);
        _sessions.Verify(repository => repository.GetAllByGroupReservationIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetAllSessionsAsync_MissingParentReservationThrowsNotFound()
    {
        _reservations.Setup(repository => repository.GetReservationByIdAsync("reservation-1")).ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(() => _service.GetAllSessionsAsync(OwnerId, GroupId));

        _sessions.Verify(repository => repository.GetAllByGroupReservationIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetAllSessionsAsync_MissingReferencedDishFailsInsteadOfReturningIncorrectTotal()
    {
        _sessions.Setup(repository => repository.GetAllByGroupReservationIdAsync(GroupId))
            .ReturnsAsync([Session("guest", "Guest", true, new OrderItem { DishId = "removed-dish", Quantity = 2 })]);
        _dishes.Setup(repository => repository.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>())).ReturnsAsync([]);

        await Assert.ThrowsAsync<DishNotFoundException>(() => _service.GetAllSessionsAsync(OwnerId, GroupId));
    }

    private static GuestSession Session(string id, string name, bool submitted, params OrderItem[] dishes) => new()
    {
        Id = id, GuestName = name, GroupReservationId = GroupId, IsSubmitted = submitted, Dishes = dishes.ToList()
    };

    private static Dish Dish(string id, string name, decimal price) => new()
    {
        Id = id, Name = name, Price = price, Description = "Test dish", Weight = "100 g",
        Calories = "100", Proteins = "1", Fats = "1", Carbohydrates = "1", Vitamins = "A"
    };
}
