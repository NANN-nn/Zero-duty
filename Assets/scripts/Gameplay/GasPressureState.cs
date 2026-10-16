using System;

/// <summary>Deterministic pressure regulation: hold rises, release falls, safe dwell latches a channel.</summary>
public sealed class GasPressureState
{
    private readonly int channels;
    private readonly float safeMin, safeMax, dwellSeconds, riseSpeed, fallSpeed;
    public float Pressure { get; private set; } = .12f;
    public float StableSeconds { get; private set; }
    public int CompletedChannels { get; private set; }
    public bool Complete => CompletedChannels >= channels;
    public bool InSafeZone => Pressure >= safeMin && Pressure <= safeMax;

    public GasPressureState(int channels, float safeMin, float safeMax,
        float dwellSeconds, float riseSpeed, float fallSpeed)
    {
        this.channels = Math.Max(1, channels);
        this.safeMin = safeMin;
        this.safeMax = safeMax;
        this.dwellSeconds = Math.Max(.1f, dwellSeconds);
        this.riseSpeed = Math.Max(.01f, riseSpeed);
        this.fallSpeed = Math.Max(.01f, fallSpeed);
    }

    public bool Advance(float seconds, bool held)
    {
        if (seconds <= 0f || Complete) return false;
        float rate = (held ? riseSpeed : -fallSpeed) * (1f + .14f * CompletedChannels);
        Pressure = Math.Max(0f, Math.Min(1f, Pressure + rate * seconds));
        StableSeconds = InSafeZone ? StableSeconds + seconds : 0f;
        if (StableSeconds < dwellSeconds) return false;
        CompletedChannels++;
        StableSeconds = 0f;
        // Every channel starts from the same low-pressure side: hold right,
        // release left. The second channel must never reverse the starting task.
        if (!Complete) Pressure = .12f;
        return true;
    }

    public void ResetStability() => StableSeconds = 0f;
}
