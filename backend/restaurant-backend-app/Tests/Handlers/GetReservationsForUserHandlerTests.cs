using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Handlers.Reservations;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.Reservations;
using APIHandler.Services.Locations;
using APIHandler.Services.Orders;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Handlers;

public class GetReservationsForUserHandlerTests
{
    [Theory]
    [InlineData(ReservationStatus.Cancelled, 200)]
    [InlineData(ReservationStatus.Reserved, 500)]
    public async Task WaiterList_CorruptCancelledHistoryDoesNotHideCurrentData(ReservationStatus status, int expectedStatus)
    {
        var reservation = new Reservation
        {
            Id = "old-reservation", TableId = "removed-table", WaiterId = "waiter",
            CustomerId = "customer", Status = status,
        };
        var current = new Reservation
        {
            Id = "current-reservation", TableId = "active-table", WaiterId = "waiter",
            CustomerId = "customer", Status = ReservationStatus.Reserved, GuestCount = 1,
        };
        var reservations = new Mock<IReservationRepository>();
        reservations.Setup(repository => repository.GetReservationsByWaiterIdAsync("waiter"))
            .ReturnsAsync([reservation, current]);
        var locations = new Mock<ILocationService>();
        locations.Setup(service => service.GetLocationByTableIdAsync("removed-table"))
            .ThrowsAsync(new ValidationException("Table with ID removed-table not found."));
        locations.Setup(service => service.GetLocationByTableIdAsync("active-table"))
            .ReturnsAsync(new Location
            {
                Id = "location", Address = "Test address", Description = "Test",
                AverageOccupancy = "0%",
            });
        var handler = new GetReservationsForUserHandler(reservations.Object, locations.Object,
            Mock.Of<IOrderService>(), Mock.Of<IGroupReservationRepository>());
        var context = Mock.Of<ILambdaContext>(value => value.Logger == Mock.Of<ILambdaLogger>());
        var request = new APIGatewayProxyRequest
        {
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new() { ["sub"] = "waiter", ["cognito:groups"] = "Waiter" },
                },
            },
        };

        var response = await handler.HandleRequest(request, context);

        Assert.Equal(expectedStatus, response.StatusCode);
        if (status == ReservationStatus.Cancelled)
        {
            using var body = JsonDocument.Parse(response.Body);
            var shown = Assert.Single(body.RootElement.EnumerateArray());
            Assert.Equal("current-reservation", shown.GetProperty("id").GetString());
        }
        else
            Assert.Contains("Error while fetching reservation details", response.Body);
        locations.Verify(service => service.GetLocationByTableIdAsync("removed-table"), Times.Once);
    }
}
