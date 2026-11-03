using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Orders;
using Restaurant_Aqa_Framework.Support.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Questions.GroupOrders;

public sealed record SubmittedRestaurantOrder(GetOrderResponse Order, string GroupStatus);

public sealed class TheSubmittedRestaurantOrder : IQuestion<SubmittedRestaurantOrder>
{
    public async Task<SubmittedRestaurantOrder> AnsweredByAsync(Actor actor)
    {
        var fixture = actor.Recall<GroupOrderScenario>();
        var api = actor.UsingTheApi();
        var order = await GroupOrderScenario.ReadRequiredAsync<GetOrderResponse>(
            await new OrdersApi(api).GetOrderAsync(fixture.ReservationId, fixture.Waiter.IdToken),
            "Waiter reads the submitted order");
        var group = await GroupOrderScenario.ReadRequiredAsync<GroupStatus>(
            await new GroupOrdersApi(api).GetGroupStatusAsync(fixture.GroupId, fixture.Owner.IdToken),
            "Owner reads submitted group status");
        return new(order, group.Status);
    }

    private sealed record GroupStatus(string Status);
}
