using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Helpers;
using APIHandler.Services.GuestSessions;
using System.Text.Json;
using System.Threading.Tasks;

namespace APIHandler.Handlers.GuestSessions;

public class SetGuestPreferenceHandler(IGuestSessionService service, IGuestAuthorizationService authorization) : IApiRequestHandler
{
    private record RequestBody(string Preference, long? ExpectedRevision);

    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (!request.TryGetPathValue("guestSessionId", out var id) || string.IsNullOrWhiteSpace(id))
            return ResponseBuilder.CreateErrorResponse(400, "Guest session is required.", context.Logger);
        if (await authorization.ValidateAsync(request, context, id) is { } denied) return denied;
        RequestBody? body;
        try { body = JsonSerializer.Deserialize<RequestBody>(request.Body ?? "", new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch (JsonException) { return ResponseBuilder.CreateErrorResponse(400, "Invalid request body.", context.Logger); }
        if (body?.ExpectedRevision is null or < 0)
            return ResponseBuilder.CreateErrorResponse(400, "Preference and expected revision are required.", context.Logger);
        try { return ResponseBuilder.CreateJsonResponse(200, await service.SetPreferenceAsync(id, body.Preference, body.ExpectedRevision.Value)); }
        catch (GuestSessionNotFoundException ex) { return ResponseBuilder.CreateErrorResponse(404, ex.Message, context.Logger); }
        catch (GroupPreOrderConflictException ex) { return ResponseBuilder.CreateErrorResponse(409, ex.Message, context.Logger); }
        catch (GuestSessionSubmittedException ex) { return ResponseBuilder.CreateErrorResponse(409, ex.Message, context.Logger); }
        catch (GroupReservationExpiredException ex) { return ResponseBuilder.CreateErrorResponse(409, ex.Message, context.Logger); }
        catch (GroupReservationSubmittedException ex) { return ResponseBuilder.CreateErrorResponse(409, ex.Message, context.Logger); }
        catch (ValidationException ex) { return ResponseBuilder.CreateErrorResponse(400, ex.Message, context.Logger); }
    }
}
