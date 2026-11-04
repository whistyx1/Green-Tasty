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
using APIHandler.Services.Locations;
using APIHandler.Models.GuestSessions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace APIHandler.Services.GroupPreOrders;

public class GroupPreOrderService(
    IReservationRepository reservations, IGroupReservationRepository groups,
    IGuestSessionRepository sessions, IOrderRepository orders,
    IDishRepository dishes, IGroupPreOrderRepository persistence, ILocationService locations,
    TimeProvider? clock = null) : IGroupPreOrderService
{
    public async Task<string> GetOwnerSessionIdAsync(string userId, string reservationId)
        => (await GetOwnerSessionContextAsync(userId, reservationId)).GuestSessionId;

    public async Task<OwnerSessionContext> GetOwnerSessionContextAsync(string userId, string reservationId)
    {
        var reservation = await LoadOwnedReservationAsync(userId, reservationId);
        var group = await groups.GetByReservationIdAsync(reservation.Id)
            ?? throw new GroupReservationNotFoundException();
        var existing = (await sessions.GetAllByGroupReservationIdAsync(group.GroupReservationId))
            .Where(s => s.IsOwnerSession && s.OwnerUserId == userId)
            .OrderBy(s => s.Id, StringComparer.Ordinal).FirstOrDefault();
        if (existing != null) return new OwnerSessionContext(existing.Id, group.GroupReservationId);
        if (group.Status != GroupReservationStatus.Open || reservation.Status != ReservationStatus.Reserved)
            throw new GroupPreOrderConflictException("An owner session cannot be created for an inactive group.");
        var owner = await persistence.EnsureOwnerSessionAsync(group.GroupReservationId, userId);
        return new OwnerSessionContext(owner.Id, group.GroupReservationId);
    }

    public async Task<OwnerPreferenceState> SetOwnerPreferenceAsync(string userId, string reservationId, string preference, long expectedRevision)
    {
        if (!OrderingPreference.IsValid(preference)) throw new ValidationException("Choose Online or InRestaurant.");
        var (reservation, group, owner) = await LoadMutableOwnerAsync(userId, reservationId);
        if (owner.Revision != expectedRevision) throw new GroupPreOrderConflictException("Owner preference changed; reload the group.");
        if (owner.IsSubmitted) throw new GroupPreOrderConflictException("Your pre-order is already submitted.");
        var order = await orders.GetOrderByReservationIdAsync(reservationId);
        if (order != null && (order.CustomerId != userId || order.ReservationId != reservationId ||
            order.Status != OrderStatus.Created || order.OwnerGroupReady || order.GroupFinalized))
            throw new GroupPreOrderConflictException("Your order changed; reload before changing preference.");
        if (OrderingPreference.Effective(owner.OrderingPreference) != preference)
            await persistence.SetOwnerPreferenceAsync(owner, group, reservation, order, preference);
        var updated = await sessions.GetByGuestSessionId(owner.Id)
            ?? throw new GroupPreOrderConflictException("Owner session changed; reload the group.");
        return new OwnerPreferenceState(OrderingPreference.Effective(updated.OrderingPreference), updated.IsSubmitted, updated.Revision);
    }

    public async Task<OwnerPreferenceState> SubmitOwnerAsync(string userId, string reservationId, long expectedOrderRevision)
    {
        var (reservation, group, owner) = await LoadMutableOwnerAsync(userId, reservationId);
        if (owner.IsSubmitted || OrderingPreference.Effective(owner.OrderingPreference) != OrderingPreference.Online)
            throw new GroupPreOrderConflictException("Only an Online draft can be submitted.");
        if (owner.Dishes is { Count: > 0 })
            throw new GroupPreOrderConflictException("The owner has a legacy session cart; resolve it before submitting to avoid duplicate dishes.");
        var order = await orders.GetOrderByReservationIdAsync(reservationId)
            ?? throw new ValidationException("Create your pre-order before submitting it.");
        if (order.CustomerId != userId || order.ReservationId != reservationId)
            throw new ActionForbiddenException("Order does not belong to the reservation owner.");
        if (order.Revision != expectedOrderRevision)
            throw new GroupPreOrderConflictException("Owner pre-order changed; reload before submitting.");
        if (order.Status != OrderStatus.Created || order.GroupFinalized || order.OwnerGroupReady || order.Dishes is not { Count: > 0 } ||
            order.Dishes.Any(d => d.State != OrderItemState.Pending || string.IsNullOrWhiteSpace(d.DishId) || d.Quantity <= 0))
            throw new ValidationException("Only a nonempty, unchanged draft pre-order can be submitted.");
        var ids = order.Dishes.Select(d => d.DishId).Distinct().ToArray();
        var available = (await dishes.GetDishesByIdsAsync(ids)).ToDictionary(d => d.Id);
        if (ids.Any(id => !available.TryGetValue(id, out var dish) || dish.State != DishState.Available))
            throw new ValidationException("A dish is unavailable; reload your pre-order.");
        await persistence.SubmitOwnerAsync(owner, group, reservation, order);
        return new OwnerPreferenceState(OrderingPreference.Online, true, owner.Revision + 1);
    }

    private async Task<(Reservation Reservation, GroupReservation Group, GuestSession Owner)> LoadMutableOwnerAsync(string userId, string reservationId)
    {
        var reservation = await LoadOwnedReservationAsync(userId, reservationId);
        var group = await groups.GetByReservationIdAsync(reservationId) ?? throw new GroupReservationNotFoundException();
        if (group.Status != GroupReservationStatus.Open || reservation.Status != ReservationStatus.Reserved ||
            (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime >= reservation.StartTime.ToUniversalTime().AddMinutes(-30))
            throw new GroupPreOrderConflictException("The group pre-order is no longer open for changes.");
        var ownerContext = await GetOwnerSessionContextAsync(userId, reservationId);
        var owner = await sessions.GetByGuestSessionId(ownerContext.GuestSessionId)
            ?? throw new GroupPreOrderConflictException("Owner session changed; reload the group.");
        if (!owner.IsOwnerSession || owner.OwnerUserId != userId || owner.GroupReservationId != group.GroupReservationId)
            throw new ActionForbiddenException("Only the reservation owner can perform this action.");
        return (reservation, group, owner);
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
        var allSessions = await sessions.GetAllByGroupReservationIdConsistentAsync(groupReservationId);
        var ownerSessions = allSessions.Where(s => s.IsOwnerSession).ToArray();
        if (ownerSessions.Length != 1 || ownerSessions[0].OwnerUserId != userId)
            throw new GroupPreOrderConflictException("Owner session changed; reload the group before finalizing.");
        if (allSessions.Count > 97) throw new ValidationException("Too many participants for one group order.");
        var online = allSessions.Where(s => OrderingPreference.Effective(s.OrderingPreference) == OrderingPreference.Online).ToList();
        if (online.Count == 0)
        {
            if ((clock ?? TimeProvider.System).GetUtcNow().UtcDateTime >= reservation.EndTime.ToUniversalTime())
                throw new GroupPreOrderConflictException("The reservation has expired; the online pre-order cannot be closed.");
            var existingOrder = await orders.GetOrderByReservationIdAsync(reservation.Id);
            if (existingOrder != null && (existingOrder.CustomerId != userId || existingOrder.ReservationId != reservation.Id ||
                existingOrder.Status != OrderStatus.Created || existingOrder.GroupFinalized || existingOrder.OwnerGroupReady))
                throw new GroupPreOrderConflictException("The owner order changed; reload before closing the online pre-order.");
            await persistence.CloseWithoutOrderAsync(group, reservation, allSessions);
            return [];
        }
        if ((clock ?? TimeProvider.System).GetUtcNow().UtcDateTime >= reservation.StartTime.ToUniversalTime().AddMinutes(-30))
            throw new ValidationException("Pre-orders can only be modified up to 30 minutes before the reservation start time.");
        if (online.Any(s => !s.IsSubmitted))
            throw new GroupPreOrderConflictException("All Online participants must submit a nonempty pre-order before finalization.");
        if (online.Any(s => !s.IsOwnerSession && s.Dishes is not { Count: > 0 }))
            throw new ValidationException("Every Online guest must submit a nonempty pre-order.");
        var guestDishes = online.Where(s => !s.IsOwnerSession).SelectMany(s => s.Dishes ?? []).ToList();
        if (guestDishes.Any(d => string.IsNullOrWhiteSpace(d.DishId) || d.Quantity <= 0))
            throw new ValidationException("Submitted dishes must have a valid ID and positive quantity.");
        var ids = guestDishes.Select(d => d.DishId).Distinct().ToArray();
        var available = (await dishes.GetDishesByIdsAsync(ids)).ToDictionary(d => d.Id);
        if (ids.Any(id => !available.TryGetValue(id, out var dish) || dish.State != DishState.Available))
            throw new ValidationException("A submitted dish is missing or unavailable.");
        var owner = ownerSessions[0];
        var order = await orders.GetOrderByReservationIdAsync(reservation.Id);
        if (order is null)
        {
            if (owner.IsSubmitted)
                throw new GroupPreOrderConflictException("Owner pre-order changed; reload the group.");
            if (guestDishes.Count == 0) throw new ValidationException("Submitted sessions have no dishes.");
            // The owner may order at the restaurant while guests submit online. The
            // ordinary Orders record is still the one container for the final group order.
            var location = await locations.GetLocationByTableIdAsync(reservation.TableId)
                ?? throw new ValidationException("Location not found for the reservation's table.");
            order = new Order { OrderId = Guid.NewGuid().ToString(), CustomerId = userId,
                ReservationId = reservation.Id, ReservationAddress = location.Address,
                ReservationTime = $"{reservation.StartTime:MMM dd, yyyy, h:mm tt} - {reservation.EndTime:h:mm tt}",
                Dishes = [], Status = OrderStatus.Created };
            await orders.SaveOrderAsync(order);
        }
        if (order.Dishes == null) throw new ValidationException("The order has missing dishes; recreate the preorder before submitting.");
        if (order.CustomerId != userId || order.ReservationId != reservation.Id)
            throw new ActionForbiddenException("Order does not belong to the reservation owner.");
        if (order.Status != OrderStatus.Created || (order.Dishes ?? []).Any(d => d.State != OrderItemState.Pending))
            throw new GroupPreOrderConflictException("Only an unprocessed order can be consolidated.");
        var includeOwner = owner.IsSubmitted && order.OwnerGroupReady &&
            OrderingPreference.Effective(owner.OrderingPreference) == OrderingPreference.Online;
        if (online.Any(s => s.IsOwnerSession) != includeOwner)
            throw new GroupPreOrderConflictException("Owner pre-order changed; reload the group.");
        if (owner?.Dishes is { Count: > 0 })
            throw new GroupPreOrderConflictException("Owner session contains a legacy cart; resolve it before finalizing.");
        var ownerDishes = includeOwner ? order.Dishes! : new List<OrderItem>();
        if (includeOwner && ownerDishes.Count == 0)
            throw new ValidationException("The owner's submitted pre-order is empty.");
        if (includeOwner)
        {
            var ownerIds = ownerDishes.Select(d => d.DishId).Distinct().ToArray();
            var ownerAvailable = (await dishes.GetDishesByIdsAsync(ownerIds)).ToDictionary(d => d.Id);
            if (ownerIds.Any(id => !ownerAvailable.TryGetValue(id, out var dish) || dish.State != DishState.Available))
                throw new ValidationException("A submitted dish is missing or unavailable.");
        }
        if (ownerDishes.Concat(guestDishes).Any(d => string.IsNullOrWhiteSpace(d.DishId) || d.Quantity <= 0))
            throw new ValidationException("Submitted dishes must have a valid ID and positive quantity.");
        if (ownerDishes.Count + guestDishes.Count == 0)
            throw new ValidationException("Submitted sessions have no dishes.");
        List<OrderItem> consolidated;
        try
        {
            consolidated = ownerDishes.Concat(guestDishes)
                .GroupBy(d => d.DishId).OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new OrderItem { DishId = g.Key, Quantity = g.Sum(d => d.Quantity), State = OrderItemState.Pending }).ToList();
        }
        catch (OverflowException) { throw new ValidationException("Consolidated quantity is too large."); }
        await persistence.SubmitAsync(order, group, reservation, consolidated, allSessions);
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
