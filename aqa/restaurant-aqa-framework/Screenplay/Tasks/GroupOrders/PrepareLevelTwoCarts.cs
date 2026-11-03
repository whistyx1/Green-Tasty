using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Orders;
using Restaurant_Aqa_Framework.Support.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

public enum LevelTwoCartSetup { Mixed, NoConfirmed, NoOrder }

public sealed class PrepareLevelTwoCarts(LevelTwoCartSetup setup) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var fixture = actor.Recall<GroupOrderScenario>();
        var api = actor.UsingTheApi();
        if (setup != LevelTwoCartSetup.NoOrder)
            await GroupOrderScenario.RequireSuccessAsync(
                await new OrdersApi(api).CreateOrderAsync(new CreateOrderRequest(fixture.ReservationId,
                    [new OrderItemRequest(fixture.Dish.Id, 1)]), fixture.Owner.IdToken),
                "Save the Owner's own preorder");
        if (setup == LevelTwoCartSetup.NoConfirmed)
        {
            var draft = await fixture.AddGuestAsync(api, fixture.SecondGuestName);
            await fixture.AddDishAsync(api, draft, fixture.SecondDish.Id, 2);
            return;
        }

        var first = await fixture.AddGuestAsync(api, fixture.FirstGuestName);
        await fixture.AddDishAsync(api, first, fixture.Dish.Id, 2);
        await fixture.SubmitGuestAsync(api, first);
        if (setup == LevelTwoCartSetup.NoOrder) return;

        var second = await fixture.AddGuestAsync(api, fixture.SecondGuestName);
        await fixture.AddDishAsync(api, second, fixture.SecondDish.Id, 1);
        await fixture.SubmitGuestAsync(api, second);
        var draftGuest = await fixture.AddGuestAsync(api, fixture.EmptyGuestName);
        await fixture.AddDishAsync(api, draftGuest, fixture.SecondDish.Id, 2);
    }
}
