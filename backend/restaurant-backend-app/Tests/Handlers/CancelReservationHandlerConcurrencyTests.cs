using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Handlers.Reservations;
using APIHandler.Services.Reservations;
using APIHandler.Services.Waiters;
using Moq;

namespace APIHandler.Tests.Handlers;

public class CancelReservationHandlerConcurrencyTests
{
    [Fact]
    public async Task ConcurrentSubmissionConflict_DoesNotReportReservationCancellationAsSuccessful()
    {
        var reservation = new Reservation { Id = "reservation", CustomerId = "owner",
            WaiterId = "waiter", TableId = "table", StartTime = DateTime.UtcNow.AddHours(2),
            Status = ReservationStatus.Reserved };
        var reservations = new Mock<IReservationService>();
        reservations.Setup(service => service.GetReservationByIdAsync(reservation.Id)).ReturnsAsync(reservation);
        reservations.Setup(service => service.UpdateReservationAsync(reservation)).ReturnsAsync(false);
        var waiters = new Mock<IWaiterService>();
        var context = new Mock<ILambdaContext>();
        context.Setup(item => item.Logger).Returns(Mock.Of<ILambdaLogger>());
        var request = new APIGatewayProxyRequest {
            Body = "{\"id\":\"reservation\"}",
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext {
                Authorizer = new APIGatewayCustomAuthorizerContext {
                    Claims = new() { ["sub"] = "owner", ["cognito:groups"] = "Customer" }
                }
            }
        };

        var response = await new CancelReservationHandler(waiters.Object, reservations.Object)
            .HandleRequest(request, context.Object);

        Assert.Equal(503, response.StatusCode);
        waiters.Verify(service => service.FindWaiterByReservationIdAsync(It.IsAny<string>()), Times.Never);
    }
}
