using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Support.GroupOrders;
using Restaurant_Aqa_Framework.Ui.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

public sealed class BookGroupReservationThroughTheUi : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var fixture = actor.Recall<GroupOrderScenario>();
        await actor.AttemptsToAsync(new SignInThroughTheLoginForm(fixture.Owner.Email, fixture.Owner.Password));
        var page = actor.LookingAtThePage();
        await page.WaitForFunctionAsync("() => !!window.localStorage.getItem('auth') && !!window.localStorage.getItem('user')");
        await page.GetByRole(AriaRole.Link, new() { Name = "Reservations", Exact = true }).WaitForAsync();
        var booking = new GroupBookingPage(page);
        var reservationId = await booking.BookAsync(fixture.Location.Address, fixture.StartTime, fixture.Table.TableNumber);
        // Register cleanup immediately after the real UI creation, before invitation waiting.
        await fixture.RecordReservationAsync(actor.UsingTheApi(), reservationId);
        var invitation = await booking.ReadInvitationAndViewReservationAsync();
        actor.Remember(new GroupOrderBrowserState(page, invitation));
        // Customer cards do not expose a stable reservation identifier in their markup.
        // Open the actual Owner screen using the identifier returned by the UI booking.
        await new GroupOrderPage(page).OpenAsync(reservationId);
    }
}
