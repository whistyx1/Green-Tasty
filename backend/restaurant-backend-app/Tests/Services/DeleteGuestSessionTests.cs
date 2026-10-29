using Amazon.CognitoIdentityProvider.Model;
using APIHandler.Constants;
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

public class DeleteGuestSessionTests
{
    private readonly Mock<IGroupReservationRepository> _groups = new();
    private readonly Mock<IReservationRepository> _reservations = new();
    private readonly Mock<IGuestSessionRepository> _sessions = new();
    private readonly Mock<IDishRepository> _dishes = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly GroupReservation _group = new() { GroupReservationId = "group", ReservationId = "reservation", ReservationToken = "token" };
    private readonly Reservation _reservation = new() { Id = "reservation", TableId = "table", WaiterId = "waiter", CustomerId = "owner" };
    private readonly GuestSession _removed = Session("removed", false, new OrderItem { DishId = "removed-dish", Quantity = 20 });
    private readonly List<GuestSession> _remaining;
    private readonly GuestSessionService _service;
    public DeleteGuestSessionTests()
    {
        _remaining = [_removed, Session("guest", true, new OrderItem { DishId = "dish", Quantity = 3 }),
            new GuestSession { Id = "owner-session", GroupReservationId = "group", GuestName = "Owner", IsOwnerSession = true, OwnerUserId = "owner", Dishes = null! }];
        _groups.Setup(r => r.GetGroupReservationByIdAsync("group")).ReturnsAsync(_group);
        _reservations.Setup(r => r.GetReservationByIdAsync("reservation")).ReturnsAsync(_reservation);
        _sessions.Setup(r => r.GetByGuestSessionId("removed")).ReturnsAsync(_removed);
        _sessions.Setup(r => r.GetAllByGroupReservationIdAsync("group")).ReturnsAsync(_remaining);
        _dishes.Setup(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>())).ReturnsAsync([new Dish { Id = "dish", Name = "Soup", Price = 10.25m }]);
        _users.Setup(r => r.GetUserByIdAsync("owner")).ReturnsAsync(new UserType { Attributes = [new AttributeType { Name = "given_name", Value = "Igor" }] });
        _service = new(_groups.Object, _sessions.Object, _dishes.Object, _reservations.Object, _users.Object, Mock.Of<ITableRepository>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Delete_ReturnsUpdatedDecimalSnapshotAndPreservesOwnerEvenWithStaleMembership(bool submitted)
    {
        _removed.IsSubmitted = submitted;
        var result = await _service.DeleteGuestSessionAsync("owner", "group", "removed");
        Assert.Equal(30.75m, result.Total);
        var participants = result.Participants.ToArray();
        Assert.Equal(2, participants.Length);
        Assert.DoesNotContain(participants, p => p.Id == "removed");
        Assert.True(participants[0].IsSubmitted);
        Assert.Equal(ParticipantType.Guest, participants[0].Type);
        Assert.Equal(30.75m, participants[0].Total);
        Assert.Equal(ParticipantType.Owner, participants[1].Type);
        Assert.Equal("Igor", participants[1].Name);
        Assert.Empty(participants[1].Items);
        _sessions.Verify(r => r.DeleteAsync(_removed), Times.Once);
        _sessions.Verify(r => r.DeleteAsync(It.Is<GuestSession>(s => s.IsOwnerSession)), Times.Never);
        _dishes.Verify(r => r.GetDishesByIdsAsync(It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { "dish" }))), Times.Once);
    }

    [Fact]
    public async Task Delete_LastGuestReturnsEmptySnapshotAndZeroTotal()
    {
        _remaining.RemoveAll(s => s.Id != "removed");
        var result = await _service.DeleteGuestSessionAsync("owner", "group", "removed");
        Assert.Empty(result.Participants); Assert.Equal(0m, result.Total);
        _dishes.VerifyNoOtherCalls(); _users.VerifyNoOtherCalls();
        _sessions.Verify(r => r.DeleteAsync(_removed), Times.Once);
    }

    [Theory]
    [InlineData("other")]
    [InlineData("")]
    public async Task Delete_NonOwnerCannotReadSessionsOrWrite(string caller)
    {
        await Assert.ThrowsAsync<ActionForbiddenException>(() => _service.DeleteGuestSessionAsync(caller, "group", "removed"));
        _sessions.VerifyNoOtherCalls(); _dishes.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("group")]
    [InlineData("reservation")]
    [InlineData("session")]
    public async Task Delete_MissingEntityIsNotFoundWithoutWrites(string missing)
    {
        if (missing == "group") _groups.Setup(r => r.GetGroupReservationByIdAsync("group")).ReturnsAsync((GroupReservation?)null);
        if (missing == "reservation") _reservations.Setup(r => r.GetReservationByIdAsync("reservation")).ReturnsAsync((Reservation?)null);
        if (missing == "session") _sessions.Setup(r => r.GetByGuestSessionId("removed")).ReturnsAsync((GuestSession?)null);
        var error = await Record.ExceptionAsync(() => _service.DeleteGuestSessionAsync("owner", "group", "removed"));
        if (missing == "group") Assert.IsType<GroupReservationNotFoundException>(error);
        if (missing == "reservation") Assert.IsType<ReservationNotFoundException>(error);
        if (missing == "session") Assert.IsType<GuestSessionNotFoundException>(error);
        _sessions.Verify(r => r.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
        _sessions.Verify(r => r.GetAllByGroupReservationIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Delete_RejectsWrongGroupAndOwnerSessionWithoutWrites(bool ownerSession)
    {
        if (ownerSession) _removed.IsOwnerSession = true; else _removed.GroupReservationId = "other-group";
        await Assert.ThrowsAsync<ValidationException>(() => _service.DeleteGuestSessionAsync("owner", "group", "removed"));
        _sessions.Verify(r => r.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
        _sessions.Verify(r => r.GetAllByGroupReservationIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(GroupReservationStatus.Submitted)]
    [InlineData(GroupReservationStatus.Closed)]
    public async Task Delete_InactiveGroupRejectedBeforeSessionReads(GroupReservationStatus status)
    {
        _group.Status = status;
        var error = await Record.ExceptionAsync(() => _service.DeleteGuestSessionAsync("owner", "group", "removed"));
        if (status == GroupReservationStatus.Submitted) Assert.IsType<GroupReservationSubmittedException>(error);
        else Assert.IsType<GroupReservationExpiredException>(error);
        _sessions.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(ReservationStatus.Cancelled)]
    [InlineData(ReservationStatus.InProgress)]
    [InlineData(ReservationStatus.Completed)]
    public async Task Delete_InactiveReservationRejectedBeforeSessionReads(ReservationStatus status)
    {
        _reservation.Status = status;
        await Assert.ThrowsAsync<GroupReservationExpiredException>(() => _service.DeleteGuestSessionAsync("owner", "group", "removed"));
        _sessions.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Delete_MissingRemainingDishDoesNotUndercountOrDelete()
    {
        _dishes.Setup(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>())).ReturnsAsync([]);
        await Assert.ThrowsAsync<DishNotFoundException>(() => _service.DeleteGuestSessionAsync("owner", "group", "removed"));
        _sessions.Verify(r => r.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    private static GuestSession Session(string id, bool submitted, params OrderItem[] dishes) => new() { Id = id, GroupReservationId = "group", GuestName = id, IsSubmitted = submitted, Dishes = dishes.ToList() };
}
