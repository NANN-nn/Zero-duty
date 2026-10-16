using System;

/// <summary>Deadline-based proximity and speed, independent of rendering and physics.</summary>
public static class GhostApproachProfile
{
    public static float Amount(float progress)
        => Math.Max(0f, Math.Min(1f, (progress - .75f) / .25f));

    public static float TargetDistance(float initialDistance, float progress, float stopDistance)
    {
        float start = Math.Max(initialDistance, stopDistance);
        return start + (stopDistance - start) * Amount(progress);
    }

    public static float Step(float distance, float initialDistance, float progress,
        float stopDistance, float maximumSpeed, float deltaSeconds)
    {
        float gap = Math.Max(0f, distance - TargetDistance(initialDistance, progress, stopDistance));
        float speed = Math.Max(0f, maximumSpeed) * (.35f + .65f * Amount(progress));
        return Math.Min(gap, speed * Math.Max(0f, deltaSeconds));
    }
}
