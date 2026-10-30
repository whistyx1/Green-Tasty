using APIHandler.Constants;
using APIHandler.Constants.Dishes;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Repositories.Dishes;
using APIHandler.Repositories.GroupPreOrders;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Services.GroupPreOrders;
using Moq;

namespace APIHandler.Tests.Services;

public class GroupPreOrderServiceTests
{
    private readonly Mock<IReservationRepository> _reservations = new();
    private readonly Mock<IGroupReservationRepository> _groups = new();
    private readonly Mock<IGuestSessionRepository> _sessions = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IDishRepository> _dishes = new();
    private readonly Mock<IGroupPreOrderRepository> _persistence = new();
    private readonly GroupPreOrderService _service;
    private readonly Reservation _reservation = new() { Id = "reservation", TableId = "table", WaiterId = "waiter", CustomerId = "owner", StartTime = DateTime.UtcNow.AddHours(2) };
    private readonly GroupReservation _group = new() { GroupReservationId = "group", ReservationId = "reservation", ReservationToken = "token" };
    private readonly Order _order = new() { OrderId = "order", CustomerId = "owner", ReservationId = "reservation", ReservationAddress = "address", ReservationTime = "time", Dishes = [Item("salad", 2)] };
    private readonly List<GuestSession> _participants = [Session("guest", true, Item("salad", 3), Item("soup", 1)), Session("draft", false, Item("soup", 100))];

