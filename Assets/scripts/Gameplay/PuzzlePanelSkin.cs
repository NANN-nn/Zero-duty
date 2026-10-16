using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Builds the visual, independently clickable layers over the existing puzzle logic.
/// Artwork lives in Resources/PuzzleUI; no room pixels are baked into these panels.
/// </summary>
[DisallowMultipleComponent]
public sealed class PuzzlePanelSkin : MonoBehaviour
{
    private const float ArtWidth = 1672f;
    private const float ArtHeight = 941f;

    private static readonly float[] PowerX = { 460f, 835f, 1210f };
    private static readonly float[] GasLampX = { 555f, 835f, 1115f };
    private static readonly Color[] PowerColors =
    {
        new Color(1f, 0.65f, 0.23f, 0.92f),
        new Color(0.43f, 0.94f, 0.94f, 0.92f),
        new Color(0.79f, 0.52f, 1f, 0.92f)
    };

    private RectTransform panel;
    private RectTransform dimmer;
    private RawImage[] powerLevers;
    private RawImage[] powerLamps;
    private RawImage[] powerLampCores;
    private RawImage[] gasLamps;
    private RawImage[] gasLampCores;
    private RawImage gasShuttle;
    private Image gasButtonGraphic;
    private TextMeshProUGUI gasStabilityLabel, gasStabilityTime;
    private TextMeshProUGUI[] gasChannelLabels;
    private RectTransform gasStabilityFill;
    private Image[] gasStabilityRings;
    private RawImage gasButtonFace;
    private Sprite gasRingSprite;
    private Texture2D gasRingTexture;
    private float gasStableFraction;
    private bool gasButtonHeld;
    private bool gasButtonHovered;
    private readonly bool[] powerPulled = new bool[3];
    private readonly float[] powerWrongFlash = new float[3];
    private int gasProgress;
    private float needlePosition;
    private float pressFlash;
    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;

    public Button GasButton { get; private set; }
    public event System.Action<bool> GasHoldChanged;
    public Button[] PowerButtons { get; private set; }

    public void ConfigurePower(CanvasGroup group, Button[] buttons)
    {
        HideTemporaryGraphics(group);
        if (!BeginPanel(group, "PuzzleUI/CircuitPanelBase")) return;

        Texture2D leverTexture = LoadArtwork("PuzzleUI/PowerLeverUp");
        powerLevers = new RawImage[3];
        powerLamps = new RawImage[3];
        powerLampCores = new RawImage[3];
        Texture2D glow = MakeGlowTexture();

        for (int i = 0; i < 3; i++)
        {
            float x = PowerX[i];
            powerLevers[i] = AddRaw("Moving breaker " + (i + 1), leverTexture,
                x, 715f, 135f, 160f);
            powerLevers[i].color = new Color(0.8f, 0.82f, 0.85f, 1f);
            powerLamps[i] = AddRaw("Breaker lamp glow " + (i + 1), glow,
                x, 548f, 170f, 170f);
            powerLamps[i].color = Color.clear;
            powerLampCores[i] = AddRaw("Breaker lamp lit core " + (i + 1), glow,
                x, 548f, 86f, 86f);
            powerLampCores[i].color = Color.clear;

            if (buttons != null && i < buttons.Length && buttons[i] != null)
            {
                Button button = buttons[i];
                button.interactable = false;
                Image target = button.GetComponent<Image>();
                if (target != null)
                {
                    target.enabled = false;
                    target.raycastTarget = false;
                }

                foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
                    label.enabled = false;
                foreach (Text label in button.GetComponentsInChildren<Text>(true))
                    label.enabled = false;

            }
        }

        // Create hit areas as direct children of the art panel. The scene's old
        // ButtonA/B/C rectangles can be elsewhere in the hierarchy and must not
        // be relied on for the visible lever positions.
        PowerButtons = new Button[3];
        for (int i = 0; i < PowerButtons.Length; i++)
        {
            GameObject hit = new GameObject("Breaker lever hit area " + (i + 1),
                typeof(RectTransform), typeof(Image), typeof(Button));
            hit.transform.SetParent(panel, false);
            Place(hit.transform as RectTransform, PowerX[i], 665f, 280f, 340f);
            Image image = hit.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.001f);
            image.raycastTarget = true;
            Button button = hit.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            PowerButtons[i] = button;
        }

