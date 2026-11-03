using Restaurant_Aqa_Framework.Support.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class AddEmptyGroupGuest : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var scenario = actor.Recall<GroupOrderScenario>();
        await scenario.AddGuestAsync(actor.UsingTheApi(), scenario.EmptyGuestName);
    }
}
