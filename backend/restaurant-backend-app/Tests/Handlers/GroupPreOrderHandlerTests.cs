using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Handlers.GroupReservations;
using APIHandler.Services.GroupPreOrders;
using Moq;
using System.Text.Json;
namespace APIHandler.Tests.Handlers;

public class GroupPreOrderHandlerTests
{
    private readonly Mock<IGroupPreOrderService> _service = new();
    private readonly ILambdaContext _context = Mock.Of<ILambdaContext>(c => c.Logger == Mock.Of<ILambdaLogger>());

    [Fact]
    public async Task Lookup_ReturnsCamelCaseSessionId()
    {
        _service.Setup(s => s.GetOwnerSessionIdAsync("owner", "reservation")).ReturnsAsync("owner-session");
        var response = await new GetOwnerSessionIdHandler(_service.Object).HandleRequest(Request(), _context);
        Assert.Equal(200, response.StatusCode);
        Assert.Equal("{\"guestSessionId\":\"owner-session\"}", response.Body);
    }

    [Fact]
    public async Task Submit_ReturnsOnlyCamelCaseDishIdsAndQuantities()
    {
        _service.Setup(s => s.SubmitAsync("owner", "group", "reservation")).ReturnsAsync([new ConsolidatedDish("dish", 3)]);
        var response = await new SubmitGroupPreOrderHandler(_service.Object).HandleRequest(Request(), _context);
        Assert.Equal(200, response.StatusCode);
        using var body = JsonDocument.Parse(response.Body);
        var dish = Assert.Single(body.RootElement.EnumerateArray());
        Assert.Equal(2, dish.EnumerateObject().Count());
        Assert.Equal("dish", dish.GetProperty("dishId").GetString());
        Assert.Equal(3, dish.GetProperty("quantity").GetInt32());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BothEndpoints_RejectUntrustedBearerWithoutClaims(bool submit)
    {
        var request = Request(); request.RequestContext = new();
        request.Headers = new Dictionary<string, string>() { ["Authorization"] = "Bearer untrusted-client-token" };
        var response = submit ? await new SubmitGroupPreOrderHandler(_service.Object).HandleRequest(request, _context)
            : await new GetOwnerSessionIdHandler(_service.Object).HandleRequest(request, _context);
        Assert.Equal(401, response.StatusCode); _service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{}")]
    [InlineData("{\"reservationId\":\" \"}")]
    public async Task Submit_InvalidBodyReturns400WithoutServiceCall(string? body)
    {
        var request = Request(); request.Body = body;
        Assert.Equal(400, (await new SubmitGroupPreOrderHandler(_service.Object).HandleRequest(request, _context)).StatusCode);
        _service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    public async Task Submit_MapsDomainFailures(int status)
    {
        Exception error = status switch {
            403 => new ActionForbiddenException("Forbidden"),
            404 => new GroupReservationNotFoundException(),
            409 => new GroupPreOrderConflictException("Changed"),
            _ => new ValidationException("Invalid") };
        _service.Setup(s => s.SubmitAsync("owner", "group", "reservation")).ThrowsAsync(error);
        Assert.Equal(status, (await new SubmitGroupPreOrderHandler(_service.Object).HandleRequest(Request(), _context)).StatusCode);
    }

    private static APIGatewayProxyRequest Request() => new() {
        PathParameters = new Dictionary<string, string>() { ["reservationId"] = "reservation", ["groupReservationId"] = "group" },
        Body = "{\"reservationId\":\"reservation\"}",
        RequestContext = new APIGatewayProxyRequest.ProxyRequestContext {
            Authorizer = new APIGatewayCustomAuthorizerContext { Claims = new() { ["sub"] = "owner" } }
        }
    };
}
