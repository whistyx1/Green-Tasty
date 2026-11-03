using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Support.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

public sealed class SendGroupPreOrder(string requester = "Owner", bool missingGroup = false, bool differentReservation = false) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var fixture = actor.Recall<GroupOrderScenario>();
        string? token = requester switch
        {
            "Owner" => fixture.Owner.IdToken,
            "another customer" => fixture.OtherCustomer.IdToken,
            "a guest" => fixture.Guests[0].SessionToken,
            "a waiter" => fixture.Waiter.IdToken,
            "an anonymous visitor" => null,
            _ => throw new InvalidOperationException("Unknown group-submit requester."),
        };
        var groupId = missingGroup ? Guid.NewGuid().ToString() : fixture.GroupId;
        var reservationId = differentReservation ? Guid.NewGuid().ToString() : fixture.ReservationId;
        var response = await new GroupOrdersApi(actor.UsingTheApi()).SubmitGroupAsync(groupId, reservationId, token);
        await actor.TheLastResponse().CaptureAsync(response);
        if (response.Ok && !missingGroup && !differentReservation)
            fixture.MarkSubmitted();
    }
}
