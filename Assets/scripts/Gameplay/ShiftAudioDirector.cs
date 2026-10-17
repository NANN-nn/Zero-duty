using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One place for the shift's music, ambience and short interaction cues.</summary>
public sealed class ShiftAudioDirector : MonoBehaviour
{
    private static ShiftAudioDirector instance;
    private AudioSource music;
    private AudioSource machinery;
    private AudioSource wind;
    private AudioSource interference;
    private AudioSource cues;
    private AudioSource scannerCharge;
    private AudioSource monitorSwitch;
    private AudioSource alarm;
    private AudioSource tension;
    private AudioSource finalFlicker;
    private bool finalFlickerActive;
    private bool finalFlickerPaused;
    private AudioClip foodChew;
    private AudioClip drinkSip;
    private AudioClip ghostHit;
    private AudioClip musicButton;
    private GameplaySignals signals;
    private float pressure;
    private float pressureTarget;
    private bool inMenu = true;
    private bool ending;
    public static bool EndingActive => instance != null && instance.ending;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        var root = new GameObject("Shift Audio (Runtime)");
        DontDestroyOnLoad(root);
        root.AddComponent<ShiftAudioDirector>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        music = MakeSource("Music", 0.38f, true);
        machinery = MakeSource("Station Machinery", 0.085f, true);
        wind = MakeSource("Ventilation", 0.035f, true);
        interference = MakeSource("Pressure Interference", 0f, true);
        tension = MakeSource("Escalating Background", 0f, true);
        cues = MakeSource("Interaction Cues", 0.42f, false);
        scannerCharge = MakeSource("Scanner Charge", 0.37f, false);
        monitorSwitch = MakeSource("Monitor Switch", 0.34f, false);
        monitorSwitch.pitch = 0.72f;
        alarm = MakeSource("Anomaly Warning", 0.52f, false);
        finalFlicker = MakeSource("Final Monitor Breakdown", 0.70f, false);
        finalFlicker.clip = Load("Interactive/SFX_FinalFlicker");
        machinery.clip = Load("Enviroment/ENV_MachineNoise");
        wind.clip = Load("Enviroment/ENV_Wind");
        interference.clip = Load("Enviroment/ENV_RadioNoise");
        monitorSwitch.clip = Load("Interactive/SwitchCamera");
        alarm.clip = Load("Interactive/SFX_AnomalyAlarm_User")
            ?? ShiftInteractionSounds.AnomalyAlarm;
        musicButton = Load("Interactive/SFX_MusicButton_User");
        foodChew = Load("Interactive/SFX_FoodChew_CC0");
        drinkSip = Load("Interactive/SFX_DrinkSip_CC0");
        ghostHit = Load("Interactive/SFX_GhostHit_CC0");
        tension.clip = ShiftInteractionSounds.TensionBed;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private AudioSource MakeSource(string label, float volume, bool loop)
    {
        var source = new GameObject(label).AddComponent<AudioSource>();
        source.transform.SetParent(transform, false);
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.loop = loop;
        source.volume = volume;
        return source;
    }

