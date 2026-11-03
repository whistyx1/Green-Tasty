using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;

public sealed record RemovalOverview(decimal Total, IReadOnlyList<string> Participants, IReadOnlyList<string> Dishes);

public sealed class TheRemovalOverview(bool afterRemoval) : IQuestion<RemovalOverview>
{
    public async Task<RemovalOverview> AnsweredByAsync(Actor actor)
    {
        var page = new GroupOrderPage(actor.LookingAtThePage());
        await Assertions.Expect(page.Participants).ToHaveCountAsync(afterRemoval ? 3 : 4, new() { Timeout = 45_000 });
        await Assertions.Expect(page.GrandTotal).ToHaveTextAsync(afterRemoval ? "$20.65" : "$37.15", new() { Timeout = 45_000 });
        await Assertions.Expect(page.ConsolidatedRows).ToHaveCountAsync(2, new() { Timeout = 45_000 });
        var headings = new List<string>();
        foreach (var card in await page.Participants.AllAsync())
            headings.Add((await card.GetByRole(AriaRole.Heading).TextContentAsync())!.Trim());
        var dishes = new List<string>();
        foreach (var row in await page.ConsolidatedRows.AllAsync())
            dishes.Add((await row.Locator("span").First.TextContentAsync())!.Trim());
        return new(GroupOrderPage.ParseMoney((await page.GrandTotal.TextContentAsync())!.Trim()), headings, dishes);
    }
}
