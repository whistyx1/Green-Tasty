using APIHandler.Constants;
using APIHandler.Constants.Dishes;
using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Repositories.Dishes;
using APIHandler.Repositories.GroupPreOrders;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace APIHandler.Services.GroupPreOrders;

public class GroupPreOrderService(
    IReservationRepository reservations, IGroupReservationRepository groups,
    IGuestSessionRepository sessions, IOrderRepository orders,
    IDishRepository dishes, IGroupPreOrderRepository persistence, TimeProvider? clock = null) : IGroupPreOrderService
{
    public async Task<string> GetOwnerSessionIdAsync(string userId, string reservationId)
    {
        var reservation = await LoadOwnedReservationAsync(userId, reservationId);
        var group = await groups.GetByReservationIdAsync(reservation.Id)
            ?? throw new GroupReservationNotFoundException();
        var existing = (await sessions.GetAllByGroupReservationIdAsync(group.GroupReservationId))
            .Where(s => s.IsOwnerSession && s.OwnerUserId == userId)
            .OrderBy(s => s.Id, StringComparer.Ordinal).FirstOrDefault();
        if (existing != null) return existing.Id;
        if (group.Status != GroupReservationStatus.Open || reservation.Status != ReservationStatus.Reserved)
            throw new GroupPreOrderConflictException("An owner session cannot be created for an inactive group.");
        return (await persistence.EnsureOwnerSessionAsync(group.GroupReservationId, userId)).Id;
    }

    public async Task<IReadOnlyList<ConsolidatedDish>> SubmitAsync(string userId, string groupReservationId, string reservationId)
    {
        var group = await groups.GetGroupReservationByIdAsync(groupReservationId)
            ?? throw new GroupReservationNotFoundException();
        var reservation = await LoadOwnedReservationAsync(userId, group.ReservationId);
        if (reservationId != group.ReservationId)
            throw new ValidationException("Reservation does not belong to this group.");
        if (group.Status != GroupReservationStatus.Open || reservation.Status != ReservationStatus.Reserved)
            throw new GroupPreOrderConflictException("Only an open group with an active reservation can be submitted.");
        if ((clock ?? TimeProvider.System).GetUtcNow().UtcDateTime >= reservation.StartTime.ToUniversalTime().AddMinutes(-30))
            throw new ValidationException("Pre-orders can only be modified up to 30 minutes before the reservation start time.");
        var submitted = (await sessions.GetAllByGroupReservationIdAsync(groupReservationId))
            .Where(s => s.IsSubmitted).ToList();
        if (submitted.Count == 0) throw new ValidationException("No submitted sessions.");
        if (submitted.Any(s => s.Dishes == null)) throw new ValidationException("A submitted cart has missing dishes; reload the cart before submitting.");
        var guestDishes = submitted.SelectMany(s => s.Dishes ?? []).ToList();
        if (guestDishes.Count == 0) throw new ValidationException("Submitted sessions have no dishes.");
        if (submitted.Count > 97) throw new ValidationException("Too many submitted sessions for one group order.");
        var order = await orders.GetOrderByReservationIdAsync(reservation.Id)
            ?? throw new ValidationException("No order found for this reservation.");
        if (order.Dishes == null) throw new ValidationException("The order has missing dishes; recreate the preorder before submitting.");
        if (order.CustomerId != userId || order.ReservationId != reservation.Id)
            throw new ActionForbiddenException("Order does not belong to the reservation owner.");
        if (order.Status != OrderStatus.Created || (order.Dishes ?? []).Any(d => d.State != OrderItemState.Pending))
            throw new GroupPreOrderConflictException("Only an unprocessed order can be consolidated.");
        if ((order.Dishes ?? []).Concat(guestDishes).Any(d => string.IsNullOrWhiteSpace(d.DishId) || d.Quantity <= 0))
            throw new ValidationException("Submitted dishes must have a valid ID and positive quantity.");
        var ids = guestDishes.Select(d => d.DishId).Distinct().ToArray();
        var available = (await dishes.GetDishesByIdsAsync(ids)).ToDictionary(d => d.Id);
        if (ids.Any(id => !available.TryGetValue(id, out var dish) || dish.State != DishState.Available))
            throw new ValidationException("A submitted dish is missing or unavailable.");
        List<OrderItem> consolidated;
        try
        {
            consolidated = (order.Dishes ?? []).Concat(guestDishes)
                .GroupBy(d => d.DishId).OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new OrderItem { DishId = g.Key, Quantity = g.Sum(d => d.Quantity), State = OrderItemState.Pending }).ToList();
        }
        catch (OverflowException) { throw new ValidationException("Consolidated quantity is too large."); }
        await persistence.SubmitAsync(order, group, reservation, consolidated, submitted);
        return consolidated.Select(d => new ConsolidatedDish(d.DishId, d.Quantity)).ToArray();
    }

    private async Task<Reservation> LoadOwnedReservationAsync(string userId, string reservationId)
    {
        var reservation = await reservations.GetReservationByIdAsync(reservationId)
            ?? throw new ReservationNotFoundException("Reservation not found.");
        if (string.IsNullOrWhiteSpace(userId) || reservation.CustomerId != userId)
            throw new ActionForbiddenException("Only the reservation owner can perform this action.");
        return reservation;
    }
}