    public GroupPreOrderServiceTests()
    {
        _service = new(_reservations.Object, _groups.Object, _sessions.Object, _orders.Object, _dishes.Object, _persistence.Object);
        _reservations.Setup(r => r.GetReservationByIdAsync("reservation")).ReturnsAsync(_reservation);
        _groups.Setup(r => r.GetGroupReservationByIdAsync("group")).ReturnsAsync(_group);
        _groups.Setup(r => r.GetByReservationIdAsync("reservation")).ReturnsAsync(_group);
        _sessions.Setup(r => r.GetAllByGroupReservationIdAsync("group")).ReturnsAsync(_participants);
        _orders.Setup(r => r.GetOrderByReservationIdAsync("reservation")).ReturnsAsync(_order);
        _dishes.Setup(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>())).ReturnsAsync([Dish("salad"), Dish("soup")]);
        _persistence.Setup(r => r.EnsureOwnerSessionAsync("group", "owner")).ReturnsAsync(Session("owner-group", false));
    }

    [Fact]
    public async Task Submit_MergesOwnerOrderAndSubmittedCartsWithoutMutatingInputs()
    {
        List<OrderItem>? saved = null;
        IReadOnlyList<GuestSession>? submitted = null;
        _persistence.Setup(r => r.SubmitAsync(_order, _group, _reservation, It.IsAny<List<OrderItem>>(), It.IsAny<IReadOnlyList<GuestSession>>()))
            .Callback<Order, GroupReservation, Reservation, List<OrderItem>, IReadOnlyList<GuestSession>>((_, _, _, items, sessions) => { saved = items; submitted = sessions; })
            .Returns(Task.CompletedTask);
        var result = await _service.SubmitAsync("owner", "group", "reservation");
        Assert.Equal([new ConsolidatedDish("salad", 5), new ConsolidatedDish("soup", 1)], result);
        Assert.Equal(2, saved!.Count);
        Assert.Equal("guest", Assert.Single(submitted!).Id);
        Assert.All(saved, item => Assert.Equal(APIHandler.Constants.Orders.OrderItemState.Pending, item.State));
        Assert.Equal(2, Assert.Single(_order.Dishes).Quantity);
        Assert.Equal(OrderStatus.Created, _order.Status);
        Assert.Equal(GroupReservationStatus.Open, _group.Status);
    }

    [Fact]
    public async Task Submit_AggregatesDuplicateDishesAcrossMultipleSubmittedSessions()
    {
        _participants.Add(Session("other", true, Item("salad", 4)));
        Assert.Equal(9, (await _service.SubmitAsync("owner", "group", "reservation"))[0].Quantity);
    }

    [Theory]
    [InlineData("noSessions")]
    [InlineData("noDishes")]
    [InlineData("noOrder")]
    [InlineData("nullOrderDishes")]
    [InlineData("nullSessionDishes")]
    [InlineData("invalidOwnerQuantity")]
    [InlineData("mismatch")]
    [InlineData("zeroQuantity")]
    [InlineData("missingDish")]
    [InlineData("unavailableDish")]
    public async Task Submit_InvalidInputFailsBeforeAnyWrite(string scenario)
    {
        if (scenario == "nullOrderDishes") _order.Dishes = null!;
        if (scenario == "nullSessionDishes") _participants[0].Dishes = null!;
        if (scenario == "invalidOwnerQuantity") _order.Dishes[0].Quantity = -1;
        if (scenario == "noSessions") _participants.Clear();
        if (scenario == "noDishes") _participants[0].Dishes.Clear();
        if (scenario == "noOrder") _orders.Setup(r => r.GetOrderByReservationIdAsync("reservation")).ReturnsAsync((Order?)null);
        if (scenario == "zeroQuantity") _participants[0].Dishes[0].Quantity = 0;
        if (scenario == "missingDish") _dishes.Setup(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>())).ReturnsAsync([]);
        if (scenario == "unavailableDish") { var dish = Dish("salad"); dish.State = DishState.OnStop; _dishes.Setup(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>())).ReturnsAsync([dish, Dish("soup")]); }
        await Assert.ThrowsAsync<ValidationException>(() => _service.SubmitAsync("owner", "group", scenario == "mismatch" ? "other" : "reservation"));
        _persistence.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("group")]
    [InlineData("reservation")]
    public async Task Submit_MissingParentIsNotFoundWithoutWrites(string missing)
    {
        if (missing == "group") _groups.Setup(r => r.GetGroupReservationByIdAsync("group")).ReturnsAsync((GroupReservation?)null);
        else _reservations.Setup(r => r.GetReservationByIdAsync("reservation")).ReturnsAsync((Reservation?)null);
        var error = await Record.ExceptionAsync(() => _service.SubmitAsync("owner", "group", "reservation"));
        if (missing == "group") Assert.IsType<GroupReservationNotFoundException>(error);
        else Assert.IsType<ReservationNotFoundException>(error);
        _persistence.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Submit_OtherCustomerCannotReadCartsOrWrite()
    {
        await Assert.ThrowsAsync<ActionForbiddenException>(() => _service.SubmitAsync("stranger", "group", "reservation"));
        _sessions.Verify(r => r.GetAllByGroupReservationIdAsync(It.IsAny<string>()), Times.Never);
        _persistence.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("submittedGroup")]
    [InlineData("closedGroup")]
    [InlineData("cancelledReservation")]
    [InlineData("processedOrder")]
    public async Task Submit_RejectsInactiveAndRepeatedSubmissions(string scenario)
    {
        if (scenario == "submittedGroup") _group.Status = GroupReservationStatus.Submitted;
        if (scenario == "closedGroup") _group.Status = GroupReservationStatus.Closed;
        if (scenario == "cancelledReservation") _reservation.Status = ReservationStatus.Cancelled;
        if (scenario == "processedOrder") _order.Status = OrderStatus.Submitted;
        await Assert.ThrowsAsync<GroupPreOrderConflictException>(() => _service.SubmitAsync("owner", "group", "reservation"));
        _persistence.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Submit_AtomicConflictPropagatesWithoutFallbackSaves()
    {
        _persistence.Setup(r => r.SubmitAsync(_order, _group, _reservation, It.IsAny<List<OrderItem>>(), It.IsAny<IReadOnlyList<GuestSession>>()))
            .ThrowsAsync(new GroupPreOrderConflictException("Changed"));
        await Assert.ThrowsAsync<GroupPreOrderConflictException>(() => _service.SubmitAsync("owner", "group", "reservation"));
        _orders.Verify(r => r.SaveOrderAsync(It.IsAny<Order>()), Times.Never);
        _groups.Verify(r => r.SaveGroupReservationAsync(It.IsAny<GroupReservation>()), Times.Never);
    }

    [Fact]
    public async Task OwnerLookup_BackfillsLegacyGroupWithoutChangingOrdinaryOrder()
    {
        Assert.Equal("owner-group", await _service.GetOwnerSessionIdAsync("owner", "reservation"));
        _persistence.Verify(r => r.EnsureOwnerSessionAsync("group", "owner"), Times.Once);
        _orders.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OwnerLookup_ReusesExistingOwnerSessionAndNeverReturnsGuest()
    {
        var owner = Session("existing-owner", false); owner.IsOwnerSession = true; owner.OwnerUserId = "owner";
        _participants.Add(owner);
        Assert.Equal("existing-owner", await _service.GetOwnerSessionIdAsync("owner", "reservation"));
        _persistence.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(GroupReservationStatus.Open)]
    [InlineData(GroupReservationStatus.Submitted)]
    [InlineData(GroupReservationStatus.Closed)]
    public async Task OwnerContext_ReturnsGroupIdWithoutParsingExistingOpaqueSessionId(GroupReservationStatus status)
    {
        _group.Status = status;
        var owner = Session("legacy-random-session", false);
        owner.IsOwnerSession = true;
        owner.OwnerUserId = "owner";
        _participants.Add(owner);

        var result = await _service.GetOwnerSessionContextAsync("owner", "reservation");

        Assert.Equal(new OwnerSessionContext("legacy-random-session", "group"), result);
        _persistence.VerifyNoOtherCalls();
        _orders.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OwnerContext_BackfillsLegacyGroupAndReturnsStableContextWithoutChangingOrder()
    {
        var first = await _service.GetOwnerSessionContextAsync("owner", "reservation");
        var repeated = await _service.GetOwnerSessionContextAsync("owner", "reservation");

        Assert.Equal(new OwnerSessionContext("owner-group", "group"), first);
        Assert.Equal(first, repeated);
        _persistence.Verify(r => r.EnsureOwnerSessionAsync("group", "owner"), Times.Exactly(2));
        _orders.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OwnerLookup_OtherCustomerCannotReadOrCreateSession()
    {
        await Assert.ThrowsAsync<ActionForbiddenException>(() => _service.GetOwnerSessionIdAsync("other", "reservation"));
        _sessions.VerifyNoOtherCalls(); _persistence.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OwnerLookup_MissingReservationOrGroupReturnsNotFound(bool missingReservation)
    {
        if (missingReservation) _reservations.Setup(r => r.GetReservationByIdAsync("reservation")).ReturnsAsync((Reservation?)null);
        else _groups.Setup(r => r.GetByReservationIdAsync("reservation")).ReturnsAsync((GroupReservation?)null);
        var error = await Record.ExceptionAsync(() => _service.GetOwnerSessionIdAsync("owner", "reservation"));
        if (missingReservation) Assert.IsType<ReservationNotFoundException>(error); else Assert.IsType<GroupReservationNotFoundException>(error);
        _persistence.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Submit_EnforcesThirtyMinuteCutoff(int minutesAfterCutoff)
    {
        var now = new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);
        _reservation.StartTime = now.UtcDateTime.AddMinutes(30 - minutesAfterCutoff);
        var service = new GroupPreOrderService(_reservations.Object, _groups.Object, _sessions.Object, _orders.Object, _dishes.Object, _persistence.Object, new FixedClock(now));
        if (minutesAfterCutoff < 0) await service.SubmitAsync("owner", "group", "reservation");
        else
        {
            await Assert.ThrowsAsync<ValidationException>(() => service.SubmitAsync("owner", "group", "reservation"));
            _persistence.VerifyNoOtherCalls();
        }
    }
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }

    private static OrderItem Item(string id, int count) => new() { DishId = id, Quantity = count };
    private static GuestSession Session(string id, bool submitted, params OrderItem[] items) => new() { Id = id, GroupReservationId = "group", GuestName = id, IsSubmitted = submitted, Dishes = items.ToList() };
    private static Dish Dish(string id) => new() { Id = id, Name = id, Description = "dish", Price = 5, Weight = "100", Calories = "100", Proteins = "1", Fats = "1", Carbohydrates = "1", Vitamins = "A", State = DishState.Available };
}
