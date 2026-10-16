using System;

public static class MonitorFaultStateTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static void Main()
    {
        var state = new MonitorFaultState();
        Check(!state.IsActive(0) && !state.IsActive(1) && !state.IsActive(2), "Normal feeds must not glitch.");
        state.Apply("corridor_anomaly_appeared");
        Check(state.IsActive(0) && !state.IsActive(1) && !state.IsActive(2), "Corridor routes only to CCTV 1.");
        state.Apply("corridor_disposed");
        Check(!state.IsActive(0), "Disposal clears the corridor cue.");
        state.Apply("corridor_interlude_complete");
        Check(!state.IsActive(0) && !state.IsActive(1) && !state.IsActive(2), "Gas fault is in the control room, not one of these feeds.");
        state.Apply("gas_interlude_complete");
        Check(!state.IsActive(0) && !state.IsActive(1) && state.IsActive(2), "Shadow routes only to CCTV 3.");
        state.Apply("ghost_destroyed");
        state.Apply("ghost_interlude_complete");
        Check(!state.IsActive(0) && !state.IsActive(1) && !state.IsActive(2), "Power fault must not falsely flag the corridor.");
        state.Apply("power_interlude_complete");
        Check(!state.IsActive(0) && state.IsActive(1) && !state.IsActive(2), "Specimens route only to CCTV 2.");
        state.Apply("specimen_room_complete");
        Check(!state.IsActive(1), "Completing BOTH specimen finds clears the cue.");
        state.Apply("corridor_anomaly_appeared");
        state.Apply("gas_interlude_complete");
        Check(state.IsActive(0) && state.IsActive(2) && !state.IsActive(1), "Debug overlap preserves independent cues.");
        state.Clear();
        Check(!state.IsActive(0) && !state.IsActive(1) && !state.IsActive(2), "Ending/disable clears all cues.");
        Check(!state.IsActive(-1) && !state.IsActive(3), "Invalid feed indices are safe.");
        Console.WriteLine("PASS: CCTV routing, normal feeds, completion, overlap and cleanup.");
    }
}
