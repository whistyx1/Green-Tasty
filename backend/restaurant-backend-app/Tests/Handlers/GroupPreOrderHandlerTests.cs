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
    public async Task Lookup_ReturnsExistingSessionIdAndAdditiveGroupIdInCamelCase()
    {
        _service.Setup(s => s.GetOwnerSessionContextAsync("owner", "reservation"))
            .ReturnsAsync(new OwnerSessionContext("opaque-owner-session", "group"));
        var response = await new GetOwnerSessionIdHandler(_service.Object).HandleRequest(Request(), _context);
        Assert.Equal(200, response.StatusCode);
        using var body = JsonDocument.Parse(response.Body);
        Assert.Equal("opaque-owner-session", body.RootElement.GetProperty("guestSessionId").GetString());
        Assert.Equal("group", body.RootElement.GetProperty("groupReservationId").GetString());
        Assert.Equal(2, body.RootElement.EnumerateObject().Count());
    }

    [Theory]
    [InlineData("forbidden", 403)]
    [InlineData("reservation", 404)]
    [InlineData("group", 404)]
    [InlineData("inactive", 409)]
    public async Task Lookup_ContextFailuresPreserveHttpContract(string scenario, int expectedStatus)
    {
        Exception error = scenario switch {
            "forbidden" => new ActionForbiddenException("Forbidden"),
            "reservation" => new ReservationNotFoundException("Reservation not found"),
            "group" => new GroupReservationNotFoundException(),
            _ => new GroupPreOrderConflictException("Inactive") };
        _service.Setup(s => s.GetOwnerSessionContextAsync("owner", "reservation")).ThrowsAsync(error);
        var response = await new GetOwnerSessionIdHandler(_service.Object).HandleRequest(Request(), _context);
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.DoesNotContain("guestSessionId", response.Body);
        Assert.DoesNotContain("groupReservationId", response.Body);
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

    [Fact]
    public async Task Submit_AllInRestaurantCompletionReturnsEmptyArrayWithoutChangingTheRoute()
    {
        _service.Setup(s => s.SubmitAsync("owner", "group", "reservation"))
            .ReturnsAsync([]);

        var response = await new SubmitGroupPreOrderHandler(_service.Object)
            .HandleRequest(Request(), _context);

        Assert.Equal(200, response.StatusCode);
        using var body = JsonDocument.Parse(response.Body);
        Assert.Empty(body.RootElement.EnumerateArray());
        _service.Verify(s => s.SubmitAsync("owner", "group", "reservation"), Times.Once);
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

    [Fact]
    public async Task OwnerPreference_RequiresTrustedClaimsAndExpectedRevision()
    {
        var request = Request();
        request.Body = "{\"preference\":\"InRestaurant\",\"expectedRevision\":2}";
        request.RequestContext = new();
        Assert.Equal(401, (await new SetOwnerPreferenceHandler(_service.Object).HandleRequest(request, _context)).StatusCode);
        _service.VerifyNoOtherCalls();

        request.RequestContext = Request().RequestContext;
        request.Body = "{\"preference\":\"InRestaurant\"}";
        Assert.Equal(400, (await new SetOwnerPreferenceHandler(_service.Object).HandleRequest(request, _context)).StatusCode);
        _service.VerifyNoOtherCalls();

        request.Body = "{\"preference\":\"InRestaurant\",\"expectedRevision\":2}";
        _service.Setup(s => s.SetOwnerPreferenceAsync("owner", "reservation", "InRestaurant", 2))
            .ReturnsAsync(new OwnerPreferenceState("InRestaurant", false, 3));
        Assert.Equal(200, (await new SetOwnerPreferenceHandler(_service.Object).HandleRequest(request, _context)).StatusCode);
        _service.Verify(s => s.SetOwnerPreferenceAsync("owner", "reservation", "InRestaurant", 2), Times.Once);
    }

    [Fact]
    public async Task OwnerSubmission_RequiresExpectedOrderRevision()
    {
        var request = Request();
        request.Body = "{}";
        Assert.Equal(400, (await new SubmitOwnerPreOrderHandler(_service.Object).HandleRequest(request, _context)).StatusCode);
        _service.VerifyNoOtherCalls();

        request.Body = "{\"expectedOrderRevision\":4}";
        _service.Setup(s => s.SubmitOwnerAsync("owner", "reservation", 4))
            .ReturnsAsync(new OwnerPreferenceState("Online", true, 1));
        Assert.Equal(200, (await new SubmitOwnerPreOrderHandler(_service.Object).HandleRequest(request, _context)).StatusCode);
        _service.Verify(s => s.SubmitOwnerAsync("owner", "reservation", 4), Times.Once);
    }

    private static APIGatewayProxyRequest Request() => new() {
        PathParameters = new Dictionary<string, string>() { ["reservationId"] = "reservation", ["groupReservationId"] = "group" },
        Body = "{\"reservationId\":\"reservation\"}",
        RequestContext = new APIGatewayProxyRequest.ProxyRequestContext {
            Authorizer = new APIGatewayCustomAuthorizerContext { Claims = new() { ["sub"] = "owner" } }
        }
    };
}
