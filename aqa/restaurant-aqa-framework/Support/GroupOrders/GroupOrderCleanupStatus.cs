namespace Restaurant_Aqa_Framework.Support.GroupOrders;

/// <summary>Per-scenario cleanup result, with no response bodies or credentials retained.</summary>
public sealed class GroupOrderCleanupStatus
{
    public int FailureCount { get; private set; }

    public void RecordFailure() => FailureCount++;
}
