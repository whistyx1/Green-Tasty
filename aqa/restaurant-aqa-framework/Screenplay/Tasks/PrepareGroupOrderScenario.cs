using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Auth;
using Restaurant_Aqa_Framework.Api.Dtos.Dishes;
using Restaurant_Aqa_Framework.Api.Dtos.Locations;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;
using Restaurant_Aqa_Framework.Api.Dtos.Reservations;
using Restaurant_Aqa_Framework.Api.Dtos.Tables;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Configuration;
using Restaurant_Aqa_Framework.Support.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>Own new data in the existing Waiter's location; never reassigns a standing Waiter.
/// UI scenarios pass false and create the actual reservation through the browser.</summary>
public sealed class PrepareGroupOrderScenario(bool createReservation = true) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var api = actor.UsingTheApi();
        var settings = Settings.Current;
        var accounts = new GroupOrderSettings();
        var owner = await SignInAsync(api, accounts.OwnerEmail, accounts.OwnerPassword, "Customer");
        var otherCustomer = await SignInAsync(api, accounts.OtherCustomerEmail, accounts.OtherCustomerPassword, "Customer");
        if (owner.UserId == otherCustomer.UserId)
            throw new InvalidOperationException("OWNER and OTHER_CUSTOMER must be distinct test accounts.");
        var (waiterEmail, waiterPassword) = settings.RequireWaiter();
        var waiter = await SignInAsync(api, waiterEmail, waiterPassword, "Waiter");
        if (string.IsNullOrWhiteSpace(settings.AdminUsername) || string.IsNullOrWhiteSpace(settings.AdminPassword))
            throw new InvalidOperationException("Set ADMIN_USERNAME/ADMIN_PASSWORD to create isolated AQA tables and dishes.");
        var admin = await SignInAsync(api, settings.AdminUsername, settings.AdminPassword, "Admin");
        var waiterLocation = await GroupOrderScenario.ReadRequiredAsync<WaiterLocation>(
            await new GroupOrdersApi(api).GetWaiterLocationAsync(waiter.UserId, waiter.IdToken), "Read standing Waiter location");
        var location = await GroupOrderScenario.ReadRequiredAsync<LocationResponse>(
            await new LocationsApi(api).GetLocationAsync(waiterLocation.LocationId), "Read standing test location");
        var cleanup = actor.Recall<CleanupScope>();
        var tableApi = new TablesApi(api);
        var table = await GroupOrderScenario.ReadRequiredAsync<TableResponse>(await tableApi.CreateTableAsync(
            new CreateTableRequest(location.Id, 4, RandomNumberGenerator.GetInt32(100_000, 1_000_000)), admin.IdToken), "Create owned test table");
        GroupOrderScenario? fixture = null;
        cleanup.TrackDelete("level 1 owned table", () => fixture is null
            ? tableApi.DeleteTableAsync(table.Id, waiter.IdToken)
            : fixture.DeleteOwnedTableAsync(api, waiter.IdToken));
        var dish = await CreateDishAsync(api, cleanup, admin.IdToken, location.Id, "Salad", 8.25m);
        var secondDish = await CreateDishAsync(api, cleanup, admin.IdToken, location.Id, "Pasta", 12.40m);
        fixture = new GroupOrderScenario(owner, otherCustomer, waiter, location, table, dish, secondDish, cleanup);
        actor.Remember(fixture);
        await WaitForOwnedSeedVisibilityAsync(api, fixture);
        if (createReservation)
        {
            var reservation = await GroupOrderScenario.ReadRequiredAsync<ReservationResponse>(await new ReservationsApi(api).CreateReservationAsync(
                new CreateReservationRequest(table.Id, fixture.StartTime, fixture.EndTime, 4), owner.IdToken), "Create owned test reservation");
            await fixture.RecordReservationAsync(api, reservation.Id);
        }
    }

    private static async Task WaitForOwnedSeedVisibilityAsync(IAPIRequestContext api, GroupOrderScenario fixture)
    {
        var groups = new GroupOrdersApi(api);
        var elapsed = Stopwatch.StartNew();
        using var polling = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
        while (true)
        {
            var tables = await GroupOrderScenario.ReadRequiredAsync<IReadOnlyList<TableResponse>>(
                await groups.GetLocationTablesAsync(fixture.Location.Id, fixture.Waiter.IdToken), "Read owned table visibility");
            var dishes = await GroupOrderScenario.ReadRequiredAsync<IReadOnlyList<GroupDish>>(
                await groups.GetLocationDishesAsync(fixture.Location.Id), "Read owned menu visibility");
            if (tables.Any(table => table.Id == fixture.Table.Id)
                && dishes.Any(dish => dish.Id == fixture.Dish.Id)
                && dishes.Any(dish => dish.Id == fixture.SecondDish.Id))
                return;
            if (elapsed.ElapsedMilliseconds >= Settings.Current.DefaultTimeoutMs)
                throw new InvalidOperationException("Owned test table and dishes did not become visible within the setup deadline.");
            await polling.WaitForNextTickAsync();
        }
    }

    private static async Task<GroupOrderIdentity> SignInAsync(IAPIRequestContext api, string email, string password, string role)
    {
        var login = await GroupOrderScenario.ReadRequiredAsync<SignInResponse>(
            await new AuthApi(api).SignInAsync(new SignInRequest(email, password)), "Sign in configured test account");
        var profile = await GroupOrderScenario.ReadRequiredAsync<AccountProfile>(
            await new GroupOrdersApi(api).GetProfileAsync(login.IdToken), "Read configured test account role");
        if (!string.Equals(profile.Role, role, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Configured account must have role {role}; account details omitted.");
        return new GroupOrderIdentity(email, password, login.IdToken, profile.Id, $"{profile.FirstName} {profile.LastName}");
    }

    private static async Task<DishResponse> CreateDishAsync(IAPIRequestContext api, CleanupScope cleanup, string adminToken, string locationId, string label, decimal price)
    {
        var dishes = new DishesApi(api);
        var request = new CreateDishRequest($"AQA-Sample-{Guid.NewGuid():N}-{label}", "Isolated level 1 test dish", DishType.MainCourse,
            price, "250 g", "200 kcal", "10 g", "5 g", "20 g", "A", [locationId], DishState.Available);
        var dish = await GroupOrderScenario.ReadRequiredAsync<DishResponse>(await dishes.CreateDishAsync(request, adminToken), "Create owned test dish");
        cleanup.TrackDelete("level 1 owned dish", () => dishes.DeleteDishAsync(dish.Id, force: true, bearerToken: adminToken));
        return dish;
    }

    private sealed record AccountProfile(string Id, string FirstName, string LastName, string Role);
    private sealed record WaiterLocation(string LocationId);
}
