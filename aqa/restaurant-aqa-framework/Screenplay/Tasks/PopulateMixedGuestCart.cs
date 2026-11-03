using Restaurant_Aqa_Framework.Support.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class PopulateMixedGuestCart : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var scenario = actor.Recall<GroupOrderScenario>();
        var api = actor.UsingTheApi();
        var guest = await scenario.AddGuestAsync(api, scenario.FirstGuestName);
        await scenario.AddDishAsync(api, guest, scenario.Dish.Id, 1);
        await scenario.AddDishAsync(api, guest, scenario.SecondDish.Id, 1);
    }
}
