using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Api;

/// <summary>Our existing group routes. Reads do not mutate or create an Owner session.
/// Guest tokens are opaque Base64 values, not JWTs. Every POST is sent exactly once.</summary>
public sealed class GroupOrdersApi(IAPIRequestContext api) : ApiClientBase(api)
{
    public Task<IAPIResponse> GetParticipantsAsync(string groupId, string? token) =>
        Api.GetAsync(Url($"/guest/groups/{Uri.EscapeDataString(groupId)}/sessions"), new() { Headers = Headers(token) });

    public Task<IAPIResponse> GetInviteTokenAsync(string reservationId, string ownerToken) =>
        Api.GetAsync(Url($"/reservations/{Uri.EscapeDataString(reservationId)}/invite-token"), new() { Headers = Headers(ownerToken) });

    public Task<IAPIResponse> ResolveInviteAsync(string inviteToken) =>
        Api.GetAsync(Url($"/guest/invites/{Uri.EscapeDataString(inviteToken)}"));

    public Task<IAPIResponse> JoinAsync(string groupId, string guestName) =>
        Api.PostAsync(Url("/guest/sessions"), new() { DataString = Serialize(new { groupReservationId = groupId, guestName }), Headers = Headers() });

    public Task<IAPIResponse> AddDishAsync(string sessionId, string guestToken, string dishId, int quantity) =>
        Api.PostAsync(Url($"/guest/sessions/{Uri.EscapeDataString(sessionId)}/dishes"), new()
        {
            DataString = Serialize(new { dishId, quantity }),
            Headers = Headers(guestToken),
        });

    public Task<IAPIResponse> SubmitGuestAsync(string sessionId, string guestToken) =>
        Api.PostAsync(Url($"/guest/sessions/{Uri.EscapeDataString(sessionId)}/submit"), new() { Headers = Headers(guestToken) });

    public Task<IAPIResponse> SubmitGroupAsync(string groupId, string reservationId, string? ownerToken) =>
        Api.PostAsync(Url($"/group-reservations/{Uri.EscapeDataString(groupId)}/submit"), new()
        {
            DataString = Serialize(new { reservationId }),
            Headers = Headers(ownerToken),
        });

    public Task<IAPIResponse> GetGroupStatusAsync(string groupId, string ownerToken) =>
        Api.GetAsync(Url($"/guest/groups/{Uri.EscapeDataString(groupId)}/status"), new() { Headers = Headers(ownerToken) });

    public Task<IAPIResponse> RemoveGuestAsync(string groupId, string sessionId, string? token) =>
        Api.DeleteAsync(Url($"/guest/groups/{Uri.EscapeDataString(groupId)}/sessions/{Uri.EscapeDataString(sessionId)}"), new() { Headers = Headers(token) });

    public Task<IAPIResponse> RemoveGuestForCleanupAsync(string groupId, string sessionId, string ownerToken) =>
        RemoveGuestAsync(groupId, sessionId, ownerToken);

    public Task<IAPIResponse> GetProfileAsync(string token) =>
        Api.GetAsync(Url("/me"), new() { Headers = Headers(token) });

    public Task<IAPIResponse> GetWaiterLocationAsync(string waiterId, string token) =>
        Api.GetAsync(Url($"/waiters/{Uri.EscapeDataString(waiterId)}/location"), new() { Headers = Headers(token) });
    public Task<IAPIResponse> GetLocationTablesAsync(string locationId, string customerToken) =>
        Api.GetAsync(Url($"/locations/{Uri.EscapeDataString(locationId)}/tables/all"), new() { Headers = Headers(customerToken) });

    public Task<IAPIResponse> GetLocationDishesAsync(string locationId) =>
        Api.GetAsync(Url($"/locations/{Uri.EscapeDataString(locationId)}/dishes"));
    public Task<IAPIResponse> GetOwnedReservationForCleanupAsync(string reservationId, string ownerToken, float timeoutMs) =>
        Api.GetAsync(Url($"/reservations/{Uri.EscapeDataString(reservationId)}"), new() { Headers = Headers(ownerToken), Timeout = timeoutMs });

    public Task<IAPIResponse> DeleteOwnedTableOnceAsync(string tableId, string adminToken, float timeoutMs) =>
        Api.DeleteAsync(Url($"/tables/{Uri.EscapeDataString(tableId)}"), new() { Headers = Headers(adminToken), Timeout = timeoutMs });
}
