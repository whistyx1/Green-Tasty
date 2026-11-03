using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Orders;
using Restaurant_Aqa_Framework.Support.GroupOrders;
using Restaurant_Aqa_Framework.Ui.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks.GroupOrders;

public sealed class PrepareLevelTwoUiSelections : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var fixture = actor.Recall<GroupOrderScenario>();
        await GroupOrderScenario.RequireSuccessAsync(
            await new OrdersApi(actor.UsingTheApi()).CreateOrderAsync(new CreateOrderRequest(fixture.ReservationId,
                [new OrderItemRequest(fixture.SecondDish.Id, 1)]), fixture.Owner.IdToken),
            "Save the Owner's own preorder");
        await actor.AttemptsToAsync(new GuestsChooseThroughTheUi());

        var state = actor.Recall<GroupOrderBrowserState>();
        // The reservation admits Owner plus three guests. Reuse the third, empty
        // guest rather than trying to join a fourth beyond its capacity.
        var guestPage = new GuestGroupOrderPage(state.GuestPages[fixture.EmptyGuestName]);
        await guestPage.AddDishAsync(fixture.Dish.Name, 1);
        await guestPage.SubmitCartAsync();
    }
}
