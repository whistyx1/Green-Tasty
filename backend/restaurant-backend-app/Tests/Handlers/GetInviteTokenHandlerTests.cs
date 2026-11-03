using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Handlers.GroupReservations;
using APIHandler.Models.GroupReservation;
using APIHandler.Services.GroupReservations;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Handlers;

public class GetInviteTokenHandlerTests
{
    private readonly Mock<IGroupReservationService> _service = new();
    private readonly ILambdaContext _context = Mock.Of<ILambdaContext>(context => context.Logger == Mock.Of<ILambdaLogger>());

    [Fact]
    public async Task HandleRequest_OwnerReceivesExistingToken()
    {
        _service.Setup(service => service.GetGroupReservationInviteTokenAsync("owner-user", "reservation-1"))
            .ReturnsAsync(new GetGroupInviteTokenResponse("existing-token"));

        var response = await new GetInviteTokenHandler(_service.Object).HandleRequest(Request(), _context);

        Assert.Equal(200, response.StatusCode);
        using var document = JsonDocument.Parse(response.Body);
        Assert.Equal("existing-token", document.RootElement.GetProperty("token").GetString());
        _service.Verify(service => service.GetGroupReservationInviteTokenAsync("owner-user", "reservation-1"), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleRequest_WithoutTrustedSubjectReturns401(string? subject)
    {
        var request = Request(subject);
        request.Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer untrusted-client-value" };

        var response = await new GetInviteTokenHandler(_service.Object).HandleRequest(request, _context);

        Assert.Equal(401, response.StatusCode);
        _service.Verify(service => service.GetGroupReservationInviteTokenAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_NonOwnerReturns403WithoutToken()
    {
        _service.Setup(service => service.GetGroupReservationInviteTokenAsync("other-user", "reservation-1"))
            .ThrowsAsync(new ActionForbiddenException("Only the reservation owner can perform this action."));

        var response = await new GetInviteTokenHandler(_service.Object).HandleRequest(Request("other-user"), _context);

        Assert.Equal(403, response.StatusCode);
        Assert.DoesNotContain("token", response.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("submitted")]
    [InlineData("expired")]
    public async Task HandleRequest_InactiveGroupReturnsValidationError(string reason)
    {
        ValidationException exception = reason == "submitted"
            ? new GroupReservationSubmittedException()
            : new GroupReservationExpiredException();
        _service.Setup(service => service.GetGroupReservationInviteTokenAsync("owner-user", "reservation-1"))
            .ThrowsAsync(exception);

        var response = await new GetInviteTokenHandler(_service.Object).HandleRequest(Request(), _context);

        Assert.Equal(400, response.StatusCode);
        Assert.DoesNotContain("token", response.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(reason, response.Body, StringComparison.OrdinalIgnoreCase);
    }

    private static APIGatewayProxyRequest Request(string? subject = "owner-user")
    {
        var request = new APIGatewayProxyRequest
        {
            HttpMethod = "GET",
            Path = "/reservations/reservation-1/invite-token",
            PathParameters = new Dictionary<string, string> { ["reservationId"] = "reservation-1" }
        };
        if (subject != null)
        {
            request.RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string> { ["sub"] = subject }
                }
            };
        }
        return request;
    }
}
