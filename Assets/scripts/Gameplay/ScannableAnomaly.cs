using UnityEngine;

/// <summary>
/// Separates detection from revelation and pickup eligibility for a specimen.
/// Intact parts and the complete broken assembly switch together to avoid loose effects.
/// </summary>
[DisallowMultipleComponent]
public class ScannableAnomaly : MonoBehaviour
{
    [SerializeField] private GameObject intactVisual;
    [SerializeField] private GameObject[] intactParts;
    [SerializeField] private GameObject brokenVisual;
    [SerializeField] private PickupItem itemAfterReveal;
    [SerializeField] private bool revealWhenDetected;
    [SerializeField] private string activationSignal;
    [SerializeField] private bool hideUntilActivated;
    [SerializeField] private string detectedSignal = "jar_detected";
    [SerializeField] private string revealedSignal = "jar_revealed";

    public bool Detected { get; private set; }
    public bool Revealed { get; private set; }
    public PickupItem RevealedItem => Revealed ? itemAfterReveal : null;
    public bool CanDetect => string.IsNullOrEmpty(activationSignal) ||
        GameplaySignals.Instance != null && GameplaySignals.Instance.HasRaised(activationSignal);

    private void Awake()
    {
        RuntimeColliderUtility.EnsureBoxCollider(gameObject);
        SetIntactVisible(!hideUntilActivated);
        if (brokenVisual != null) brokenVisual.SetActive(false);
        if (itemAfterReveal != null) itemAfterReveal.enabled = false;
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
        if (hideUntilActivated && CanDetect && !Revealed)
            SetIntactVisible(true);
    }

    private void OnSignalRaised(string signalId)
    {
        if (hideUntilActivated && !Revealed && signalId == activationSignal)
            SetIntactVisible(true);
    }

    public void Detect()
    {
        if (!CanDetect || Detected || Revealed) return;
        Detected = true;
        GameplaySignals.Instance?.Raise(detectedSignal);
        if (revealWhenDetected) Reveal();
    }

    public void RevealIfDetected()
    {
        if (Detected) Reveal();
    }

    public void Reveal()
    {
        if (Revealed) return;
        Revealed = true;
        SetIntactVisible(false);
        if (brokenVisual != null) brokenVisual.SetActive(true);
        if (itemAfterReveal != null) itemAfterReveal.enabled = true;
        GameplaySignals.Instance?.Raise(revealedSignal);
    }

    private void SetIntactPartsActive(bool active)
    {
        if (intactParts == null) return;
        foreach (GameObject part in intactParts)
            if (part != null) part.SetActive(active);
    }

    private void SetIntactVisible(bool visible)
    {
        if (intactVisual != null) intactVisual.SetActive(visible);
        SetIntactPartsActive(visible);
    }

    private void OnDisable()
    {
        if (GameplaySignals.Instance != null)
            GameplaySignals.Instance.SignalRaised -= OnSignalRaised;
    }
}
