namespace Restaurant_Aqa_Framework.Support.GroupOrders;

public sealed class GroupOrderGuest(string sessionId, string sessionToken, string name)
{
    public string SessionId { get; } = sessionId;
    public string SessionToken { get; } = sessionToken;
    public string Name { get; } = name;
    public override string ToString() => Name;
}
