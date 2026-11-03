using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Configuration;
using Restaurant_Aqa_Framework.Support.GroupOrders;
using Restaurant_Aqa_Framework.Ui.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

public sealed class GuestsChooseThroughTheUi : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var fixture = actor.Recall<GroupOrderScenario>();
        await JoinAsync(actor, fixture.FirstGuestName, fixture.Dish.Name, quantity: 2, submit: true);
        await JoinAsync(actor, fixture.SecondGuestName, fixture.SecondDish.Name, quantity: 1, submit: false);
        await JoinAsync(actor, fixture.EmptyGuestName, null, quantity: 0, submit: false);
    }

    private static async Task JoinAsync(Actor actor, string name, string? dishName, int quantity, bool submit)
    {
        var fixture = actor.Recall<GroupOrderScenario>();
        var state = actor.Recall<GroupOrderBrowserState>();
        var browser = state.OwnerPage.Context.Browser
            ?? throw new InvalidOperationException("The Owner browser is unavailable.");
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = Settings.Current.UiBaseUrl,
            TimezoneId = "UTC",
        });
        var page = await context.NewPageAsync();
        state.AddGuest(name, context, page);
        var guestPage = new GuestGroupOrderPage(page);
        var guest = await guestPage.JoinAsync(state.InvitationUrl, name);
        fixture.TrackGuest(actor.UsingTheApi(), guest.SessionId, guest.SessionToken, name);
        if (dishName is not null)
            await guestPage.AddDishAsync(dishName, quantity);
        if (submit)
            await guestPage.SubmitCartAsync();
    }
}
