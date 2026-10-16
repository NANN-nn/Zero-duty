using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Start overlay for the first scene. It is created at runtime, so the player's
/// existing (possibly unsaved) scene layout does not need to be rewritten.
/// </summary>
public sealed class ShiftStartMenu : MonoBehaviour
{
    private const string VolumeKey = "ZeroDuty.MasterVolume";
    private const float ArtworkWidth = 1672f;
    private const float ArtworkHeight = 941f;

    private static ShiftStartMenu instance;
    private Canvas canvas;
    private RectTransform visualRoot;
    private RectTransform chairLayer;
    private RectTransform mugLayer;
    private Image roomLightDip;
    private RawImage ceilingLampShade;
    private RawImage rearLampShade;
    private RawImage warmLampShade;
    private RawImage ceilingLightGlow;
    private RawImage rearLightGlow;
    private RawImage warmLightGlow;
    private Texture2D lightGlowTexture;
    private float menuOpenedAt;
    private float nextColdFlicker;
    private float coldFlickerUntil;
    private float nextWarmFlicker;
    private float warmFlickerUntil;
    private readonly float[] nextFeedFlicker = new float[3];
    private readonly float[] feedFlickerUntil = new float[3];
    private readonly System.Random flickerRandom = new System.Random();
    private GameObject mainPage;
    private GameObject settingsPage;
    private GameObject introductionPage;
    private TMP_FontAsset menuFont;
    private TextMeshProUGUI volumeValue;
    private readonly List<RawImage> feeds = new List<RawImage>();
    private readonly List<RawImage> menuButtonArtwork = new List<RawImage>();
    private readonly List<TextMeshProUGUI> menuButtonLabels = new List<TextMeshProUGUI>();
    private Texture menuArtwork;
    private readonly Dictionary<PlayerInteractor, bool> interactors = new Dictionary<PlayerInteractor, bool>();
    private readonly Dictionary<playerController, bool> players = new Dictionary<playerController, bool>();
    private readonly Dictionary<FirstPersonCameraController, bool> looks =
        new Dictionary<FirstPersonCameraController, bool>();
    private bool visible;
    private bool startedThisRun;