        ResetPowerLevers();
    }

    public void ConfigureGas(CanvasGroup group, Slider oldSlider)
    {
        HideTemporaryGraphics(group);
        if (!BeginPanel(group, "PuzzleUI/GasPanel")) return;

        // The scene's temporary slider remains referenced by the puzzle but its
        // white Unity graphics are replaced by the separate moving art sprite.
        if (oldSlider != null) oldSlider.gameObject.SetActive(false);

        Texture2D glow = MakeGlowTexture();
        gasLamps = new RawImage[3];
        gasLampCores = new RawImage[3];
        gasStabilityRings = new Image[3];
        gasChannelLabels = new TextMeshProUGUI[3];
        gasRingSprite = MakeGasRingSprite();
        for (int i = 0; i < 3; i++)
        {
            gasLamps[i] = AddRaw("Pressure success lamp " + (i + 1), glow,
                GasLampX[i], 258f, 164f, 164f);
            gasLamps[i].color = Color.clear;
            gasLampCores[i] = AddRaw("Pressure success lit core " + (i + 1), glow,
                GasLampX[i], 258f, 84f, 84f);
            gasLampCores[i].color = Color.clear;
            AddGasText("Channel " + (i + 1), "0" + (i + 1), GasLampX[i], 147f, 90f, 32f, 26f);
            gasChannelLabels[i] = AddGasText("Channel State " + (i + 1), "WAIT",
                GasLampX[i], 345f, 150f, 28f, 21f);
            GameObject ringObject = new GameObject("Active channel stability ring " + (i + 1),
                typeof(RectTransform), typeof(Image));
            ringObject.transform.SetParent(panel, false);
            Image ring = ringObject.GetComponent<Image>();
            ring.sprite = gasRingSprite;
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.fillAmount = 0f;
            ring.color = new Color(1f, .68f, .25f, .85f);
            ring.raycastTarget = false;
            Place(ring.rectTransform, GasLampX[i], 258f, 138f, 138f);
            gasStabilityRings[i] = ring;
        }

        AddGasText("Pressure title", "PRESSURE CONTROL", 835f, 372f, 430f, 34f, 26f);
        TextMeshProUGUI safe = AddGasText("Safe pressure band", "SAFE", 835f, 414f, 180f, 25f, 19f);
        safe.color = new Color(.57f, .82f, .61f, .95f);
        gasStabilityLabel = AddGasText("Stability state", "ADJUST PRESSURE", 571f, 557f, 240f, 25f, 20f);
        gasStabilityTime = AddGasText("Stability dwell time", "0.0 / 1.5 s", 1090f, 557f, 200f, 25f, 21f);
        GameObject progressTrack = new GameObject("Stability track", typeof(RectTransform), typeof(Image));
        progressTrack.transform.SetParent(panel, false);
        Place(progressTrack.transform as RectTransform, 835f, 557f, 300f, 10f);
        Image trackImage = progressTrack.GetComponent<Image>();
        trackImage.color = new Color(.03f, .05f, .06f, .95f);
        trackImage.raycastTarget = false;
        GameObject progressFill = new GameObject("Stability fill", typeof(RectTransform), typeof(Image));
        progressFill.transform.SetParent(progressTrack.transform, false);
        gasStabilityFill = progressFill.transform as RectTransform;
        gasStabilityFill.anchorMin = Vector2.zero;
        gasStabilityFill.anchorMax = new Vector2(0f, 1f);
        gasStabilityFill.offsetMin = gasStabilityFill.offsetMax = Vector2.zero;
        Image fillImage = progressFill.GetComponent<Image>();
        fillImage.color = new Color(.91f, .65f, .28f, .94f);
        fillImage.raycastTarget = false;

        gasShuttle = AddRaw("Moving pressure shuttle",
            LoadArtwork("PuzzleUI/GasShuttle"),
            340f, 452f, 86f, 120f);
        gasShuttle.color = new Color(0.82f, 0.84f, 0.87f, 1f);

        gasButtonFace = AddRaw("Moving metal button face", LoadArtwork("PuzzleUI/GasPanel"),
            835f, 665f, 185f, 130f);
        gasButtonFace.uvRect = new Rect(744f / ArtWidth, (ArtHeight - 730f) / ArtHeight,
            185f / ArtWidth, 130f / ArtHeight);

        GameObject buttonObject = new GameObject("Pressure timing press area",
            typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(panel, false);
        Place(buttonObject.transform as RectTransform, 835f, 652f, 260f, 175f);
        gasButtonGraphic = buttonObject.GetComponent<Image>();
        gasButtonGraphic.color = new Color(1f, 1f, 1f, 0.001f);
        gasButtonGraphic.raycastTarget = true;
        GasButton = buttonObject.GetComponent<Button>();
        GasButton.transition = Selectable.Transition.None;
        GasButton.navigation = new Navigation { mode = Navigation.Mode.None };
        GasButton.targetGraphic = gasButtonGraphic;

        TextMeshProUGUI holdLabel = new GameObject("HOLD label", typeof(RectTransform),
            typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        holdLabel.transform.SetParent(buttonObject.transform, false);
        holdLabel.rectTransform.anchoredPosition = new Vector2(0f, -12f);
        holdLabel.rectTransform.sizeDelta = new Vector2(180f, 90f);
        holdLabel.font = StationTypography.UIAsset;
        holdLabel.text = "HOLD";
        holdLabel.fontSize = 30f;
        holdLabel.fontStyle = FontStyles.Bold;
        holdLabel.characterSpacing = 1f;
        holdLabel.alignment = TextAlignmentOptions.Center;
        holdLabel.color = new Color(.06f, .08f, .09f, 1f);
        holdLabel.raycastTarget = false;

        EventTrigger hover = buttonObject.AddComponent<EventTrigger>();
        EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => gasButtonHovered = true);
        hover.triggers.Add(enter);
        EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => { gasButtonHovered = false; ReleaseGasButton(); });
        hover.triggers.Add(exit);
        EventTrigger.Entry down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener(data =>
        {
            if (data is PointerEventData pointer && pointer.button == PointerEventData.InputButton.Left
                && GasButton.IsInteractable()) SetGasHeld(true);
        });
        hover.triggers.Add(down);
        EventTrigger.Entry up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        up.callback.AddListener(_ => ReleaseGasButton());
        hover.triggers.Add(up);

        SetGasProgress(0);
        SetGasNeedlePosition(0f);
    }

    private TextMeshProUGUI AddGasText(string objectName, string content,
        float x, float y, float width, float height, float size)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(panel, false);
        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        text.font = StationTypography.UIAsset;
        text.text = content;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.characterSpacing = 1f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(.72f, .79f, .82f, .95f);
        text.raycastTarget = false;
        Place(text.rectTransform, x, y, width, height);
        return text;
    }

    private void SetGasHeld(bool held)
    {
        if (gasButtonHeld == held) return;
        gasButtonHeld = held;
        GasHoldChanged?.Invoke(held);
    }

    public void ReleaseGasButton() => SetGasHeld(false);

    public void SetGasStability(float seconds, float required, bool safe)
    {
        gasStableFraction = Mathf.Clamp01(seconds / Mathf.Max(.1f, required));
        if (gasStabilityLabel != null)
            gasStabilityLabel.text = gasProgress >= 3 ? "PRESSURE STABLE"
                : safe ? "STABILIZING" : "ADJUST PRESSURE";
        if (gasStabilityTime != null) gasStabilityTime.text = $"{seconds:0.0} / {required:0.0} s";
        if (gasStabilityFill != null) gasStabilityFill.anchorMax = new Vector2(gasStableFraction, 1f);
    }

    public void ResetPowerLevers()
    {
        for (int i = 0; i < powerPulled.Length; i++)
        {
            powerPulled[i] = false;
            powerWrongFlash[i] = 0f;
        }
    }

    public void SetPowerLeverState(int index, bool pulled)
    {
        if (index >= 0 && index < powerPulled.Length) powerPulled[index] = pulled;
    }

    public void PulseWrongLever(int index)
    {
        if (index >= 0 && index < powerWrongFlash.Length)
            powerWrongFlash[index] = 1f;
    }

    public void SetGasProgress(int count)
    {
        gasProgress = Mathf.Clamp(count, 0, 3);
    }

    public void SetGasNeedlePosition(float position)
    {
        needlePosition = Mathf.Clamp01(position);
        if (gasShuttle != null)
            Place(gasShuttle.rectTransform,
                Mathf.Lerp(340f, 1330f, needlePosition), 452f, 86f, 120f);
    }

    public void PulseGasButton()
    {
        pressFlash = 1f;
    }

    private bool BeginPanel(CanvasGroup group, string texturePath)
    {
        if (group == null) return false;
        Texture2D texture = LoadArtwork(texturePath);
        if (texture == null)
        {
            Debug.LogWarning("Missing puzzle UI art: " + texturePath, this);
            return false;
        }

        panel = group.transform as RectTransform;
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(ArtWidth, ArtHeight);

        Image oldBackground = group.GetComponent<Image>();
        if (oldBackground != null)
        {
            oldBackground.color = Color.clear;
            oldBackground.raycastTarget = false;
        }

        GameObject veil = new GameObject("Scene dimmer", typeof(RectTransform), typeof(Image));
        veil.transform.SetParent(panel, false);
        veil.transform.SetAsFirstSibling();
        dimmer = veil.transform as RectTransform;
        Image veilImage = veil.GetComponent<Image>();
        veilImage.color = new Color(0f, 0.015f, 0.035f, 0.34f);
        veilImage.raycastTarget = true;

        RawImage artwork = AddRaw("Machine panel artwork", texture,
            ArtWidth * 0.5f, ArtHeight * 0.5f, ArtWidth, ArtHeight);
        artwork.transform.SetSiblingIndex(1);
        LayoutForScreen();
        return true;
    }

    private static void HideTemporaryGraphics(CanvasGroup group)
    {
        if (group == null) return;
        foreach (Graphic graphic in group.GetComponentsInChildren<Graphic>(true))
            if (graphic.gameObject != group.gameObject) graphic.enabled = false;
    }

    private static Texture2D LoadArtwork(string resourcePath)
    {
        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture != null) return texture;
        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null) return sprite.texture;

        // The Editor can enter Play before newly imported Resources are added to
        // its runtime lookup. The direct asset path keeps this Play test usable.
