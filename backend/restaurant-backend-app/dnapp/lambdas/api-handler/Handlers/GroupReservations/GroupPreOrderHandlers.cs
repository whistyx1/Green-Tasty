using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Helpers;
using APIHandler.Services.GroupPreOrders;
using System;
using System.Text.Json;
using System.Threading.Tasks;
namespace APIHandler.Handlers.GroupReservations;

public class GetOwnerSessionIdHandler(IGroupPreOrderService service) : IApiRequestHandler
{
    public Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
        => GroupPreOrderHandler.HandleAsync(request, context, "reservationId", async (userId, reservationId) =>
            new { GuestSessionId = await service.GetOwnerSessionIdAsync(userId, reservationId) });
}

public class SubmitGroupPreOrderHandler(IGroupPreOrderService service) : IApiRequestHandler
{
    private record SubmitRequest(string ReservationId);
    public Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
        => GroupPreOrderHandler.HandleAsync(request, context, "groupReservationId", async (userId, groupId) => {
            SubmitRequest? body;
            try { body = JsonSerializer.Deserialize<SubmitRequest>(request.Body ?? "", new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch (JsonException) { throw new ValidationException("Invalid request body."); }
            if (string.IsNullOrWhiteSpace(body?.ReservationId)) throw new ValidationException("ReservationId is required.");
            return await service.SubmitAsync(userId, groupId, body.ReservationId);
        });
}

internal static class GroupPreOrderHandler
{
    public static async Task<APIGatewayProxyResponse> HandleAsync(APIGatewayProxyRequest request, ILambdaContext context,
        string pathKey, Func<string, string, Task<object>> action)
    {
        if (request.RequestContext?.Authorizer?.Claims == null ||
            !request.RequestContext.Authorizer.Claims.TryGetValue("sub", out var userId) || string.IsNullOrWhiteSpace(userId))
            return ResponseBuilder.CreateErrorResponse(401, "Unauthorized.", context.Logger);
        if (!request.TryGetPathValue(pathKey, out var id) || string.IsNullOrWhiteSpace(id))
            return ResponseBuilder.CreateErrorResponse(400, "Missing '" + pathKey + "' in path parameters.", context.Logger);
        try { return ResponseBuilder.CreateJsonResponse(200, await action(userId, id)); }
        catch (ReservationNotFoundException ex) { return ResponseBuilder.CreateErrorResponse(404, ex.Message, context.Logger); }
        catch (GroupReservationNotFoundException ex) { return ResponseBuilder.CreateErrorResponse(404, ex.Message, context.Logger); }
        catch (ActionForbiddenException ex) { return ResponseBuilder.CreateErrorResponse(403, ex.Message, context.Logger); }
        catch (GroupPreOrderConflictException ex) { return ResponseBuilder.CreateErrorResponse(409, ex.Message, context.Logger); }
        catch (ValidationException ex) { return ResponseBuilder.CreateErrorResponse(400, ex.Message, context.Logger); }
    }
}
