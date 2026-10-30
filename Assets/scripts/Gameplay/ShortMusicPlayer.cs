using UnityEngine;

/// <summary>A small, optional one-shot music player in the station.</summary>
[DisallowMultipleComponent]
public class ShortMusicPlayer : MonoBehaviour, IPlayerInteractable
{
    public enum PreviewTrack { QuietBreak_Piano, LittleWaltz_Pluck, LateCoffee_Rhodes }
    [SerializeField] private PreviewTrack previewTrack;
    [Tooltip("Assign the short music clip here when it is ready.")]
    [SerializeField] private AudioClip musicClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.75f;
    [SerializeField, Min(1f)] private float audibleDistance = 7f;

    public string InteractionPrompt => "Toggle music";

    private AudioSource audioSource;
    private GameplaySignals signals;

    private void Awake()
    {
        // Update the existing scene's serialized level without rewriting its layout.
        if (Mathf.Approximately(volume, .55f)) volume = .75f;
        if (musicClip == null)
            musicClip = Resources.Load<AudioClip>("Monitor/Audio/Music/Player_04_UserMusic");
        EnsureCaseCollider();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.minDistance = 1.2f;
        audioSource.maxDistance = audibleDistance;
        audioSource.volume = volume;
    }

    private void EnsureCaseCollider()
    {
        if (GetComponent<BoxCollider>() != null) return;
        // Transform all world-space bounds corners back to the case's local space.
        // This keeps the hit box aligned with the complete rotated equipment case.
        bool hasBounds = false;
        Bounds bounds = default;
        foreach (Renderer part in GetComponentsInChildren<Renderer>(true))
        {
            Bounds world = part.bounds;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = world.center + Vector3.Scale(world.extents,
                    new Vector3(x, y, z));
                Vector3 local = transform.InverseTransformPoint(corner);
                if (!hasBounds) { bounds = new Bounds(local, Vector3.zero); hasBounds = true; }
                else bounds.Encapsulate(local);
            }
        }
        if (!hasBounds) return;
        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        box.center = bounds.center;
        box.size = new Vector3(Mathf.Max(0.05f, bounds.size.x),
            Mathf.Max(0.05f, bounds.size.y), Mathf.Max(0.05f, bounds.size.z));
    }

    private void Start()
    {
        signals = GameplaySignals.Instance;
        if (signals != null) signals.SignalRaised += OnSignalRaised;
    }

    public bool CanInteract(PlayerInteractor interactor) => interactor != null;

    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor)) return;
        ShiftAudioDirector.PlayMusicPlayerButton();
        if (audioSource.isPlaying)
        {
            audioSource.Stop();
            return;
        }

        string[] previews = { "Player_01_QuietBreak_Piano", "Player_02_LittleWaltz_Pluck", "Player_03_LateCoffee_Rhodes" };
        AudioClip selected = musicClip != null ? musicClip
            : Resources.Load<AudioClip>("Monitor/Audio/Music/" + previews[(int)previewTrack]);
        if (selected == null)
        {
            Debug.Log("[Music player] Assign a short AudioClip to Short Music Player in the Inspector.", this);
            return;
        }

        audioSource.clip = selected;
        audioSource.volume = volume;
        audioSource.maxDistance = audibleDistance;
        audioSource.Play();
    }

    private void OnSignalRaised(string signalId)
    {
        if (signalId == "corridor_anomaly_appeared" ||
            signalId == "corridor_interlude_complete" ||
            signalId == "gas_interlude_complete" ||
            signalId == "ghost_interlude_complete" ||
            signalId == "power_interlude_complete" ||
            signalId == "final_protocol_unlocked")
            audioSource.Stop();
    }

    private void Update()
    {
        if (ShiftAudioDirector.EndingActive && audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
    }

    private void OnDisable()
    {
        if (signals != null) signals.SignalRaised -= OnSignalRaised;
        if (audioSource != null) audioSource.Stop();
    }
}
