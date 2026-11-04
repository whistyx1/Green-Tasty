using APIHandler.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace APIHandler.Repositories.GroupPreOrders;
public interface IGroupPreOrderRepository
{
    Task<GuestSession> EnsureOwnerSessionAsync(string groupReservationId, string userId);
    Task SetOwnerPreferenceAsync(GuestSession owner, GroupReservation group, Reservation reservation,
        Order? order, string preference);
    Task SubmitOwnerAsync(GuestSession owner, GroupReservation group, Reservation reservation, Order order);
    Task SubmitAsync(Order order, GroupReservation group, Reservation reservation,
        List<OrderItem> dishes, IReadOnlyList<GuestSession> submittedSessions);
}
