using System.Collections.Generic;
using System.Threading.Tasks;
namespace APIHandler.Services.GroupPreOrders;
public record ConsolidatedDish(string DishId, int Quantity);
public interface IGroupPreOrderService
{
    Task<string> GetOwnerSessionIdAsync(string userId, string reservationId);
    Task<IReadOnlyList<ConsolidatedDish>> SubmitAsync(string userId, string groupReservationId, string reservationId);
}
