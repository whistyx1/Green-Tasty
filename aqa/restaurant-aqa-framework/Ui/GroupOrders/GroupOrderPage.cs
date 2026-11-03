using System.Globalization;
using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Ui.GroupOrders;

public sealed class GroupOrderPage(IPage page) : BasePage(page)
{
    public ILocator Title => Page.GetByRole(AriaRole.Heading, new() { Name = "Group Pre-Order", Exact = true });
    public ILocator Participants => Page.GetByRole(AriaRole.List, new() { Name = "Preorder participants", Exact = true }).GetByRole(AriaRole.Article);
    public ILocator GrandTotal => Page.Locator(".grand-total > span:last-child");
    public ILocator ConsolidatedRows => Page.Locator(".consolidated-items > li");
    public ILocator PendingWarning => Page.Locator(".pending-warning").Filter(new() { HasText = "haven't submitted" });
    public ILocator EmptyMessage => Page.GetByText("Participants haven't added any dishes yet", new() { Exact = true });
    public ILocator NoSubmittedDishes => Page.GetByText("No submitted dishes yet", new() { Exact = true });
    public ILocator SubmitPreOrder => Page.GetByRole(AriaRole.Button, new() { Name = "Submit Pre-Order", Exact = true });
    public ILocator ConfirmSubmission => Page.GetByRole(AriaRole.Button, new() { Name = "Submit", Exact = true });
    public ILocator SentMessage => Page.GetByText("Your pre-order has been sent to the restaurant.", new() { Exact = true });
    public ILocator SentStatus => Page.Locator(".group-status.sent");
    public Task OpenAsync(string reservationId) => NavigateToAsync($"/reservations/{Uri.EscapeDataString(reservationId)}/group-order");

    public async Task SubmitAsync()
    {
        await SubmitPreOrder.ClickAsync();
        await ConfirmSubmission.ClickAsync();
        await Assertions.Expect(SentMessage).ToBeVisibleAsync(new() { Timeout = 45_000 });
    }

    public async Task RemoveGuestAsync(string guestName)
    {
        await Page.GetByRole(AriaRole.Button, new() { Name = $"Remove {guestName}", Exact = true }).ClickAsync();
        var confirmation = Page.GetByRole(AriaRole.Dialog, new() { Name = "Remove Guest", Exact = true });
        await Assertions.Expect(confirmation).ToBeVisibleAsync();
        await confirmation.GetByRole(AriaRole.Button, new() { Name = "Remove Guest", Exact = true }).ClickAsync();
        await Assertions.Expect(confirmation).ToHaveCountAsync(0, new() { Timeout = 45_000 });
        await Assertions.Expect(Participant($"Guest: {guestName}")).ToHaveCountAsync(0, new() { Timeout = 45_000 });
    }

    public ILocator Participant(string heading) => Participants.Filter(new()
    {
        Has = Page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }),
    });
    public static ILocator Status(ILocator card) => card.Locator(".status");
    public static ILocator Subtotal(ILocator card) => card.Locator(".subtotal > span:last-child");
    public static ILocator Dishes(ILocator card) => card.GetByRole(AriaRole.List, new() { Name = "Selected dishes", Exact = true }).GetByRole(AriaRole.Listitem);
    public static ILocator Dish(ILocator card, string name) => Dishes(card).Filter(new() { HasText = name });
    public static string Money(decimal value) => value.ToString("C2", CultureInfo.GetCultureInfo("en-US"));
    public static decimal ParseMoney(string value) => decimal.Parse(value, NumberStyles.Currency, CultureInfo.GetCultureInfo("en-US"));
}
