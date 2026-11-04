namespace APIHandler.Models.GuestSessions;

public static class OrderingPreference
{
    public const string Online = "Online";
    public const string InRestaurant = "InRestaurant";

    public static string Effective(string? value) => value ?? Online;

    public static bool IsValid(string? value) => value is Online or InRestaurant;
}
