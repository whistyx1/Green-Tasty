using System.Diagnostics;
using System.Text.Json;
using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Dishes;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;
using Restaurant_Aqa_Framework.Api.Dtos.Locations;
using Restaurant_Aqa_Framework.Api.Dtos.Reservations;
using Restaurant_Aqa_Framework.Api.Dtos.Tables;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Configuration;

namespace Restaurant_Aqa_Framework.Support.GroupOrders;

/// <summary>One scenario owns its table, dishes, reservation and guest sessions. The existing
/// location metadata and standing accounts are preserved; owned dish links are added and removed.
/// Cancellation retains history where no hard-delete API exists.</summary>
public sealed class GroupOrderScenario(
    GroupOrderIdentity owner, GroupOrderIdentity otherCustomer, GroupOrderIdentity waiter,
    LocationResponse location, TableResponse table, DishResponse dish, DishResponse secondDish,
    CleanupScope cleanup)
{
    public GroupOrderIdentity Owner { get; } = owner;
    public GroupOrderIdentity OtherCustomer { get; } = otherCustomer;
    public GroupOrderIdentity Waiter { get; } = waiter;
    public LocationResponse Location { get; } = location;
    public TableResponse Table { get; } = table;
    public DishResponse Dish { get; } = dish;
    public DishResponse SecondDish { get; } = secondDish;
    public DateTime StartTime { get; } = DateTime.UtcNow.Date.AddDays(1).AddHours(13);
    public DateTime EndTime => StartTime.AddMinutes(90);
    public string FirstGuestName { get; } = $"AQA First {Guid.NewGuid():N}";
    public string SecondGuestName { get; } = $"AQA Draft {Guid.NewGuid():N}";
    public string EmptyGuestName { get; } = $"AQA Empty {Guid.NewGuid():N}";
    public string ReservationId { get; private set; } = "";
    public string GroupId { get; private set; } = "";
    public string InviteToken { get; private set; } = "";
    public List<GroupOrderGuest> Guests { get; } = [];
    private bool _cancellationConfirmed;
    private bool _submitted;
    private readonly HashSet<string> _removedGuestIds = [];

    public void MarkSubmitted() => _submitted = true;
    public void MarkGuestRemoved(string sessionId)
    {
        if (!Guests.Any(guest => guest.SessionId == sessionId))
            throw new InvalidOperationException("Only a tracked guest may be marked removed.");
        _removedGuestIds.Add(sessionId);
    }

    public async Task RecordReservationAsync(IAPIRequestContext api, string reservationId)
    {
        if (!string.IsNullOrEmpty(ReservationId))
            throw new InvalidOperationException("A scenario cannot replace its owned reservation.");
        ReservationId = reservationId;
        cleanup.TrackDelete("level 1 owned reservation cancellation", () => CancelOwnedReservationAsync(api));
        var groups = new GroupOrdersApi(api);
        var invite = await ReadOwnedInvitationAsync(groups, reservationId);
        InviteToken = invite.Token;
        GroupId = (await ReadRequiredAsync<GroupInvite>(await groups.ResolveInviteAsync(InviteToken), "Resolve owned invitation")).GroupReservationId;
    }

    private async Task<IAPIResponse> CancelOwnedReservationAsync(IAPIRequestContext api)
    {
        // Cancellation is the supported cleanup API; the reservation/group history remains
        // because the application deliberately exposes no hard-delete operation for it.
        var cancellation = await new ReservationsApi(api).CancelReservationAsync(ReservationId, Owner.IdToken);
        if (!cancellation.Ok)
            return cancellation;
        var elapsed = Stopwatch.StartNew();
        using var polling = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
        while (true)
        {
            var remainingMs = 5_000 - elapsed.ElapsedMilliseconds;
            if (remainingMs <= 0)
                throw new InvalidOperationException("Owned reservation cancellation was not visible before the cleanup deadline.");
            var state = await ReadRequiredAsync<ReservationCleanupState>(
                await new GroupOrdersApi(api).GetOwnedReservationForCleanupAsync(ReservationId, Owner.IdToken, remainingMs),
                "Confirm owned reservation cancellation");
            if (state.Status == ReservationStatus.Cancelled)
            {
                _cancellationConfirmed = true;
                return cancellation;
            }
            if (state.Status != ReservationStatus.Reserved)
                throw new InvalidOperationException("Owned reservation has an unexpected state after cancellation.");
            await polling.WaitForNextTickAsync();
        }
    }

    public async Task<IAPIResponse> DeleteOwnedTableAsync(IAPIRequestContext api, string adminToken)
    {
        // Only this scenario's new table is eligible. After confirmed cancellation, the
        // table-reservation GSI can briefly retain Reserved. Retry only that precise 400;
        // every other failure is returned immediately and strict cleanup reports it.
        var elapsed = Stopwatch.StartNew();
        using var polling = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
        while (true)
        {
            var remainingMs = 5_000 - elapsed.ElapsedMilliseconds;
            if (remainingMs <= 0)
                throw new InvalidOperationException("Owned table remained blocked by stale reservation state at the cleanup deadline.");
            var response = await new GroupOrdersApi(api).DeleteOwnedTableOnceAsync(Table.Id, adminToken, remainingMs);
            if (response.Status != 400 || !_cancellationConfirmed)
                return response;
            var body = await response.TextAsync();
            if (!body.Contains("Cannot delete table", StringComparison.Ordinal)
                || !body.Contains("active upcoming reservations", StringComparison.Ordinal))
                return response;
            Console.WriteLine("[Level1Cleanup] Owned table reservation index has not caught up with confirmed cancellation; retrying cleanup only.");
            await polling.WaitForNextTickAsync();
        }
    }

    private sealed record ReservationCleanupState(ReservationStatus Status);

    private async Task<ReservationInvite> ReadOwnedInvitationAsync(GroupOrdersApi groups, string reservationId)
    {
        // The existing backend dereferences its reservation-id GSI lookup immediately after a
        // successful reservation POST. Only this owned setup read may retry transient 5xx.
        // Actual assertions and permission/missing-group requests never use this helper.
        var elapsed = Stopwatch.StartNew();
        using var polling = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
        while (true)
        {
            var response = await groups.GetInviteTokenAsync(reservationId, Owner.IdToken);
            if (response.Ok || response.Status < 500 || elapsed.ElapsedMilliseconds >= Settings.Current.DefaultTimeoutMs)
                return await ReadRequiredAsync<ReservationInvite>(response, "Read owned invitation");
            Console.WriteLine($"[Level1Setup] Owned invitation index not ready (HTTP {response.Status}); retrying read only.");
            await polling.WaitForNextTickAsync();
        }
    }

    public GroupOrderGuest TrackGuest(IAPIRequestContext api, string sessionId, string sessionToken, string name)
    {
        var guest = new GroupOrderGuest(sessionId, sessionToken, name);
        Guests.Add(guest);
        cleanup.TrackAction("owned guest removal", async () =>
        {
            // A submitted group locks guest removal. Its cancelled reservation and group
            // history remain where the application has no hard-delete route.
            if (_submitted || _removedGuestIds.Contains(guest.SessionId)) return;
            var response = await new GroupOrdersApi(api).RemoveGuestForCleanupAsync(GroupId, guest.SessionId, Owner.IdToken);
            await RequireSuccessAsync(response, "Remove owned guest during cleanup");
        });
        return guest;
    }

    public async Task<GroupOrderGuest> AddGuestAsync(IAPIRequestContext api, string name)
    {
        var session = await ReadRequiredAsync<GroupGuestSession>(await new GroupOrdersApi(api).JoinAsync(GroupId, name), "Create owned guest session");
        return TrackGuest(api, session.SessionId, session.SessionToken, name);
    }

    public async Task AddDishAsync(IAPIRequestContext api, GroupOrderGuest guest, string dishId, int quantity)
    {
        EnsureOwnedGuest(guest);
        if (dishId != Dish.Id && dishId != SecondDish.Id)
            throw new InvalidOperationException("A scenario may add only its own seeded dishes.");
        await RequireSuccessAsync(await new GroupOrdersApi(api).AddDishAsync(guest.SessionId, guest.SessionToken, dishId, quantity), "Add guest dish");
    }

    public async Task SubmitGuestAsync(IAPIRequestContext api, GroupOrderGuest guest)
    {
        EnsureOwnedGuest(guest);
        await RequireSuccessAsync(await new GroupOrdersApi(api).SubmitGuestAsync(guest.SessionId, guest.SessionToken), "Submit guest cart");
    }

    private void EnsureOwnedGuest(GroupOrderGuest guest)
    {
        if (!Guests.Contains(guest))
            throw new InvalidOperationException("A scenario may mutate only a guest session that it created and tracks for cleanup.");
    }

    public static Task RequireSuccessAsync(IAPIResponse response, string operation)
    {
        if (!response.Ok)
            throw new InvalidOperationException($"{operation} failed with HTTP {response.Status}; credentials and response body omitted.");
        return Task.CompletedTask;
    }

    public static async Task<T> ReadRequiredAsync<T>(IAPIResponse response, string operation)
    {
        await RequireSuccessAsync(response, operation);
        try
        {
            return JsonSerializer.Deserialize<T>(await response.TextAsync(), JsonDefaults.Options)
                ?? throw new InvalidOperationException($"{operation} returned no data.");
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"{operation} returned an invalid response; body omitted.");
        }
    }
}