#if UNITY_EDITOR
        texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Resources/" + resourcePath + ".png");
        if (texture == null)
        {
            sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Resources/" + resourcePath + ".png");
            if (sprite != null) texture = sprite.texture;
        }
        if (texture != null) return texture;
#endif
        string file = Path.Combine(Application.streamingAssetsPath,
            resourcePath + ".png");
#if UNITY_EDITOR
        if (!File.Exists(file))
            file = Path.Combine(Application.dataPath, "Resources", resourcePath + ".png");
#endif
        if (File.Exists(file))
        {
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (texture.LoadImage(File.ReadAllBytes(file), true))
            {
                texture.name = Path.GetFileNameWithoutExtension(file);
                Debug.Log("Puzzle UI art loaded: " + resourcePath);
                return texture;
            }
            Destroy(texture);
            texture = null;
        }
        else
            Debug.LogWarning("Puzzle UI file not found: " + file);
        return texture;
    }

    private RawImage AddRaw(string objectName, Texture2D texture,
        float imageX, float imageY, float width, float height)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(RawImage));
        child.transform.SetParent(panel, false);
        RawImage image = child.GetComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        Place(image.rectTransform, imageX, imageY, width, height);
        return image;
    }

    private static void Place(RectTransform rect,
        float imageX, float imageY, float width, float height)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(imageX - ArtWidth * 0.5f,
            ArtHeight * 0.5f - imageY);
        rect.sizeDelta = new Vector2(width, height);
    }

    private void LateUpdate()
    {
        if (panel == null) return;
        if (lastScreenWidth != Screen.width || lastScreenHeight != Screen.height)
            LayoutForScreen();

        if (powerLevers != null)
        {
            for (int i = 0; i < powerLevers.Length; i++)
            {
                if (powerLevers[i] == null) continue;
                powerWrongFlash[i] = Mathf.MoveTowards(powerWrongFlash[i], 0f,
                    Time.unscaledDeltaTime * 2.8f);
                float target = powerPulled[i] ? 1f : powerWrongFlash[i] * 0.34f;
                float current = powerLevers[i].rectTransform.localPosition.y;
                float desired = Mathf.Lerp(-244.5f, -164.5f, target);
                Vector3 p = powerLevers[i].rectTransform.localPosition;
                p.y = Mathf.MoveTowards(current, desired, Time.unscaledDeltaTime * 520f);
                powerLevers[i].rectTransform.localPosition = p;

                Color lampColor = PowerColors[i];
                if (powerWrongFlash[i] > 0f)
                    lampColor = new Color(1f, 0.18f, 0.12f, powerWrongFlash[i] * 0.58f);
                else
                    lampColor.a = powerPulled[i] ? 0.58f : 0f;
                powerLamps[i].color = Color.Lerp(powerLamps[i].color, lampColor,
                    Mathf.Clamp01(Time.unscaledDeltaTime * 12f));
                lampColor.a = powerWrongFlash[i] > 0f
                    ? powerWrongFlash[i] * 0.92f : powerPulled[i] ? 0.92f : 0f;
                powerLampCores[i].color = Color.Lerp(powerLampCores[i].color, lampColor,
                    Mathf.Clamp01(Time.unscaledDeltaTime * 12f));
            }
        }

        if (gasLamps != null)
        {
            for (int i = 0; i < gasLamps.Length; i++)
            {
                bool done = i < gasProgress;
                bool active = i == gasProgress;
                Color target = done ? new Color(.35f, .89f, .46f, .7f)
                    : active ? new Color(1f, .67f, .23f, .62f) : Color.clear;
                gasLamps[i].color = Color.Lerp(gasLamps[i].color, target,
                    Mathf.Clamp01(Time.unscaledDeltaTime * 12f));
                Color core = done ? new Color(.55f, 1f, .66f, .94f)
                    : active ? new Color(1f, .78f, .40f, .92f) : Color.clear;
                gasLampCores[i].color = Color.Lerp(gasLampCores[i].color, core,
                    Mathf.Clamp01(Time.unscaledDeltaTime * 12f));
                if (gasStabilityRings != null && gasStabilityRings[i] != null)
                    gasStabilityRings[i].fillAmount = active ? gasStableFraction : 0f;
                if (gasChannelLabels != null && gasChannelLabels[i] != null)
                {
                    gasChannelLabels[i].text = done ? "OK" : active ? "ACTIVE" : "WAIT";
                    gasChannelLabels[i].color = done ? new Color(.5f, .9f, .6f)
                        : active ? new Color(1f, .72f, .3f) : new Color(.42f, .47f, .49f);
                }
            }
        }

        if (gasButtonGraphic != null)
        {
            pressFlash = Mathf.MoveTowards(pressFlash, 0f, Time.unscaledDeltaTime * 5f);
            gasButtonGraphic.color = new Color(0.54f, 0.85f, 0.95f,
                0.001f + (gasButtonHovered ? 0.04f : 0f) + (gasButtonHeld ? .1f : 0f) + pressFlash * 0.08f);
            if (gasButtonFace != null)
            {
                Place(gasButtonFace.rectTransform, 835f, gasButtonHeld ? 670f : 665f, 185f, 130f);
                gasButtonFace.color = gasButtonHeld ? new Color(.77f, .79f, .8f) : Color.white;
            }
        }
    }

    private void LayoutForScreen()
    {
        if (panel == null) return;
        lastScreenWidth = Mathf.Max(1, Screen.width);
        lastScreenHeight = Mathf.Max(1, Screen.height);
        float scale = Mathf.Min(lastScreenWidth * 0.84f / ArtWidth,
            lastScreenHeight * 0.84f / ArtHeight);
        panel.localScale = Vector3.one * scale;
        if (dimmer != null)
        {
            dimmer.anchorMin = dimmer.anchorMax = dimmer.pivot = new Vector2(0.5f, 0.5f);
            dimmer.anchoredPosition = Vector2.zero;
            dimmer.sizeDelta = new Vector2(lastScreenWidth / scale,
                lastScreenHeight / scale);
        }
    }

    private static Texture2D MakeGlowTexture()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Puzzle UI soft lamp glow";
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f - size * 0.5f) / (size * 0.5f);
            float dy = (y + 0.5f - size * 0.5f) / (size * 0.5f);
            float fade = Mathf.Pow(1f - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy)), 2f);
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(fade * 255f));
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        return texture;
    }

    private Sprite MakeGasRingSprite()
    {
        const int size = 128;
        gasRingTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float r = new Vector2(x + .5f - size / 2f, y + .5f - size / 2f).magnitude;
            float alpha = Mathf.Clamp01(1f - Mathf.Abs(r - 58f) / 2.5f);
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
        }
        gasRingTexture.SetPixels32(pixels);
        gasRingTexture.Apply(false, true);
        return Sprite.Create(gasRingTexture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f));
    }

    private void OnDisable()
    {
        ReleaseGasButton();
        gasButtonHovered = false;
    }

    private void OnDestroy()
    {
        if (gasRingSprite != null) Destroy(gasRingSprite);
        if (gasRingTexture != null) Destroy(gasRingTexture);
    }
}
