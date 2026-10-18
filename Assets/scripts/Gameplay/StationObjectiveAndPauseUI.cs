using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>Contextual inner thoughts and the in-game pause/settings menu.</summary>
[DefaultExecutionOrder(-900)]
[DisallowMultipleComponent]
public sealed class StationObjectiveAndPauseUI : MonoBehaviour
{
    private enum MenuPage { Pause, Audio }

    private PlayerInteractor interactor;
    private playerController player;
    private FirstPersonCameraController look;
    private GameplaySignals signals;
    private CanvasGroup actionGroup;
    private Text actionText;
    private Text openingControlsText;
    private Text escapeHintText;
    private PressureTimingPuzzle gasPanel;
    private SequencePuzzle powerPanel;
    private bool introShown;
    private float actionShownAt;
    private readonly HashSet<string> shownSteps = new HashSet<string>();
    private StationMoodLighting moodLighting;
    private GameObject pausePanel;
    private GameObject pausePage, audioPage;
    private Texture2D leftArrowTexture, rightArrowTexture;
    private Sprite leftArrowSprite, rightArrowSprite;
    private Sprite menuPanelSprite, menuButtonSprite;
    private TextMeshProUGUI volumeValueText;
    private MenuPage currentPage;
    private bool paused;
    private bool previousPlayerInput, previousLookInput, previousInteractorInput;
    private bool previousCursorVisible, previousAudioPause;
    private CursorLockMode previousCursorLock;
    private float previousTimeScale;

    private void Awake()
    {
        interactor = GetComponent<PlayerInteractor>();
        player = GetComponentInParent<playerController>();
        look = player != null ? player.GetComponentInChildren<FirstPersonCameraController>(true) : null;
        BuildObjective();
        BuildPauseMenu();
        if (GetComponent<AnomalyArrivalFeedback>() == null)
            gameObject.AddComponent<AnomalyArrivalFeedback>();
    }

    private void Start()
    {
        SubscribeSignals();
        gasPanel = FindAnyObjectByType<PressureTimingPuzzle>();
        powerPanel = FindAnyObjectByType<SequencePuzzle>();
    }

