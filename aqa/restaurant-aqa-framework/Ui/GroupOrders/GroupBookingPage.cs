using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui.Components;

namespace Restaurant_Aqa_Framework.Ui.GroupOrders;

/// <summary>Real booking controls; captures the response to the user's confirmation only.</summary>
public sealed class GroupBookingPage(IPage page) : BasePage(page)
{
    private ILocator Field(string name) => Page.Locator(".booking-card .field").Filter(new()
    {
        Has = Page.Locator(".field-label").Filter(new() { HasTextRegex = new Regex($"^{Regex.Escape(name)}$") }),
    });

    private async Task SelectAsync(string field, string value)
    {
        var control = Field(field).Locator("app-custom-select");
        await control.ClickAsync();
        await control.GetByRole(AriaRole.Option, new() { Name = value, Exact = true }).ClickAsync();
    }

    public async Task<string> BookAsync(string location, DateTime start, int tableNumber)
    {
        await Page.GetByRole(AriaRole.Link, new() { Name = "Book a Table", Exact = true }).ClickAsync();
        await SelectAsync("Location", location);
        await SelectAsync("Date", start.ToString("MMM d, yyyy", CultureInfo.InvariantCulture));
        await SelectAsync("Time", start.ToString("HH:mm", CultureInfo.InvariantCulture));
        await new CustomNumberComponent(Page.Locator(".booking-card .field-guests app-custom-number")).SetAsync(4);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Find a Table", Exact = true }).ClickAsync();
        var table = Page.Locator("app-table-card").Filter(new()
        {
            Has = Page.Locator(".table-number").GetByText($"Table {tableNumber}", new() { Exact = true }),
        });
        // The available-table search reads a separate index from the Admin table
        // listing. Re-run this read-only search while a just-created table appears.
        for (var attempt = 0; attempt < 4 && !await table.IsVisibleAsync(); attempt++)
        {
            try
            {
                await table.WaitForAsync(new() { Timeout = 5_000 });
            }
            catch (TimeoutException)
            {
                if (attempt < 3)
                    await Page.GetByRole(AriaRole.Button, new() { Name = "Find a Table", Exact = true }).ClickAsync();
            }
        }
        if (!await table.IsVisibleAsync())
            throw new InvalidOperationException("The newly created test table was not offered by the booking search before the readiness deadline.");
        await table.Locator("app-time-slot:not(:has(.disabled))").Filter(new()
        {
            Has = Page.GetByText(start.ToString("HH:mm", CultureInfo.InvariantCulture), new() { Exact = true }),
        }).ClickAsync();
        var review = Page.GetByRole(AriaRole.Dialog, new() { Name = "Review & Confirm Reservation", Exact = true });
        await review.WaitForAsync();
        var sawReservationRequest = false;
        Page.Request += (_, request) =>
        {
            if (request.Method == "POST" && new Uri(request.Url).AbsolutePath.EndsWith("/reservations", StringComparison.Ordinal))
                sawReservationRequest = true;
        };
        IResponse response;
        try
        {
            response = await Page.RunAndWaitForResponseAsync(
                () => review.GetByRole(AriaRole.Button, new() { Name = "Confirm", Exact = true }).ClickAsync(),
                candidate => candidate.Request.Method == "POST" && new Uri(candidate.Url).AbsolutePath.EndsWith("/reservations", StringComparison.Ordinal));
        }
        catch (TimeoutException ex)
        {
            throw new TimeoutException($"UI booking did not return a reservation response; POST observed: {sawReservationRequest}; review dialog visible: {await review.IsVisibleAsync()}. URLs and body omitted.", ex);
        }
        if (!response.Ok)
            throw new InvalidOperationException($"UI reservation confirmation failed with HTTP {response.Status}; response body omitted.");
        using var json = JsonDocument.Parse(await response.TextAsync());
        return json.RootElement.EnumerateObject().Single(property => property.Name.Equals("id", StringComparison.OrdinalIgnoreCase)).Value.GetString()
            ?? throw new InvalidOperationException("UI booking response did not contain a reservation identifier.");
    }

    public async Task<string> ReadInvitationAndViewReservationAsync()
    {
        var confirmation = Page.GetByRole(AriaRole.Dialog, new() { Name = "Reservation Confirmed", Exact = true });
        await confirmation.WaitForAsync();
        // The invitation arrives asynchronously after booking. No clipboard permissions are needed.
        await Page.WaitForFunctionAsync("() => !!document.querySelector('#invite-link-input')?.value", null,
            new() { Timeout = 45_000 });
        var invitation = await confirmation.GetByLabel("Invite Link", new() { Exact = true }).InputValueAsync();
        await confirmation.GetByRole(AriaRole.Button, new() { Name = "View Reservation", Exact = true }).ClickAsync();
        return invitation;
    }
}
