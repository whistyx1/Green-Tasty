using APIHandler.Entities;
using APIHandler.Constants;
using APIHandler.Repositories.Auth;
using APIHandler.Repositories.Dishes;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.GuestSessions;
using Moq;
namespace APIHandler.Tests.Services;
public class GroupOwnerCapacityTests
{
    [Theory]
    [InlineData(ReservationStatus.Reserved, true)]
    [InlineData(ReservationStatus.Cancelled, false)]
    public async Task Status_ReportsOnlineClosureOnlyWhileTableReservationRemainsActive(
        ReservationStatus reservationStatus, bool expected)
    {
        var groups = new Mock<IGroupReservationRepository>();
        var reservations = new Mock<IReservationRepository>();
        groups.Setup(r => r.GetGroupReservationByIdAsync("group")).ReturnsAsync(new GroupReservation {
            GroupReservationId = "group", ReservationId = "reservation", ReservationToken = "token",
            Status = GroupReservationStatus.Closed, OnlinePreOrderClosed = true });
        reservations.Setup(r => r.GetReservationByIdAsync("reservation")).ReturnsAsync(new Reservation {
            Id = "reservation", WaiterId = "waiter", TableId = "table", Status = reservationStatus });
        var service = new GuestSessionService(groups.Object, Mock.Of<IGuestSessionRepository>(),
            Mock.Of<IDishRepository>(), reservations.Object, Mock.Of<IUserRepository>(), Mock.Of<ITableRepository>());

        var status = await service.GetGroupReservationStatusAsync("group");

        Assert.Equal(expected, status.OnlinePreOrderClosed);
        Assert.Equal(GroupReservationStatus.Closed, status.Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Status_CountsOwnerExactlyOnceWithOrWithoutStoredOwnerSession(bool storedOwner)
    {
        var groups = new Mock<IGroupReservationRepository>();
        var reservations = new Mock<IReservationRepository>();
        var sessions = new Mock<IGuestSessionRepository>();
        groups.Setup(r => r.GetGroupReservationByIdAsync("group")).ReturnsAsync(new GroupReservation { GroupReservationId = "group", ReservationId = "reservation", ReservationToken = "token" });
        reservations.Setup(r => r.GetReservationByIdAsync("reservation")).ReturnsAsync(new Reservation { Id = "reservation", WaiterId = "waiter", TableId = "table", GuestCount = 3 });
        List<GuestSession> participants = [new() { Id = "guest", GroupReservationId = "group", GuestName = "Guest", Dishes = [] }];
        if (storedOwner) participants.Add(new() { Id = "owner", GroupReservationId = "group", GuestName = "Owner", IsOwnerSession = true, Dishes = [] });
        sessions.Setup(r => r.GetAllByGroupReservationIdAsync("group")).ReturnsAsync(participants);
        var service = new GuestSessionService(groups.Object, sessions.Object, Mock.Of<IDishRepository>(), reservations.Object, Mock.Of<IUserRepository>(), Mock.Of<ITableRepository>());
        var status = await service.GetGroupReservationStatusAsync("group");
        Assert.Equal(2, status.CurrentParticipants);
        Assert.Equal(3, status.MaxCapacity);
    }
}
