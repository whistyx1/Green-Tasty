using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Support.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;

public sealed class TheCurrentGroupStatus : IQuestion<string>
{
    public async Task<string> AnsweredByAsync(Actor actor)
    {
        var fixture = actor.Recall<GroupOrderScenario>();
        var response = await GroupOrderScenario.ReadRequiredAsync<GroupStatus>(
            await new GroupOrdersApi(actor.UsingTheApi()).GetGroupStatusAsync(fixture.GroupId, fixture.Owner.IdToken),
            "Read the owned group status");
        return response.Status;
    }

    private sealed record GroupStatus(string Status);
}
