using System;

public static class GasPressureStateTests
{
    private static GasPressureState Create() => new GasPressureState(3, .36f, .64f, 1.5f, .35f, .27f);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    private static void Regulate(GasPressureState state, int frames)
    {
        for (int i = 0; i < frames; i++) state.Advance(1f / 60f, state.Pressure < .5f);
    }

    public static int Main()
    {
        var state = Create();
        float initial = state.Pressure;
        state.Advance(.1f, true);
        Check(state.Pressure > initial, "Holding must move pressure right.");
        float held = state.Pressure;
        state.Advance(.1f, false);
        Check(state.Pressure < held, "Releasing must move pressure left.");
        state.Advance(100f, true);
        Check(state.Pressure == 1f && state.CompletedChannels == 0, "Pressure must clamp at the right edge.");
        state.Advance(100f, false);
        Check(state.Pressure == 0f, "Pressure must clamp at the left edge.");

        state = Create();
        while (state.Pressure < .42f) state.Advance(1f / 60f, true);
        state.ResetStability();
        Regulate(state, 89);
        Check(state.CompletedChannels == 0, "A channel must not latch before 1.5 seconds.");
        Regulate(state, 2);
        Check(state.CompletedChannels == 1, "A continuous safe dwell must latch one channel.");
        state.Advance(.2f, true);
        Check(state.CompletedChannels == 1 && state.StableSeconds == 0f,
            "An unsafe next channel must not erase an already completed channel.");
        float pressure = state.Pressure;
        state.Advance(0f, true);
        Check(state.Pressure == pressure, "Paused time must not advance pressure.");
        state.ResetStability();
        Check(state.CompletedChannels == 1 && state.Pressure == pressure,
            "Closing a panel must preserve completed channels and pressure.");
        Regulate(state, 2000);
        Check(state.Complete && state.CompletedChannels == 3, "All three channels must be solvable.");
        pressure = state.Pressure;
        state.Advance(5f, false);
        Check(state.Pressure == pressure && state.CompletedChannels == 3,
            "Completion must not advance or award extra channels.");
        state = Create();
        for (int channel = 0; channel < 3; channel++)
        {
            Check(state.Pressure < .36f, "Each channel must start left of the safe band.");
            initial = state.Pressure;
            state.Advance(.1f, true);
            Check(state.Pressure > initial, "Holding must move right on every channel.");
            held = state.Pressure;
            state.Advance(.05f, false);
            Check(state.Pressure < held, "Releasing must move left on every channel.");
            int frames = 0;
            while (state.CompletedChannels == channel && frames++ < 1000) Regulate(state, 1);
            Check(state.CompletedChannels == channel + 1, "Each channel must stabilize independently.");
        }
        Console.WriteLine("PASS: all three start left, hold right/release left, bounds, dwell, partial progress, pause and completion.");
        return 0;
    }
}
