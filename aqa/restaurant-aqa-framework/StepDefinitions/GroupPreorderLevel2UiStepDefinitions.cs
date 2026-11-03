using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;
using Restaurant_Aqa_Framework.Screenplay.Tasks;
using Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;
using Restaurant_Aqa_Framework.Support.GroupOrders;

namespace Restaurant_Aqa_Framework.StepDefinitions;

[Binding]
[Scope(Tag = "level2ui")]
public sealed class GroupPreorderLevel2UiStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    [Given("an isolated group-order restaurant is ready for level two")]
    public Task GivenEnvironment() => Actor.AttemptsToAsync(new PrepareGroupOrderScenario(createReservation: false));

    [Given("the Owner has booked a table through the storefront for level two")]
    public Task GivenBooking() => Actor.AttemptsToAsync(new BookGroupReservationThroughTheUi());

    [Given("the Owner has a saved dish and guests have confirmed and draft carts in the storefront")]
    public Task GivenSelections() => Actor.AttemptsToAsync(new PrepareLevelTwoUiSelections());

    [When("the Owner submits the consolidated group pre-order in the storefront")]
    public Task WhenOwnerSends() => Actor.AttemptsToAsync(new SubmitGroupThroughTheUi());

    [Then("the Owner sees the sent status and exact confirmed quantities")]
    public async Task ThenOwnerSeesSubmitted()
    {
        var fixture = Recall<GroupOrderScenario>();
        var view = await Actor.AsksForAsync(new TheOwnerSubmission());
        await Assert.That(view.Status).IsEqualTo("Sent to Restaurant");
        await Assert.That(view.Message).IsEqualTo("Your pre-order has been sent to the restaurant.");
        await Assert.That(view.Total).IsEqualTo(37.15m);
        await Assert.That(view.Dishes.Count).IsEqualTo(2);
        await Assert.That(view.Dishes.Order().SequenceEqual(new[]
        {
            $"{fixture.Dish.Name} × 3", $"{fixture.SecondDish.Name} × 1",
        }.Order())).IsTrue().Because("The draft pasta portion must stay out of the final order.");
    }

    [When("the assigned Waiter opens the reservation pre-order in the storefront")]
    public Task WhenWaiterOpens() => Actor.AttemptsToAsync(new OpenSubmittedOrderAsWaiter());

    [Then("the Waiter sees the same confirmed dishes and quantities")]
    public async Task ThenWaiterSeesOrder()
    {
        var fixture = Recall<GroupOrderScenario>();
        var view = await Actor.AsksForAsync(new TheWaiterSubmission());
        await Assert.That(view.Dishes.Count).IsEqualTo(2);
        await Assert.That(view.Dishes.Single(item => item.Name == fixture.Dish.Name).Quantity).IsEqualTo(3);
        await Assert.That(view.Dishes.Single(item => item.Name == fixture.SecondDish.Name).Quantity).IsEqualTo(1);
    }
}
