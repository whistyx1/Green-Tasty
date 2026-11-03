using Restaurant_Aqa_Framework.Support.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class IncreaseDraftGuestCart : ITask
{
    public Task PerformAsAsync(Actor actor)
    {
        var scenario = actor.Recall<GroupOrderScenario>();
        var guest = scenario.Guests.Single(candidate => candidate.Name == scenario.SecondGuestName);
        // The real endpoint increments quantity. It is a single mutation, never retried.
        return scenario.AddDishAsync(actor.UsingTheApi(), guest, scenario.SecondDish.Id, 1);
    }
}
