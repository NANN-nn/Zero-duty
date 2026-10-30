using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Final two-way choice at the exit control, after every room is resolved.</summary>
[DisallowMultipleComponent]
public class FinalChoiceTerminal : MonoBehaviour, IPlayerInteractable
{
    [SerializeField] private string[] requiredSignals =
    {
        "final_protocol_unlocked"
    };
    [SerializeField] private string dataTransferSignal = "ending_data_transfer";
    [SerializeField] private string lockdownSignal = "ending_facility_lockdown";
    [SerializeField] private PickupItem gun;
    [SerializeField] private PickupItem scanner;

    public string InteractionPrompt => "Use exit control";
    public bool ChoiceMade { get; private set; }
    public bool IsOpen => activeInteractor != null && !ChoiceMade;

    private PlayerInteractor activeInteractor;
    private playerController activePlayer;
    private FirstPersonCameraController activeLook;
    private bool previousPlayerInput;
    private bool previousLookInput;
    private bool previousInteractorInput;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;
    private PickupItem hiddenHeldItem;

    private GameObject runtimeCanvas;
    private CanvasGroup screen;
    private TMP_Text title;
    private TMP_Text description;
    private Button transferButton;
    private Button lockdownButton;
    private TMP_Text thoughtText;
    private float thoughtStartedAt;
    private static readonly string[] Thoughts =
    {
        "Made it this far... and there's still a choice to make?",
        "Send the records. At least someone would know what happened here.",
        "But would the records be the only thing getting out?",
        "Seal this place, and everyone outside stays safe.",
        "But I'm still in here."
    };
    private static readonly float[] ThoughtDurations = { 3.5f, 5f, 4.5f, 4.5f };
#if UNITY_EDITOR
    private bool editorForceReady;
#endif

    private void Awake()
    {
        BuildChoiceUI();
        ShowScreen(false);
    }

