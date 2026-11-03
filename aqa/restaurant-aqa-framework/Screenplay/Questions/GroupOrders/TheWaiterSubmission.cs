using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Support.GroupOrders;
using Restaurant_Aqa_Framework.Ui.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;

public sealed record WaiterSubmission(IReadOnlyList<(string Name, int Quantity)> Dishes);

public sealed class TheWaiterSubmission : IQuestion<WaiterSubmission>
{
    public async Task<WaiterSubmission> AnsweredByAsync(Actor actor)
    {
        var fixture = actor.Recall<GroupOrderScenario>();
        var page = actor.Recall<GroupOrderBrowserState>().WaiterPage
            ?? throw new InvalidOperationException("The Waiter browser has not been opened.");
        var waiter = new WaiterGroupOrderPage(page);
        await Assertions.Expect(waiter.Dishes).ToHaveCountAsync(2, new() { Timeout = 45_000 });
        var result = new List<(string Name, int Quantity)>();
        foreach (var name in new[] { fixture.Dish.Name, fixture.SecondDish.Name })
        {
            var card = waiter.Dish(name);
            await Assertions.Expect(card).ToHaveCountAsync(1);
            var text = (await WaiterGroupOrderPage.Quantity(card).TextContentAsync())!.Trim();
            if (!text.StartsWith("Quantity: ", StringComparison.Ordinal)
                || !int.TryParse(text["Quantity: ".Length..], out var quantity))
                throw new InvalidOperationException("Waiter card did not display a numeric quantity.");
            result.Add((name, quantity));
        }
        return new(result);
    }
}
