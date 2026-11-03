using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Support.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class RequestGroupParticipants(string requester = "Owner", bool missingGroup = false) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var scenario = actor.Recall<GroupOrderScenario>();
        string? token = requester switch
        {
            "Owner" => scenario.Owner.IdToken,
            "another customer" => scenario.OtherCustomer.IdToken,
            "a guest" => scenario.Guests[0].SessionToken,
            "a waiter" => scenario.Waiter.IdToken,
            "an anonymous visitor" => null,
            _ => throw new InvalidOperationException("Unknown participant-list requester."),
        };
        var groupId = missingGroup ? Guid.NewGuid().ToString() : scenario.GroupId;
        await actor.TheLastResponse().CaptureAsync(await new GroupOrdersApi(actor.UsingTheApi()).GetParticipantsAsync(groupId, token));
    }
}
