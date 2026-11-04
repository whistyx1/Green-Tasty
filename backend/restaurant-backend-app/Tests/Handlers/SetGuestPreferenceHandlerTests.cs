using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Handlers.GuestSessions;
using APIHandler.Models.GuestSessions;
using APIHandler.Services.GuestSessions;
using Moq;

namespace APIHandler.Tests.Handlers;

public class SetGuestPreferenceHandlerTests
{
    private readonly Mock<IGuestSessionService> service = new();
    private readonly Mock<IGuestAuthorizationService> authorization = new();
    private readonly ILambdaContext context = Mock.Of<ILambdaContext>(c => c.Logger == Mock.Of<ILambdaLogger>());

    [Fact]
    public async Task DeniedGuestTokenNeverChangesPreference()
    {
        var request = Request("{\"preference\":\"InRestaurant\",\"expectedRevision\":1}");
        authorization.Setup(a => a.ValidateAsync(request, context, "guest"))
            .ReturnsAsync(new APIGatewayProxyResponse { StatusCode = 401, Body = "Unauthorized" });
        var response = await new SetGuestPreferenceHandler(service.Object, authorization.Object)
            .HandleRequest(request, context);
        Assert.Equal(401, response.StatusCode);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AuthorizedGuestMustSupplyRevision()
    {
        var request = Request("{\"preference\":\"InRestaurant\"}");
        var response = await new SetGuestPreferenceHandler(service.Object, authorization.Object)
            .HandleRequest(request, context);
        Assert.Equal(400, response.StatusCode);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AuthorizedGuestChangesOnlyItsSessionWithExpectedRevision()
    {
        var request = Request("{\"preference\":\"InRestaurant\",\"expectedRevision\":1}");
        service.Setup(s => s.SetPreferenceAsync("guest", OrderingPreference.InRestaurant, 1))
            .ReturnsAsync(new GetGuestSessionResponse("Guest", [], 0, false, OrderingPreference.InRestaurant, 2));
        var response = await new SetGuestPreferenceHandler(service.Object, authorization.Object)
            .HandleRequest(request, context);
        Assert.Equal(200, response.StatusCode);
        service.Verify(s => s.SetPreferenceAsync("guest", OrderingPreference.InRestaurant, 1), Times.Once);
    }

    private static APIGatewayProxyRequest Request(string body) => new()
    {
        PathParameters = new Dictionary<string, string> { ["guestSessionId"] = "guest" },
        Body = body,
    };
}
