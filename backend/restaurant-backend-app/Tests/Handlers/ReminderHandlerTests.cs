using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Handlers.GuestSessions;
using APIHandler.Models.GuestSessions;
using APIHandler.Services.GuestSessions;
using Moq;
using System.Collections.Generic;
using System.Text.Json;

namespace APIHandler.Tests.Handlers;

public class ReminderHandlerTests
{
    private readonly Mock<IReminderService> _service = new();
    private readonly Mock<IGuestAuthorizationService> _guestAuthorization = new();
    private readonly ILambdaContext _context = Mock.Of<ILambdaContext>(c => c.Logger == Mock.Of<ILambdaLogger>());

    [Fact]
    public async Task OwnerReminder_WithoutTrustedSubject_Returns401WithoutServiceCall()
    {
        var response = await new SendGuestReminderHandler(_service.Object).HandleRequest(new APIGatewayProxyRequest
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer untrusted" },
            PathParameters = new Dictionary<string, string> { ["groupReservationId"] = "group", ["guestSessionId"] = "guest" }
        }, _context);
        Assert.Equal(401, response.StatusCode);
        _service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OwnerReminder_ReturnsCamelCaseChannelAndCooldown()
    {
        var next = new DateTime(2026, 11, 5, 12, 15, 0, DateTimeKind.Utc);
        _service.Setup(s => s.SendAsync("owner", "group", "guest"))
            .ReturnsAsync(new ReminderResponse("InApp", next));
        var response = await new SendGuestReminderHandler(_service.Object).HandleRequest(new APIGatewayProxyRequest
        {
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            { Authorizer = new APIGatewayCustomAuthorizerContext { Claims = new Dictionary<string, string> { ["sub"] = "owner" } } },
            PathParameters = new Dictionary<string, string> { ["groupReservationId"] = "group", ["guestSessionId"] = "guest" }
        }, _context);
        Assert.Equal(200, response.StatusCode);
        using var document = JsonDocument.Parse(response.Body);
        Assert.Equal("InApp", document.RootElement.GetProperty("channel").GetString());
        Assert.Equal(next, document.RootElement.GetProperty("nextAllowedAt").GetDateTime());
    }

    [Fact]
    public async Task GuestNotifications_InvalidSessionToken_Returns401WithoutQuery()
    {
        _guestAuthorization.Setup(a => a.ValidateAsync(It.IsAny<APIGatewayProxyRequest>(), _context, "guest"))
            .Returns(new ValueTask<APIGatewayProxyResponse?>(new APIGatewayProxyResponse { StatusCode = 401 }));
        var response = await new GetGuestReminderNotificationsHandler(_service.Object, _guestAuthorization.Object)
            .HandleRequest(new APIGatewayProxyRequest { PathParameters = new Dictionary<string, string> { ["guestSessionId"] = "guest" } }, _context);
        Assert.Equal(401, response.StatusCode);
        _service.VerifyNoOtherCalls();
    }
}
