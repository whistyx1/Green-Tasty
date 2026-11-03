using dotenv.net.Utilities;

namespace Restaurant_Aqa_Framework.Configuration;

/// <summary>Standing test accounts: no registration or mutation of existing waiter assignments.</summary>
public sealed class GroupOrderSettings
{
    public string OwnerEmail { get; } = Required("OWNER_USERNAME");
    public string OwnerPassword { get; } = Required("OWNER_PASSWORD");
    public string OtherCustomerEmail { get; } = Required("OTHER_CUSTOMER_USERNAME");
    public string OtherCustomerPassword { get; } = Required("OTHER_CUSTOMER_PASSWORD");

    public GroupOrderSettings() => EnvConfig.EnsureLoaded();

    private static string Required(string name)
    {
        EnvConfig.EnsureLoaded();
        return EnvReader.TryGetStringValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value : throw new InvalidOperationException($"Set {name} in the private AQA .env or CI secrets before running level 1.");
    }
}
