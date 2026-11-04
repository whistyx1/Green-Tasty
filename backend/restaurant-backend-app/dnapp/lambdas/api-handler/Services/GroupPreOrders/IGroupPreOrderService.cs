using System.Collections.Generic;
using System.Threading.Tasks;
namespace APIHandler.Services.GroupPreOrders;
public record ConsolidatedDish(string DishId, int Quantity);
public record OwnerSessionContext(string GuestSessionId, string GroupReservationId);
public record OwnerPreferenceState(string OrderingPreference, bool IsSubmitted, long Revision);
public interface IGroupPreOrderService
{
    Task<string> GetOwnerSessionIdAsync(string userId, string reservationId);
    Task<OwnerSessionContext> GetOwnerSessionContextAsync(string userId, string reservationId);
    Task<OwnerPreferenceState> SetOwnerPreferenceAsync(string userId, string reservationId, string preference, long expectedRevision);
    Task<OwnerPreferenceState> SubmitOwnerAsync(string userId, string reservationId, long expectedOrderRevision);
    Task<IReadOnlyList<ConsolidatedDish>> SubmitAsync(string userId, string groupReservationId, string reservationId);
}
