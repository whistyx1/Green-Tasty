using Restaurant_Aqa_Framework.Support.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

public sealed class PrepareLevelThreeGuests(bool twoGuests) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var fixture = actor.Recall<GroupOrderScenario>();
        var api = actor.UsingTheApi();
        var confirmed = await fixture.AddGuestAsync(api, fixture.FirstGuestName);
        await fixture.AddDishAsync(api, confirmed, fixture.Dish.Id, 2);
        await fixture.SubmitGuestAsync(api, confirmed);
        if (!twoGuests) return;
        var choosing = await fixture.AddGuestAsync(api, fixture.SecondGuestName);
        await fixture.AddDishAsync(api, choosing, fixture.SecondDish.Id, 1);
    }
}
