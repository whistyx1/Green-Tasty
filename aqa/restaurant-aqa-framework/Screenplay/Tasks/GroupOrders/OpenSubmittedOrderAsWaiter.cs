using Microsoft.Playwright;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Reservations;
using Restaurant_Aqa_Framework.Api.Dtos.Orders;
using Restaurant_Aqa_Framework.Configuration;
using Restaurant_Aqa_Framework.Support.GroupOrders;
using Restaurant_Aqa_Framework.Ui;
using Restaurant_Aqa_Framework.Ui.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

public sealed class OpenSubmittedOrderAsWaiter : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var fixture = actor.Recall<GroupOrderScenario>();
        var reservation = await GroupOrderScenario.ReadRequiredAsync<ReservationResponse>(
            await new ReservationsApi(actor.UsingTheApi()).GetReservationAsync(fixture.ReservationId, fixture.Owner.IdToken),
            "Check the reservation's assigned Waiter");
        if (reservation.WaiterId != fixture.Waiter.UserId)
            throw new InvalidOperationException("The storefront assigned this reservation to a different Waiter than the configured test account.");
        var waiterReservations = new ReservationsApi(actor.UsingTheApi());
        var elapsed = Stopwatch.StartNew();
        using var polling = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        while (true)
        {
            var listed = await GroupOrderScenario.ReadRequiredAsync<IReadOnlyList<ReservationResponse>>(
                await waiterReservations.GetReservationsAsync(fixture.Waiter.IdToken),
                "Read the assigned Waiter's reservation list");
            if (listed.Any(item => item.Id == fixture.ReservationId)) break;
            if (elapsed.ElapsedMilliseconds >= 15_000)
                throw new InvalidOperationException("The assigned Waiter's reservation list did not include the owned booking before the readiness deadline.");
            await polling.WaitForNextTickAsync();
        }
        // The Waiter dashboard loads each pre-order separately. Its first load can
        // precede order-index propagation and then render the card without a button.
        // Wait for the same read the dashboard uses before opening the browser.
        var orders = new OrdersApi(actor.UsingTheApi());
        elapsed.Restart();
        while (true)
        {
            var response = await orders.GetOrderAsync(fixture.ReservationId, fixture.Waiter.IdToken);
            if (response.Ok)
            {
                var order = await GroupOrderScenario.ReadRequiredAsync<GetOrderResponse>(response,
                    "Read the submitted order as the assigned Waiter");
                if (order.Status == OrderStatus.SubmittedByCustomer && order.OrderItems.Count > 0)
                    break;
            }
            else if (response.Status != 404 && response.Status < 500)
                throw new InvalidOperationException($"The assigned Waiter cannot read the submitted order (HTTP {response.Status}).");
            if (elapsed.ElapsedMilliseconds >= 30_000)
                throw new InvalidOperationException("The assigned Waiter's submitted order was not visible before the readiness deadline.");
            await polling.WaitForNextTickAsync();
        }
        var state = actor.Recall<GroupOrderBrowserState>();
        var browser = state.OwnerPage.Context.Browser
            ?? throw new InvalidOperationException("The Owner browser is unavailable.");
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = Settings.Current.UiBaseUrl,
            TimezoneId = "UTC",
        });
        var page = await context.NewPageAsync();
        state.AddWaiter(context, page);
        var orderResponses = new ConcurrentQueue<IResponse>();
        void RecordOrderResponse(object? _, IResponse response)
        {
            if (response.Request.Method == "GET" &&
                new Uri(response.Url).AbsolutePath.EndsWith($"/orders/{fixture.ReservationId}", StringComparison.Ordinal))
                orderResponses.Enqueue(response);
        }
        page.Response += RecordOrderResponse;
        var login = new LoginPage(page);
        try
        {
            await login.NavigateAsync();
            await login.SignInAsync(fixture.Waiter.Email, fixture.Waiter.Password);
            await page.WaitForFunctionAsync("() => !!window.localStorage.getItem('auth') && !!window.localStorage.getItem('user')");
            await new WaiterGroupOrderPage(page).OpenOwnedReservationAsync(fixture.Table.TableNumber);
        }
        finally
        {
            page.Response -= RecordOrderResponse;
            var summaries = new List<string>();
            foreach (var response in orderResponses)
            {
                if (!response.Ok)
                {
                    summaries.Add($"HTTP {response.Status}");
                    continue;
                }
                try
                {
                    using var body = JsonDocument.Parse(await response.TextAsync());
                    var count = body.RootElement.TryGetProperty("orderItems", out var items) &&
                        items.ValueKind == JsonValueKind.Array
                            ? items.GetArrayLength().ToString(System.Globalization.CultureInfo.InvariantCulture)
                            : "unknown";
                    summaries.Add($"HTTP {response.Status}, items {count}");
                }
                catch (Exception)
                {
                    summaries.Add($"HTTP {response.Status}, items unreadable");
                }
            }
            Console.WriteLine($"[WaiterOrderProbe] Browser order reads: {summaries.Count}; " +
                string.Join("; ", summaries.TakeLast(20)));
        }
    }
}
