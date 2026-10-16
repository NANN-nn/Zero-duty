using UnityEngine;

/// <summary>Small procedural cues used until final recorded Foley is available.</summary>
public static class ShiftInteractionSounds
{
    private const int Rate = 22050;
    private static AudioClip scannerWarning, scannerCharge, scannerSuccess;
    private static AudioClip gasPress, gasMiss, powerSwitch, powerReject, playerButton, steam;
    private static AudioClip footstep, fastFootstep, pickup, drop, cabinetOpen,
        cabinetClose, disposal, gun, ghostHit;
    private static AudioClip ambientDrone, tensionBed, anomalyAlarm, foodCrunch, drinkSip;

    public static AudioClip ScannerWarning => scannerWarning ??=
        Tone("Scanner Warning", .13f, 660f, 740f, .05f, .22f);
    public static AudioClip ScannerCharge => scannerCharge ??=
        Tone("Scanner Charge", .95f, 180f, 850f, .12f, .24f);
    public static AudioClip ScannerSuccess => scannerSuccess ??=
        Tone("Scanner Confirmed", .36f, 760f, 1240f, .08f, .30f);
    public static AudioClip GasPress => gasPress ??=
        Tone("Gas Valve Press", .17f, 160f, 100f, .72f, .30f);
    public static AudioClip GasMiss => gasMiss ??=
        Tone("Gas Valve Miss", .16f, 190f, 90f, .55f, .21f);
    public static AudioClip PowerSwitch => powerSwitch ??=
        Tone("Power Switch", .21f, 135f, 75f, .62f, .34f);
    public static AudioClip PowerReject => powerReject ??=
        Tone("Power Switch Reject", .19f, 210f, 95f, .48f, .23f);
    public static AudioClip PlayerButton => playerButton ??=
        Tone("Music Player Button", .11f, 420f, 270f, .55f, .26f);
    public static AudioClip Steam => steam ??= MakeSteam();
    public static AudioClip Footstep => footstep ??=
        Impact("Floor Step", .19f, 95f, .23f, 1821);
    public static AudioClip FastFootstep => fastFootstep ??=
        Impact("Fast Floor Step", .16f, 115f, .27f, 1822);
    public static AudioClip Pickup => pickup ??=
        MakePickup();
    public static AudioClip Drop => drop ??=
        Impact("Put Down", .24f, 120f, .35f, 3042);
    public static AudioClip CabinetOpen => cabinetOpen ??=
        Tone("Cabinet Opens", .32f, 145f, 85f, .4f, .23f);
    public static AudioClip CabinetClose => cabinetClose ??=
        Impact("Cabinet Closes", .22f, 105f, .34f, 3043);
    public static AudioClip Disposal => disposal ??=
        Tone("Disposal Unit", .55f, 180f, 56f, .55f, .52f);
    public static AudioClip Gun => gun ??=
        Impact("Energy Shot", .31f, 185f, .6f, 3044);
    public static AudioClip GhostHit => ghostHit ??=
        Tone("Ghost Hit", .32f, 520f, 150f, .32f, .29f);
    public static AudioClip AmbientDrone => ambientDrone ??= MakeAmbientDrone();
    public static AudioClip TensionBed => tensionBed ??= MakeTensionBed();
    public static AudioClip AnomalyAlarm => anomalyAlarm ??= MakeAlarm();
    public static AudioClip FoodCrunch => foodCrunch ??=
        MakeFoodCrunch();
    public static AudioClip DrinkSip => drinkSip ??=
        MakeDrinkSip();

