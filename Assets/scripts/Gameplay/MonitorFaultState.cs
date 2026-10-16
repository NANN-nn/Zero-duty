/// <summary>Routes incidents to the scene's CCTV feeds, not the menu artwork.</summary>
public sealed class MonitorFaultState
{
    // The actual first scene wiring is: corridor, specimen room, shooting room.
    private static readonly string[] Starts =
    {
        "corridor_anomaly_appeared", "power_interlude_complete", "gas_interlude_complete"
    };
    private static readonly string[] Ends =
    {
        "corridor_disposed", "specimen_room_complete", "ghost_destroyed"
    };
    private readonly bool[] active = new bool[3];

    public bool IsActive(int feed) => feed >= 0 && feed < active.Length && active[feed];

    public void Apply(string signal)
    {
        for (int i = 0; i < active.Length; i++)
        {
            if (signal == Starts[i]) active[i] = true;
            if (signal == Ends[i]) active[i] = false;
        }
    }

    public void Clear()
    {
        for (int i = 0; i < active.Length; i++) active[i] = false;
    }
}
