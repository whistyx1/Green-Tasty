using Restaurant_Aqa_Framework.Support.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class PopulateGroupCarts : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var scenario = actor.Recall<GroupOrderScenario>();
        var api = actor.UsingTheApi();
        var first = await scenario.AddGuestAsync(api, scenario.FirstGuestName);
        await scenario.AddDishAsync(api, first, scenario.Dish.Id, 2);
        await scenario.SubmitGuestAsync(api, first);
        var draft = await scenario.AddGuestAsync(api, scenario.SecondGuestName);
        await scenario.AddDishAsync(api, draft, scenario.SecondDish.Id, 1);
        await scenario.AddGuestAsync(api, scenario.EmptyGuestName);
    }
}
