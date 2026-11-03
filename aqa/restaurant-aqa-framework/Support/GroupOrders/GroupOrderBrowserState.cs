using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Support.GroupOrders;

/// <summary>Browser sessions are scenario-local. Invitation and browser URLs never enter diagnostics.</summary>
public sealed class GroupOrderBrowserState(IPage ownerPage, string invitationUrl) : IAsyncDisposable
{
    private readonly Dictionary<string, IBrowserContext> _contexts = [];
    private readonly Dictionary<string, IPage> _pages = [];
    public IPage OwnerPage { get; } = ownerPage;
    public string InvitationUrl { get; } = invitationUrl;
    public IReadOnlyDictionary<string, IPage> GuestPages => _pages;
    public IPage? WaiterPage { get; private set; }

    public void AddGuest(string name, IBrowserContext context, IPage page)
    {
        _contexts.Add(name, context);
        _pages.Add(name, page);
    }

    public void AddWaiter(IBrowserContext context, IPage page)
    {
        _contexts.Add("waiter", context);
        WaiterPage = page;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var context in _contexts.Values)
            await context.CloseAsync();
        _contexts.Clear();
        _pages.Clear();
    }

    public override string ToString() => "Group browser sessions (private URLs omitted)";
}
