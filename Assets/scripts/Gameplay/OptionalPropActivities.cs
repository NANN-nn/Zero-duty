using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Per-play collection progress and lightweight prop presentation.</summary>
public class OptionalPropActivities : MonoBehaviour
{
    private static OptionalPropActivities instance;
    public static bool Busy => instance != null && instance.activeInteractor != null
        || RobotDeskCollection.ShowcasePlaying;
    public static bool ReadingNote => instance != null && instance.currentNote != null;
    public static OptionalPropActivities Instance
    {
        get
        {
            if (instance == null) instance = new GameObject("Optional Prop Activities (Runtime)").AddComponent<OptionalPropActivities>();
            return instance;
        }
    }
    public int RobotsCollected { get; private set; }
    public const int RobotGoal = RobotCollectionState.Goal;
    private readonly RobotCollectionState robotCollection = new RobotCollectionState();
    private RobotDeskCollection robotDisplay;
    private Text toast, noteTitle, noteBody, noteRightBody;
    private GameObject notePanel;
    private NativePaperGraphic noteArtwork;
    private RectTransform noteCloseRect;
    private Text handbookRightTitle;
    private Font font, neatHandFont, quickHandFont;
    private float toastUntil;
    private PlayerInteractor activeInteractor;
    private playerController player;
    private FirstPersonCameraController look;
    private PickupItem hiddenItem;
    private bool savedPlayer, savedLook, savedInteractor, savedVisible;
    private CursorLockMode savedLock;
    private OptionalPropInteraction currentNote, consumedProp;
    private Vector3 originalPosition, originalScale;
    private Quaternion originalRotation;
    private Collider[] foodColliders;
    private bool[] foodColliderStates;
    private bool finishedFood;
    private int openedFrame;
    private int noteClosedFrame = -1;
    public static bool ClosedNoteThisFrame => instance != null
        && instance.noteClosedFrame == Time.frameCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState() => instance = null;

