using System.Globalization;
using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;

public sealed record OwnerSubmission(string Status, string Message, decimal Total, IReadOnlyList<string> Dishes);

public sealed class TheOwnerSubmission : IQuestion<OwnerSubmission>
{
    public async Task<OwnerSubmission> AnsweredByAsync(Actor actor)
    {
        var page = new GroupOrderPage(actor.LookingAtThePage());
        await Assertions.Expect(page.SentMessage).ToBeVisibleAsync(new() { Timeout = 45_000 });
        await Assertions.Expect(page.ConsolidatedRows).ToHaveCountAsync(2, new() { Timeout = 45_000 });
        var rows = new List<string>();
        foreach (var row in await page.ConsolidatedRows.AllAsync())
            rows.Add((await row.Locator("span").First.TextContentAsync())!.Trim());
        return new(
            (await page.SentStatus.TextContentAsync())!.Trim(),
            (await page.SentMessage.TextContentAsync())!.Trim(),
            GroupOrderPage.ParseMoney((await page.GrandTotal.TextContentAsync())!.Trim()), rows);
    }
}
