using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Shared result screen for both exit choices and the time-out failure.</summary>
public sealed class ShiftEndingScreen : MonoBehaviour
{
    private enum Result { DataTransfer, Lockdown, Failure }

    private static ShiftEndingScreen instance;
    private CanvasGroup group;
    private TextMeshProUGUI categoryText, titleText, descriptionText;
    private Image accentLine;
    private Image failureCard;
    private RawImage endingImage;
    private RectTransform captionPanel;
    private RectTransform restartButtonRect, exitButtonRect;
    private bool showing;
    private bool ready;

    public static ShiftEndingScreen Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("Shift Ending Screen (Runtime)")
                    .AddComponent<ShiftEndingScreen>();
            return instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() => instance = null;

    private void Awake()
    {
        instance = this;
        BuildUI();
    }

    public void ShowDataTransfer() => Show(Result.DataTransfer);
    public void ShowLockdown() => Show(Result.Lockdown);
    public void ShowFailure() => Show(Result.Failure);

    private void Show(Result result)
    {
        if (showing) return;
        showing = true;
        ready = false;
        if (result == Result.DataTransfer) ShiftAudioDirector.PlayEndingData();
        else if (result == Result.Lockdown) ShiftAudioDirector.PlayEndingLockdown();
        else ShiftAudioDirector.PlayEndingFailure();
        Color accent;
        switch (result)
        {
            case Result.DataTransfer:
                StationLocalization.Set(categoryText, "FINAL REPORT  /  01");
                StationLocalization.Set(titleText, "DATA TRANSMITTED");
                StationLocalization.Set(descriptionText, "The truth got out. So might the anomaly.");
                accent = new Color(.32f, .77f, .91f);
                break;
            case Result.Lockdown:
                StationLocalization.Set(categoryText, "FINAL REPORT  /  02");
                StationLocalization.Set(titleText, "FACILITY SEALED");
                StationLocalization.Set(descriptionText, "Outside is safe. You're locked in with it.");
                accent = new Color(.83f, .68f, .43f);
                break;
            default:
                StationLocalization.Set(categoryText, "SHIFT REPORT  /  FAILED");
                StationLocalization.Set(titleText, "SHIFT FAILED");
                StationLocalization.Set(descriptionText, "You didn't finish your shift.");
                accent = new Color(.85f, .42f, .42f);
                break;
        }

        accentLine.color = accent;
        string artPath = result == Result.DataTransfer ? "EndingArt/DataTransmitted"
            : result == Result.Lockdown ? "EndingArt/FacilitySealed" : "EndingArt/ShiftFailed-v1";
        Texture2D art = artPath != null ? Resources.Load<Texture2D>(artPath) : null;
        if (artPath != null && art == null)
            Debug.LogWarning($"Ending artwork not found at Resources/{artPath}.", this);
        bool showArt = art != null;
        endingImage.texture = art;
        endingImage.gameObject.SetActive(showArt);
        failureCard.gameObject.SetActive(!showArt);
        PositionCaptions(showArt);
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
        foreach (PlayerInteractor interactor in FindObjectsByType<PlayerInteractor>(FindObjectsSortMode.None))
            interactor.InputEnabled = false;
        foreach (playerController player in FindObjectsByType<playerController>(FindObjectsSortMode.None))
            player.InputEnabled = false;
        foreach (FirstPersonCameraController look in FindObjectsByType<FirstPersonCameraController>(FindObjectsSortMode.None))
            look.InputEnabled = false;
        StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        const float duration = 1.2f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        group.alpha = 1f;
        group.interactable = true;
        ready = true;
    }

    private void Update()
    {
        if (!ready || !Application.isFocused || UnityEngine.InputSystem.Keyboard.current == null) return;
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            RestartShift();
    }