    private void Awake()
    {
        instance = this;
        robotDisplay = gameObject.AddComponent<RobotDeskCollection>();
        font = StationTypography.UIFont;
        neatHandFont = Resources.Load<Font>("NoteFonts/PatrickHand/PatrickHand-Regular");
        quickHandFont = Resources.Load<Font>("NoteFonts/Kalam/Kalam-Regular");
        if (neatHandFont == null) neatHandFont = font;
        if (quickHandFont == null) quickHandFont = neatHandFont;
        var root = new GameObject("Optional Props UI", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        root.GetComponent<Canvas>().sortingOrder = 80;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        toast = MakeText("Collection progress", root.transform, 28, Color.white);
        var toastRect = toast.rectTransform;
        toastRect.anchorMin = toastRect.anchorMax = new Vector2(1, 1);
        toastRect.pivot = new Vector2(1, 1);
        toastRect.anchoredPosition = new Vector2(-48, -48);
        toastRect.sizeDelta = new Vector2(650, 90);
        toast.alignment = TextAnchor.UpperRight;
        toast.enabled = false;
        notePanel = Panel("Note Reader", root.transform, new Color(.01f, .02f, .03f, .8f));
        Stretch(notePanel.GetComponent<RectTransform>());
        var page = new GameObject("Native Paper", typeof(RectTransform), typeof(NativePaperGraphic));
        page.transform.SetParent(notePanel.transform, false);
        noteArtwork = page.GetComponent<NativePaperGraphic>();
        noteArtwork.raycastTarget = false;
        noteTitle = MakeText("Title", notePanel.transform, 34, new Color(.07f, .10f, .12f));
        noteTitle.fontStyle = FontStyle.Bold;
        noteBody = MakeText("Body", notePanel.transform, 28, new Color(.07f, .10f, .12f));
        noteRightBody = MakeText("Right Page Body", notePanel.transform, 28,
            new Color(.07f, .10f, .12f));
        var closeObject = new GameObject("Back Chevron", typeof(RectTransform),
            typeof(Image), typeof(Button));
        closeObject.transform.SetParent(notePanel.transform, false);
        noteCloseRect = closeObject.GetComponent<RectTransform>();
        Image closeImage = closeObject.GetComponent<Image>();
        closeImage.color = new Color(.08f, .13f, .17f, .82f);
        Button closeButton = closeObject.GetComponent<Button>();
        closeButton.targetGraphic = closeImage;
        closeButton.onClick.AddListener(CloseNote);
        Text chevron = MakeText("Chevron", closeObject.transform, 27, Color.white);
        chevron.alignment = TextAnchor.MiddleCenter;
        StationLocalization.Set(chevron, "<  BACK");
        Place(chevron.rectTransform, Vector2.zero, new Vector2(150, 56));
        ConfigureNoteLayout(OptionalPropInteraction.NotePaperStyle.Record);
        notePanel.SetActive(false);
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void ConfigureNoteLayout(OptionalPropInteraction.NotePaperStyle style)
    {
        if (handbookRightTitle != null) handbookRightTitle.gameObject.SetActive(false);
        noteBody.supportRichText = noteRightBody.supportRichText = false;
        noteBody.verticalOverflow = noteRightBody.verticalOverflow = VerticalWrapMode.Truncate;
        // Printed/ordinary notes are neat; personal scraps and the handbook's
        // handwritten addendum use a quicker, less formal hand.
        noteTitle.font = style == OptionalPropInteraction.NotePaperStyle.Sticky
            ? quickHandFont : neatHandFont;
        noteBody.font = noteTitle.font;
        noteRightBody.font = style == OptionalPropInteraction.NotePaperStyle.Book
            ? quickHandFont : noteTitle.font;
        noteRightBody.enabled = style == OptionalPropInteraction.NotePaperStyle.Book;
        noteArtwork.color = Color.white;
        noteArtwork.SetStyle(style);
        noteTitle.alignment = TextAnchor.UpperCenter;
        noteBody.alignment = TextAnchor.UpperCenter;
        noteRightBody.alignment = TextAnchor.UpperCenter;
        noteTitle.lineSpacing = 1.1f;
        noteBody.lineSpacing = 1.2f;
        noteRightBody.lineSpacing = 1.2f;
        if (style == OptionalPropInteraction.NotePaperStyle.Book)
        {
            Place(noteArtwork.rectTransform, Vector2.zero, new Vector2(1600, 900));
            Place(noteTitle.rectTransform, new Vector2(-315, 317), new Vector2(495, 62));
            Place(noteBody.rectTransform, new Vector2(-315, -25), new Vector2(495, 620));
            Place(noteRightBody.rectTransform, new Vector2(315, -5), new Vector2(495, 665));
            noteTitle.fontSize = 36; noteBody.fontSize = noteRightBody.fontSize = 29;
            Place(noteCloseRect, new Vector2(0, -490), new Vector2(150, 56));
        }
        else if (style == OptionalPropInteraction.NotePaperStyle.Sticky)
        {
            Place(noteArtwork.rectTransform, Vector2.zero, new Vector2(940, 856));
            Place(noteTitle.rectTransform, new Vector2(0, 270), new Vector2(590, 68));
            Place(noteBody.rectTransform, new Vector2(0, -45), new Vector2(650, 530));
            noteTitle.fontSize = 38; noteBody.fontSize = 35;
            Place(noteCloseRect, new Vector2(0, -470), new Vector2(150, 56));
        }
        else
        {
            Place(noteArtwork.rectTransform, new Vector2(45, 0), new Vector2(720, 850));
            Place(noteTitle.rectTransform, new Vector2(45, 315), new Vector2(500, 65));
            Place(noteBody.rectTransform, new Vector2(45, -25), new Vector2(500, 600));
            noteTitle.fontSize = 36; noteBody.fontSize = 34;
            Place(noteCloseRect, new Vector2(45, -465), new Vector2(150, 56));
        }
    }

    private Text MakeText(string name, Transform parent, int size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>(); text.font = font; text.fontSize = size;
        text.color = color; text.alignment = TextAnchor.UpperLeft; text.raycastTarget = false;
        text.supportRichText = false; text.horizontalOverflow = HorizontalWrapMode.Wrap;
        return text;
    }
    private static GameObject Panel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false); go.GetComponent<Image>().color = color;
        go.GetComponent<Image>().raycastTarget = false; return go;
    }
    private static void Stretch(RectTransform rect)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }

    public void Collect(OptionalPropInteraction robot)
    {
        if (robot == null || robot.Kind != OptionalPropInteraction.PropKind.Robot
            || !robotCollection.TryCollect(robot.GetInstanceID())) return;
        ShiftAudioDirector.PlayPickup();
        RobotsCollected = robotCollection.Count;
        robotDisplay.RecordCollected(RobotsCollected);
        StationLocalization.Set(toast, RobotsCollected >= RobotGoal ? "All 5 robots collected!" : $"Robots collected: {RobotsCollected} / {RobotGoal}");
        toast.enabled = true; toastUntil = Time.unscaledTime + (RobotsCollected >= RobotGoal ? 5f : 2.5f);
        foreach (var collider in robot.GetComponentsInChildren<Collider>()) collider.enabled = false;
        StartCoroutine(CollectAnimation(robot));
    }
    private IEnumerator CollectAnimation(OptionalPropInteraction robot)
    {
        Vector3 start = robot.transform.position, scale = robot.transform.localScale;
        for (float elapsed = 0; elapsed < .3f; elapsed += Time.deltaTime)
        {
            if (robot == null) yield break;
            float t = Mathf.Clamp01(elapsed / .3f);
            robot.transform.position = start + Vector3.up * (.12f * t);
            robot.transform.localScale = scale * (1 - Mathf.SmoothStep(0, 1, t));
            yield return null;
        }
        if (robot != null) robot.gameObject.SetActive(false);
    }

    private void LockPlayer(PlayerInteractor owner)
    {
        activeInteractor = owner;
        player = owner.GetComponentInParent<playerController>();
        look = player != null ? player.GetComponentInChildren<FirstPersonCameraController>() : null;
        savedInteractor = owner.InputEnabled; savedPlayer = player != null && player.InputEnabled;
        savedLook = look != null && look.InputEnabled;
        savedLock = Cursor.lockState; savedVisible = Cursor.visible;
        owner.InputEnabled = false;
        if (player != null) player.InputEnabled = false;
        if (look != null) look.InputEnabled = false;
        hiddenItem = owner.HeldItem;
        if (hiddenItem != null) hiddenItem.SetViewHidden(true);
    }

    public void Read(OptionalPropInteraction note, PlayerInteractor owner)
    {
        ShiftAudioDirector.PlayPageFlip();
        note.EnsureNoteContent();
        LockPlayer(owner); currentNote = note; openedFrame = Time.frameCount;
        OptionalPropInteraction.NotePaperStyle style = note.NoteStyle;
        ConfigureNoteLayout(style);
        StationLocalization.Set(noteTitle, note.NoteTitle);
        string[] bookPages = style == OptionalPropInteraction.NotePaperStyle.Book
            ? (note.NoteBody ?? string.Empty).Split('\f') : null;
        StationLocalization.Set(noteBody, bookPages != null ? bookPages[0] : note.NoteBody);
        StationLocalization.Set(noteRightBody, bookPages != null && bookPages.Length > 1 ? bookPages[1] : string.Empty);
        notePanel.SetActive(true);
        if (note.name == "ShiftLog_Open1") ConfigureHandbookPages();
        else
        {
            FitNoteText(noteBody);
            if (bookPages != null) FitNoteText(noteRightBody);
        }
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }

    private void ConfigureHandbookPages()
    {
        // Both authored pages belong to the same open-book spread. Each text
        // block stays centered on its own sheet and clear of the spine.
        string[] pages = currentNote.NoteBody.Split('\f');
        Place(noteArtwork.rectTransform, Vector2.zero, new Vector2(1760f, 990f));
        Place(noteTitle.rectTransform, new Vector2(-330f, 398f), new Vector2(590f, 58f));
        Place(noteBody.rectTransform, new Vector2(-330f, -10f), new Vector2(590f, 750f));
        Place(noteRightBody.rectTransform, new Vector2(330f, -10f), new Vector2(590f, 750f));
        if (StationLocalization.IsChinese)
        {
            Place(noteBody.rectTransform, new Vector2(-330f, 10f), new Vector2(590f, 690f));
            Place(noteRightBody.rectTransform, new Vector2(330f, 10f), new Vector2(590f, 690f));
        }
        Place(noteCloseRect, new Vector2(0f, -505f), new Vector2(150f, 56f));
        StationLocalization.Set(noteTitle, "NIGHT SHIFT HANDBOOK");
        noteTitle.fontSize = 36;
        noteBody.supportRichText = noteRightBody.supportRichText = true;
        noteBody.alignment = noteRightBody.alignment = TextAnchor.MiddleCenter;
        noteBody.lineSpacing = noteRightBody.lineSpacing = 1.03f;
        noteRightBody.font = neatHandFont;
        noteRightBody.enabled = true;
        StationLocalization.Set(noteBody, FormatHandbookPage(pages[0]));
        StationLocalization.Set(noteRightBody, pages.Length > 1 ? FormatHandbookPage(pages[1]) : string.Empty);
        if (handbookRightTitle == null)
        {
            handbookRightTitle = MakeText("Handbook Right Page Title", notePanel.transform, 36,
                new Color(.07f, .10f, .12f));
            handbookRightTitle.font = neatHandFont;
            handbookRightTitle.fontStyle = FontStyle.Bold;
            handbookRightTitle.alignment = TextAnchor.MiddleCenter;
        }
        handbookRightTitle.gameObject.SetActive(true);
        StationLocalization.Set(handbookRightTitle, "FOR THE NEXT SHIFT");
        Place(handbookRightTitle.rectTransform, new Vector2(330f, 398f), new Vector2(590f, 58f));
        Canvas.ForceUpdateCanvases();
        FitHandbookText(noteBody);
        FitHandbookText(noteRightBody);
        noteCloseRect.SetAsLastSibling();
    }

    private bool FitsHandbookPage(Text body)
    {
        TextGenerationSettings settings = body.GetGenerationSettings(
            new Vector2(body.rectTransform.rect.width, 0f));
        float height = body.cachedTextGeneratorForLayout.GetPreferredHeight(body.text, settings)
            / body.pixelsPerUnit;
        return height <= body.rectTransform.rect.height - 16f;
    }

    private void FitHandbookText(Text body)
    {
        for (int size = 36; size >= 18; size--)
        {
            body.fontSize = size;
            StationLocalization.ReflowChineseNote(body);
            if (FitsHandbookPage(body)) break;
        }
    }

    private void FitNoteText(Text body)
    {
        for (int size = body.fontSize; size >= 24; size--)
        {
            body.fontSize = size;
            StationLocalization.ReflowChineseNote(body);
            if (FitsHandbookPage(body)) break;
        }
    }

    private static string FormatHandbookPage(string text)
    {
        text = text.Replace("INCIDENT PROCEDURES\n\n", string.Empty)
            .Replace("FOR THE NEXT SHIFT\n\n", string.Empty).Replace("\n\n", "\n");
        foreach (string heading in new[] { "YOUR DUTIES", "SHIFT PROCEDURE", "CONTROLS" })
            text = text.Replace(heading + "\n", "<b>" + heading + "</b>\n");
        foreach (string heading in new[] { "UNAUTHORIZED OBJECTS", "GAS LEAK", "SHOOTING-ROOM FIGURE",
            "POWER FAILURE", "SPECIMEN IRREGULARITIES", "RESPONSE TIMES" })
            text = text.Replace(heading + "\n", "<b>" + heading + "</b> — ");
        return text;
    }

    public void Consume(OptionalPropInteraction food, PlayerInteractor owner)
    {
        LockPlayer(owner); consumedProp = food; finishedFood = false;
        originalPosition = food.transform.position; originalRotation = food.transform.rotation;
        originalScale = food.transform.localScale;
        foodColliders = food.GetComponentsInChildren<Collider>(); foodColliderStates = new bool[foodColliders.Length];
        for (int i = 0; i < foodColliders.Length; i++) { foodColliderStates[i] = foodColliders[i].enabled; foodColliders[i].enabled = false; }
        StartCoroutine(ConsumeAnimation(food, owner.InteractionCamera));
    }
    private IEnumerator ConsumeAnimation(OptionalPropInteraction food, Camera camera)
    {
        if (camera == null) { Restore(); yield break; }
        var renderer = food.GetComponentInChildren<Renderer>();
        Vector3 centerOffset = renderer != null ? renderer.bounds.center - food.transform.position : Vector3.zero;
        float maxSize = renderer != null ? Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.y, renderer.bounds.size.z) : .15f;
        float scaleFactor = Mathf.Clamp(.16f / Mathf.Max(.001f, maxSize), .05f, 1f);
        bool playedConsumeSound = false;
        for (float elapsed = 0; elapsed < 1.15f; elapsed += Time.deltaTime)
        {
            if (food == null || camera == null || activeInteractor == null) { Restore(); yield break; }
            if (!playedConsumeSound && elapsed >= .4f)
            {
                if (food.ConsumptionAudio != null)
                    AudioSource.PlayClipAtPoint(food.ConsumptionAudio, camera.transform.position, .65f);
                else ShiftAudioDirector.PlayConsume(food.IsDrink);
                playedConsumeSound = true;
            }
            float approach = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / .4f));
            float bite = elapsed > .4f ? Mathf.Sin((elapsed - .4f) * Mathf.PI * 6) * .016f : 0;
            Vector3 mouth = camera.transform.TransformPoint(new Vector3(.10f, -.14f + bite, .32f));
            float shrink = elapsed < .95f ? 1 : Mathf.Lerp(1, .05f, (elapsed - .95f) / .2f);
            food.transform.localScale = Vector3.Lerp(originalScale, originalScale * scaleFactor, approach) * shrink;
            food.transform.rotation = Quaternion.Slerp(originalRotation,
                camera.transform.rotation * Quaternion.Euler(food.IsDrink ? -28 : -10, 0, food.IsDrink ? 8 : -8), approach);
            // Recalculate the visible center because imported prop pivots sit on their base.
            centerOffset = renderer != null ? renderer.bounds.center - food.transform.position : Vector3.zero;
            food.transform.position = Vector3.Lerp(originalPosition, mouth - centerOffset, approach);
            yield return null;
        }
        if (food != null) { finishedFood = true; food.gameObject.SetActive(false); }
        Restore();
    }

    private void Update()
    {
        if (toast != null && toast.enabled && Time.unscaledTime >= toastUntil) toast.enabled = false;
        if (notePanel != null && notePanel.activeSelf && (currentNote == null || !currentNote.isActiveAndEnabled)) { Restore(); return; }
        if (currentNote == null || Time.frameCount == openedFrame || !Application.isFocused) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame
            || Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) CloseNote();
    }
    private void CloseNote()
    {
        if (currentNote == null) return;
        if (currentNote.name == "ShiftLog_Open1"
            && GameplaySignals.Instance != null
            && !GameplaySignals.Instance.HasRaised("handbook_read"))
            GameplaySignals.Instance.Raise("handbook_read");
        noteClosedFrame = Time.frameCount;
        Restore();
    }
    private void Restore()
    {
        if (notePanel != null) notePanel.SetActive(false);
        currentNote = null;
        if (consumedProp != null && !finishedFood)
        {
            consumedProp.transform.SetPositionAndRotation(originalPosition, originalRotation);
            consumedProp.transform.localScale = originalScale;
            consumedProp.CancelConsumption();
            for (int i = 0; i < foodColliders.Length; i++) if (foodColliders[i] != null) foodColliders[i].enabled = foodColliderStates[i];
        }
        consumedProp = null;
        if (activeInteractor == null) return;
        activeInteractor.InputEnabled = savedInteractor;
        if (player != null) player.InputEnabled = savedPlayer;
        if (look != null) look.InputEnabled = savedLook;
        if (hiddenItem != null) hiddenItem.SetViewHidden(false);
        Cursor.lockState = savedLock; Cursor.visible = savedVisible;
        activeInteractor = null; player = null; look = null; hiddenItem = null;
    }
    private void OnDisable() { StopAllCoroutines(); Restore(); }
    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
