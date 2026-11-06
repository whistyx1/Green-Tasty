using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Services.GuestSessions;
using Moq;

namespace APIHandler.Tests.Services;

public class GuestAuthorizationServiceTests
{
    [Fact]
    public async Task Validate_MissingHeaders_Returns401WithoutTokenLookup()
    {
        var sessions = new Mock<IGuestSessionService>();
        var context = Mock.Of<ILambdaContext>(c => c.Logger == Mock.Of<ILambdaLogger>());
        var response = await new GuestAuthorizationService(sessions.Object)
            .ValidateAsync(new APIGatewayProxyRequest { Headers = null }, context, "guest");
        Assert.NotNull(response);
        Assert.Equal(401, response.StatusCode);
        sessions.Verify(s => s.IsAuthorizedAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
