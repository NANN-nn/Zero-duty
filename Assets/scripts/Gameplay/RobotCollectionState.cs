using System.Collections.Generic;

/// <summary>One count per scene robot, capped at the five-robot collection.</summary>
public sealed class RobotCollectionState
{
    private readonly HashSet<int> collected = new HashSet<int>();
    public const int Goal = 5;
    public int Count => collected.Count;
    public bool Complete => Count == Goal;

    public bool TryCollect(int instanceId)
    {
        if (instanceId == 0 || Complete) return false;
        return collected.Add(instanceId);
    }
}
