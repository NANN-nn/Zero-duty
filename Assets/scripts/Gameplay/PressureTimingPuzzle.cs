using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Hold to raise pressure, release to lower it, and stabilize three channels.</summary>
[DisallowMultipleComponent]
public class PressureTimingPuzzle : MonoBehaviour, IPlayerInteractable
{
    [Header("Temporary UI (replace Image sprites later)")]
    [SerializeField] private CanvasGroup puzzleUI;
    [Tooltip("Recommended: use a Unity UI Slider as the moving pressure needle.")]
    [SerializeField] private Slider pressureSlider;
    [Tooltip("Green Image under the Slider's Background, or under Track in the older setup.")]
    [SerializeField] private RectTransform safeZone;
    [Header("Optional older Image setup (not needed with Slider)")]
    [SerializeField] private RectTransform track;
    [SerializeField] private RectTransform needle;
    [SerializeField] private Image progressFill;

    [Header("Timing")]
    [SerializeField, Range(0f, 1f)] private float safeStart = 0.4f;
    [SerializeField, Range(0f, 1f)] private float safeEnd = 0.6f;
    [SerializeField, Min(0.05f)] private float pressureRiseSpeed = 0.35f;
    [SerializeField, Min(0.05f)] private float pressureFallSpeed = 0.27f;
    [SerializeField, Min(0.1f)] private float stabilitySecondsRequired = 1.5f;
    [SerializeField, Min(1)] private int successesNeeded = 3;

    [Header("Stage")]
    [Tooltip("Leave empty for the first test. Later, use a signal to unlock the gas fault.")]
    [SerializeField] private string activationSignal;
    [SerializeField] private string completedSignal = "gas_restored";
    [SerializeField] private GameObject[] faultVisuals;
    [SerializeField] private GameObject[] normalVisuals;

    public string InteractionPrompt => "Stabilize gas pressure";
    public bool FaultActive { get; private set; }
    public bool Completed { get; private set; }
    public bool IsOpen => activeInteractor != null;

    private PlayerInteractor activeInteractor;
    private playerController activePlayer;
    private FirstPersonCameraController activeLook;
    private bool savedPlayerInput;
    private bool savedLookInput;
    private bool savedInteractorInput;
    private CursorLockMode savedCursorLock;
    private bool savedCursorVisible;
    private float needlePosition;
    private int successes;
    private bool buttonHeld;
    private GasPressureState pressureState;
    private PuzzlePanelSkin panelSkin;
    private PickupItem hiddenHeldItem;
    private AudioSource[] leakSounds;

    private void Awake()
    {
        BuildLeakSounds();
        SetUI(false);
        if (pressureSlider != null)
        {
            pressureSlider.minValue = 0f;
            pressureSlider.maxValue = 1f;
            pressureSlider.wholeNumbers = false;
            pressureSlider.interactable = false;
            pressureSlider.transition = Selectable.Transition.None;
        }
        UpdateLayout();
        UpdateProgress();
        if (puzzleUI != null)
        {
            panelSkin = puzzleUI.GetComponent<PuzzlePanelSkin>();
            if (panelSkin == null) panelSkin = puzzleUI.gameObject.AddComponent<PuzzlePanelSkin>();
            panelSkin.ConfigureGas(puzzleUI, pressureSlider);
            if (panelSkin.GasButton != null)
            {
                // Match the visible green interval in GasPanel.png.
                safeStart = 0.36f;
                safeEnd = 0.64f;
                panelSkin.GasHoldChanged += OnHoldChanged;
            }
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
        }
        SetFaultActive(string.IsNullOrWhiteSpace(activationSignal)
            || GameplaySignals.Instance != null && GameplaySignals.Instance.HasRaised(activationSignal));
    }

    private void OnSignalRaised(string signalId)
    {
        if (!Completed && signalId == activationSignal) SetFaultActive(true);
    }

