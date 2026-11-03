using Restaurant_Aqa_Framework.Support.GroupOrders;
using Restaurant_Aqa_Framework.Ui.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

public sealed class SubmitGroupThroughTheUi : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var fixture = actor.Recall<GroupOrderScenario>();
        await new GroupOrderPage(actor.LookingAtThePage()).SubmitAsync();
        fixture.MarkSubmitted();
    }
}