    private void Update()
    {
        if (signals == null) SubscribeSignals();
        if (!introShown && ShiftStartMenu.ShiftStarted)
        {
            introShown = true;
            ShowAction("I should read the handbook first. The arrow will point me to it.");
            PulseRoom(StationMoodLighting.Zone.ControlRoom);
        }
        UpdateStepHints();
        if (actionGroup != null)
        {
            bool visible = introShown && !paused && !OptionalPropActivities.Busy
                && ReferencePictureView.ActiveView == null
                && (interactor != null && interactor.InputEnabled
                    || MonitorViewManager.ActiveSession != null || SequencePuzzle.IsPanelOpen);
            actionGroup.alpha = visible ? 1f : 0f;
        }
        if (!Application.isFocused || Keyboard.current == null
            || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (paused)
        {
            if (currentPage == MenuPage.Pause) Resume();
            else ShowPage(MenuPage.Pause);
        }
        else if (CanPause()) Pause();
    }

    private void LateUpdate()
    {
        bool show = ShiftStartMenu.ShiftStarted && !ShiftAudioDirector.EndingActive;
        MonitorViewManager monitor = MonitorViewManager.ActiveSession;
        if (openingControlsText != null)
            openingControlsText.enabled = show && !Has("handbook_read") && !paused
                && !OptionalPropActivities.Busy && ReferencePictureView.ActiveView == null
                && monitor == null && interactor != null && interactor.InputEnabled;
        if (escapeHintText == null) return;
        string message = show && !paused && (OptionalPropActivities.ReadingNote || monitor != null
            || ReferencePictureView.ActiveView != null)
            ? "ESC — Close" : string.Empty;
        escapeHintText.enabled = message.Length > 0;
        if (escapeHintText.text != message) StationLocalization.Set(escapeHintText, message);
    }

    private bool CanPause()
        => interactor != null && ShiftStartMenu.ShiftStarted && !ShiftAudioDirector.EndingActive
           && (SequencePuzzle.IsPanelOpen || gasPanel != null && gasPanel.IsOpen
               || interactor.InputEnabled && (player == null || player.InputEnabled)
                   && (look == null || look.InputEnabled))
           && MonitorViewManager.ActiveSession == null
           && !MonitorViewManager.HandledEscapeThisFrame
           && !SequencePuzzle.ClosedPanelThisFrame
           && ReferencePictureView.ActiveView == null
           && !OptionalPropActivities.Busy
           && !OptionalPropActivities.ClosedNoteThisFrame
           && Time.timeScale > 0f;

    private void SubscribeSignals()
    {
        GameplaySignals active = GameplaySignals.Instance;
        if (active == null || signals == active) return;
        if (signals != null) signals.SignalRaised -= OnSignalRaised;
        signals = active;
        signals.SignalRaised += OnSignalRaised;
    }

    private void OnSignalRaised(string signal)
    {
        switch (signal)
        {
            case "handbook_read":
                ShowAction("Those pictures on the wall... Is that how this place should look?");
                break;
            case "reference_picture_viewed":
                if (Has("handbook_read") && !Has("corridor_anomaly_appeared"))
                    ShowStep("reference_checked", "I'll see if the camera feeds match.");
                break;
            case "corridor_anomaly_appeared":
                ShowAction("Wait. Where did that picture come from?");
                PulseRoom(StationMoodLighting.Zone.Corridor);
                break;
            case "corridor_item_picked_up":
                if (!Has("corridor_disposed"))
                    ShowStep(signal, "There's a disposal unit in the lab... That should take care of this.");
                break;
            case "corridor_disposed":
                ShowAction("That's done. Back to the monitors.");
                break;
            case "gas_restored":
                ShowAction("The hissing's stopped. I'll check the monitors again.");
                break;
            case "ghost_destroyed":
                ShowAction("It's gone... Better check the monitors.");
                break;
            case "power_restored":
                ShowAction("Finally. Back to the monitors.");
                break;
            case "corridor_interlude_complete":
                ShowAction("What's that hiss... A leak in the control room?");
                PulseRoom(StationMoodLighting.Zone.ControlRoom);
                break;
            case "gas_interlude_complete":
                ShowAction("Did something just move in the shooting room?");
                PulseRoom(StationMoodLighting.Zone.ShootingRoom);
                break;
            case "ghost_interlude_complete":
                ShowAction("Everything's out... The breakers are in the control room.");
                PulseRoom(StationMoodLighting.Zone.ControlRoom);
                break;
            case "power_panel_opened":
                ShowStep(signal, "Let's see where these circuits lead before pulling anything.");
                break;
            case "power_interlude_complete":
                ShowAction("Nothing looks different... Maybe the scanner will pick something up.");
                PulseRoom(StationMoodLighting.Zone.SpecimenRoom);
                break;
            case "cabinet_jar_detected":
            case "table_jar_detected":
                if (!Has("specimen_room_complete"))
                    ShowAction("There it is. This one needs disposing of too.");
                break;
            case "cabinet_jar_disposed":
            case "table_jar_disposed":
                if (Has("cabinet_jar_disposed") && Has("table_jar_disposed"))
                    ShowAction("Nothing now. I'll head back to the monitors.");
                else
                    ShowAction("Still getting a reading... Did I miss one?");
                break;
            case "specimen_room_complete":
                ShowAction("Nothing now. I'll head back to the monitors.");
                break;
            case "final_protocol_unlocked":
                ShowAction("There's still a light by the elevator... Maybe there's a way out.");
                PulseRoom(StationMoodLighting.Zone.Corridor);
                break;
        }
    }

    private void ShowAction(string message)
    {
        if (actionText == null) return;
        StationLocalization.Set(actionText, message);
        actionShownAt = Time.time;
    }

    private bool Has(string signal) => signals != null && signals.HasRaised(signal);

    private void ShowStep(string key, string message)
    {
        if (shownSteps.Add(key)) ShowAction(message);
    }

    private void UpdateStepHints()
    {
        if (!introShown || paused || interactor == null || Time.timeScale <= 0f
            || OptionalPropActivities.Busy || Time.time - actionShownAt < 3.5f) return;
        if (!Has("handbook_read"))
        {
            if (MonitorViewManager.ActiveSession != null)
                ShowStep("handbook_reminder", "I'd better read the handbook first. It's marked by an arrow.");
            return;
        }
        if (Has("power_interlude_complete") && !Has("specimen_room_complete"))
        {
            if (!Has("cabinet_jar_detected") && !Has("table_jar_detected")
                && interactor.HeldItem != null && interactor.HeldItem.ItemId == "scanner")
                ShowStep("scanner_held", "Let's start with the jars in the cabinet and on the table.");
            return;
        }
        if (Has("gas_interlude_complete") && !Has("ghost_destroyed"))
        {
            bool hasGun = interactor.HeldItem != null && interactor.HeldItem.ItemId == "gun"
                || interactor.StowedTool != null && interactor.StowedTool.ItemId == "gun";
            if (!hasGun && MonitorViewManager.ActiveSession == null)
                ShowStep("gun_needed", "I'm not going in empty-handed. There's a gun on the rack.");
            return;
        }
        if (Has("corridor_interlude_complete") && !Has("gas_restored")
            && interactor.FeedbackTarget is PressureTimingPuzzle)
            ShowStep("gas_panel_nearby", "This must be it. Let's get the pressure steady.");
    }

    private void PulseRoom(StationMoodLighting.Zone zone)
    {
        if (moodLighting == null) moodLighting = FindAnyObjectByType<StationMoodLighting>();
        moodLighting?.PulseZone(zone);
    }

    private void Pause()
    {
        previousTimeScale = Time.timeScale;
        previousAudioPause = AudioListener.pause;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        previousInteractorInput = interactor.InputEnabled;
        previousPlayerInput = player != null && player.InputEnabled;
        previousLookInput = look != null && look.InputEnabled;
        paused = true;
        if (player != null) player.InputEnabled = false;
        if (look != null) look.InputEnabled = false;
        interactor.InputEnabled = false;
        powerPanel?.SuspendForPause(true);
        gasPanel?.SuspendForPause(true);
        Time.timeScale = 0f;
        // Keep ambient audio audible so volume changes can be judged immediately.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        pausePanel.SetActive(true);
        ShowPage(MenuPage.Pause);
    }

    private void Resume()
    {
        if (!paused) return;
        paused = false;
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = previousTimeScale;
        powerPanel?.SuspendForPause(false);
        gasPanel?.SuspendForPause(false);
        AudioListener.pause = previousAudioPause;
        if (player != null) player.InputEnabled = previousPlayerInput;
        if (look != null) look.InputEnabled = previousLookInput;
        if (interactor != null) interactor.InputEnabled = previousInteractorInput;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        PlayerPrefs.Save();
    }

    private void ConfirmQuit()
    {
        Resume();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void BuildObjective()
    {
        Canvas canvas = NewCanvas("Objective HUD", 95, false);
        GameObject action = NewImage("Action Cue", canvas.transform,
            new Color(0.012f, 0.028f, 0.043f, 0.78f));
        RectTransform actionRect = action.GetComponent<RectTransform>();
        actionRect.anchorMin = actionRect.anchorMax = actionRect.pivot = new Vector2(0.5f, 0f);
        actionRect.anchoredPosition = new Vector2(0f, 96f);
        actionRect.sizeDelta = new Vector2(1080f, 76f);
        actionGroup = action.AddComponent<CanvasGroup>();
        actionGroup.alpha = 0f;
        actionText = NewText("Action Cue Text", action.transform, string.Empty, 29,
            new Color(0.91f, 0.96f, 1f), TextAnchor.MiddleCenter);
        Place(actionText.rectTransform, Vector2.zero, new Vector2(1010f, 64f));

        Canvas controlsCanvas = NewCanvas("Context Controls", 450, false);
        openingControlsText = NewText("Opening Controls", controlsCanvas.transform,
            "WASD — Move", 26,
            new Color(.88f, .93f, .96f), TextAnchor.MiddleCenter);
        RectTransform openingRect = openingControlsText.rectTransform;
        openingRect.anchorMin = openingRect.anchorMax = new Vector2(.5f, 0f);
        openingRect.pivot = new Vector2(.5f, 0f);
        openingRect.anchoredPosition = new Vector2(0f, 38f);
        openingRect.sizeDelta = new Vector2(1050f, 42f);
        openingControlsText.enabled = false;
        AddHintShadow(openingControlsText);

        escapeHintText = NewText("Escape Hint", controlsCanvas.transform, string.Empty, 24,
            new Color(.86f, .91f, .94f), TextAnchor.MiddleLeft);
        RectTransform escapeRect = escapeHintText.rectTransform;
        escapeRect.anchorMin = escapeRect.anchorMax = escapeRect.pivot = new Vector2(0f, 1f);
        escapeRect.anchoredPosition = new Vector2(38f, -34f);
        escapeRect.sizeDelta = new Vector2(260f, 42f);
        escapeHintText.enabled = false;
        AddHintShadow(escapeHintText);
    }

    private static void AddHintShadow(Text text)
    {
        Shadow shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, .9f);
        shadow.effectDistance = new Vector2(1f, -2f);
    }

    private void BuildPauseMenu()
    {
        LoadMenuArt();
        Canvas canvas = NewCanvas("Pause Menu", 400, true);
        pausePanel = NewImage("Pause Backdrop", canvas.transform, new Color(0f, 0f, 0f, 0.58f));
        pausePanel.GetComponent<Image>().raycastTarget = true;
        Stretch(pausePanel.GetComponent<RectTransform>());

        pausePage = NewMenuCard("Pause Page", "PAUSED", 120f);
        NewMenuButton("Resume", pausePage.transform, "RESUME", 12f, Resume);
        NewMenuButton("Exit", pausePage.transform, "EXIT GAME", -94f,
            ConfirmQuit);
        NewChevronButton("Next Page", pausePage.transform, true,
            () => ShowPage(MenuPage.Audio));

        audioPage = NewMenuCard("Audio Page", "AUDIO");
        NewMenuText("Volume Label", audioPage.transform, "MASTER VOLUME", 23,
            new Color(0.76f, 0.84f, 0.88f), TextAlignmentOptions.Left,
            new Vector2(-85f, 13f), new Vector2(350f, 38f));
        volumeValueText = NewMenuText("Volume Value", audioPage.transform, "100%", 23,
            Color.white, TextAlignmentOptions.Right,
            new Vector2(224f, -36f), new Vector2(85f, 38f));
        BuildVolumeSlider(audioPage.transform);
        NewChevronButton("Previous Page", audioPage.transform, false,
            () => ShowPage(MenuPage.Pause));

        ShowPage(MenuPage.Pause);
        pausePanel.SetActive(false);
    }

    private void ShowPage(MenuPage page)
    {
        currentPage = page;
        if (pausePage != null) pausePage.SetActive(page == MenuPage.Pause);
        if (audioPage != null) audioPage.SetActive(page == MenuPage.Audio);
    }

    private GameObject NewMenuCard(string label, string title, float titleY = 96f)
    {
        GameObject card = NewImage(label, pausePanel.transform, Color.white);
        card.GetComponent<Image>().sprite = menuPanelSprite;
        Place(card.GetComponent<RectTransform>(), Vector2.zero, new Vector2(824f, 502f));
        NewMenuText("Title", card.transform, title, 42, new Color(0.82f, 0.94f, 1f),
            TextAlignmentOptions.Center, new Vector2(0f, titleY), new Vector2(550f, 60f));
        return card;
    }

    private void LoadMenuArt()
    {
        menuPanelSprite = Resources.Load<Sprite>("PauseUI/MenuPanel");
        menuButtonSprite = Resources.Load<Sprite>("PauseUI/MenuButton");
        if (menuPanelSprite == null || menuButtonSprite == null)
            Debug.LogError("Pause menu artwork failed to load from Resources/PauseUI.", this);
    }

    private void BuildVolumeSlider(Transform parent)
    {
        GameObject holder = new GameObject("Volume Slider", typeof(RectTransform), typeof(Image), typeof(Slider));
        holder.transform.SetParent(parent, false);
        Place(holder.GetComponent<RectTransform>(), new Vector2(-30f, -40f), new Vector2(390f, 44f));
        Image hitArea = holder.GetComponent<Image>();
        hitArea.color = Color.clear;
        hitArea.raycastTarget = true;
        Slider slider = holder.GetComponent<Slider>();
        GameObject track = NewImage("Track", holder.transform, new Color(0.045f, 0.12f, 0.17f));
        StretchWithMargins(track.GetComponent<RectTransform>(), new Vector2(8f, 18f));
        UnityEngine.UI.Outline trackEdge = track.AddComponent<UnityEngine.UI.Outline>();
        trackEdge.effectColor = new Color(0.48f, 0.68f, 0.77f, 0.8f);
        trackEdge.effectDistance = new Vector2(1f, -1f);
        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(holder.transform, false);
        StretchWithMargins(fillArea.GetComponent<RectTransform>(), new Vector2(8f, 18f));
        GameObject fill = NewImage("Fill", fillArea.transform, new Color(0.10f, 0.72f, 0.92f));
        Stretch(fill.GetComponent<RectTransform>());
        GameObject handleArea = new GameObject("Handle Area", typeof(RectTransform));
        handleArea.transform.SetParent(holder.transform, false);
        StretchWithMargins(handleArea.GetComponent<RectTransform>(), new Vector2(9f, 0f));
        GameObject handle = NewImage("Handle", handleArea.transform, new Color(0.80f, 0.95f, 1f));
        Place(handle.GetComponent<RectTransform>(), Vector2.zero, new Vector2(22f, 22f));
        UnityEngine.UI.Outline handleEdge = handle.AddComponent<UnityEngine.UI.Outline>();
        handleEdge.effectColor = new Color(0.17f, 0.60f, 0.83f);
        handleEdge.effectDistance = new Vector2(2f, -2f);
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handle.GetComponent<RectTransform>();
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        AudioListener.volume = PlayerPrefs.GetFloat("ZeroDuty.MasterVolume", AudioListener.volume);
        slider.SetValueWithoutNotify(AudioListener.volume);
        StationLocalization.Set(volumeValueText, Mathf.RoundToInt(AudioListener.volume * 100f) + "%");
        slider.onValueChanged.AddListener(value =>
        {
            AudioListener.volume = value;
            PlayerPrefs.SetFloat("ZeroDuty.MasterVolume", value);
            StationLocalization.Set(volumeValueText, Mathf.RoundToInt(value * 100f) + "%");
        });
    }

    private Canvas NewCanvas(string label, int order, bool clickable)
    {
        GameObject go = clickable
            ? new GameObject(label, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))
            : new GameObject(label, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static GameObject NewImage(string label, Transform parent, Color color)
    {
        GameObject go = new GameObject(label, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return go;
    }

    private static Text NewText(string label, Transform parent, string content, int size,
        Color color, TextAnchor alignment)
    {
        GameObject go = new GameObject(label, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = StationTypography.UIFont;
        StationLocalization.Set(text, content);
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.raycastTarget = false;
        return text;
    }

    private static TextMeshProUGUI NewMenuText(string label, Transform parent, string content,
        int size, Color color, TextAlignmentOptions alignment, Vector2 position, Vector2 dimensions)
    {
        GameObject go = new GameObject(label, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), position, dimensions);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = StationTypography.UIAsset;
        StationLocalization.Set(text, content);
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private Button NewMenuButton(string label, Transform parent, string content,
        float y, UnityEngine.Events.UnityAction action, float scale = 1f)
    {
        GameObject go = NewImage(label, parent, Color.clear);
        Place(go.GetComponent<RectTransform>(), new Vector2(0f, y),
            new Vector2(376f, 70f) * scale);
        go.GetComponent<Image>().raycastTarget = true;
        GameObject art = NewImage("Button Art", go.transform,
            label == "Resume" ? Color.white : new Color(0.67f, 0.77f, 0.84f));
        Place(art.GetComponent<RectTransform>(), Vector2.zero,
            new Vector2(404f, 136f) * scale);
        Image image = art.GetComponent<Image>();
        image.sprite = menuButtonSprite;
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        colors.pressedColor = new Color(0.68f, 0.88f, 1f);
        button.colors = colors;
        NewMenuText("Label", go.transform, content, Mathf.RoundToInt(25f * scale), Color.white,
            TextAlignmentOptions.Center, Vector2.zero,
            new Vector2(352f, 62f) * scale);
        button.onClick.AddListener(action);
        return button;
    }

    private Button NewChevronButton(string label, Transform parent, bool right,
        UnityEngine.Events.UnityAction action)
    {
        Sprite sprite = GetChevronSprite(right);
        GameObject go = NewImage(label, parent, Color.white);
        Place(go.GetComponent<RectTransform>(), new Vector2(right ? 420f : -420f, 0f),
            new Vector2(70f, 108f));
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = true;
        image.alphaHitTestMinimumThreshold = 0.1f;
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.79f, 0.94f, 1f);
        colors.pressedColor = new Color(0.54f, 0.78f, 0.9f);
        button.colors = colors;
        NewMenuText("Chevron", go.transform, right ? "›" : "‹", 66,
            new Color(0.89f, 0.96f, 1f), TextAlignmentOptions.Center,
            new Vector2(right ? 6f : -6f, 0f), new Vector2(60f, 92f));
        button.onClick.AddListener(action);
        return button;
    }

    private Sprite GetChevronSprite(bool right)
    {
        Sprite existing = right ? rightArrowSprite : leftArrowSprite;
        if (existing != null) return existing;
        const int width = 70, height = 108;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        Color32 fill = new Color32(10, 22, 29, 245);
        Color32 edge = new Color32(150, 184, 197, 225);
        Color32 clear = new Color32(0, 0, 0, 0);
        for (int y = 0; y < height; y++)
        {
            float v = y / (float)(height - 1);
            float side = v < 0.28f ? v / 0.28f
                : v > 0.72f ? (1f - v) / 0.28f : 1f;
            float outer = 0.58f + 0.42f * side;
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)(width - 1);
                if (!right) u = 1f - u;
                bool inside = u <= outer;
                bool border = inside && (u < 0.045f || u > outer - 0.045f
                    || v < 0.035f || v > 0.965f);
                texture.SetPixel(x, y, inside ? border ? edge : fill : clear);
            }
        }
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f), 100f);
        if (right) { rightArrowTexture = texture; rightArrowSprite = sprite; }
        else { leftArrowTexture = texture; leftArrowSprite = sprite; }
        return sprite;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void StretchWithMargins(RectTransform rect, Vector2 margin)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = margin;
        rect.offsetMax = -margin;
    }

    private void OnDisable()
    {
        if (paused) Resume();
        PlayerPrefs.Save();
        if (signals != null) signals.SignalRaised -= OnSignalRaised;
        signals = null;
    }

    private void OnDestroy()
    {
        if (leftArrowSprite != null) Destroy(leftArrowSprite);
        if (rightArrowSprite != null) Destroy(rightArrowSprite);
        if (leftArrowTexture != null) Destroy(leftArrowTexture);
        if (rightArrowTexture != null) Destroy(rightArrowTexture);
    }
}
