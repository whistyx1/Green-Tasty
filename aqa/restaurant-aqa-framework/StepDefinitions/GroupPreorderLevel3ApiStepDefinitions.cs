using System.Text.Json;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;
using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions;
using Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;
using Restaurant_Aqa_Framework.Screenplay.Tasks;
using Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;
using Restaurant_Aqa_Framework.Support.GroupOrders;

namespace Restaurant_Aqa_Framework.StepDefinitions;

[Binding]
[Scope(Tag = "level3api")]
public sealed class GroupPreorderLevel3ApiStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    [Given("an isolated future group reservation for level three")]
    public Task GivenGroup() => Actor.AttemptsToAsync(new PrepareGroupOrderScenario());

    [Given("two guests have selected dishes in the group")]
    public Task GivenTwoGuests() => Actor.AttemptsToAsync(new PrepareLevelThreeGuests(twoGuests: true));

    [Given("one guest has confirmed a cart in the group")]
    public Task GivenOneGuest() => Actor.AttemptsToAsync(new PrepareLevelThreeGuests(twoGuests: false));

    [Given("another independent group has its own guest")]
    public Task GivenOtherGroup() => Actor.AttemptsToAsync(new PrepareForeignGroupGuest());

    [When("the Owner removes the choosing guest")]
    public Task WhenRemoveChoosing() => Actor.AttemptsToAsync(new RemoveLevelThreeGuest(RemovalTarget.Choosing));

    [When("the Owner removes the confirmed guest")]
    public Task WhenRemoveConfirmed() => Actor.AttemptsToAsync(new RemoveLevelThreeGuest(RemovalTarget.Confirmed));

    [When("the removed guest tries to add another dish")]
    public Task WhenRemovedGuestAdds() => Actor.AttemptsToAsync(new RemovedGuestAddsDish());

    [When("the Owner tries to remove that guest from the first group")]
    public Task WhenForeign() => Actor.AttemptsToAsync(new RemoveLevelThreeGuest(RemovalTarget.Foreign));

    [When("the Owner tries to remove a guest who does not exist")]
    public Task WhenMissing() => Actor.AttemptsToAsync(new RemoveLevelThreeGuest(RemovalTarget.Missing));

    [When("{group_requester} tries to remove the guest")]
    public Task WhenOther(string requester) => Actor.AttemptsToAsync(new RemoveLevelThreeGuest(RemovalTarget.Confirmed, requester));

    [Then("only the confirmed guest remains with the correct total")]
    public Task ThenConfirmedRemains() => AssertRemovalAsync(RemovalTarget.Choosing, 16.50m, true);

    [Then("only the choosing guest remains with the correct total")]
    public Task ThenChoosingRemains() => AssertRemovalAsync(RemovalTarget.Confirmed, 12.40m, false);

    [Then("the group has no guests and a zero total")]
    public Task ThenNoGuests() => AssertRemovalAsync(RemovalTarget.Confirmed, 0m, false, noGuests: true);

    [Then("the old guest session is rejected and does not return to the group")]
    public async Task ThenRemovedGuestRejected()
    {
        var fixture = Recall<GroupOrderScenario>();
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(401);
        var removedId = fixture.Guests.Single(item => item.Name == fixture.SecondGuestName).SessionId;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var snapshot = await Actor.AsksForAsync(new TheCurrentGroupParticipants());
            await Assert.That(snapshot.Participants.Any(guest => guest.Id == removedId)).IsFalse();
            await Assert.That(snapshot.Total).IsEqualTo(16.50m);
            if (attempt < 4)
                await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }

    [Then("removal is refused because the guest belongs to another group")]
    public async Task ThenForeignRefused()
    {
        await AssertRefusalAsync(400, "Guest session does not belong to this group reservation.");
        var first = await Actor.AsksForAsync(new TheCurrentGroupParticipants());
        await Assert.That(first.Participants.Count).IsEqualTo(0);
        await Assert.That(first.Total).IsEqualTo(0m);
        var other = await Actor.AsksForAsync(new TheCurrentGroupParticipants(foreign: true));
        var foreign = Recall<ForeignGroupGuest>();
        await Assert.That(other.Participants.Single().Id).IsEqualTo(foreign.Guest.SessionId);
        await Assert.That(other.Total).IsEqualTo(12.40m);
    }

    [Then("removal is refused because the guest does not exist")]
    public async Task ThenMissingRefused()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(404);
        await Assert.That(Error(outcome.Body).StartsWith("Guest session with id ", StringComparison.Ordinal)).IsTrue();
        await AssertOneConfirmedUnchangedAsync();
    }

    [Then("{group_requester} cannot remove the guest and the group is unchanged")]
    public async Task ThenOtherRefused(string requester)
    {
        var status = requester is "a guest" or "an anonymous visitor" ? 401 : 403;
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(status).Because($"{requester} is not the group Owner.");
        if (status == 403)
            await Assert.That(Error(outcome.Body)).IsEqualTo("Only the reservation owner can perform this action.");
        await AssertOneConfirmedUnchangedAsync();
    }

    private async Task AssertRemovalAsync(RemovalTarget removed, decimal expectedTotal, bool remainingSubmitted, bool noGuests = false)
    {
        var fixture = Recall<GroupOrderScenario>();
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(200);
        var response = await Actor.AsksForAsync(new TheResponseAs<GroupSessionsSnapshot>());
        var current = await Actor.AsksForAsync(new TheCurrentGroupParticipants());
        await Assert.That(current.Total).IsEqualTo(response.Total).Because("The returned total must match persisted state.");
        await Assert.That(current.Participants.Select(guest => guest.Id).Order()
            .SequenceEqual(response.Participants.Select(guest => guest.Id).Order())).IsTrue()
            .Because("The returned participant IDs must match persisted state.");
        await Assert.That(current.Total).IsEqualTo(expectedTotal);
        var removedId = fixture.Guests.Single(guest => guest.Name ==
            (removed == RemovalTarget.Confirmed ? fixture.FirstGuestName : fixture.SecondGuestName)).SessionId;
        await Assert.That(current.Participants.Any(guest => guest.Id == removedId)).IsFalse();
        if (noGuests)
        {
            await Assert.That(current.Participants.Count).IsEqualTo(0);
            return;
        }
        var remaining = current.Participants.Single();
        var expected = fixture.Guests.Single(guest => guest.SessionId != removedId);
        await Assert.That(remaining.Id).IsEqualTo(expected.SessionId);
        await Assert.That(remaining.IsSubmitted).IsEqualTo(remainingSubmitted);
        await Assert.That(remaining.Total).IsEqualTo(expectedTotal);
        await Assert.That(remaining.Items.Count).IsEqualTo(1);
        await Assert.That(remaining.Items.Single().Quantity).IsEqualTo(remainingSubmitted ? 2 : 1);
        await Assert.That(remaining.Items.Single().Dish.Id)
            .IsEqualTo(remainingSubmitted ? fixture.Dish.Id : fixture.SecondDish.Id);
    }

    private async Task AssertOneConfirmedUnchangedAsync()
    {
        var fixture = Recall<GroupOrderScenario>();
        var current = await Actor.AsksForAsync(new TheCurrentGroupParticipants());
        await Assert.That(current.Participants.Single().Id).IsEqualTo(fixture.Guests.Single().SessionId);
        await Assert.That(current.Participants.Single().IsSubmitted).IsTrue();
        await Assert.That(current.Total).IsEqualTo(16.50m);
    }

    private async Task AssertRefusalAsync(int status, string message)
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(status);
        await Assert.That(Error(outcome.Body)).IsEqualTo(message);
    }

    private static string Error(string body)
    {
        using var json = JsonDocument.Parse(body);
        if (json.RootElement.TryGetProperty("error", out var error))
            return error.GetString() ?? "";
        if (json.RootElement.TryGetProperty("message", out var message))
            return message.GetString() ?? "";
        throw new InvalidOperationException("The rejection response contained neither an error nor a message.");
    }
}
