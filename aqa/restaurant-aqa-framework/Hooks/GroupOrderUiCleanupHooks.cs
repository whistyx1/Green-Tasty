using Reqnroll.BoDi;
using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Support.GroupOrders;

namespace Restaurant_Aqa_Framework.Hooks;

[Binding]
public sealed class GroupOrderUiCleanupHooks(IObjectContainer container)
{
    // Failure evidence runs at -200. Close guest sessions before API teardown (-100).
    [AfterScenario("level1", Order = -150)]
    [AfterScenario("level2", Order = -150)]
    [AfterScenario("level3", Order = -150)]
    public async Task CloseGuestBrowsers()
    {
        if (!container.IsRegistered<Actor>())
            return;
        var actor = container.Resolve<Actor>();
        if (actor.TryRecall<GroupOrderBrowserState>(out var state))
        {
            await state.DisposeAsync();
            if (!state.OwnerPage.IsClosed)
                await state.OwnerPage.GotoAsync("about:blank");
        }
    }
}
