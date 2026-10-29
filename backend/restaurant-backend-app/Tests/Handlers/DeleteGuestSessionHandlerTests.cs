using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Handlers.GuestSessions;
using APIHandler.Models.GuestSessions;
using APIHandler.Services.GuestSessions;
using Moq;
using System.Text.Json;
namespace APIHandler.Tests.Handlers;

public class DeleteGuestSessionHandlerTests
{
    private readonly Mock<IGuestSessionService> _service = new();
    private readonly ILambdaContext _context = Mock.Of<ILambdaContext>(c => c.Logger == Mock.Of<ILambdaLogger>());

    [Fact]
    public async Task Delete_ReturnsLowerCamelSnapshotWithOwnerTypeAndDecimalTotal()
    {
        _service.Setup(s => s.DeleteGuestSessionAsync("owner", "group", "guest")).ReturnsAsync(new GetAllSessionsResponse([
            new Participant("owner-session", ParticipantType.Owner, "Igor", [], 10.25m, false)
        ], 10.25m));
        var response = await new DeleteGuestSessionHandler(_service.Object).HandleRequest(Request(), _context);
        Assert.Equal(200, response.StatusCode);
        using var body = JsonDocument.Parse(response.Body);
        Assert.Equal(10.25m, body.RootElement.GetProperty("total").GetDecimal());
        Assert.False(body.RootElement.TryGetProperty("Participants", out _));
        var participant = body.RootElement.GetProperty("participants")[0];
        Assert.Equal("Owner", participant.GetProperty("type").GetString());
        Assert.Equal("Igor", participant.GetProperty("name").GetString());
        Assert.False(participant.GetProperty("isSubmitted").GetBoolean());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Delete_RequiresNonblankTrustedSubject(string? subject)
    {
        var request = Request();
        request.Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer untrusted" };
        if (subject == null) request.RequestContext.Authorizer.Claims.Clear();
        else request.RequestContext.Authorizer.Claims["sub"] = subject;
        Assert.Equal(401, (await new DeleteGuestSessionHandler(_service.Object).HandleRequest(request, _context)).StatusCode);
        _service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("groupReservationId", null)]
    [InlineData("guestSessionId", null)]
    [InlineData("groupReservationId", "")]
    [InlineData("guestSessionId", " ")]
    public async Task Delete_RequiresBothNonblankPaths(string key, string? value)
    {
        var request = Request();
        if (value == null) request.PathParameters.Remove(key); else request.PathParameters[key] = value;
        Assert.Equal(400, (await new DeleteGuestSessionHandler(_service.Object).HandleRequest(request, _context)).StatusCode);
        _service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("group", 404)]
    [InlineData("reservation", 404)]
    [InlineData("session", 404)]
    [InlineData("dish", 404)]
    [InlineData("forbidden", 403)]
    [InlineData("mismatch", 400)]
    [InlineData("submitted", 409)]
    [InlineData("closed", 409)]
    public async Task Delete_MapsDomainErrors(string kind, int expected)
    {
        Exception error = kind switch {
            "group" => new GroupReservationNotFoundException(),
            "reservation" => new ReservationNotFoundException("missing"),
            "session" => new GuestSessionNotFoundException("missing"),
            "dish" => new DishNotFoundException("missing"),
            "forbidden" => new ActionForbiddenException("forbidden"),
            "submitted" => new GroupReservationSubmittedException(),
            "closed" => new GroupReservationExpiredException(),
            _ => new ValidationException("wrong group") };
        _service.Setup(s => s.DeleteGuestSessionAsync("owner", "group", "guest")).ThrowsAsync(error);
        Assert.Equal(expected, (await new DeleteGuestSessionHandler(_service.Object).HandleRequest(Request(), _context)).StatusCode);
    }

    private static APIGatewayProxyRequest Request() => new() {
        PathParameters = new Dictionary<string, string> { ["groupReservationId"] = "group", ["guestSessionId"] = "guest" },
        RequestContext = new APIGatewayProxyRequest.ProxyRequestContext { Authorizer = new APIGatewayCustomAuthorizerContext { Claims = new() { ["sub"] = "owner" } } }
    };
}