    public bool CanInteract(PlayerInteractor interactor)
        => FaultActive && !Completed && activeInteractor == null && interactor != null
            && puzzleUI != null && safeZone != null
            && (pressureSlider != null || track != null && needle != null);

    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor)) return;
        activeInteractor = interactor;
        activePlayer = interactor.GetComponentInParent<playerController>();
        activeLook = activePlayer != null
            ? activePlayer.GetComponentInChildren<FirstPersonCameraController>(true)
            : null;
        savedPlayerInput = activePlayer != null && activePlayer.InputEnabled;
        savedLookInput = activeLook != null && activeLook.InputEnabled;
        savedInteractorInput = interactor.InputEnabled;
        savedCursorLock = Cursor.lockState;
        savedCursorVisible = Cursor.visible;
        hiddenHeldItem = interactor.HeldItem;
        if (hiddenHeldItem != null) hiddenHeldItem.SetViewHidden(true);
        if (activePlayer != null) activePlayer.InputEnabled = false;
        if (activeLook != null) activeLook.InputEnabled = false;
        interactor.InputEnabled = false;
        if (pressureState == null)
            pressureState = new GasPressureState(successesNeeded, safeStart, safeEnd,
                stabilitySecondsRequired, pressureRiseSpeed, pressureFallSpeed);
        pressureState.ResetStability();
        buttonHeld = false;
        panelSkin?.ReleaseGasButton();
        needlePosition = pressureState.Pressure;
        successes = pressureState.CompletedChannels;
        UpdateLayout();
        UpdateProgress();
        UpdateStabilityDisplay();
        SetUI(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        if (ShiftAudioDirector.EndingActive) SetLeakAudio(false);
        if (activeInteractor == null) return;
        if (ShiftAudioDirector.EndingActive || Time.timeScale <= 0f) return;
        if (!Application.isFocused || Mouse.current == null || !Mouse.current.leftButton.isPressed)
        {
            buttonHeld = false;
            panelSkin?.ReleaseGasButton();
        }
        if (!Application.isFocused) return;
        bool channelFinished = pressureState.Advance(Time.deltaTime, buttonHeld);
        needlePosition = pressureState.Pressure;
        successes = pressureState.CompletedChannels;
        MoveNeedle();
        UpdateProgress();
        UpdateStabilityDisplay();
        if (leakSounds != null)
            foreach (AudioSource sound in leakSounds)
                if (sound != null) sound.volume = buttonHeld ? .72f : .58f;
        if (!channelFinished) return;
        ShiftAudioDirector.PlayGasPress(true);
        buttonHeld = false;
        panelSkin?.ReleaseGasButton();
        if (pressureState.Complete)
        {
            Completed = true;
            SetFaultActive(false);
            GameplaySignals.Instance?.Raise(completedSignal);
            Close();
        }
    }

    private void OnHoldChanged(bool held)
    {
        buttonHeld = held && activeInteractor != null && !Completed && Time.timeScale > 0f;
        if (buttonHeld)
        {
            panelSkin?.PulseGasButton();
            ShiftAudioDirector.PlayGasPress(true);
        }
    }

    public void SuspendForPause(bool paused)
    {
        if (activeInteractor == null || puzzleUI == null) return;
        // Release a held button without throwing away the stabilized channels.
        // Resuming requires a fresh press rather than charging through the menu.
        if (paused)
        {
            buttonHeld = false;
            panelSkin?.ReleaseGasButton();
        }
        puzzleUI.interactable = !paused;
        puzzleUI.blocksRaycasts = !paused;
    }

    private void UpdateStabilityDisplay()
        => panelSkin?.SetGasStability(pressureState != null ? pressureState.StableSeconds : 0f,
            stabilitySecondsRequired, pressureState != null && pressureState.InSafeZone);

    private void UpdateLayout()
    {
        if (safeZone != null)
        {
            safeZone.anchorMin = new Vector2(safeStart, 0f);
            safeZone.anchorMax = new Vector2(safeEnd, 1f);
            safeZone.offsetMin = Vector2.zero;
            safeZone.offsetMax = Vector2.zero;
        }
        MoveNeedle();
    }

    private void MoveNeedle()
    {
        panelSkin?.SetGasNeedlePosition(needlePosition);
        if (pressureSlider != null)
        {
            pressureSlider.value = needlePosition;
            return;
        }
        if (needle == null) return;
        needle.anchorMin = new Vector2(needlePosition, 0.5f);
        needle.anchorMax = needle.anchorMin;
        needle.anchoredPosition = Vector2.zero;
    }

    private void UpdateProgress()
    {
        panelSkin?.SetGasProgress(successes);
        if (progressFill != null)
            progressFill.fillAmount = (float)successes / Mathf.Max(1, successesNeeded);
    }

    private void SetFaultActive(bool active)
    {
        FaultActive = active && !Completed;
        if (faultVisuals != null)
            foreach (GameObject visual in faultVisuals)
                if (visual != null) visual.SetActive(FaultActive);
        if (normalVisuals != null)
            foreach (GameObject visual in normalVisuals)
                if (visual != null) visual.SetActive(!FaultActive);
        SetLeakAudio(FaultActive);
    }

    private void BuildLeakSounds()
    {
        AudioClip clip = Resources.Load<AudioClip>("Monitor/Audio/Enviroment/SFX_GasLeak_Loop")
            ?? ShiftInteractionSounds.Steam;
        int count = faultVisuals != null && faultVisuals.Length > 0 ? faultVisuals.Length : 1;
        leakSounds = new AudioSource[count];
        for (int i = 0; i < count; i++)
        {
            Transform sourcePosition = faultVisuals != null && i < faultVisuals.Length
                && faultVisuals[i] != null ? faultVisuals[i].transform : transform;
            GameObject holder = new GameObject("Gas Leak Hiss " + (i + 1));
            holder.transform.SetParent(sourcePosition, false);
            AudioSource sound = holder.AddComponent<AudioSource>();
            sound.clip = clip;
            sound.playOnAwake = false;
            sound.loop = true;
            sound.spatialBlend = 1f;
            sound.rolloffMode = AudioRolloffMode.Linear;
            sound.minDistance = 2f;
            sound.maxDistance = 14f;
            sound.volume = .65f;
            sound.dopplerLevel = 0f;
            leakSounds[i] = sound;
        }
    }

    private void SetLeakAudio(bool active)
    {
        if (leakSounds == null) return;
        foreach (AudioSource sound in leakSounds)
        {
            if (sound == null) continue;
            if (active && sound.isActiveAndEnabled && !sound.isPlaying)
            {
                if (sound.clip != null) sound.time = Random.Range(0f, sound.clip.length * .7f);
                sound.Play();
            }
            else if (!active && sound.isPlaying) sound.Stop();
        }
    }

    private void SetUI(bool visible)
    {
        if (puzzleUI == null) return;
        puzzleUI.alpha = visible ? 1f : 0f;
        puzzleUI.interactable = visible;
        puzzleUI.blocksRaycasts = visible;
    }

    private void Close()
    {
        buttonHeld = false;
        panelSkin?.ReleaseGasButton();
        pressureState?.ResetStability();
        SetUI(false);
        if (hiddenHeldItem != null) hiddenHeldItem.SetViewHidden(false);
        hiddenHeldItem = null;
        if (activePlayer != null) activePlayer.InputEnabled = savedPlayerInput;
        if (activeLook != null) activeLook.InputEnabled = savedLookInput;
        if (activeInteractor != null) activeInteractor.InputEnabled = savedInteractorInput;
        activeInteractor = null;
        activePlayer = null;
        activeLook = null;
        Cursor.lockState = savedCursorLock;
        Cursor.visible = savedCursorVisible;
    }

    private void OnDisable()
    {
        SetLeakAudio(false);
        if (GameplaySignals.Instance != null)
            GameplaySignals.Instance.SignalRaised -= OnSignalRaised;
        if (activeInteractor != null) Close();
    }

    private void OnDestroy()
    {
        if (panelSkin != null) panelSkin.GasHoldChanged -= OnHoldChanged;
        if (leakSounds != null)
            foreach (AudioSource sound in leakSounds)
                if (sound != null) Destroy(sound.gameObject);
    }
}
