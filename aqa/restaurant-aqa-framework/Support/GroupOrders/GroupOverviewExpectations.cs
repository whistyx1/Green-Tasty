namespace Restaurant_Aqa_Framework.Support.GroupOrders;

/// <summary>Independent exact expectations for the two deliberately seeded decimal prices.</summary>
public static class GroupOverviewExpectations
{
    public static GroupOverviewSnapshot For(GroupOrderScenario scenario, GroupOverviewStage stage)
    {
        var owner = new GroupParticipantView("My Pre-Order", "Still choosing", 0m, []);
        if (stage == GroupOverviewStage.Empty)
            return new([owner], 0m, []);
        var secondQuantity = stage == GroupOverviewStage.InitialGuests ? 1 : 2;
        var secondSubmitted = stage == GroupOverviewStage.SecondGuestSubmitted;
        var firstDish = new GroupDishView(scenario.Dish.Name, 2, 16.50m);
        var secondDish = new GroupDishView(scenario.SecondDish.Name, secondQuantity, secondQuantity == 1 ? 12.40m : 24.80m);
        GroupParticipantView[] participants =
        [
            owner,
            new($"Guest: {scenario.FirstGuestName}", "Submitted", 16.50m, [firstDish]),
            new($"Guest: {scenario.SecondGuestName}", secondSubmitted ? "Submitted" : "Still choosing", secondDish.Amount, [secondDish]),
            new($"Guest: {scenario.EmptyGuestName}", "Still choosing", 0m, []),
        ];
        return new(participants, secondSubmitted ? 41.30m : 16.50m,
            secondSubmitted ? [firstDish, secondDish] : [firstDish]);
    }
}