    private static AudioClip Load(string path) =>
        Resources.Load<AudioClip>("Monitor/Audio/" + path);

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StopFinalFlicker();
        Unsubscribe();
        ending = false;
        pressure = pressureTarget = 0f;
        interference.volume = 0f;
        tension.volume = 0f;
        inMenu = scene.name == "first" && !ShiftStartMenu.ShiftStarted;
        if (inMenu)
        {
            // Use the same exploration bed on the start screen and in-game.
            // The old main-menu track contained the unwanted vocal passage.
            StopAll();
            PlayMusic("MUS_Explore", 0.48f, true);
            machinery.volume = 0.05f;
            wind.volume = 0.02f;
            if (machinery.clip != null) machinery.Play();
            if (wind.clip != null) wind.Play();
        }
        else if (scene.name == "first") StartGameplay();
        else StopAll();
    }

    private void Update()
    {
        // Pause only this one-shot with the visual sequence. Ambient audio
        // intentionally stays audible in the pause menu for volume adjustment.
        if (finalFlickerActive && finalFlicker != null)
        {
            bool shouldPause = Time.timeScale <= 0f;
            if (shouldPause && !finalFlickerPaused) finalFlicker.Pause();
            else if (!shouldPause && finalFlickerPaused) finalFlicker.UnPause();
            finalFlickerPaused = shouldPause;
        }
        if (signals == null && GameplaySignals.Instance != null)
        {
            signals = GameplaySignals.Instance;
            signals.SignalRaised += OnSignal;
        }
        if (inMenu || ending) return;
        pressure = Mathf.MoveTowards(pressure, pressureTarget, Time.unscaledDeltaTime * 0.3f);
        interference.volume = pressure < 0.25f ? 0f :
            Mathf.Lerp(0.012f, 0.08f, Mathf.InverseLerp(0.25f, 1f, pressure));
        wind.volume = 0.035f + pressure * 0.025f;
        float tensionAmount = Mathf.InverseLerp(0.20f, 1f, pressure);
        tension.volume = 0.25f * tensionAmount * tensionAmount;
        tension.pitch = Mathf.Lerp(0.78f, 1.85f, tensionAmount);
        music.volume = Mathf.Lerp(0.48f, 0.38f, tensionAmount);
    }

    private void OnSignal(string id)
    {
        if (inMenu || ending) return;
        switch (id)
        {
            case "corridor_anomaly_appeared":
            case "corridor_interlude_complete":
            case "gas_interlude_complete":
            case "ghost_interlude_complete":
            case "power_interlude_complete":
                pressure = pressureTarget = 0f;
                tension.volume = 0f;
                tension.pitch = .78f;
                alarm.Stop();
                alarm.Play();
                break;
            case "corridor_disposed":
                ResetPressure();
                break;
            case "gas_restored":
            case "ghost_destroyed":
            case "power_restored":
            case "specimen_room_complete":
                ResetPressure();
                PlayCue("UI_Confirm", 0.65f);
                break;
            case "final_protocol_unlocked":
                PlayCue("BigRedButton", 0.5f);
                break;
        }
    }

    private void PlayMusic(string clipName, float volume, bool loop)
    {
        var clip = Load("Music/" + clipName);
        if (clip != null && music.clip == clip && music.isPlaying && music.loop == loop)
        {
            music.volume = volume;
            return;
        }
        music.Stop();
        music.clip = clip;
        music.volume = volume;
        music.loop = loop;
        if (clip != null) music.Play();
        else Debug.LogWarning("Missing shift music: " + clipName, this);
    }

    private void PlayCue(string clipName, float scale = 1f)
    {
        var clip = Load("Interactive/" + clipName);
        if (clip != null) cues.PlayOneShot(clip, scale);
    }

    private void StopAll()
    {
        StopFinalFlicker();
        music.Stop();
        machinery.Stop();
        wind.Stop();
        interference.Stop();
        cues.Stop();
        scannerCharge.Stop();
        monitorSwitch.Stop();
        alarm.Stop();
        tension.Stop();
    }

    private void StartGameplay()
    {
        StopFinalFlicker();
        inMenu = false;
        ending = false;
        pressure = pressureTarget = 0f;
        PlayMusic("MUS_Explore", 0.48f, true);
        machinery.volume = 0.085f;
        wind.volume = 0.035f;
        if (machinery.clip != null) machinery.Play();
        if (wind.clip != null) wind.Play();
        if (interference.clip != null) interference.Play();
        tension.volume = 0f;
        tension.pitch = .78f;
        if (tension.clip != null) tension.Play();
    }

    private void StartEnding(string musicName)
    {
        StopFinalFlicker();
        ending = true;
        inMenu = false;
        machinery.Stop();
        wind.Stop();
        interference.Stop();
        cues.Stop();
        scannerCharge.Stop();
        monitorSwitch.Stop();
        alarm.Stop();
        tension.Stop();
        PlayMusic(musicName, 0.32f, false);
    }

    private void PlayVoiceFreeAmbience(float volume)
    {
        music.Stop();
        music.clip = ShiftInteractionSounds.AmbientDrone;
        music.loop = true;
        music.volume = volume;
        music.Play();
    }

    public static void BeginShift() => instance?.StartGameplay();
    public static void SetPressure(float amount)
    {
        if (instance != null && !instance.inMenu && !instance.ending)
            instance.pressureTarget = Mathf.Clamp01(amount);
    }
    public static void ClearPressure() => instance?.ResetPressure();

    public static void StartFinalFlicker(float seconds)
    {
        if (instance == null || instance.inMenu || instance.ending) return;
        AudioSource source = instance.finalFlicker;
        if (source == null || source.clip == null) return;
        StopFinalFlicker();
        // Fit the authored four-second cue to the visual sequence duration.
        source.pitch = Mathf.Clamp(source.clip.length / Mathf.Max(.1f, seconds), .1f, 3f);
        source.Play();
        instance.finalFlickerActive = true;
    }

    public static void StopFinalFlicker()
    {
        if (instance == null) return;
        if (instance.finalFlicker != null) instance.finalFlicker.Stop();
        instance.finalFlickerActive = false;
        instance.finalFlickerPaused = false;
    }

    private void ResetPressure()
    {
        pressure = pressureTarget = 0f;
        interference.volume = 0f;
        tension.volume = 0f;
        tension.pitch = .78f;
    }
    public static void PlaySwitchCamera()
    {
        if (instance == null || instance.inMenu || instance.ending || instance.monitorSwitch.clip == null)
            return;
        instance.monitorSwitch.Stop();
        instance.monitorSwitch.Play();
    }
    public static void PlayUiSelect() => instance?.PlayCue("UI_Select", 0.45f);
    public static void PlayUiConfirm() => instance?.PlayCue("UI_Confirm", 0.8f);
    public static void PlayPageFlip() => instance?.PlayCue("PageFlip", 0.55f);
    public static void PlayMusicPlayerButton() => instance?.PlayInteraction(
        instance.musicButton ?? ShiftInteractionSounds.PlayerButton, 0.9f);
    public static void PlayFootstep(bool sprinting) => instance?.PlayInteraction(
        sprinting ? ShiftInteractionSounds.FastFootstep : ShiftInteractionSounds.Footstep,
        sprinting ? 0.8f : 0.62f);
    public static void PlayPickup() => instance?.PlayInteraction(ShiftInteractionSounds.Pickup, 0.95f);
    public static void PlayDrop() => instance?.PlayInteraction(ShiftInteractionSounds.Drop, 0.85f);
    public static void PlayCabinet(bool opening) => instance?.PlayInteraction(
        opening ? ShiftInteractionSounds.CabinetOpen : ShiftInteractionSounds.CabinetClose, 0.8f);
    public static void PlayDisposal() => instance?.PlayInteraction(ShiftInteractionSounds.Disposal, 1.1f);
    public static void PlayGunFallback() => instance?.PlayInteraction(ShiftInteractionSounds.Gun, 0.95f);
    public static void PlayGhostHit() => instance?.PlayInteraction(
        instance.ghostHit ?? ShiftInteractionSounds.GhostHit, 0.9f);
    public static void PlayConsume(bool drink) => instance?.PlayInteraction(
        drink ? instance.drinkSip ?? ShiftInteractionSounds.DrinkSip
            : instance.foodChew ?? ShiftInteractionSounds.FoodCrunch, 0.85f);
    public static void PlayScannerWarning() => instance?.PlayInteraction(ShiftInteractionSounds.ScannerWarning, 0.55f);
    public static void PlayScannerSuccess() => instance?.PlayInteraction(ShiftInteractionSounds.ScannerSuccess, 0.95f);
    public static void PlayGasPress(bool accurate) => instance?.PlayInteraction(
        accurate ? ShiftInteractionSounds.GasPress : ShiftInteractionSounds.GasMiss, 0.9f);
    public static void PlayPowerSwitch(bool correct) => instance?.PlayInteraction(
        correct ? ShiftInteractionSounds.PowerSwitch : ShiftInteractionSounds.PowerReject, 0.95f);

    public static void StartScannerCharge()
    {
        if (instance == null || instance.inMenu || instance.ending) return;
        instance.scannerCharge.Stop();
        instance.scannerCharge.clip = ShiftInteractionSounds.ScannerCharge;
        instance.scannerCharge.Play();
    }

    public static void StopScannerCharge()
    {
        if (instance != null) instance.scannerCharge.Stop();
    }
    public static void PlayEndingData() => instance?.StartEnding("MUS_TrueEnding");
    public static void PlayEndingLockdown() => instance?.StartEnding("MUS_NormalEnding");
    public static void PlayEndingFailure() => instance?.StartEnding("MUS_BadEnding");

    private void PlayInteraction(AudioClip clip, float scale)
    {
        if (!inMenu && !ending && clip != null) cues.PlayOneShot(clip, scale);
    }

    private void Unsubscribe()
    {
        if (signals != null) signals.SignalRaised -= OnSignal;
        signals = null;
    }

    private void OnDestroy()
    {
        Unsubscribe();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }
}
