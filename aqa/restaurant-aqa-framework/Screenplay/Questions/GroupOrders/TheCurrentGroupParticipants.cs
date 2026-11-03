using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;
using Restaurant_Aqa_Framework.Support.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;

public sealed class TheCurrentGroupParticipants(bool foreign = false) : IQuestion<GroupSessionsSnapshot>
{
    public async Task<GroupSessionsSnapshot> AnsweredByAsync(Actor actor)
    {
        var fixture = foreign ? actor.Recall<ForeignGroupGuest>().Group : actor.Recall<GroupOrderScenario>();
        return await GroupOrderScenario.ReadRequiredAsync<GroupSessionsSnapshot>(
            await new GroupOrdersApi(actor.UsingTheApi()).GetParticipantsAsync(fixture.GroupId, fixture.Owner.IdToken),
            "Read owned group participants");
    }
}
