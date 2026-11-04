using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Constants.Dishes;
using APIHandler.Entities;
using APIHandler.Handlers.Dishes;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Handlers;

public class GetAllDishesHandlerTests
{
    static GetAllDishesHandlerTests()
    {
        Environment.SetEnvironmentVariable("dishesTable", "test-dishes");
        Environment.SetEnvironmentVariable("ordersTable", "test-orders");
    }

    [Fact]
    public async Task PopularityDescending_ExcludesGuardsAndCountsRealOrders()
    {
        var (context, handler) = Handler(
            [Dish("salad"), Dish("pasta")],
            [Guard(null), Guard([Item("pasta"), Item("pasta")]),
             Order("first", [Item("salad"), Item("pasta")]),
             Order("second", [Item("salad")]), Order("empty", [])]);

        var response = await handler.HandleRequest(Request(DishSortingLabels.PopularityDescending), Mock.Of<ILambdaContext>());

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(["salad", "pasta"], DishIds(response.Body));
        context.Verify(value => value.ScanAsync<Order>(It.IsAny<List<ScanCondition>>(),
            It.IsAny<DynamoDBOperationConfig>()), Times.Once);
    }

    [Fact]
    public async Task PopularityAscending_HandlesLegacyOrderWithoutDishes()
    {
        var (_, handler) = Handler(
            [Dish("salad"), Dish("pasta")],
            [Order("legacy", null), Order("current", [Item("salad")])]);

        var response = await handler.HandleRequest(Request(DishSortingLabels.PopularityAscending), Mock.Of<ILambdaContext>());

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(["pasta", "salad"], DishIds(response.Body));
    }

    [Fact]
    public async Task PopularityDescending_WithOnlyGuardsAndEmptyOrders_ReturnsDishes()
    {
        var (_, handler) = Handler(
            [Dish("salad"), Dish("pasta")],
            [Guard(null), Order("empty", [])]);

        var response = await handler.HandleRequest(Request(DishSortingLabels.PopularityDescending), Mock.Of<ILambdaContext>());

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(["salad", "pasta"], DishIds(response.Body));
    }

    [Fact]
    public async Task PriceSorting_DoesNotScanOrders()
    {
        var (context, handler) = Handler([Dish("salad", 8), Dish("pasta", 14)], []);

        var response = await handler.HandleRequest(Request(DishSortingLabels.PriceDescending), Mock.Of<ILambdaContext>());

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(["pasta", "salad"], DishIds(response.Body));
        context.Verify(value => value.ScanAsync<Order>(It.IsAny<List<ScanCondition>>(),
            It.IsAny<DynamoDBOperationConfig>()), Times.Never);
    }

    private static (Mock<IDynamoDBContext>, GetAllDishesHandler) Handler(
        List<Dish> dishes, List<Order> orders)
    {
        var context = new Mock<IDynamoDBContext>();
        var dishSearch = new Mock<AsyncSearch<Dish>>();
        dishSearch.Setup(value => value.GetRemainingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(dishes);
        context.Setup(value => value.ScanAsync<Dish>(It.IsAny<List<ScanCondition>>(),
            It.IsAny<DynamoDBOperationConfig>())).Returns(dishSearch.Object);
        var orderSearch = new Mock<AsyncSearch<Order>>();
        orderSearch.Setup(value => value.GetRemainingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(orders);
        context.Setup(value => value.ScanAsync<Order>(It.IsAny<List<ScanCondition>>(),
            It.IsAny<DynamoDBOperationConfig>())).Returns(orderSearch.Object);
        return (context, new GetAllDishesHandler(context.Object));
    }

    private static APIGatewayProxyRequest Request(string sortBy) => new()
    {
        QueryStringParameters = new Dictionary<string, string> { ["sortBy"] = sortBy }
    };

    private static string[] DishIds(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("id").GetString()!).ToArray();
    }

    private static Dish Dish(string id, decimal price = 8) => new()
    {
        Id = id, Name = id, Price = price, Weight = "250 g", Description = "Test",
        Calories = "0", Proteins = "0", Fats = "0", Carbohydrates = "0", Vitamins = "0",
        DishType = DishType.MainCourse, State = DishState.Available
    };

    private static OrderItem Item(string id) => new() { DishId = id, Quantity = 1 };

    private static Order Order(string id, List<OrderItem>? dishes) => new()
    {
        OrderId = id, CustomerId = "customer", ReservationId = "reservation",
        ReservationAddress = "Test", ReservationTime = "2030-11-03", Dishes = dishes!
    };

    private static Order Guard(List<OrderItem>? dishes) => new()
    {
        OrderId = "reservation-order#reservation", CustomerId = "__reservation_order_guard__",
        ReservationId = null!, ReservationAddress = null!, ReservationTime = null!, Dishes = dishes!
    };
}