    public bool CanInteract(PlayerInteractor interactor)
    {
        if (ChoiceMade || activeInteractor != null || interactor == null
            || MonitorViewManager.ActiveSession != null)
            return false;
#if UNITY_EDITOR
        if (editorForceReady) return true;
#endif
        GameplaySignals signals = GameplaySignals.Instance;
        if (signals == null) return false;
        foreach (string signal in requiredSignals)
            if (string.IsNullOrWhiteSpace(signal) || !signals.HasRaised(signal)) return false;
        return true;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor)) return;
        OpenChoice(interactor);
    }

    private void OpenChoice(PlayerInteractor interactor)
    {
        activeInteractor = interactor;
        activePlayer = interactor.GetComponentInParent<playerController>();
        activeLook = activePlayer != null
            ? activePlayer.GetComponentInChildren<FirstPersonCameraController>(true) : null;
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

        if (title != null) StationLocalization.Set(title, "FINAL PROTOCOL");
        if (description != null) StationLocalization.Set(description, string.Empty);
        thoughtStartedAt = Time.unscaledTime;
        if (thoughtText != null) StationLocalization.Set(thoughtText, Thoughts[0]);
        transferButton.gameObject.SetActive(true);
        lockdownButton.gameObject.SetActive(true);
        ShowScreen(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        if (activeInteractor != null && !ChoiceMade && thoughtText != null)
        {
            float elapsed = Time.unscaledTime - thoughtStartedAt;
            int index = 0;
            while (index < ThoughtDurations.Length && elapsed >= ThoughtDurations[index])
            {
                elapsed -= ThoughtDurations[index];
                index++;
            }
            StationLocalization.Set(thoughtText, Thoughts[index]);
        }
#if UNITY_EDITOR
        if (!ChoiceMade && Keyboard.current != null
            && Keyboard.current.f10Key.wasPressedThisFrame)
        {
            editorForceReady = true;
            Debug.Log("Final choice test unlocked: aim at the lockdown reader and press E.", this);
        }
        if (!ChoiceMade && activeInteractor == null && Application.isFocused
            && Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
        {
            if (MonitorViewManager.ActiveSession != null)
            {
                Debug.Log("Leave the monitor seat before testing the final choice.", this);
            }
            else
            {
                PlayerInteractor interactor = FindAnyObjectByType<PlayerInteractor>();
                if (interactor != null)
                {
                    OpenChoice(interactor);
                    Debug.Log("Final choice test opened with F9; normal unlock conditions were bypassed.", this);
                }
                else Debug.LogWarning("Final choice test: PlayerInteractor was not found.", this);
            }
        }
#endif
        if (activeInteractor != null && !ChoiceMade && Keyboard.current != null
            && Keyboard.current.escapeKey.wasPressedThisFrame)
            CloseChoice();
    }

    private void ChooseDataTransfer()
    {
        if (activeInteractor == null || ChoiceMade) return;
        Finish(dataTransferSignal, true);
    }

    private void ChooseLockdown()
    {
        if (activeInteractor == null || ChoiceMade) return;
        Finish(lockdownSignal, false);
    }

    private void Finish(string signal, bool transfer)
    {
        ChoiceMade = true;
        transferButton.gameObject.SetActive(false);
        lockdownButton.gameObject.SetActive(false);
        GameplaySignals.Instance?.Raise(signal);
        ShowScreen(false);
        if (transfer) ShiftEndingScreen.Instance.ShowDataTransfer();
        else ShiftEndingScreen.Instance.ShowLockdown();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void CloseChoice()
    {
        ShowScreen(false);
        RestoreInput();
    }

    private void RestoreInput()
    {
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
    }

    private void ShowScreen(bool visible)
    {
        if (screen == null) return;
        screen.alpha = visible ? 1f : 0f;
        screen.interactable = visible;
        screen.blocksRaycasts = visible;
    }

    private void BuildChoiceUI()
    {
        runtimeCanvas = new GameObject("Final Choice UI", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = runtimeCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = runtimeCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform backdrop = NewRect("Backdrop", runtimeCanvas.transform);
        Stretch(backdrop);
        Image tint = backdrop.gameObject.AddComponent<Image>();
        // Dim the live room without replacing it with the concept art's backdrop.
        tint.color = new Color(0.015f, 0.025f, 0.04f, 0.55f);
        screen = backdrop.gameObject.AddComponent<CanvasGroup>();

        var panel=NativeInterfaceParts.Plate("Native Final Protocol Panel",backdrop,new Vector2(0,20),new Vector2(1240,660),
            new Color(.025f,.041f,.053f),new Color(.29f,.40f,.45f));
        NativeInterfaceParts.Chassis(panel.transform,new Vector2(1240,660));
        title=NewText("Title",panel.transform,new Vector2(1000,65),new Vector2(0,225),48,TextAlignmentOptions.Center);
        StationLocalization.Set(title,"FINAL PROTOCOL");
        transferButton=NativeChoiceButton(panel.transform,"Data Transfer","SEND RECORDS",-280,new Color(.32f,.66f,.74f),false);
        lockdownButton=NativeChoiceButton(panel.transform,"Facility Lockdown","SEAL THE FACILITY",280,new Color(.77f,.48f,.30f),true);
        transferButton.onClick.AddListener(ChooseDataTransfer);
        lockdownButton.onClick.AddListener(ChooseLockdown);

        thoughtText = NewText("Final Choice Inner Thought", backdrop,
            new Vector2(1740f, 100f), Vector2.zero, 30f, TextAlignmentOptions.Center);
        RectTransform thoughtRect = thoughtText.rectTransform;
        thoughtRect.anchorMin = thoughtRect.anchorMax = new Vector2(0.5f, 0f);
        thoughtRect.pivot = new Vector2(0.5f, 0f);
        thoughtRect.anchoredPosition = new Vector2(0f, 28f);

        if (EventSystem.current == null)
            Debug.LogWarning("Final choice needs an EventSystem for mouse clicks.", this);
    }

    private static Button NativeChoiceButton(Transform parent,string name,string caption,float x,Color accent,bool lockdown)
    {
        var rect=NativeInterfaceParts.Rect(name,parent,new Vector2(x,-35),new Vector2(500,330));
        var hit=rect.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
        var face=NativeInterfaceParts.ButtonFace(rect,new Vector2(500,330),accent);
        var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=face;
        button.navigation=new Navigation{mode=Navigation.Mode.None};
        var colors=button.colors;colors.highlightedColor=new Color(1.45f,1.6f,1.7f);colors.selectedColor=Color.white;button.colors=colors;
        var lamp=NativeInterfaceParts.Plate("Choice indicator",rect,new Vector2(0,115),new Vector2(15,15),accent,new Color(.19f,.26f,.28f));lamp.round=true;
        var icon=NativeInterfaceParts.Rect("Choice symbol",rect,new Vector2(0,25),new Vector2(180,130));
        Color metal=new Color(.42f,.52f,.55f);
        if(lockdown)
        {
            NativeInterfaceParts.Plate("Lock body",icon,new Vector2(0,-17),new Vector2(100,65),new Color(.09f,.13f,.15f),metal);
            NativeInterfaceParts.Bar("Lock left",icon,new Vector2(-31,35),new Vector2(9,50),metal);
            NativeInterfaceParts.Bar("Lock right",icon,new Vector2(31,35),new Vector2(9,50),metal);
            NativeInterfaceParts.Bar("Lock top",icon,new Vector2(0,57),new Vector2(70,9),metal);
            NativeInterfaceParts.Bar("Lock key",icon,new Vector2(0,-17),new Vector2(7,22),accent);
        }
        else
        {
            NativeInterfaceParts.Bar("Data stem",icon,new Vector2(0,18),new Vector2(9,73),metal);
            for(int side=-1;side<=1;side+=2)
            {
                var stroke=NativeInterfaceParts.Bar("Data arrow",icon,new Vector2(side*20,43),new Vector2(55,9),metal);
                stroke.transform.localRotation=Quaternion.Euler(0,0,-side*43);
            }
            NativeInterfaceParts.Bar("Data tray",icon,new Vector2(0,-40),new Vector2(110,8),metal);
            for(int side=-1;side<=1;side+=2)NativeInterfaceParts.Bar("Tray side",icon,new Vector2(side*51,-29),new Vector2(8,28),metal);
        }
        var label=NewText("Label",rect,new Vector2(420,68),new Vector2(0,-100),34,TextAlignmentOptions.Center);
        StationLocalization.Set(label,caption);return button;
    }

    private static RectTransform NewRect(string objectName, Transform parent)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static TMP_Text NewText(string objectName, Transform parent, Vector2 size,
        Vector2 position, float fontSize, TextAlignmentOptions alignment)
    {
        RectTransform rect = NewRect(objectName, parent);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = StationTypography.UIAsset;
        label.fontSize = fontSize;
        label.color = new Color(0.88f, 0.94f, 1f);
        label.alignment = alignment;
        label.enableWordWrapping = true;
        label.raycastTarget = false;
        return label;
    }

    private static Button NewButton(string label, Transform parent, Vector2 position)
    {
        RectTransform rect = NewRect(label, parent);
        rect.sizeDelta = new Vector2(360f, 85f);
        rect.anchoredPosition = position;
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.10f, 0.19f, 0.28f);
        Button button = rect.gameObject.AddComponent<Button>();
        TMP_Text buttonText = NewText("Label", rect, Vector2.zero, Vector2.zero,
            27, TextAlignmentOptions.Center);
        Stretch((RectTransform)buttonText.transform);
        StationLocalization.Set(buttonText, label.ToUpperInvariant());
        return button;
    }

    private static TMP_Text NewPanelText(string objectName, Transform panel, string content,
        Vector2 center, Vector2 size, float fontSize)
    {
        TMP_Text label = NewText(objectName, panel, Vector2.zero, Vector2.zero,
            fontSize, TextAlignmentOptions.Center);
        RectTransform rect = label.rectTransform;
        rect.anchorMin = center - size * .5f;
        rect.anchorMax = center + size * .5f;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        StationLocalization.Set(label, content);
        label.enableAutoSizing = true;
        label.fontSizeMin = fontSize * .8f;
        label.fontSizeMax = fontSize;
        return label;
    }

    private static Button NewArtButton(string label, Transform parent, Vector2 min, Vector2 max,
        Color highlight)
    {
        RectTransform rect = NewRect(label + " Hit Area", parent);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = rect.gameObject.AddComponent<Image>();
        image.color = Color.clear;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.colors = new ColorBlock
        {
            normalColor = Color.clear,
            highlightedColor = new Color(highlight.r, highlight.g, highlight.b, 0.045f),
            pressedColor = new Color(highlight.r, highlight.g, highlight.b, 0.12f),
            selectedColor = Color.clear,
            disabledColor = Color.clear,
            colorMultiplier = 1f,
            fadeDuration = 0.08f
        };
        return button;
    }

    private void OnDisable()
    {
        if (activeInteractor != null) RestoreInput();
        ShowScreen(false);
    }

    private void OnDestroy()
    {
        if (runtimeCanvas != null) Destroy(runtimeCanvas);
    }
}
