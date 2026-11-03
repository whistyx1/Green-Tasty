using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;
using Restaurant_Aqa_Framework.Screenplay.Tasks;
using Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;
using Restaurant_Aqa_Framework.Support.GroupOrders;
using Restaurant_Aqa_Framework.Ui.GroupOrders;

namespace Restaurant_Aqa_Framework.StepDefinitions;

[Binding]
[Scope(Tag = "level3ui")]
public sealed class GroupPreorderLevel3UiStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    [Given("an isolated group-order restaurant is ready for level three")]
    public Task GivenEnvironment() => Actor.AttemptsToAsync(new PrepareGroupOrderScenario(createReservation: false));

    [Given("the Owner has booked a table through the storefront for level three")]
    public Task GivenBooking() => Actor.AttemptsToAsync(new BookGroupReservationThroughTheUi());

    [Given("guests have selected confirmed and draft carts in the storefront")]
    public Task GivenCarts() => Actor.AttemptsToAsync(new PrepareLevelTwoUiSelections());

    [Then("the Owner sees the confirmed guest before removal")]
    public async Task ThenOwnerSeesConfirmedGuest()
    {
        var fixture = Recall<GroupOrderScenario>();
        var view = await Actor.AsksForAsync(new TheRemovalOverview(afterRemoval: false));
        await Assert.That(view.Participants).Contains($"Guest: {fixture.FirstGuestName}");
        await Assert.That(view.Total).IsEqualTo(37.15m);
    }

    [When("the Owner removes that confirmed guest in the storefront")]
    public Task WhenOwnerRemoves() => Actor.AttemptsToAsync(new RemoveConfirmedGuestThroughTheUi());

    [Then("the removed confirmed guest cannot start another pre-order")]
    public async Task ThenRemovedConfirmedGuestCannotReorder()
    {
        var fixture = Recall<GroupOrderScenario>();
        var guestPage = new GuestGroupOrderPage(Recall<GroupOrderBrowserState>().GuestPages[fixture.FirstGuestName]);
        await Assertions.Expect(guestPage.InactiveHeading).ToBeVisibleAsync(new() { Timeout = 25_000 });
        await Assertions.Expect(guestPage.CreateNewPreorder).ToHaveCountAsync(0);
    }

    [When("the Owner removes the choosing guest in the storefront")]
    public async Task WhenOwnerRemovesChoosing()
    {
        var fixture = Recall<GroupOrderScenario>();
        await new GroupOrderPage(Actor.LookingAtThePage()).RemoveGuestAsync(fixture.SecondGuestName);
        fixture.MarkGuestRemoved(fixture.Guests.Single(guest => guest.Name == fixture.SecondGuestName).SessionId);
    }

    [Then("the removed guest sees that the pre-order is inactive")]
    public async Task ThenGuestSeesInactive()
    {
        var fixture = Recall<GroupOrderScenario>();
        var guestPage = Recall<GroupOrderBrowserState>().GuestPages[fixture.SecondGuestName];
        await Assertions.Expect(guestPage.GetByRole(AriaRole.Heading, new()
        {
            Name = "This guest pre-order is no longer active",
            Exact = true,
        })).ToBeVisibleAsync(new() { Timeout = 25_000 });
        await Assertions.Expect(guestPage.GetByRole(AriaRole.Region, new()
        {
            Name = "Pre-order menu",
            Exact = true,
        })).ToHaveCountAsync(0);
    }

    [Then("the removed guest stays inactive after reloading")]
    public async Task ThenGuestStaysInactiveAfterReload()
    {
        var fixture = Recall<GroupOrderScenario>();
        var guestPage = Recall<GroupOrderBrowserState>().GuestPages[fixture.SecondGuestName];
        await guestPage.ReloadAsync();
        await Assertions.Expect(guestPage.GetByRole(AriaRole.Heading, new()
        {
            Name = "This guest pre-order is no longer active",
            Exact = true,
        })).ToBeVisibleAsync(new() { Timeout = 25_000 });
        await Assertions.Expect(guestPage.GetByRole(AriaRole.Region, new()
        {
            Name = "Pre-order menu",
            Exact = true,
        })).ToHaveCountAsync(0);
    }

    [Then("the Owner does not see the removed guest after reloading")]
    public async Task ThenOwnerKeepsGuestRemoved()
    {
        var fixture = Recall<GroupOrderScenario>();
        var page = Actor.LookingAtThePage();
        await page.ReloadAsync();
        var view = new GroupOrderPage(page);
        await Assertions.Expect(view.Participants).ToHaveCountAsync(3, new() { Timeout = 45_000 });
        await Assertions.Expect(view.Participant($"Guest: {fixture.SecondGuestName}"))
            .ToHaveCountAsync(0);
        // The removed guest's cart was still a draft, so it never contributed
        // to the submit-ready total shown by the Owner.
        await Assertions.Expect(view.GrandTotal).ToHaveTextAsync("$37.15", new() { Timeout = 45_000 });
    }

    [Then("the Owner sees the remaining guests and recalculated total")]
    public async Task ThenOwnerSeesReducedGroup()
    {
        var fixture = Recall<GroupOrderScenario>();
        var view = await Actor.AsksForAsync(new TheRemovalOverview(afterRemoval: true));
        await Assert.That(view.Participants.Count).IsEqualTo(3);
        await Assert.That(view.Participants.Contains($"Guest: {fixture.FirstGuestName}")).IsFalse();
        await Assert.That(view.Participants).Contains($"Guest: {fixture.SecondGuestName}");
        await Assert.That(view.Participants).Contains($"Guest: {fixture.EmptyGuestName}");
        await Assert.That(view.Total).IsEqualTo(20.65m);
        await Assert.That(view.Dishes.Order().SequenceEqual(new[]
        {
            $"{fixture.Dish.Name} × 1", $"{fixture.SecondDish.Name} × 1",
        }.Order())).IsTrue();
    }

    [When("the Owner sends the remaining group pre-order in the storefront")]
    public Task WhenOwnerSends() => Actor.AttemptsToAsync(new SubmitGroupThroughTheUi());

    [Then("the Owner sees only the remaining confirmed dishes in the sent order")]
    public async Task ThenOwnerSeesSent()
    {
        var fixture = Recall<GroupOrderScenario>();
        var sent = await Actor.AsksForAsync(new TheOwnerSubmission());
        await Assert.That(sent.Status).IsEqualTo("Sent to Restaurant");
        await Assert.That(sent.Message).IsEqualTo("Your pre-order has been sent to the restaurant.");
        await Assert.That(sent.Total).IsEqualTo(20.65m);
        await Assert.That(sent.Dishes.Order().SequenceEqual(new[]
        {
            $"{fixture.Dish.Name} × 1", $"{fixture.SecondDish.Name} × 1",
        }.Order())).IsTrue();
    }

    [When("the assigned Waiter opens the reduced reservation pre-order")]
    public Task WhenWaiterOpens() => Actor.AttemptsToAsync(new OpenSubmittedOrderAsWaiter());

    [Then("the Waiter sees only the remaining confirmed dishes")]
    public async Task ThenWaiterSeesSent()
    {
        var fixture = Recall<GroupOrderScenario>();
        var view = await Actor.AsksForAsync(new TheWaiterSubmission());
        await Assert.That(view.Dishes.Count).IsEqualTo(2);
        await Assert.That(view.Dishes.Single(item => item.Name == fixture.Dish.Name).Quantity).IsEqualTo(1);
        await Assert.That(view.Dishes.Single(item => item.Name == fixture.SecondDish.Name).Quantity).IsEqualTo(1);
    }
}
