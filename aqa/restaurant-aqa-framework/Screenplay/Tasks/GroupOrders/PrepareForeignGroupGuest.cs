using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Reservations;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Support.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

public sealed class PrepareForeignGroupGuest : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var first = actor.Recall<GroupOrderScenario>();
        var api = actor.UsingTheApi();
        // A separate booking on the same owned table creates a genuinely independent group.
        var second = new GroupOrderScenario(first.Owner, first.OtherCustomer, first.Waiter,
            first.Location, first.Table, first.Dish, first.SecondDish, actor.Recall<CleanupScope>());
        var reservation = await GroupOrderScenario.ReadRequiredAsync<ReservationResponse>(
            await new ReservationsApi(api).CreateReservationAsync(
                new CreateReservationRequest(first.Table.Id, first.StartTime.AddDays(2), first.EndTime.AddDays(2), 4),
                first.Owner.IdToken), "Create the second owned group");
        await second.RecordReservationAsync(api, reservation.Id);
        var guest = await second.AddGuestAsync(api, second.FirstGuestName);
        await second.AddDishAsync(api, guest, second.SecondDish.Id, 1);
        actor.Remember(new ForeignGroupGuest(second, guest));
    }
}
