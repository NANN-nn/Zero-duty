using System;

public static class RobotCollectionStateTests
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); }
    public static void Main()
    {
        var state = new RobotCollectionState();
        Check(state.Count == 0 && !state.Complete, "A new shift begins empty.");
        Check(!state.TryCollect(0), "Null instance is not a collectible.");
        Check(state.TryCollect(-12), "Unity negative instance IDs remain valid.");
        Check(!state.TryCollect(-12) && state.Count == 1, "A robot is counted only once.");
        for (int id = 1; id < 5; id++)
        {
            Check(state.TryCollect(id), "Every distinct robot occupies one desk slot.");
            Check(state.Complete == (id == 4), "The reward unlocks exactly on robot five.");
        }
        Check(state.Count == 5 && !state.TryCollect(99), "Display remains capped at five.");
        Check(!state.TryCollect(4), "Completed collection cannot retrigger the reward.");
        Check(new RobotCollectionState().Count == 0, "Restarting creates an empty collection.");
        Console.WriteLine("PASS: unique robots, five slots, one completion and restart reset.");
    }
}
