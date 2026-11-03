namespace Restaurant_Aqa_Framework.Support.GroupOrders;

public enum GroupOverviewStage { Empty, InitialGuests, DraftChanged, SecondGuestSubmitted }
public sealed record GroupDishView(string Name, int Quantity, decimal Amount);
public sealed record GroupParticipantView(string Heading, string Status, decimal Subtotal, IReadOnlyList<GroupDishView> Dishes);
public sealed record GroupOverviewSnapshot(IReadOnlyList<GroupParticipantView> Participants, decimal GrandTotal, IReadOnlyList<GroupDishView> Consolidated);
