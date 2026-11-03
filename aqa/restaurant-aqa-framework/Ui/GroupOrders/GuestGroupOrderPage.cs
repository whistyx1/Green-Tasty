using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

namespace Restaurant_Aqa_Framework.Ui.GroupOrders;

/// <summary>Guest actions use only visible invitation, menu and cart controls.</summary>
public sealed class GuestGroupOrderPage(IPage page) : BasePage(page)
{
    public ILocator InactiveHeading => Page.GetByRole(AriaRole.Heading, new()
    {
        Name = "This guest pre-order is no longer active",
        Exact = true,
    });
    public ILocator CreateNewPreorder => Page.GetByRole(AriaRole.Button, new()
    {
        Name = "Create New Pre-Order",
        Exact = true,
    });
    private ILocator Cart => Page.GetByRole(AriaRole.Dialog, new() { Name = "Cart", Exact = true });
    private ILocator CartItem(string dishName) => Cart.Locator(".item").Filter(new() { HasText = dishName });

    public async Task<GroupGuestSession> JoinAsync(string invitation, string name)
    {
        await Page.GotoAsync(invitation);
        await Page.GetByLabel("Enter Your Name", new() { Exact = true }).FillAsync(name);
        var response = await Page.RunAndWaitForResponseAsync(
            () => Page.GetByRole(AriaRole.Button, new() { Name = "Browse Menu", Exact = true }).ClickAsync(),
            response => response.Request.Method == "POST" && new Uri(response.Url).AbsolutePath.EndsWith("/guest/sessions", StringComparison.Ordinal));
        RequireSuccess(response, "Guest joined through invitation");
        var guest = JsonSerializer.Deserialize<GroupGuestSession>(await response.TextAsync(), JsonDefaults.Options)
            ?? throw new InvalidOperationException("Guest invitation returned no session; body omitted.");
        await Page.GetByRole(AriaRole.Region, new() { Name = "Pre-order menu", Exact = true }).WaitForAsync();
        return guest;
    }

    public async Task AddDishAsync(string name, int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        var dish = Page.Locator("app-dish-card").Filter(new()
        {
            Has = Page.Locator(".dish-name").GetByText(name, new() { Exact = true }),
        });
        var response = await Page.RunAndWaitForResponseAsync(
            () => dish.GetByRole(AriaRole.Button, new() { Name = "Pre-Order", Exact = true }).ClickAsync(),
            response => response.Request.Method == "POST" && new Uri(response.Url).AbsolutePath.EndsWith("/dishes", StringComparison.Ordinal));
        RequireSuccess(response, "Guest selected a dish");
        await OpenCartAsync();
        await CartItem(name).Locator(".qty-value").Filter(new() { HasTextRegex = new Regex("^1$") }).WaitForAsync();
        for (var index = 1; index < quantity; index++)
            await IncreaseQuantityAsync(name);
    }

    public async Task OpenCartAsync() =>
        await Page.GetByRole(AriaRole.Button, new() { Name = "Open cart", Exact = true }).ClickAsync();

    public async Task IncreaseQuantityAsync(string dishName)
    {
        if (!await Cart.IsVisibleAsync())
            await OpenCartAsync();
        var row = CartItem(dishName);
        var current = int.Parse((await row.Locator(".qty-value").TextContentAsync())!.Trim(), CultureInfo.InvariantCulture);
        var response = await Page.RunAndWaitForResponseAsync(
            () => row.GetByRole(AriaRole.Button, new() { Name = "Increase quantity", Exact = true }).ClickAsync(),
            response => response.Request.Method == "POST" && new Uri(response.Url).AbsolutePath.EndsWith("/dishes", StringComparison.Ordinal));
        RequireSuccess(response, "Guest increased dish quantity");
        await row.Locator(".qty-value").Filter(new()
        {
            HasTextRegex = new Regex($"^{current + 1}$"),
        }).WaitForAsync();
    }

    public async Task SubmitCartAsync()
    {
        if (!await Cart.IsVisibleAsync())
            await OpenCartAsync();
        var response = await Page.RunAndWaitForResponseAsync(
            () => Cart.GetByRole(AriaRole.Button, new() { Name = "Submit Pre-Order", Exact = true }).ClickAsync(),
            response => response.Request.Method == "POST" && new Uri(response.Url).AbsolutePath.EndsWith("/submit", StringComparison.Ordinal));
        RequireSuccess(response, "Guest confirmed their individual cart");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Pre-Order Submitted!", Exact = true }).WaitForAsync();
    }

    private static void RequireSuccess(IResponse response, string operation)
    {
        if (!response.Ok)
            throw new InvalidOperationException($"{operation} failed with HTTP {response.Status}; response body omitted.");
    }
}
