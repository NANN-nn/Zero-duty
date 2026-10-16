using UnityEngine;

/// <summary>Shows the control-room steam leak after the corridor is cleared.</summary>
[DisallowMultipleComponent]
public class GasLeakEffectController : MonoBehaviour
{
    [SerializeField] private GameObject gasLeakEffect;
    [SerializeField] private string startSignal = "corridor_interlude_complete";
    [SerializeField] private string stopSignal = "gas_restored";

    private bool repaired;
    private AudioSource steamAudio;

    private void Awake()
    {
        if (gasLeakEffect == null)
            Debug.LogWarning($"{name}: Assign GasLeakFX to Gas Leak Effect Controller.", this);
        else
        {
            var soundObject = new GameObject("Steam Hiss (Runtime)");
            soundObject.transform.SetParent(gasLeakEffect.transform, false);
            steamAudio = soundObject.AddComponent<AudioSource>();
            steamAudio.clip = ShiftInteractionSounds.Steam;
            steamAudio.playOnAwake = false;
            steamAudio.loop = true;
            steamAudio.spatialBlend = 1f;
            steamAudio.rolloffMode = AudioRolloffMode.Logarithmic;
            steamAudio.minDistance = .75f;
            steamAudio.maxDistance = 9f;
            steamAudio.volume = .38f;
        }
    }

    private void OnEnable()
    {
        if (GameplaySignals.Instance != null)
            GameplaySignals.Instance.SignalRaised += OnSignalRaised;
    }

    private void Start()
    {
        if (GameplaySignals.Instance != null)
        {
            GameplaySignals.Instance.SignalRaised -= OnSignalRaised;
            GameplaySignals.Instance.SignalRaised += OnSignalRaised;
            repaired = GameplaySignals.Instance.HasRaised(stopSignal);
            bool started = GameplaySignals.Instance.HasRaised(startSignal);
            SetLeakVisible(started && !repaired);
        }
        else SetLeakVisible(false);
    }

    private void OnSignalRaised(string signalId)
    {
        if (signalId == stopSignal)
        {
            repaired = true;
            SetLeakVisible(false);
        }
        else if (signalId == startSignal && !repaired)
            SetLeakVisible(true);
    }

    private void SetLeakVisible(bool visible)
    {
        if (gasLeakEffect == null) return;
        if (!visible && steamAudio != null) steamAudio.Stop();
        gasLeakEffect.SetActive(visible);
        if (visible && steamAudio != null && !steamAudio.isPlaying) steamAudio.Play();
    }

    private void OnDisable()
    {
        if (GameplaySignals.Instance != null)
            GameplaySignals.Instance.SignalRaised -= OnSignalRaised;
        if (steamAudio != null) steamAudio.Stop();
    }
}
