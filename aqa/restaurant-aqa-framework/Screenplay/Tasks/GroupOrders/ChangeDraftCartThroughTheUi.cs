using Restaurant_Aqa_Framework.Support.GroupOrders;
using Restaurant_Aqa_Framework.Ui.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

public sealed class ChangeDraftCartThroughTheUi : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var fixture = actor.Recall<GroupOrderScenario>();
        var page = actor.Recall<GroupOrderBrowserState>().GuestPages[fixture.SecondGuestName];
        await new GuestGroupOrderPage(page).IncreaseQuantityAsync(fixture.SecondDish.Name);
    }
}

public sealed class ConfirmDraftCartThroughTheUi : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var fixture = actor.Recall<GroupOrderScenario>();
        var page = actor.Recall<GroupOrderBrowserState>().GuestPages[fixture.SecondGuestName];
        await new GuestGroupOrderPage(page).SubmitCartAsync();
    }
}
