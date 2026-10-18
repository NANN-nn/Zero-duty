using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SequencePuzzle : MonoBehaviour, IPlayerInteractable
{
    public static bool IsPanelOpen { get; private set; }
    private static int closedPanelFrame = -1;
    public static bool ClosedPanelThisFrame => closedPanelFrame == Time.frameCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPanelState()
    {
        IsPanelOpen = false;
        closedPanelFrame = -1;
    }

    [SerializeField] private string prompt = "Use control panel";
    [SerializeField] private CanvasGroup puzzleUI;
    [SerializeField] private Button[] buttons = new Button[3];
    [SerializeField] private int[] correctOrder = { 0, 1, 2 };
    [SerializeField] private TMP_Text statusText;
    [Header("故障阶段")]
    [Tooltip("收到该信号后才允许操作；留空则游戏开始即可操作。")]
    [SerializeField] private string activationSignal;
    [SerializeField] private GameObject[] faultVisuals;
    [SerializeField] private GameObject[] restoredVisuals;
    [SerializeField] private string completedSignal;

    public string InteractionPrompt => prompt;
    public bool Completed { get; private set; }
    public bool FaultActive { get; private set; }

    private PlayerInteractor activeInteractor;
    private playerController activePlayer;
    private FirstPersonCameraController activeLook;
    private int progress;
    private bool previousPlayerInput;
    private bool previousLookInput;
    private bool previousInteractorInput;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;
    private PuzzlePanelSkin panelSkin;
    private CircuitRoutingUI circuitRouting;
    private Button[] leverHitAreas;
    private bool circuitReady;
    private PickupItem hiddenHeldItem;

    private void Awake()
    {
        SetUI(false);
        if (correctOrder == null || correctOrder.Length != 3
            || correctOrder[0] < 0 || correctOrder[0] > 2
            || correctOrder[1] < 0 || correctOrder[1] > 2
            || correctOrder[2] < 0 || correctOrder[2] > 2
            || correctOrder[0] == correctOrder[1]
            || correctOrder[0] == correctOrder[2]
            || correctOrder[1] == correctOrder[2])
        {
            Debug.LogWarning($"{name}: Three-button order must contain 0, 1, and 2 once each; using 0-1-2.", this);
            correctOrder = new[] { 0, 1, 2 };
        }
        if (puzzleUI != null)
        {
            panelSkin = puzzleUI.GetComponent<PuzzlePanelSkin>();
            if (panelSkin == null) panelSkin = puzzleUI.gameObject.AddComponent<PuzzlePanelSkin>();
            panelSkin.ConfigurePower(puzzleUI, buttons);
            leverHitAreas = panelSkin.PowerButtons;
            if (leverHitAreas != null)
                for (int i = 0; i < leverHitAreas.Length; i++)
                {
                    int index = i;
                    leverHitAreas[i].onClick.AddListener(() => Press(index));
                }
            circuitRouting = puzzleUI.GetComponent<CircuitRoutingUI>();
            if (circuitRouting == null)
                circuitRouting = puzzleUI.gameObject.AddComponent<CircuitRoutingUI>();
            circuitRouting.Configure(puzzleUI, OnCircuitRouted);
        }
    }

    private void OnEnable()
    {
        if (GameplaySignals.Instance != null)
            GameplaySignals.Instance.SignalRaised += OnSignalRaised;
    }

    private void Start()
    {
        // GameplaySignals may have awakened after this component's OnEnable.
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

    private void SetFaultActive(bool active)
    {
        FaultActive = active && !Completed;
        if (faultVisuals != null)
            foreach (GameObject visual in faultVisuals)
                if (visual != null) visual.SetActive(FaultActive);
        if (restoredVisuals != null)
            foreach (GameObject visual in restoredVisuals)
                if (visual != null) visual.SetActive(!FaultActive);
    }

    public bool CanInteract(PlayerInteractor interactor)
        => FaultActive && !Completed && activeInteractor == null && interactor != null
            && puzzleUI != null
            && leverHitAreas != null && leverHitAreas.Length == 3;

    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor)) return;
        activeInteractor = interactor;
        activePlayer = interactor.GetComponentInParent<playerController>();
        if (activePlayer == null) activePlayer = FindAnyObjectByType<playerController>();
        activeLook = activePlayer != null
            ? activePlayer.GetComponentInChildren<FirstPersonCameraController>(true)
            : null;
        previousPlayerInput = activePlayer != null && activePlayer.InputEnabled;
        previousLookInput = activeLook != null && activeLook.InputEnabled;
        previousInteractorInput = interactor.InputEnabled;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        hiddenHeldItem = interactor.HeldItem;
        if (hiddenHeldItem != null) hiddenHeldItem.SetViewHidden(true);
        if (activePlayer != null) activePlayer.InputEnabled = false;
        if (activeLook != null) activeLook.InputEnabled = false;
        interactor.InputEnabled = false;
        progress = 0;
        panelSkin?.ResetPowerLevers();
        circuitReady = false;
        foreach (Button button in leverHitAreas) button.interactable = false;
        circuitRouting?.BeginRound();
        UpdateStatus(string.Empty);
        SetUI(true);
        IsPanelOpen = true;
        GameplaySignals.Instance?.Raise("power_panel_opened");
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void SuspendForPause(bool paused)
    {
        // ESC is owned by the pause menu. Keep the circuit and lever progress
        // intact until the player completes the repair; never close on Escape.
        if (activeInteractor == null || puzzleUI == null) return;
        puzzleUI.interactable = !paused;
        puzzleUI.blocksRaycasts = !paused;
    }

    private void Press(int index)
    {
        if (activeInteractor == null || Completed || !circuitReady || Time.timeScale <= 0f) return;
        bool correct = index == correctOrder[progress];
        ShiftAudioDirector.PlayPowerSwitch(correct);
        if (correct)
        {
            progress++;
            panelSkin?.SetPowerLeverState(index, true);
            UpdateStatus($"Correct: {progress}/{correctOrder.Length}");
            if (progress >= correctOrder.Length)
            {
                Completed = true;
                SetFaultActive(false);
                GameplaySignals.Instance?.Raise(completedSignal);
                Close(true);
            }
        }
        else
        {
            progress = 0;
            panelSkin?.ResetPowerLevers();
            panelSkin?.PulseWrongLever(index);
            circuitRouting?.ShowLeverMistake();
            UpdateStatus("Wrong order. Try again");
        }
    }

    private void OnCircuitRouted(int[] order)
    {
        if (activeInteractor == null || Completed || order == null || order.Length != 3) return;
        correctOrder = order;
        circuitReady = true;
        foreach (Button button in leverHitAreas) button.interactable = true;
        UpdateStatus(string.Empty);
    }

    private void Close(bool completed)
    {
        closedPanelFrame = Time.frameCount;
        IsPanelOpen = false;
        circuitRouting?.EndRound();
        SetUI(false);
        if (hiddenHeldItem != null) hiddenHeldItem.SetViewHidden(false);
        hiddenHeldItem = null;
        if (activePlayer != null) activePlayer.InputEnabled = previousPlayerInput;
        if (activeLook != null) activeLook.InputEnabled = previousLookInput;
        if (activeInteractor != null) activeInteractor.InputEnabled = previousInteractorInput;
        activeInteractor = null;
        activePlayer = null;
        activeLook = null;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        if (completed) UpdateStatus("System restored");
    }

    private void SetUI(bool visible)
    {
        if (puzzleUI == null) return;
        puzzleUI.alpha = visible ? 1f : 0f;
        puzzleUI.interactable = visible;
        puzzleUI.blocksRaycasts = visible;
    }

    private void UpdateStatus(string value)
    {
        if (statusText != null) StationLocalization.Set(statusText, value);
    }

    private void OnDisable()
    {
        IsPanelOpen = false;
        if (GameplaySignals.Instance != null)
            GameplaySignals.Instance.SignalRaised -= OnSignalRaised;
        if (activeInteractor != null) Close(false);
    }
}
