using System.Text.Json;
using Restaurant_Aqa_Framework.Api.Dtos.Orders;
using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions;
using Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;
using Restaurant_Aqa_Framework.Screenplay.Tasks;
using Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;
using Restaurant_Aqa_Framework.Support.GroupOrders;

namespace Restaurant_Aqa_Framework.StepDefinitions;

[Binding]
[Scope(Tag = "level2api")]
public sealed class GroupPreorderLevel2ApiStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    [Given("an isolated future group reservation for level two")]
    public Task GivenOwnedGroup() => Actor.AttemptsToAsync(new PrepareGroupOrderScenario());

    [Given("the Owner has a saved dish and guests have confirmed and draft selections")]
    public Task GivenMixedCarts() => Actor.AttemptsToAsync(new PrepareLevelTwoCarts(LevelTwoCartSetup.Mixed));

    [Given("the Owner has a saved dish but no guest has confirmed a cart")]
    public Task GivenNoConfirmedCart() => Actor.AttemptsToAsync(new PrepareLevelTwoCarts(LevelTwoCartSetup.NoConfirmed));

    [Given("an empty guest has joined the level two group")]
    public Task GivenEmptyGuest() => Actor.AttemptsToAsync(new AddEmptyGroupGuest());

    [Given("a guest confirmed a dish but the Owner has not created an order")]
    public Task GivenNoOrder() => Actor.AttemptsToAsync(new PrepareLevelTwoCarts(LevelTwoCartSetup.NoOrder));

    [Given("the Owner has already sent the group pre-order")]
    public async Task GivenAlreadySubmitted()
    {
        await Actor.AttemptsToAsync(new SendGroupPreOrder());
        if ((await Actor.AsksForAsync(new TheOutcome())).Status != 200)
            throw new InvalidOperationException("Level two setup could not submit the owned group.");
    }

    [When("the Owner sends the group pre-order")]
    [When("the Owner sends the group pre-order again")]
    public Task WhenOwnerSubmits() => Actor.AttemptsToAsync(new SendGroupPreOrder());

    [When("the Owner sends a group that does not exist")]
    public Task WhenMissingGroup() => Actor.AttemptsToAsync(new SendGroupPreOrder(missingGroup: true));

    [When("the Owner sends the group with a different reservation")]
    public Task WhenMismatchedReservation() => Actor.AttemptsToAsync(new SendGroupPreOrder(differentReservation: true));

    [When("that guest tries to confirm the empty cart")]
    public Task WhenEmptyGuestConfirms() => Actor.AttemptsToAsync(new ConfirmEmptyGroupCart());

    [When("{group_requester} sends the group pre-order")]
    public Task WhenOtherSubmits(string requester) => Actor.AttemptsToAsync(new SendGroupPreOrder(requester));

    [Then("the restaurant order contains the exact combined confirmed quantities")]
    public async Task ThenExactConsolidation()
    {
        var fixture = Recall<GroupOrderScenario>();
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(200).Because("The Owner's group submission should succeed once.");
        var response = await Actor.AsksForAsync(new TheResponseAs<IReadOnlyList<SubmittedDish>>());
        await Assert.That(response.Count).IsEqualTo(2);
        await Assert.That(response.Single(item => item.DishId == fixture.Dish.Id).Quantity).IsEqualTo(3)
            .Because("Owner salad one plus first confirmed guest salad two must merge.");
        await Assert.That(response.Single(item => item.DishId == fixture.SecondDish.Id).Quantity).IsEqualTo(1)
            .Because("The other confirmed guest contributes one pasta; the draft guest's two are excluded.");
        var final = await Actor.AsksForAsync(new TheSubmittedRestaurantOrder());
        await AssertOrderItemsAsync(final.Order, fixture);
    }

    [Then("the group and order have their submitted statuses")]
    public async Task ThenSubmittedStatuses()
    {
        var final = await Actor.AsksForAsync(new TheSubmittedRestaurantOrder());
        await Assert.That(final.GroupStatus).IsEqualTo("Submitted");
        await Assert.That(final.Order.Status).IsEqualTo(OrderStatus.SubmittedByCustomer);
        await AssertOrderItemsAsync(final.Order, Recall<GroupOrderScenario>());
    }

    [Then("submission is refused because no cart was confirmed")]
    public Task ThenNoConfirmed() => AssertRefusalAsync(400, "No submitted sessions.");

    [Then("the guest is told the cart is empty and the group remains open")]
    public async Task ThenEmptyCartCannotConfirm()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(400);
        using var json = JsonDocument.Parse(outcome.Body);
        await Assert.That(json.RootElement.GetProperty("message").GetString()).IsEqualTo("The cart is empty");
        await Assert.That(await Actor.AsksForAsync(new TheCurrentGroupStatus())).IsEqualTo("Open");
    }

    [Then("submission is refused because the order is missing")]
    public Task ThenNoOrder() => AssertRefusalAsync(400, "No order found for this reservation.");

    [Then("submission is refused because the group is missing")]
    public Task ThenMissingGroup() => AssertRefusalAsync(404, "Group reservation was not found");

    [Then("submission is refused because the reservation does not match")]
    public Task ThenMismatch() => AssertRefusalAsync(400, "Reservation does not belong to this group.");

    [Then("submission is refused because the group is already submitted")]
    public async Task ThenRepeat()
    {
        await AssertRefusalAsync(409, "Only an open group with an active reservation can be submitted.");
        await Assert.That(await Actor.AsksForAsync(new TheCurrentGroupStatus())).IsEqualTo("Submitted");
    }

    [Then("{group_requester} is refused and the group remains open")]
    public async Task ThenOtherDenied(string requester)
    {
        var status = requester is "a guest" or "an anonymous visitor" ? 401 : 403;
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(status).Because($"{requester} is not the reservation Owner.");
        if (status == 403)
            await Assert.That(Error(outcome.Body)).IsEqualTo("Only the reservation owner can perform this action.");
        await Assert.That(await Actor.AsksForAsync(new TheCurrentGroupStatus())).IsEqualTo("Open");
    }

    private async Task AssertRefusalAsync(int status, string message)
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(status);
        await Assert.That(Error(outcome.Body)).IsEqualTo(message);
    }

    private static string? Error(string body)
    {
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("error").GetString();
    }

    private static async Task AssertOrderItemsAsync(GetOrderResponse order, GroupOrderScenario fixture)
    {
        await Assert.That(order.ReservationId).IsEqualTo(fixture.ReservationId);
        await Assert.That(order.OrderItems.Count).IsEqualTo(2);
        await Assert.That(order.OrderItems.Single(item => item.Dish.Id == fixture.Dish.Id).Quantity).IsEqualTo(3);
        await Assert.That(order.OrderItems.Single(item => item.Dish.Id == fixture.SecondDish.Id).Quantity).IsEqualTo(1);
    }

    private sealed record SubmittedDish(string DishId, int Quantity);
}
