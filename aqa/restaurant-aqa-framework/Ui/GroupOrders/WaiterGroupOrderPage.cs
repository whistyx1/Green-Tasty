using Microsoft.Playwright;
using System.Diagnostics;

namespace Restaurant_Aqa_Framework.Ui.GroupOrders;

public sealed class WaiterGroupOrderPage(IPage page) : BasePage(page)
{
    public ILocator Dishes => Page.Locator(".preorder-list app-dish-card");

    public async Task OpenOwnedReservationAsync(int tableNumber)
    {
        await NavigateToAsync("/waiter-reservations");
        var card = Page.Locator("ul.reservation-list > app-card-reservation")
            .Filter(new() { HasText = tableNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        await Assertions.Expect(card).ToHaveCountAsync(1, new() { Timeout = 45_000 });
        var preorder = card.GetByRole(AriaRole.Button, new() { Name = "Pre-order", Exact = true });
        var elapsed = Stopwatch.StartNew();
        // The card's one-time order lookup can precede index propagation.
        // Reload only the read-only dashboard until its button appears.
        while (!await preorder.IsVisibleAsync())
        {
            if (elapsed.Elapsed >= TimeSpan.FromSeconds(30))
                throw new TimeoutException("The assigned Waiter's reservation card did not expose its submitted pre-order.");
            await Task.Delay(TimeSpan.FromSeconds(2));
            await Page.ReloadAsync();
            await Assertions.Expect(card).ToHaveCountAsync(1, new() { Timeout = 15_000 });
        }
        await preorder.ClickAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { NameRegex = new System.Text.RegularExpressions.Regex("^Pre-order of") }))
            .ToBeVisibleAsync();
    }

    public ILocator Dish(string name) => Dishes.Filter(new() { HasText = name });
    public static ILocator Quantity(ILocator dish) => dish.Locator(".pre-order-quantity");
}
