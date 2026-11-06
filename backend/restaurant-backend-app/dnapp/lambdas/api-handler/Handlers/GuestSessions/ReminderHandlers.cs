using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Helpers;
using APIHandler.Models.GuestSessions;
using APIHandler.Services.GuestSessions;
using System;
using System.Threading.Tasks;

namespace APIHandler.Handlers.GuestSessions;

public class SendGuestReminderHandler(IReminderService service) : IApiRequestHandler
{
    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (request.RequestContext?.Authorizer?.Claims is null ||
            !request.RequestContext.Authorizer.Claims.TryGetValue("sub", out var ownerId) ||
            string.IsNullOrWhiteSpace(ownerId))
            return ResponseBuilder.CreateErrorResponse(401, "Authentication required.", context.Logger);
        if (!request.TryGetPathValue("groupReservationId", out var groupId) ||
            !request.TryGetPathValue("guestSessionId", out var sessionId))
            return ResponseBuilder.CreateErrorResponse(400, "Group and guest session are required.", context.Logger);

        try { return ResponseBuilder.CreateJsonResponse(200, await service.SendAsync(ownerId, groupId, sessionId)); }
        catch (ActionForbiddenException) { return ResponseBuilder.CreateErrorResponse(403, "Only the reservation owner can send reminders.", context.Logger); }
        catch (GroupReservationNotFoundException) { return ResponseBuilder.CreateErrorResponse(404, "Group was not found.", context.Logger); }
        catch (ReservationNotFoundException) { return ResponseBuilder.CreateErrorResponse(404, "Reservation was not found.", context.Logger); }
        catch (GuestSessionNotFoundException) { return ResponseBuilder.CreateErrorResponse(404, "Guest session was not found.", context.Logger); }
        catch (GroupPreOrderConflictException ex) { return ResponseBuilder.CreateErrorResponse(409, ex.Message, context.Logger); }
        catch (ReminderDeliveryException ex) { return ResponseBuilder.CreateErrorResponse(503, ex.Message, context.Logger); }
    }
}

public class SetGuestReminderEmailHandler(IReminderService service, IGuestAuthorizationService authorization) : IApiRequestHandler
{
    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (!request.TryGetPathValue("guestSessionId", out var sessionId))
            return ResponseBuilder.CreateErrorResponse(404, "Guest session was not found.", context.Logger);
        if (await authorization.ValidateAsync(request, context, sessionId) is { } error) return error;
        if (!RequestBodyHelper.TryParseAndValidate(request, context, out ReminderEmailRequest? body, out var parseError))
            return parseError!;
        try { return ResponseBuilder.CreateJsonResponse(200, await service.SetEmailAsync(sessionId, body!)); }
        catch (GuestSessionNotFoundException) { return ResponseBuilder.CreateErrorResponse(404, "Guest session was not found.", context.Logger); }
        catch (ValidationException ex) { return ResponseBuilder.CreateErrorResponse(400, ex.Message, context.Logger); }
        catch (ActionForbiddenException) { return ResponseBuilder.CreateErrorResponse(403, "This action is for invited guests only.", context.Logger); }
        catch (GroupPreOrderConflictException ex) { return ResponseBuilder.CreateErrorResponse(409, ex.Message, context.Logger); }
    }
}

public class RemoveGuestReminderEmailHandler(IReminderService service, IGuestAuthorizationService authorization) : IApiRequestHandler
{
    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (!request.TryGetPathValue("guestSessionId", out var sessionId))
            return ResponseBuilder.CreateErrorResponse(404, "Guest session was not found.", context.Logger);
        if (await authorization.ValidateAsync(request, context, sessionId) is { } error) return error;
        try { return ResponseBuilder.CreateJsonResponse(200, await service.RemoveEmailAsync(sessionId)); }
        catch (GuestSessionNotFoundException) { return ResponseBuilder.CreateErrorResponse(404, "Guest session was not found.", context.Logger); }
        catch (ActionForbiddenException) { return ResponseBuilder.CreateErrorResponse(403, "This action is for invited guests only.", context.Logger); }
        catch (GroupPreOrderConflictException ex) { return ResponseBuilder.CreateErrorResponse(409, ex.Message, context.Logger); }
    }
}

public class GetGuestReminderNotificationsHandler(IReminderService service, IGuestAuthorizationService authorization) : IApiRequestHandler
{
    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (!request.TryGetPathValue("guestSessionId", out var sessionId))
            return ResponseBuilder.CreateErrorResponse(404, "Guest session was not found.", context.Logger);
        if (await authorization.ValidateAsync(request, context, sessionId) is { } error) return error;
        try { return ResponseBuilder.CreateJsonResponse(200, await service.GetNotificationsAsync(sessionId)); }
        catch (GuestSessionNotFoundException) { return ResponseBuilder.CreateErrorResponse(404, "Guest session was not found.", context.Logger); }
        catch (ActionForbiddenException) { return ResponseBuilder.CreateErrorResponse(403, "This action is for invited guests only.", context.Logger); }
    }
}
