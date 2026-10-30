using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Stage deadlines after the corridor, with quiet gaps between them.</summary>
[DisallowMultipleComponent]
public class LaterAnomalyPressure : MonoBehaviour
{
    private static readonly string[] StartSignals =
    {
        "corridor_interlude_complete", "gas_interlude_complete",
        "ghost_interlude_complete", "power_interlude_complete"
    };

    private static readonly string[] CompletionSignals =
    {
        "gas_restored", "ghost_destroyed", "power_restored", "specimen_room_complete"
    };

    private static readonly string[] NextSignals =
    {
        "gas_interlude_complete", "ghost_interlude_complete",
        "power_interlude_complete"
    };

    [SerializeField, Min(15f)] private float timeLimitSeconds = 120f;
    [SerializeField] private Vector2 interludeSeconds = Vector2.zero;
    [SerializeField, Min(0.1f)] private float nextAnomalyWatchSeconds = 10f;
    [SerializeField] private CorridorPressureSequence failureAndEffects;

    private GameplaySignals signals;
    private int activeStage = -1;
    private int nextStage = -1;
    private float remaining;
    private float activeTimeLimitSeconds;
    private float interludeRemaining;
    private float nextAnomalyWatchTime;
    private bool nextStageReady;
    private MonitorViewManager monitorView;

    private void Start()
    {
        interludeSeconds = Vector2.zero;
        nextAnomalyWatchSeconds = 10f;
        signals = GameplaySignals.Instance;
        if (signals == null)
        {
            Debug.LogError("LaterAnomalyPressure needs GameplaySignals.", this);
            enabled = false;
            return;
        }
        if (failureAndEffects == null)
            failureAndEffects = GetComponent<CorridorPressureSequence>();
        monitorView = FindAnyObjectByType<MonitorViewManager>();
        signals.SignalRaised += OnSignalRaised;
        for (int i = 0; i < StartSignals.Length; i++)
            if (signals.HasRaised(StartSignals[i]) && !signals.HasRaised(CompletionSignals[i]))
            {
                BeginStage(i);
                break;
            }
    }

    private void OnSignalRaised(string signalId)
    {
        if (activeStage >= 0 && signalId == CompletionSignals[activeStage])
        {
            int completedStage = activeStage;
            activeStage = -1;
            pressureStarted = false;
            // PowerOutageLighting handles its own restore on the power completion signal.
            failureAndEffects?.EndExternalPressure(completedStage != 2);
            if (completedStage < NextSignals.Length)
            {
                float first = Mathf.Max(0f, Mathf.Min(interludeSeconds.x, interludeSeconds.y));
                float last = Mathf.Max(first, Mathf.Max(interludeSeconds.x, interludeSeconds.y));
                interludeRemaining = Random.Range(first, last);
                nextStage = completedStage;
                nextStageReady = false;
                nextAnomalyWatchTime = 0f;
                Debug.Log($"[Shift] Quiet interval before the next anomaly: {interludeRemaining:0}s.", this);
            }
            return;
        }

        for (int i = 0; i < StartSignals.Length; i++)
            if (signalId == StartSignals[i])
            {
                BeginStage(i);
                break;
            }
    }

    private void BeginStage(int index)
    {
        // Only the active stage owns a deadline; duplicate signals cannot restart it.
        if (activeStage >= 0 || index < 0 || index >= StartSignals.Length ||
            signals.HasRaised(CompletionSignals[index])) return;
        activeStage = index;
        pressureStarted = false;
        // The specimen-room scanner task needs more time to locate both finds.
        activeTimeLimitSeconds = index == 3 ? 180f : timeLimitSeconds;
        remaining = activeTimeLimitSeconds;
        // The lighting controller can react to the same signal; capture its final state next frame.
        Debug.Log($"[Shift] {StartSignals[index]} started; {remaining:0}s remaining.", this);
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
#if UNITY_EDITOR
        if (nextStage >= 0 && Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame)
            interludeRemaining = 0f;
#endif
        if (nextStage >= 0 && !nextStageReady)
        {
            interludeRemaining -= Time.deltaTime;
            if (interludeRemaining <= 0f)
                nextStageReady = true;
        }
        if (nextStage >= 0 && nextStageReady)
        {
            if (monitorView == null)
                monitorView = FindAnyObjectByType<MonitorViewManager>();
            nextAnomalyWatchTime = monitorView != null && monitorView.IsSeated
                ? nextAnomalyWatchTime + Time.deltaTime : 0f;
            if (nextAnomalyWatchTime >= nextAnomalyWatchSeconds)
            {
                int completedStage = nextStage;
                nextStage = -1;
                nextStageReady = false;
                signals?.Raise(NextSignals[completedStage]);
            }
        }

        if (activeStage < 0) return;
        if (!pressureStarted)
        {
            failureAndEffects?.BeginExternalPressure();
            pressureStarted = true;
        }
        // The power-panel number lock and levers are a deliberate untimed step.
        // Pause only this anomaly's failure clock; do not freeze the whole game.
        if (activeStage == 2 && SequencePuzzle.IsPanelOpen) return;
#if UNITY_EDITOR
        if (Keyboard.current != null && Keyboard.current.f6Key.wasPressedThisFrame)
            remaining = 0f;
#endif
        remaining -= Time.deltaTime;
        if (remaining <= 0f)
        {
            activeStage = -1;
            failureAndEffects?.TriggerFailure();
            return;
        }
        failureAndEffects?.SetExternalPressure(1f - remaining / activeTimeLimitSeconds);
    }

    private bool pressureStarted;

    private void OnDisable()
    {
        if (signals != null) signals.SignalRaised -= OnSignalRaised;
    }
}
