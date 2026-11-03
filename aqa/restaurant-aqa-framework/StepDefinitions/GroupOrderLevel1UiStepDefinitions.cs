using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;
using Restaurant_Aqa_Framework.Screenplay.Tasks;
using Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;
using Restaurant_Aqa_Framework.Support.GroupOrders;

namespace Restaurant_Aqa_Framework.StepDefinitions;

[Binding]
[Scope(Tag = "level1ui")]
public sealed class GroupOrderLevel1UiStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    [Given("an isolated group-order restaurant is ready")]
    public Task GivenEnvironmentIsReady() => Actor.AttemptsToAsync(new PrepareGroupOrderScenario(createReservation: false));

    [Given("the Owner has booked a table through the storefront")]
    public Task GivenOwnerBookedThroughTheStorefront() => Actor.AttemptsToAsync(new BookGroupReservationThroughTheUi());

    [Given("invited guests have selected submitted, draft and empty carts through the storefront")]
    public Task GivenGuestsChoose() => Actor.AttemptsToAsync(new GuestsChooseThroughTheUi());

    [Then("the Owner sees every guest's selections, quantities, status and subtotal")]
    public Task ThenInitialGuestSelections() => VerifyAsync(GroupOverviewStage.InitialGuests);

    [Then("the consolidated total includes only the confirmed guest cart")]
    public Task ThenOnlyConfirmedTotal() => VerifyAsync(GroupOverviewStage.InitialGuests);

    [When("the choosing guest adds another portion through the storefront")]
    public Task WhenGuestAddsAnotherPortion() => Actor.AttemptsToAsync(new ChangeDraftCartThroughTheUi());

    [Then("the Owner automatically sees the changed draft subtotal without adding it to the consolidated total")]
    public Task ThenDraftUpdateIsShown() => VerifyAsync(GroupOverviewStage.DraftChanged);

    [When("the choosing guest confirms their cart through the storefront")]
    public Task WhenGuestConfirmsCart() => Actor.AttemptsToAsync(new ConfirmDraftCartThroughTheUi());

    [Then("the Owner automatically sees both confirmed carts in the consolidated total")]
    public Task ThenBothConfirmedTotalsAreShown() => VerifyAsync(GroupOverviewStage.SecondGuestSubmitted);

    [Then("the Owner sees no guest selections and a zero consolidated total")]
    public Task ThenEmptyGroupIsShown() => VerifyAsync(GroupOverviewStage.Empty);

    private async Task VerifyAsync(GroupOverviewStage stage)
    {
        var expected = GroupOverviewExpectations.For(Recall<GroupOrderScenario>(), stage);
        var actual = await Actor.AsksForAsync(new TheGroupOverview(stage));
        var evidence = $"Rendered {actual.Participants.Count} participant cards; Grand Total {actual.GrandTotal:C2}.";
        await Assert.That(actual.Participants.Count).IsEqualTo(expected.Participants.Count).Because(evidence);
        foreach (var participant in expected.Participants)
        {
            var shown = actual.Participants.Single(item => item.Heading == participant.Heading);
            await Assert.That(shown.Status).IsEqualTo(participant.Status).Because(evidence);
            await Assert.That(shown.Subtotal).IsEqualTo(participant.Subtotal).Because(evidence);
            await Assert.That(shown.Dishes.SequenceEqual(participant.Dishes)).IsTrue().Because(evidence);
        }
        await Assert.That(actual.GrandTotal).IsEqualTo(expected.GrandTotal).Because(evidence);
        await Assert.That(actual.Consolidated.OrderBy(item => item.Name).SequenceEqual(expected.Consolidated.OrderBy(item => item.Name)))
            .IsTrue().Because(evidence);
    }
}
