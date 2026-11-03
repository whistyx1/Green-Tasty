using System.Globalization;
using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Support.GroupOrders;
using Restaurant_Aqa_Framework.Ui.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;

/// <summary>Waits for actual rendered values across polling/GSI propagation, without Refresh or reload.</summary>
public sealed class TheGroupOverview(GroupOverviewStage stage) : IQuestion<GroupOverviewSnapshot>
{
    private const float PollingTimeout = 45_000;

    public async Task<GroupOverviewSnapshot> AnsweredByAsync(Actor actor)
    {
        var expected = GroupOverviewExpectations.For(actor.Recall<GroupOrderScenario>(), stage);
        var page = new GroupOrderPage(actor.LookingAtThePage());
        await Assertions.Expect(page.Title).ToBeVisibleAsync(new() { Timeout = PollingTimeout });
        await Assertions.Expect(page.Participants).ToHaveCountAsync(expected.Participants.Count, new() { Timeout = PollingTimeout });
        foreach (var participant in expected.Participants)
        {
            var card = page.Participant(participant.Heading);
            await Assertions.Expect(GroupOrderPage.Status(card)).ToHaveTextAsync(participant.Status, new() { Timeout = PollingTimeout });
            await Assertions.Expect(GroupOrderPage.Subtotal(card)).ToHaveTextAsync(GroupOrderPage.Money(participant.Subtotal), new() { Timeout = PollingTimeout });
            await Assertions.Expect(GroupOrderPage.Dishes(card)).ToHaveCountAsync(participant.Dishes.Count, new() { Timeout = PollingTimeout });
            foreach (var dish in participant.Dishes)
            {
                var row = GroupOrderPage.Dish(card, dish.Name);
                await Assertions.Expect(row.GetByLabel($"Quantity: {dish.Quantity}", new() { Exact = true })).ToBeVisibleAsync(new() { Timeout = PollingTimeout });
                await Assertions.Expect(row.Locator(".price")).ToHaveTextAsync(GroupOrderPage.Money(dish.Amount), new() { Timeout = PollingTimeout });
            }
            if (participant.Dishes.Count == 0)
                await Assertions.Expect(card.GetByText("No dishes yet", new() { Exact = true })).ToBeVisibleAsync(new() { Timeout = PollingTimeout });
        }
        await Assertions.Expect(page.GrandTotal).ToHaveTextAsync(GroupOrderPage.Money(expected.GrandTotal), new() { Timeout = PollingTimeout });
        await Assertions.Expect(page.ConsolidatedRows).ToHaveCountAsync(expected.Consolidated.Count, new() { Timeout = PollingTimeout });
        foreach (var dish in expected.Consolidated)
        {
            var row = page.ConsolidatedRows.Filter(new() { HasText = dish.Name });
            await Assertions.Expect(row.Locator("span").First).ToHaveTextAsync($"{dish.Name} × {dish.Quantity}", new() { Timeout = PollingTimeout });
            await Assertions.Expect(row.Locator("span").Last).ToHaveTextAsync(GroupOrderPage.Money(dish.Amount), new() { Timeout = PollingTimeout });
        }
        if (stage == GroupOverviewStage.Empty)
        {
            await Assertions.Expect(page.EmptyMessage).ToBeVisibleAsync();
            await Assertions.Expect(page.NoSubmittedDishes).ToBeVisibleAsync();
        }
        else
        {
            await Assertions.Expect(page.PendingWarning).ToBeVisibleAsync();
        }
        return await ReadAsync(page);
    }

    private static async Task<GroupOverviewSnapshot> ReadAsync(GroupOrderPage page)
    {
        var participants = new List<GroupParticipantView>();
        foreach (var card in await page.Participants.AllAsync())
        {
            var dishes = new List<GroupDishView>();
            foreach (var row in await GroupOrderPage.Dishes(card).AllAsync())
            {
                var quantity = (await row.Locator(".quantity").TextContentAsync())!.Replace("×", "", StringComparison.Ordinal).Trim();
                dishes.Add(new((await row.Locator(".dish-name").TextContentAsync())!.Trim(),
                    int.Parse(quantity, CultureInfo.InvariantCulture), GroupOrderPage.ParseMoney((await row.Locator(".price").TextContentAsync())!.Trim())));
            }
            participants.Add(new((await card.GetByRole(AriaRole.Heading).TextContentAsync())!.Trim(),
                (await GroupOrderPage.Status(card).TextContentAsync())!.Trim(),
                GroupOrderPage.ParseMoney((await GroupOrderPage.Subtotal(card).TextContentAsync())!.Trim()), dishes));
        }
        var consolidated = new List<GroupDishView>();
        foreach (var row in await page.ConsolidatedRows.AllAsync())
        {
            var label = (await row.Locator("span").First.TextContentAsync())!.Trim();
            var separator = label.LastIndexOf('×');
            consolidated.Add(new(label[..separator].Trim(), int.Parse(label[(separator + 1)..].Trim(), CultureInfo.InvariantCulture),
                GroupOrderPage.ParseMoney((await row.Locator("span").Last.TextContentAsync())!.Trim())));
        }
        return new(participants, GroupOrderPage.ParseMoney((await page.GrandTotal.TextContentAsync())!.Trim()), consolidated);
    }
}
