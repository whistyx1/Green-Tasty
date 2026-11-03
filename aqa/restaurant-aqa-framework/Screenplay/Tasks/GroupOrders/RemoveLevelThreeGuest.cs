using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Support.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

public enum RemovalTarget { Confirmed, Choosing, Foreign, Missing }

public sealed class RemoveLevelThreeGuest(RemovalTarget target, string requester = "Owner") : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var fixture = actor.Recall<GroupOrderScenario>();
        var sessionId = target switch
        {
            RemovalTarget.Confirmed => fixture.Guests.Single(guest => guest.Name == fixture.FirstGuestName).SessionId,
            RemovalTarget.Choosing => fixture.Guests.Single(guest => guest.Name == fixture.SecondGuestName).SessionId,
            RemovalTarget.Foreign => actor.Recall<ForeignGroupGuest>().Guest.SessionId,
            RemovalTarget.Missing => Guid.NewGuid().ToString(),
            _ => throw new InvalidOperationException("Unknown removal target."),
        };
        string? token = requester switch
        {
            "Owner" => fixture.Owner.IdToken,
            "another customer" => fixture.OtherCustomer.IdToken,
            "a guest" => fixture.Guests[0].SessionToken,
            "a waiter" => fixture.Waiter.IdToken,
            "an anonymous visitor" => null,
            _ => throw new InvalidOperationException("Unknown removal requester."),
        };
        var response = await new GroupOrdersApi(actor.UsingTheApi()).RemoveGuestAsync(fixture.GroupId, sessionId, token);
        await actor.TheLastResponse().CaptureAsync(response);
        if (response.Ok && target is RemovalTarget.Confirmed or RemovalTarget.Choosing)
            fixture.MarkGuestRemoved(sessionId);
    }
}