    public static bool ShiftStarted => instance != null && instance.startedThisRun;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        var root = new GameObject("End of Shift Start Menu (Runtime)");
        DontDestroyOnLoad(root);
        root.AddComponent<ShiftStartMenu>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        BuildUI();
        canvas.enabled = false;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "first" && !startedThisRun) OpenMenu();
        else
        {
            visible = false;
            if (canvas != null) canvas.enabled = false;
        }
    }

    private void OpenMenu()
    {
        visible = true;
        canvas.enabled = true;
        ShowPage(mainPage);
        Time.timeScale = 0f;
        AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, AudioListener.volume);
        DisableGameInput();
        ShowCursor();
        menuOpenedAt = Time.unscaledTime;
        nextColdFlicker = RandomInterval(1.8f, 4.2f);
        nextWarmFlicker = RandomInterval(1.2f, 3.1f);
        coldFlickerUntil = warmFlickerUntil = 0f;
        for (int i = 0; i < feeds.Count; i++)
        {
            nextFeedFlicker[i] = RandomInterval(1.1f, 3.7f) + i * .55f;
            feedFlickerUntil[i] = 0f;
        }
    }

    private void Update()
    {
        if (!visible) return;
        // The player's camera may try to lock the cursor during scene startup.
        ShowCursor();

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (settingsPage.activeSelf) ShowPage(mainPage);
        }
        if (introductionPage != null && introductionPage.activeSelf) return;

        // Only the isolated foreground chair changes position. Keep the room,
        // mug and UI fixed, while lamps and screens still flicker in brightness.
        float elapsed = Time.unscaledTime - menuOpenedAt;
        visualRoot.localScale = Vector3.one;
        visualRoot.anchoredPosition = Vector2.zero;
        if (chairLayer != null)
        {
            chairLayer.anchoredPosition = new Vector2(
                13f * Mathf.Sin(elapsed * .92f + .3f),
                4.5f * Mathf.Sin(elapsed * .73f + 1.1f));
            chairLayer.localEulerAngles = new Vector3(0f, 0f,
                .32f * Mathf.Sin(elapsed * .72f));
        }
        if (mugLayer != null) mugLayer.anchoredPosition = Vector2.zero;
        UpdateLightFlicker(elapsed);
        UpdateFeedFlicker(elapsed);
    }

    private void StartShift()
    {
        if (!visible) return;
        ShowPage(introductionPage);
    }

    private void BeginGameplay()
    {
        if (!visible) return;
        ShiftAudioDirector.BeginShift();
        startedThisRun = true;
        visible = false;
        canvas.enabled = false;
        Time.timeScale = 1f;
        RestoreGameInput();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void ExitGame()
    {
        Time.timeScale = 1f;
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void DisableGameInput()
    {
        interactors.Clear();
        players.Clear();
        looks.Clear();
        foreach (var item in FindObjectsByType<PlayerInteractor>())
        {
            interactors[item] = item.InputEnabled;
            item.InputEnabled = false;
        }
        foreach (var item in FindObjectsByType<playerController>())
        {
            players[item] = item.InputEnabled;
            item.InputEnabled = false;
        }
        foreach (var item in FindObjectsByType<FirstPersonCameraController>())
        {
            looks[item] = item.InputEnabled;
            item.InputEnabled = false;
        }
    }

    private void RestoreGameInput()
    {
        foreach (var entry in interactors) if (entry.Key != null) entry.Key.InputEnabled = entry.Value;
        foreach (var entry in players) if (entry.Key != null) entry.Key.InputEnabled = entry.Value;
        foreach (var entry in looks) if (entry.Key != null) entry.Key.InputEnabled = entry.Value;
        interactors.Clear();
        players.Clear();
        looks.Clear();
    }

    private static void ShowCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ShowPage(GameObject page)
    {
        mainPage.SetActive(page == mainPage);
        settingsPage.SetActive(page == settingsPage);
        if (introductionPage != null) introductionPage.SetActive(page == introductionPage);
        ResetButtonHighlights();
    }

    private void ResetButtonHighlights()
    {
        for (int i = 0; i < menuButtonArtwork.Count; i++)
        {
            menuButtonArtwork[i].uvRect = ButtonArtworkUv(false);
            menuButtonLabels[i].color = new Color(.78f, .86f, .9f);
        }
    }

    private void UpdateLightFlicker(float elapsed)
    {
        if (elapsed >= nextColdFlicker)
        {
            coldFlickerUntil = elapsed + RandomInterval(.10f, .25f);
            nextColdFlicker = elapsed + RandomInterval(3.8f, 8.2f);
        }
        if (elapsed >= nextWarmFlicker)
        {
            warmFlickerUntil = elapsed + RandomInterval(.13f, .34f);
            nextWarmFlicker = elapsed + RandomInterval(2.1f, 5.4f);
        }

        float coldPower = elapsed < coldFlickerUntil ? .06f
            : .84f + .16f * Mathf.PerlinNoise(elapsed * 1.5f, .37f);
        float warmPower = elapsed < warmFlickerUntil ? .04f
            : .78f + .22f * Mathf.PerlinNoise(elapsed * 1.2f, 2.1f);
        float breath = .5f + .5f * Mathf.Sin(elapsed * .52f);

        roomLightDip.color = new Color(0f, .015f, .025f,
            .045f + .045f * breath + .22f * (1f - coldPower));
        ceilingLampShade.color = new Color(.01f, .025f, .04f, .94f * (1f - coldPower));
        rearLampShade.color = new Color(.01f, .025f, .04f, .88f * (1f - coldPower));
        warmLampShade.color = new Color(.04f, .02f, .005f, .95f * (1f - warmPower));
        ceilingLightGlow.color = new Color(.61f, .82f, 1f, .31f * coldPower);
        rearLightGlow.color = new Color(.48f, .75f, 1f, .20f * coldPower);
        warmLightGlow.color = new Color(1f, .59f, .22f, .39f * warmPower);
    }

    private void UpdateFeedFlicker(float elapsed)
    {
        for (int i = 0; i < feeds.Count; i++)
        {
            if (elapsed >= nextFeedFlicker[i])
            {
                feedFlickerUntil[i] = elapsed + RandomInterval(.08f, .19f);
                nextFeedFlicker[i] = elapsed + RandomInterval(2.8f, 6.7f);
            }
            float drift = .94f + .04f * Mathf.PerlinNoise(elapsed * 1.8f, i * 4.1f);
            float power = elapsed < feedFlickerUntil[i] ? .34f : drift;
            feeds[i].color = new Color(power * .94f, power * .98f, power, 1f);
        }
    }

    private float RandomInterval(float min, float max)
    {
        return Mathf.Lerp(min, max, (float)flickerRandom.NextDouble());
    }

    private void BuildUI()
    {
        menuFont = StationTypography.UIAsset;

        var root = new GameObject("Start Menu Canvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4000;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var visuals = new GameObject("Slow Moving Artwork", typeof(RectTransform));
        visuals.transform.SetParent(root.transform, false);
        visualRoot = visuals.GetComponent<RectTransform>();
        Stretch(visualRoot);

        Texture2D cleanBackdrop = Resources.Load<Texture2D>("StartMenu/Backdrop-Clean-v1");
        Texture2D chairTexture = Resources.Load<Texture2D>("StartMenu/Chair-Foreground-v1");
        Texture2D mugTexture = Resources.Load<Texture2D>("StartMenu/Mug-Foreground-v1");
        bool hasSeparateProps = cleanBackdrop != null && chairTexture != null && mugTexture != null;
        if (!hasSeparateProps)
            Debug.LogWarning("Start menu foreground layers missing; using the original still backdrop.", this);
        var background = NewRawImage("Menu Backdrop", visualRoot, hasSeparateProps
            ? cleanBackdrop : Resources.Load<Texture2D>("StartMenu/Backdrop-v1"));
        menuArtwork = background.texture;
        Stretch(background.rectTransform);
        background.raycastTarget = true;
        if (hasSeparateProps)
        {
            RawImage chair = NewRawImage("Foreground Chair", visualRoot, chairTexture);
            chairLayer = chair.rectTransform;
            Stretch(chairLayer);
            chair.raycastTarget = false;
            RawImage mug = NewRawImage("Desk Mug", visualRoot, mugTexture);
            mugLayer = mug.rectTransform;
            Stretch(mugLayer);
            mug.raycastTarget = false;
        }

        // Light overlays sit over the painted room, but below the three monitor
        // stills. The screens stay readable when the ceiling lamps briefly dip.
        roomLightDip = NewImage("Room Light Dip", visualRoot, Color.clear);
        PlacePixels(roomLightDip.rectTransform, new Rect(350f, 0f, 1322f, 941f));
        lightGlowTexture = CreateLightGlowTexture();
        ceilingLampShade = AddLightGlow("Ceiling Lamp Shade", new Rect(590f, -65f, 245f, 150f));
        rearLampShade = AddLightGlow("Rear Ceiling Shade", new Rect(960f, 38f, 190f, 125f));
        warmLampShade = AddLightGlow("Warm Side Lamp Shade", new Rect(1480f, -25f, 150f, 130f));
        ceilingLightGlow = AddLightGlow("Ceiling Lamp Glow", new Rect(590f, -65f, 245f, 150f));
        rearLightGlow = AddLightGlow("Rear Ceiling Glow", new Rect(960f, 38f, 190f, 125f));
        warmLightGlow = AddLightGlow("Warm Side Lamp Glow", new Rect(1480f, -25f, 150f, 130f));

        // Fixed painted stills based on the three actual in-game camera angles.
        AddFeed(visualRoot, "CAM 01 / Specimen Room", "Feed-01-v1",
            new Rect(548f, 322f, 310f, 212f));
        AddFeed(visualRoot, "CAM 02 / Corridor", "Feed-02-v1",
            new Rect(910f, 322f, 311f, 212f));
        AddFeed(visualRoot, "CAM 03 / Chamber", "Feed-03-v1",
            new Rect(1270f, 322f, 314f, 212f));

        // Keep the title/button panel stationary while the room on the right
        // breathes. The cut ends inside the dark divider beside the panel.
        const float stationaryPanelPixels = 510f;
        RawImage stationaryPanel = NewRawImage("Stationary Left Menu Panel",
            root.transform, background.texture);
        stationaryPanel.uvRect = new Rect(0f, 0f,
            stationaryPanelPixels / ArtworkWidth, 1f);
        stationaryPanel.raycastTarget = false;
        RectTransform panelRect = stationaryPanel.rectTransform;
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, .5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(
            1920f * stationaryPanelPixels / ArtworkWidth, 0f);

        mainPage = NewPage("Main Page", root.transform);
        NewText("Game Title", mainPage.transform, "END OF SHIFT", 72,
            new Vector2(-640f, 282f), new Vector2(510f, 120f));
        NewText("Tagline", mainPage.transform, "Everything looks normal", 27,
            new Vector2(-640f, 186f), new Vector2(430f, 44f));
        NewButton("Start Shift", mainPage.transform, "START", 34f, StartShift);
        NewButton("Settings", mainPage.transform, "SETTINGS", -73f,
            () => ShowPage(settingsPage));
        NewButton("Exit Game", mainPage.transform, "EXIT GAME", -179f, ExitGame);

        settingsPage = NewPage("Settings Page", root.transform);
        NewText("Settings Title", settingsPage.transform, "SETTINGS", 57,
            new Vector2(-640f, 273f), new Vector2(445f, 88f));
        NewText("Volume Label", settingsPage.transform, "MASTER VOLUME", 25,
            new Vector2(-640f, 127f), new Vector2(420f, 40f));
        BuildVolumeSlider(settingsPage.transform);
        volumeValue = NewText("Volume Value", settingsPage.transform, "100%", 28,
            new Vector2(-640f, -73f), new Vector2(380f, 65f));
        volumeValue.text = Mathf.RoundToInt(PlayerPrefs.GetFloat(VolumeKey,
            AudioListener.volume) * 100f) + "%";
        NewButton("Back From Settings", settingsPage.transform, "BACK", -179f,
            () => ShowPage(mainPage));
        BuildIntroduction(root.transform);
    }

    private void BuildIntroduction(Transform parent)
    {
        introductionPage = NewPage("Night Shift Introduction", parent);
        Image background = NewImage("Introduction Black Background", introductionPage.transform, Color.black);
        Stretch(background.rectTransform);
        background.raycastTarget = true;

        // Only the illustration portion is used from the mockup. All lettering
        // is live UI text in the same font as the rest of the game.
        Texture2D artwork = Resources.Load<Texture2D>("StartMenu/Introduction-v1");
        if (artwork != null)
        {
            RawImage picture = NewRawImage("Introduction Illustration", introductionPage.transform, artwork);
            picture.uvRect = new Rect(0f, 0f, .655f, 1f);
            Place(picture.rectTransform, new Vector2(-331f, 0f), new Vector2(1258f, 1080f));
            picture.raycastTarget = false;
        }
        else Debug.LogError("Opening illustration is missing: StartMenu/Introduction-v1", this);

        TextMeshProUGUI title = NewText("Introduction Title", introductionPage.transform,
            "NIGHT SHIFT", 90f, new Vector2(640f, 337f), new Vector2(530f, 130f));
        title.alignment = TextAlignmentOptions.Left;
        Image rule = NewImage("Introduction Accent", introductionPage.transform,
            new Color(.32f, .61f, .72f));
        Place(rule.rectTransform, new Vector2(415f, 247f), new Vector2(80f, 7f));
        TextMeshProUGUI story = NewText("Introduction Story", introductionPage.transform,
            "The last shift has gone home.\n\n"
            + "You're on your own down here until morning.\n\n"
            + "They said it would be a quiet night.", 38f,
            new Vector2(640f, -3f), new Vector2(530f, 450f));
        story.alignment = TextAlignmentOptions.MidlineLeft;
        story.lineSpacing = 5f;
        story.paragraphSpacing = 16f;
        story.color = new Color(.88f, .90f, .90f);

        Image hit = NewImage("Continue Into Shift", introductionPage.transform,
            new Color(.035f, .065f, .08f, 1f));
        Place(hit.rectTransform, new Vector2(640f, -385f), new Vector2(350f, 70f));
        hit.raycastTarget = true;
        UnityEngine.UI.Outline border = hit.gameObject.AddComponent<UnityEngine.UI.Outline>();
        border.effectColor = new Color(.34f, .65f, .76f, .95f);
        border.effectDistance = new Vector2(1f, -1f);
        Button button = hit.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.55f, 1.55f, 1.55f);
        colors.pressedColor = new Color(.7f, .7f, .7f);
        colors.selectedColor = Color.white;
        button.colors = colors;
        NewText("Continue Label", hit.transform, "CONTINUE", 34f, Vector2.zero, new Vector2(320f, 65f));
        button.onClick.AddListener(() => { ShiftAudioDirector.PlayUiConfirm(); BeginGameplay(); });
        introductionPage.SetActive(false);
    }

    private void AddFeed(Transform parent, string label, string asset, Rect pixels)
    {
        Texture2D texture = Resources.Load<Texture2D>("StartMenu/Feeds/" + asset);
        if (texture == null)
        {
            Debug.LogError("Start menu painted CCTV image missing: " + asset, this);
            return;
        }
        RawImage screen = NewRawImage(label, parent, texture);
        float crop = (pixels.width / pixels.height) / ((float)texture.width / texture.height);
        screen.uvRect = new Rect((1f - crop) * 0.5f, 0f, crop, 1f);
        PlacePixels(screen.rectTransform, pixels);
        screen.raycastTarget = false;
        feeds.Add(screen);
    }

    private RawImage AddLightGlow(string label, Rect pixels)
    {
        RawImage glow = NewRawImage(label, visualRoot, lightGlowTexture);
        PlacePixels(glow.rectTransform, pixels);
        glow.raycastTarget = false;
        return glow;
    }

    private static Texture2D CreateLightGlowTexture()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Start Menu Soft Light Glow (Runtime)";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float nx = (x + .5f - size * .5f) / (size * .5f);
            float ny = (y + .5f - size * .5f) / (size * .5f);
            float falloff = Mathf.Pow(Mathf.Clamp01(1f - nx * nx - ny * ny), 2f);
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, falloff));
        }
        texture.Apply(false, true);
        return texture;
    }

    private void BuildVolumeSlider(Transform parent)
    {
        var holder = new GameObject("Master Volume Slider", typeof(RectTransform),
            typeof(Image), typeof(Slider));
        holder.transform.SetParent(parent, false);
        Place(holder.GetComponent<RectTransform>(), new Vector2(-640f, 34f),
            new Vector2(365f, 58f));
        Image hit = holder.GetComponent<Image>();
        hit.color = Color.clear;
        hit.raycastTarget = true;
        var slider = holder.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        var track = NewImage("Track", holder.transform, new Color(.08f, .18f, .23f));
        Place(track.rectTransform, Vector2.zero, new Vector2(320f, 8f));
        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(holder.transform, false);
        Place(fillArea.GetComponent<RectTransform>(), Vector2.zero, new Vector2(320f, 8f));
        var fill = NewImage("Fill", fillArea.transform, new Color(.28f, .69f, .85f));
        Stretch(fill.rectTransform);
        var handleArea = new GameObject("Handle Area", typeof(RectTransform));
        handleArea.transform.SetParent(holder.transform, false);
        Place(handleArea.GetComponent<RectTransform>(), Vector2.zero, new Vector2(320f, 42f));
        var handle = NewImage("Handle", handleArea.transform, new Color(.83f, .94f, 1f));
        Place(handle.rectTransform, Vector2.zero, new Vector2(14f, 38f));
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.SetValueWithoutNotify(PlayerPrefs.GetFloat(VolumeKey, AudioListener.volume));
        slider.onValueChanged.AddListener(value =>
        {
            AudioListener.volume = value;
            PlayerPrefs.SetFloat(VolumeKey, value);
            if (volumeValue != null)
                volumeValue.text = Mathf.RoundToInt(value * 100f) + "%";
        });
    }

    private static GameObject NewPage(string label, Transform parent)
    {
        var page = new GameObject(label, typeof(RectTransform));
        page.transform.SetParent(parent, false);
        Stretch(page.GetComponent<RectTransform>());
        return page;
    }

    private Button NewButton(string label, Transform parent, string caption,
        float y, UnityEngine.Events.UnityAction action)
    {
        // The painted first button is always bright in the background image.
        // A matching button crop covers it, then swaps to that bright crop on hover.
        RawImage artwork = NewRawImage(label + " Artwork", parent, menuArtwork);
        Place(artwork.rectTransform, new Vector2(-640f, y), new Vector2(395f, 78f));
        artwork.uvRect = ButtonArtworkUv(false);
        artwork.raycastTarget = false;

        var hit = NewImage(label, parent, Color.clear);
        Place(hit.rectTransform, new Vector2(-640f, y), new Vector2(395f, 78f));
        hit.raycastTarget = true;
        Button button = hit.gameObject.AddComponent<Button>();
        TextMeshProUGUI text = NewText("Caption", hit.transform, caption, 31,
            Vector2.zero, new Vector2(360f, 70f));
        text.color = new Color(.78f, .86f, .9f);
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        EventTrigger hover = hit.gameObject.AddComponent<EventTrigger>();
        AddHoverEvent(hover, EventTriggerType.PointerEnter, () =>
        {
            artwork.uvRect = ButtonArtworkUv(true);
            text.color = new Color(.94f, .98f, 1f);
            ShiftAudioDirector.PlayUiSelect();
        });
        AddHoverEvent(hover, EventTriggerType.PointerExit, () =>
        {
            artwork.uvRect = ButtonArtworkUv(false);
            text.color = new Color(.78f, .86f, .9f);
        });
        menuButtonArtwork.Add(artwork);
        menuButtonLabels.Add(text);
        button.onClick.AddListener(() =>
        {
            ShiftAudioDirector.PlayUiConfirm();
            action();
        });
        return button;
    }

    private static Rect ButtonArtworkUv(bool illuminated)
    {
        // Both matching bezel states are already painted in the menu backdrop.
        const float left = 106f;
        const float top = 406f;
        const float darkTop = 499f;
        const float width = 346f;
        const float height = 70f;
        float sourceTop = illuminated ? top : darkTop;
        return new Rect(left / ArtworkWidth,
            1f - (sourceTop + height) / ArtworkHeight,
            width / ArtworkWidth, height / ArtworkHeight);
    }

    private static void AddHoverEvent(EventTrigger trigger, EventTriggerType type,
        UnityEngine.Events.UnityAction action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    private TextMeshProUGUI NewText(string label, Transform parent, string content,
        float size, Vector2 position, Vector2 dimensions, bool autoSize = false)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), position, dimensions);
        var text = go.GetComponent<TextMeshProUGUI>();
        if (menuFont != null) text.font = menuFont;
        text.text = content;
        text.fontSize = size;
        text.enableAutoSizing = autoSize;
        if (autoSize) { text.fontSizeMin = 42; text.fontSizeMax = size; }
        text.color = new Color(.85f, .94f, .98f);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static RawImage NewRawImage(string label, Transform parent, Texture texture)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<RawImage>();
        image.texture = texture;
        return image;
    }

    private static Image NewImage(string label, Transform parent, Color color)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void PlacePixels(RectTransform rect, Rect pixels)
    {
        float scaleX = 1920f / ArtworkWidth;
        float scaleY = 1080f / ArtworkHeight;
        Vector2 center = pixels.center;
        Place(rect, new Vector2(center.x * scaleX - 960f,
            540f - center.y * scaleY),
            new Vector2(pixels.width * scaleX, pixels.height * scaleY));
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
        if (visible) Time.timeScale = 1f;
        if (lightGlowTexture != null) Destroy(lightGlowTexture);
        PlayerPrefs.Save();
    }
}
