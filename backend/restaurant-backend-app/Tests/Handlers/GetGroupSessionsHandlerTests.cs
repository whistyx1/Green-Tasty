using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Handlers.GuestSessions;
using APIHandler.Models.Dishes;
using APIHandler.Models.GuestSessions;
using APIHandler.Models.Orders;
using APIHandler.Services.GuestSessions;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Handlers;

public class GetGroupSessionsHandlerTests
{
    private readonly Mock<IGuestSessionService> _service = new();
    private readonly ILambdaContext _context = Mock.Of<ILambdaContext>(context => context.Logger == Mock.Of<ILambdaLogger>());

    [Fact]
    public async Task HandleRequest_OwnerReceivesCamelCaseSnapshotWithStringParticipantType()
    {
        var dish = new DishResponse
        {
            Id = "dish-1", Name = "Salad", Description = "Fresh", DishType = "Appetizer", Price = 8.25m,
            Weight = "100 g", Calories = "100", Proteins = "1", Fats = "1", Carbohydrates = "1",
            Vitamins = "A", State = "Available", ImageUrl = null
        };
        _service.Setup(service => service.GetAllSessionsAsync("owner-user", "group-1"))
            .ReturnsAsync(new GetAllSessionsResponse([
                new Participant("session-1", ParticipantType.Owner, "Owner", [new OrderItemResponse { Dish = dish, Quantity = 2 }], 16.50m, false)
            ], 16.50m));

        var response = await new GetGroupSessionsHandler(_service.Object).HandleRequest(Request(), _context);

        Assert.Equal(200, response.StatusCode);
        using var document = JsonDocument.Parse(response.Body);
        var root = document.RootElement;
        Assert.False(root.TryGetProperty("Participants", out _));
        Assert.Equal(16.50m, root.GetProperty("total").GetDecimal());
        var participant = root.GetProperty("participants")[0];
        Assert.Equal("session-1", participant.GetProperty("id").GetString());
        Assert.Equal("Owner", participant.GetProperty("type").GetString());
        Assert.Equal("Owner", participant.GetProperty("name").GetString());
        Assert.False(participant.GetProperty("isSubmitted").GetBoolean());
        Assert.Equal(16.50m, participant.GetProperty("total").GetDecimal());
        var item = participant.GetProperty("items")[0];
        Assert.Equal(2, item.GetProperty("quantity").GetInt32());
        Assert.Equal("dish-1", item.GetProperty("dish").GetProperty("id").GetString());
        Assert.Equal(8.25m, item.GetProperty("dish").GetProperty("price").GetDecimal());
        Assert.Equal("application/json", response.Headers["Content-Type"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleRequest_MissingTrustedSubjectReturns401WithoutCallingService(string? subject)
    {
        var request = Request(subject);
        request.Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer client-supplied-token" };

        var response = await new GetGroupSessionsHandler(_service.Object).HandleRequest(request, _context);

        Assert.Equal(401, response.StatusCode);
        _service.Verify(service => service.GetAllSessionsAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleRequest_MissingGroupIdReturns400WithoutCallingService(string? groupId)
    {
        var request = Request();
        request.PathParameters = groupId == null ? null : new Dictionary<string, string> { ["groupReservationId"] = groupId };

        var response = await new GetGroupSessionsHandler(_service.Object).HandleRequest(request, _context);

        Assert.Equal(400, response.StatusCode);
        _service.Verify(service => service.GetAllSessionsAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_EmptyGroupReturnsEmptyArrayAndZero()
    {
        _service.Setup(service => service.GetAllSessionsAsync("owner-user", "group-1"))
            .ReturnsAsync(new GetAllSessionsResponse([], 0m));

        var response = await new GetGroupSessionsHandler(_service.Object).HandleRequest(Request(), _context);

        Assert.Equal(200, response.StatusCode);
        using var document = JsonDocument.Parse(response.Body);
        Assert.Equal(0, document.RootElement.GetProperty("participants").GetArrayLength());
        Assert.Equal(0m, document.RootElement.GetProperty("total").GetDecimal());
    }

    [Theory]
    [InlineData("group", 404)]
    [InlineData("reservation", 404)]
    [InlineData("dish", 404)]
    [InlineData("owner", 403)]
    public async Task HandleRequest_ServiceFailuresMapToExpectedHttpStatus(string failure, int status)
    {
        Exception exception = failure switch
        {
            "group" => new GroupReservationNotFoundException(),
            "reservation" => new ReservationNotFoundException("missing"),
            "dish" => new DishNotFoundException("missing"),
            _ => new ActionForbiddenException("Only the owner may read this group.")
        };
        _service.Setup(service => service.GetAllSessionsAsync("owner-user", "group-1")).ThrowsAsync(exception);

        var response = await new GetGroupSessionsHandler(_service.Object).HandleRequest(Request(), _context);

        Assert.Equal(status, response.StatusCode);
        using var document = JsonDocument.Parse(response.Body);
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("error").GetString()));
    }

    private static APIGatewayProxyRequest Request(string? subject = "owner-user")
    {
        var request = new APIGatewayProxyRequest
        {
            HttpMethod = "GET", Path = "/guest/groups/group-1/sessions",
            PathParameters = new Dictionary<string, string> { ["groupReservationId"] = "group-1" }
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