    private void RestartShift()
    {
        if (!ready) return;
        ready = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void QuitGame()
    {
        if (!ready) return;
        ready = false;
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void BuildUI()
    {
        var root = new GameObject("Ending Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;

        Image backdrop = NewImage("Black Backdrop", root.transform, Color.black);
        Stretch(backdrop.rectTransform);
        backdrop.raycastTarget = true;
        group = backdrop.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        // The art is 70% of the 1920x1080 reference frame in each dimension.
        // It sits near the vertical center, leaving a compact caption strip below.
        // RawImage accepts the PNGs as regular textures, without Sprite import settings.
        var artwork = new GameObject("Ending Artwork", typeof(RectTransform), typeof(RawImage));
        artwork.transform.SetParent(backdrop.transform, false);
        endingImage = artwork.GetComponent<RawImage>();
        endingImage.raycastTarget = false;
        endingImage.rectTransform.anchorMin = endingImage.rectTransform.anchorMax =
            endingImage.rectTransform.pivot = new Vector2(.5f, 1f);
        endingImage.rectTransform.anchoredPosition = new Vector2(0f, -120f);
        endingImage.rectTransform.sizeDelta = new Vector2(1344f, 756f);
        artwork.SetActive(false);

        failureCard = NewImage("Failure Report Card", backdrop.transform, Color.white);
        failureCard.sprite = Resources.Load<Sprite>("PauseUI/MenuPanel");
        if (failureCard.sprite == null) failureCard.color = new Color(.035f, .075f, .105f);
        Place(failureCard.rectTransform, Vector2.zero, new Vector2(950f, 575f));
        failureCard.gameObject.SetActive(false);

        var captions = new GameObject("Ending Captions", typeof(RectTransform));
        captions.transform.SetParent(backdrop.transform, false);
        captionPanel = captions.GetComponent<RectTransform>();
        Place(captionPanel, Vector2.zero, new Vector2(1344f, 272f));
        accentLine = NewImage("Result Accent", captionPanel, Color.white);
        Place(accentLine.rectTransform, new Vector2(0f, 130f), new Vector2(410f, 2f));
        categoryText = NewText("Result Type", captionPanel, 20,
            new Vector2(0f, 110f), new Vector2(820f, 28f));
        titleText = NewText("Ending Title", captionPanel, 40,
            new Vector2(0f, 62f), new Vector2(900f, 54f));
        descriptionText = NewText("Ending Description", captionPanel, 23,
            new Vector2(0f, -3f), new Vector2(1040f, 72f));
        descriptionText.lineSpacing = 6f;

        Sprite buttonArt = Resources.Load<Sprite>("PauseUI/MenuButton");
        restartButtonRect = NewButton("Restart Shift", captionPanel, "RESTART SHIFT",
            new Vector2(-175f, -103f), buttonArt, RestartShift).GetComponent<RectTransform>();
        exitButtonRect = NewButton("Exit Game", captionPanel, "EXIT GAME",
            new Vector2(175f, -103f), buttonArt, QuitGame).GetComponent<RectTransform>();
        PositionCaptions(false);
    }

    private void PositionCaptions(bool belowArtwork)
    {
        captionPanel.anchorMin = captionPanel.anchorMax = belowArtwork
            ? new Vector2(.5f, 0f) : new Vector2(.5f, .5f);
        captionPanel.pivot = belowArtwork ? new Vector2(.5f, 0f) : new Vector2(.5f, .5f);
        captionPanel.anchoredPosition = belowArtwork ? new Vector2(0f, 8f) : Vector2.zero;
        captionPanel.sizeDelta = belowArtwork
            ? new Vector2(1344f, 188f) : new Vector2(1344f, 272f);
        if (titleText == null) return;

        categoryText.gameObject.SetActive(!belowArtwork);
        accentLine.gameObject.SetActive(!belowArtwork);
        Place(titleText.rectTransform,
            new Vector2(0f, belowArtwork ? 58f : 62f),
            new Vector2(900f, belowArtwork ? 52f : 54f));
        Place(descriptionText.rectTransform,
            new Vector2(0f, belowArtwork ? 10f : -3f),
            new Vector2(1040f, belowArtwork ? 44f : 72f));
        descriptionText.lineSpacing = belowArtwork ? 0f : 6f;
        Place(restartButtonRect,
            new Vector2(-175f, belowArtwork ? -55f : -103f),
            new Vector2(300f, 58f));
        Place(exitButtonRect,
            new Vector2(175f, belowArtwork ? -55f : -103f),
            new Vector2(300f, 58f));
    }

    private static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, float size,
        Vector2 position, Vector2 dimensions)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), position, dimensions);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = StationTypography.UIAsset;
        text.fontSize = size;
        text.color = new Color(.84f, .92f, .97f);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static Button NewButton(string name, Transform parent, string label,
        Vector2 position, Sprite art, UnityEngine.Events.UnityAction action)
    {
        Image hit = NewImage(name, parent, Color.clear);
        Place(hit.rectTransform, position, new Vector2(300f, 58f));
        hit.raycastTarget = true;
        Image face = NewImage("Button Art", hit.transform, Color.white);
        face.sprite = art;
        face.color = art != null ? Color.white : new Color(.09f, .2f, .27f);
        Place(face.rectTransform, Vector2.zero, new Vector2(314f, 86f));
        Button button = hit.gameObject.AddComponent<Button>();
        button.targetGraphic = face;
        TextMeshProUGUI caption = NewText("Label", hit.transform, 22,
            Vector2.zero, new Vector2(290f, 52f));
        StationLocalization.Set(caption, label);
        button.onClick.AddListener(action);
        return button;
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
        if (showing) Time.timeScale = 1f;
        if (instance == this) instance = null;
    }
}
