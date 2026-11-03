using System.Text.Json;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;
using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions;
using Restaurant_Aqa_Framework.Screenplay.Tasks;
using Restaurant_Aqa_Framework.Support.GroupOrders;

namespace Restaurant_Aqa_Framework.StepDefinitions;

[Binding]
[Scope(Tag = "level1api")]
public sealed class GroupParticipantsApiStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    [Given("an isolated future group reservation for its Owner")]
    public Task GivenOwnedGroup() => Actor.AttemptsToAsync(new PrepareGroupOrderScenario());

    [Given("three guests have confirmed draft and empty carts")]
    public Task GivenGuestCarts() => Actor.AttemptsToAsync(new PopulateGroupCarts());

    [Given("a guest has selected two different dishes")]
    public Task GivenMixedCart() => Actor.AttemptsToAsync(new PopulateMixedGuestCart());

    [Given("an empty guest has joined the isolated group")]
    public Task GivenEmptyGuest() => Actor.AttemptsToAsync(new AddEmptyGroupGuest());

    [When("the Owner requests the group participant list")]
    public Task WhenOwnerReads() => Actor.AttemptsToAsync(new RequestGroupParticipants());

    [When("the draft guest increases the selected dish quantity")]
    public Task WhenDraftChanges() => Actor.AttemptsToAsync(new IncreaseDraftGuestCart());

    [When("{group_requester} requests the group participant list")]
    public Task WhenOtherReads(string requester) => Actor.AttemptsToAsync(new RequestGroupParticipants(requester));

    [When("the Owner requests a participant list for a group that does not exist")]
    public Task WhenMissingGroup() => Actor.AttemptsToAsync(new RequestGroupParticipants(missingGroup: true));

    [Then("the group participant list is empty with no charges")]
    public async Task ThenEmptyGroup()
    {
        var result = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(result.Status).IsEqualTo(200).Because("The Owner's raw group read must succeed before Owner-session bootstrap.");
        var snapshot = await Actor.AsksForAsync(new TheResponseAs<GroupSessionsSnapshot>());
        await Assert.That(snapshot.Participants).IsEmpty().Because("Invitation lookup must not create a participant session.");
        await Assert.That(snapshot.Total).IsEqualTo(0m).Because("A group with no sessions has no charges.");
    }

    [Then("every guest and selected dish is listed with the correct confirmation state")]
    public async Task ThenExactCarts()
    {
        var snapshot = await Actor.AsksForAsync(new TheStableGroupSnapshot(3, 28.90m));
        await AssertExpectedCartsAsync(snapshot, 1, 12.40m);
    }

    [Then("the guest cart amounts are 16.50 and 12.40 with an overall amount of 28.90")]
    public async Task ThenRawTotals()
    {
        var snapshot = await Actor.AsksForAsync(new TheStableGroupSnapshot(3, 28.90m));
        await Assert.That(snapshot.Total).IsEqualTo(28.90m).Because("Raw API totals include both confirmed and draft carts.");
        await Assert.That(snapshot.Participants.Sum(person => person.Total)).IsEqualTo(28.90m);
        await AssertExpectedCartsAsync(snapshot, 1, 12.40m);
    }

    [Then("both selected dishes appear once and their combined amount is 20.65")]
    public async Task ThenMixedCart()
    {
        var scenario = Recall<GroupOrderScenario>();
        var snapshot = await Actor.AsksForAsync(new TheStableGroupSnapshot(1, 20.65m, 0));
        await Assert.That(snapshot.Total).IsEqualTo(20.65m).Because("Both different dishes must be preserved, including decimal prices.");
        await Assert.That(snapshot.Participants.Count).IsEqualTo(1);
        var guest = snapshot.Participants.Single();
        await Assert.That(guest.Id).IsEqualTo(scenario.Guests.Single().SessionId);
        await Assert.That(guest.Name).IsEqualTo(scenario.FirstGuestName);
        await Assert.That(guest.Type).IsEqualTo("Guest");
        await Assert.That(guest.IsSubmitted).IsFalse();
        await Assert.That(guest.Total).IsEqualTo(20.65m);
        await Assert.That(guest.Items.Count).IsEqualTo(2);
        var salad = guest.Items.Single(item => item.Dish.Id == scenario.Dish.Id);
        await Assert.That(salad.Dish.Name).IsEqualTo(scenario.Dish.Name);
        await Assert.That(salad.Dish.Price).IsEqualTo(8.25m);
        await Assert.That(salad.Quantity).IsEqualTo(1);
        var pasta = guest.Items.Single(item => item.Dish.Id == scenario.SecondDish.Id);
        await Assert.That(pasta.Dish.Name).IsEqualTo(scenario.SecondDish.Name);
        await Assert.That(pasta.Dish.Price).IsEqualTo(12.40m);
        await Assert.That(pasta.Quantity).IsEqualTo(1);
    }

    [Then("the draft cart has two dishes worth 24.80 and the overall amount is 41.30")]
    public async Task ThenUpdatedDraft()
    {
        var snapshot = await Actor.AsksForAsync(new TheStableGroupSnapshot(3, 41.30m));
        await Assert.That(snapshot.Total).IsEqualTo(41.30m).Because("One added 12.40 dish must change the raw amount from 28.90 to 41.30.");
        await AssertExpectedCartsAsync(snapshot, 2, 24.80m);
    }

    [Then("the other guest carts retain their selections and confirmation states")]
    public async Task ThenUnchangedOtherCarts() =>
        await AssertExpectedCartsAsync(await Actor.AsksForAsync(new TheStableGroupSnapshot(3, 41.30m)), 2, 24.80m);

    [Then("{group_requester} is refused without receiving any participant data")]
    public async Task ThenDenied(string requester)
    {
        var result = await Actor.AsksForAsync(new TheOutcome());
        var expectedStatus = requester is "a guest" or "an anonymous visitor" ? 401 : 403;
        await Assert.That(result.Status).IsEqualTo(expectedStatus).Because($"The {requester} must not read a different Owner's group.");
        using var json = JsonDocument.Parse(result.Body);
        await Assert.That(json.RootElement.TryGetProperty("participants", out _)).IsFalse().Because("Denied reads must not leak participant data.");
        if (expectedStatus == 403)
            await Assert.That(json.RootElement.GetProperty("error").GetString()).IsEqualTo("Only the reservation owner can perform this action.");
        else
            await Assert.That(json.RootElement.GetProperty("message").GetString()).IsEqualTo("Unauthorized");
    }

    [Then("the Owner is told the group was not found without receiving participant data")]
    public async Task ThenNotFound()
    {
        var result = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(result.Status).IsEqualTo(404).Because("A valid Owner identity requesting a nonexistent group must receive not found.");
        using var json = JsonDocument.Parse(result.Body);
        await Assert.That(json.RootElement.GetProperty("error").GetString()).IsEqualTo("Group reservation was not found");
        await Assert.That(json.RootElement.TryGetProperty("participants", out _)).IsFalse();
    }

    private async Task AssertExpectedCartsAsync(GroupSessionsSnapshot snapshot, int draftQuantity, decimal draftTotal)
    {
        var scenario = Recall<GroupOrderScenario>();
        await Assert.That(snapshot.Participants.Count).IsEqualTo(3).Because("Every joined guest appears exactly once, with no implicit Owner session in this raw API scenario.");
        await Assert.That(snapshot.Participants.Select(person => person.Id).Order().SequenceEqual(scenario.Guests.Select(guest => guest.SessionId).Order())).IsTrue();
        await AssertCartAsync(snapshot, scenario.FirstGuestName, scenario.Dish.Id, scenario.Dish.Name, 8.25m, 2, 16.50m, true);
        await AssertCartAsync(snapshot, scenario.SecondGuestName, scenario.SecondDish.Id, scenario.SecondDish.Name, 12.40m, draftQuantity, draftTotal, false);
        var empty = snapshot.Participants.Single(person => person.Name == scenario.EmptyGuestName);
        await Assert.That(empty.Type).IsEqualTo("Guest");
        await Assert.That(empty.Items).IsEmpty();
        await Assert.That(empty.Total).IsEqualTo(0m);
        await Assert.That(empty.IsSubmitted).IsFalse();
    }

    private static async Task AssertCartAsync(GroupSessionsSnapshot snapshot, string name, string dishId, string dishName, decimal price, int quantity, decimal total, bool submitted)
    {
        var participant = snapshot.Participants.Single(person => person.Name == name);
        await Assert.That(participant.Type).IsEqualTo("Guest");
        await Assert.That(participant.Total).IsEqualTo(total).Because("The participant subtotal must preserve decimal currency precision.");
        await Assert.That(participant.IsSubmitted).IsEqualTo(submitted);
        await Assert.That(participant.Items.Count).IsEqualTo(1);
        var item = participant.Items.Single();
        await Assert.That(item.Dish.Id).IsEqualTo(dishId);
        await Assert.That(item.Dish.Name).IsEqualTo(dishName);
        await Assert.That(item.Dish.Price).IsEqualTo(price);
        await Assert.That(item.Quantity).IsEqualTo(quantity);
    }
}
