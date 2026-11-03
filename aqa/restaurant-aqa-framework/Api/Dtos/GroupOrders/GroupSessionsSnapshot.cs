namespace Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

public sealed record GroupSessionsSnapshot(IReadOnlyList<GroupParticipant> Participants, decimal Total);
public sealed record GroupParticipant(string Id, string Type, string Name, IReadOnlyList<GroupCartItem> Items, decimal Total, bool IsSubmitted);
public sealed record GroupCartItem(GroupDish Dish, int Quantity);
public sealed record GroupDish(string Id, string Name, decimal Price);
public sealed record GroupGuestSession(string SessionId, string SessionToken);
public sealed record GroupInvite(string GroupReservationId);
public sealed record ReservationInvite(string Token);
