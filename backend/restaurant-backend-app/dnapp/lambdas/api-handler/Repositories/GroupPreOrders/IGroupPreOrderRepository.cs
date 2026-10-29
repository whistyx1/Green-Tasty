using APIHandler.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace APIHandler.Repositories.GroupPreOrders;
public interface IGroupPreOrderRepository
{
    Task<GuestSession> EnsureOwnerSessionAsync(string groupReservationId, string userId);
    Task SubmitAsync(Order order, GroupReservation group, Reservation reservation,
        List<OrderItem> dishes, IReadOnlyList<GuestSession> submittedSessions);
}
