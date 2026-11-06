using APIHandler.Models.Orders;
using System.Collections.Generic;

namespace APIHandler.Models.GuestSessions;

public record GetGuestSessionResponse(
    string Name,
    IEnumerable<OrderItemResponse> Items,
    decimal Total,
    bool IsSubmitted,
    string OrderingPreference = OrderingPreference.Online,
    long Revision = 0,
    string? Email = null,
    bool EmailConsent = false);
