using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Support.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

public sealed class ConfirmEmptyGroupCart : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var fixture = actor.Recall<GroupOrderScenario>();
        var guest = fixture.Guests.Single();
        await actor.TheLastResponse().CaptureAsync(await new GroupOrdersApi(actor.UsingTheApi())
            .SubmitGuestAsync(guest.SessionId, guest.SessionToken));
    }
}