    private static AudioClip MakePickup()
    {
        const float duration = .43f;
        int count = Mathf.CeilToInt(duration * Rate);
        float[] samples = new float[count];
        var random = new System.Random(3041);
        float softNoise = 0f;
        for (int i = 0; i < count; i++)
        {
            float time = i / (float)Rate;
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            softNoise = Mathf.Lerp(softNoise, white, .16f);
            float rustle = Mathf.Sin(Mathf.PI * Mathf.Clamp01((time - .015f) / .28f));
            rustle = Mathf.Max(0f, rustle);
            float contact = Mathf.Exp(-Mathf.Abs(time - .29f) * 85f);
            float weight = Mathf.Sin(2f * Mathf.PI * 95f * (time - .29f))
                * Mathf.Exp(-Mathf.Max(0f, time - .29f) * 28f);
            samples[i] = .23f * softNoise * rustle
                + .18f * white * contact + .09f * weight * (time >= .29f ? 1f : 0f);
        }
        AudioClip clip = AudioClip.Create("Soft Pickup and Grip", count, 1, Rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip MakeFoodCrunch()
    {
        const float duration = .72f;
        int count = Mathf.CeilToInt(duration * Rate);
        float[] samples = new float[count];
        var random = new System.Random(8120);
        float coarse = 0f;
        float fine = 0f;
        for (int i = 0; i < count; i++)
        {
            float time = i / (float)Rate;
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            coarse = Mathf.Lerp(coarse, white, .055f);
            fine = Mathf.Lerp(fine, white, .34f);
            float first = Mathf.Exp(-Mathf.Max(0f, time - .035f) * 27f)
                * (time >= .035f ? 1f : 0f);
            float second = Mathf.Exp(-Mathf.Max(0f, time - .22f) * 31f)
                * (time >= .22f ? 1f : 0f);
            float third = Mathf.Exp(-Mathf.Max(0f, time - .39f) * 40f)
                * (time >= .39f ? 1f : 0f);
            float jaw = .08f * Mathf.Sin(2f * Mathf.PI * 145f * time)
                * Mathf.Exp(-time * 6f);
            samples[i] = (fine * .24f + coarse * .18f)
                * (first + .74f * second + .46f * third) + jaw;
        }
        AudioClip clip = AudioClip.Create("Three Soft Bites", count, 1, Rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip MakeDrinkSip()
    {
        const float duration = .88f;
        int count = Mathf.CeilToInt(duration * Rate);
        float[] samples = new float[count];
        var random = new System.Random(8121);
        float phase = 0f;
        float liquid = 0f;
        for (int i = 0; i < count; i++)
        {
            float time = i / (float)Rate;
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            liquid = Mathf.Lerp(liquid, white, .11f);
            float sip = Mathf.Sin(Mathf.PI * Mathf.Clamp01((time - .07f) / .58f));
            sip = Mathf.Max(0f, sip);
            float bubble1 = Mathf.Exp(-Mathf.Abs(time - .28f) * 22f);
            float bubble2 = Mathf.Exp(-Mathf.Abs(time - .48f) * 23f);
            float bubble3 = Mathf.Exp(-Mathf.Abs(time - .64f) * 24f);
            phase += 2f * Mathf.PI * (170f + 32f * Mathf.Sin(time * 24f)) / Rate;
            float bubbles = bubble1 + .7f * bubble2 + .45f * bubble3;
            samples[i] = .19f * liquid * sip
                + .11f * Mathf.Sin(phase) * bubbles
                + .06f * white * Mathf.Exp(-Mathf.Abs(time - .78f) * 65f);
        }
        AudioClip clip = AudioClip.Create("Sip and Swallow", count, 1, Rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip MakeAmbientDrone()
    {
        const int count = Rate * 8;
        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float time = i / (float)Rate;
            float breathing = .76f + .24f * Mathf.Sin(2f * Mathf.PI * time / 8f);
            samples[i] = breathing * (.18f * Mathf.Sin(2f * Mathf.PI * 55f * time)
                + .078f * Mathf.Sin(2f * Mathf.PI * 65.375f * time)
                + .047f * Mathf.Sin(2f * Mathf.PI * 82.375f * time));
        }
        AudioClip clip = AudioClip.Create("Voice-Free Station Drone", count, 1, Rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip MakeTensionBed()
    {
        const int count = Rate * 8;
        float[] samples = new float[count];
        var random = new System.Random(92041);
        float softNoise = 0f;
        for (int i = 0; i < count; i++)
        {
            float time = i / (float)Rate;
            float beat = time - Mathf.Floor(time);
            float envelope = Mathf.Exp(-beat * 18f)
                * Mathf.Min(1f, beat / .004f);
            float lowThrob = Mathf.Sin(2f * Mathf.PI *
                (55f * beat - 13f * beat * beat));
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            softNoise = Mathf.Lerp(softNoise, white, .18f);
            float darkDrone = .055f * Mathf.Sin(2f * Mathf.PI * 41.25f * time)
                + .025f * Mathf.Sin(2f * Mathf.PI * 55f * time);
            samples[i] = darkDrone + envelope * (.38f * lowThrob + .11f * softNoise);
        }
        AudioClip clip = AudioClip.Create("Escalating Tension Bed", count, 1, Rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip MakeAlarm()
    {
        const float duration = 1.72f;
        int count = Mathf.CeilToInt(duration * Rate);
        float[] samples = new float[count];
        float phase = 0f;
        var random = new System.Random(54812);
        float softNoise = 0f;
        for (int i = 0; i < count; i++)
        {
            float time = i / (float)Rate;
            int pulse = Mathf.FloorToInt(time / .53f);
            float withinPulse = time - pulse * .53f;
            if (pulse >= 3 || withinPulse > .39f) continue;
            float edge = Mathf.Min(1f, withinPulse / .012f)
                * Mathf.Min(1f, (.39f - withinPulse) / .055f);
            float frequency = Mathf.Lerp(740f, 1130f, withinPulse / .39f)
                + 24f * Mathf.Sin(time * 38f);
            phase += 2f * Mathf.PI * frequency / Rate;
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            softNoise = Mathf.Lerp(softNoise, white, .2f);
            samples[i] = Mathf.Max(0f, edge) *
                (.29f * Mathf.Sin(phase) + .12f * Mathf.Sin(phase * 2f)
                + .055f * Mathf.Sin(phase * 3f) + .075f * softNoise);
        }
        AudioClip clip = AudioClip.Create("Anomaly Alarm", count, 1, Rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip Impact(string label, float seconds, float bodyHz,
        float volume, int seed)
    {
        int count = Mathf.CeilToInt(seconds * Rate);
        float[] samples = new float[count];
        var random = new System.Random(seed);
        float filteredNoise = 0f;
        for (int i = 0; i < count; i++)
        {
            float time = i / (float)Rate;
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            filteredNoise = Mathf.Lerp(filteredNoise, white, .24f);
            float thump = Mathf.Sin(2f * Mathf.PI * bodyHz * time)
                * Mathf.Exp(-time * 24f);
            float scrape = filteredNoise * Mathf.Exp(-time * 17f);
            float attack = Mathf.Min(1f, i / (Rate * .002f));
            samples[i] = volume * attack * (.72f * thump + .45f * scrape);
        }
        AudioClip clip = AudioClip.Create(label, count, 1, Rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip Tone(string label, float seconds, float startHz,
        float endHz, float noiseAmount, float volume)
    {
        int count = Mathf.CeilToInt(seconds * Rate);
        float[] samples = new float[count];
        var random = new System.Random(label.GetHashCode());
        float phase = 0f;
        float previousNoise = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;
            float hz = Mathf.Lerp(startHz, endHz, t);
            phase += 2f * Mathf.PI * hz / Rate;
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            float click = noise - previousNoise * .45f;
            previousNoise = noise;
            float envelope = Mathf.Min(1f, i / (Rate * .004f))
                * Mathf.Min(1f, (count - i) / (Rate * .055f));
            envelope *= Mathf.Lerp(1f, .22f, t);
            samples[i] = volume * envelope *
                ((1f - noiseAmount) * Mathf.Sin(phase) + noiseAmount * click * .55f);
        }
        AudioClip clip = AudioClip.Create(label, count, 1, Rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip MakeSteam()
    {
        const int count = Rate * 3;
        float[] samples = new float[count];
        var random = new System.Random(51472);
        float softNoise = 0f;
        float lowNoise = 0f;
        for (int i = 0; i < count; i++)
        {
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            softNoise = Mathf.Lerp(softNoise, white, .17f);
            lowNoise = Mathf.Lerp(lowNoise, white, .008f);
            samples[i] = white * .18f + softNoise * .55f + lowNoise * .25f;
        }
        // Blend the tail into the head so the continuous leak has no hard click.
        int blend = Rate / 8;
        for (int i = 0; i < blend; i++)
        {
            int tail = count - blend + i;
            samples[tail] = Mathf.Lerp(samples[tail], samples[i], i / (float)blend);
        }
        AudioClip clip = AudioClip.Create("Steam Leak Loop", count, 1, Rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
