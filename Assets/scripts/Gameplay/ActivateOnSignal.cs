using UnityEngine;

/// <summary>Shows scene objects only after a gameplay signal has been raised.</summary>
[DisallowMultipleComponent]
public class ActivateOnSignal : MonoBehaviour
{
    [SerializeField] private string activationSignal = "gas_interlude_complete";
    [SerializeField] private GameObject[] targets;

    private void Awake()
    {
        SetTargetsActive(false);
    }

    private void OnEnable()
    {
        if (GameplaySignals.Instance != null)
            GameplaySignals.Instance.SignalRaised += OnSignalRaised;
    }

    private void Start()
    {
        if (GameplaySignals.Instance == null) return;
        GameplaySignals.Instance.SignalRaised -= OnSignalRaised;
        GameplaySignals.Instance.SignalRaised += OnSignalRaised;
        if (GameplaySignals.Instance.HasRaised(activationSignal))
            SetTargetsActive(true);
    }

    private void OnSignalRaised(string signalId)
    {
        if (signalId == activationSignal) SetTargetsActive(true);
    }

    private void SetTargetsActive(bool active)
    {
        if (targets == null) return;
        foreach (GameObject target in targets)
        {
            if (target == null) continue;
            if (target == gameObject || transform.IsChildOf(target.transform))
            {
                Debug.LogWarning("ActivateOnSignal cannot disable its own object or an ancestor.", this);
                continue;
            }
            target.SetActive(active);
            if (active)
                target.GetComponent<MonitorDiscoveryTarget>()?.Discover();
        }
    }

    private void OnDisable()
    {
        if (GameplaySignals.Instance != null)
            GameplaySignals.Instance.SignalRaised -= OnSignalRaised;
    }
}
