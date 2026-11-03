using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Support.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

public sealed class RemovedGuestAddsDish : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var fixture = actor.Recall<GroupOrderScenario>();
        var removed = fixture.Guests.Single(guest => guest.Name == fixture.SecondGuestName);
        var response = await new GroupOrdersApi(actor.UsingTheApi())
            .AddDishAsync(removed.SessionId, removed.SessionToken, fixture.Dish.Id, 1);
        await actor.TheLastResponse().CaptureAsync(response);
    }
}
