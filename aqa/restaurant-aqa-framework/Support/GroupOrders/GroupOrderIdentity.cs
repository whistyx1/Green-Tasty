namespace Restaurant_Aqa_Framework.Support.GroupOrders;

/// <summary>Credentials remain scenario-local and must never be rendered into diagnostics.</summary>
public sealed class GroupOrderIdentity(string email, string password, string idToken, string userId, string displayName)
{
    public string Email { get; } = email;
    public string Password { get; } = password;
    public string IdToken { get; } = idToken;
    public string UserId { get; } = userId;
    public string DisplayName { get; } = displayName;
    public override string ToString() => "Configured test identity (credentials omitted)";
}
