using System;

public static class GhostApproachProfileTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static void Main()
    {
        const float start = 12f, stop = 1.1f;
        Check(Math.Abs(GhostApproachProfile.TargetDistance(start, .75f, stop) - start) < .001f,
            "First glimpse must begin at the authored distance, without a teleport.");
        float previous = start;
        for (int i = 0; i <= 100; i++)
        {
            float gap = GhostApproachProfile.TargetDistance(start, .75f + .25f * i / 100f, stop);
            Check(gap <= previous + .001f && gap >= stop - .001f, "The desired gap only decreases.");
            previous = gap;
        }
        Check(Math.Abs(previous - stop) < .001f, "Deadline ends near the player, not just three metres from spawn.");
        Check(GhostApproachProfile.Step(12f, start, 1f, stop, 3f, 0f) == 0f,
            "Pause must freeze movement.");
        Check(GhostApproachProfile.Step(.8f, start, 1f, stop, 3f, .016f) == 0f,
            "A ghost already too close must not advance through the player.");
        Check(GhostApproachProfile.Step(start, start, 1f, stop, 3f, .016f) <= 3f * .016f,
            "One frame must never jump straight to the target radius.");

        // Simulate the last 30 seconds of a two-minute task with a stationary
        // player and an unobstructed corridor, using the production movement rule.
        float distance = start;
        for (int frame = 0; frame < 30 * 60; frame++)
        {
            float progress = .75f + .25f * (frame + 1) / (30f * 60f);
            float step = GhostApproachProfile.Step(distance, start, progress, stop, 3f, 1f / 60f);
            Check(step >= 0f && step <= 3f / 60f + .001f, "Chase step must be speed-limited.");
            distance -= step;
        }
        Check(distance < 1.2f && distance >= stop - .001f, "Ghost visibly closes a twelve-metre gap.");
        Check(GhostApproachProfile.TargetDistance(.5f, .8f, stop) >= stop,
            "Starting close must not demand an unsafe approach.");
        Console.WriteLine("PASS: monotonic approach, long-distance chase, speed limit, stop radius and pause.");
    }
}
