using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Small in-world equipment and room labels, not HUD waypoints.</summary>
[DisallowMultipleComponent]
public sealed class StationWayfinding : MonoBehaviour
{
    private sealed class Plate
    {
        public Canvas canvas;
        public Image status;
        public Image secondStatus;
        public float visibleDistance;
    }

    private Plate disposal;
    private Plate controls;
    private Plate[] roomSigns;
    private PlayerInteractor interactor;
    private Camera playerCamera;

    private void Start()
    {
        Renderer panel = FindRenderer("Power and gas | panel.001");
        if (panel != null)
        {
            controls = CreatePlate("Gas and power identifier", "GAS  /  POWER",
                panel.bounds.center + Vector3.up * (panel.bounds.extents.y + 0.13f), 5f);
            controls.status = AddStatus(controls.canvas.transform, -182f);
            controls.secondStatus = AddStatus(controls.canvas.transform, 182f);
        }

        Renderer hatch = FindRenderer("V9 Disposal | smoked hatch.001");
        if (hatch != null)
        {
            disposal = CreatePlate("Disposal identifier", "DISPOSAL",
                hatch.bounds.center + Vector3.up * (hatch.bounds.extents.y + 0.16f), 5f);
            disposal.status = AddStatus(disposal.canvas.transform, -182f);
        }

        // These sit near the three room entrances. They are deliberately
        // understated, so the event light pulse remains the primary cue.
        roomSigns = new[]
        {
            CreatePlate("Control room sign", "CONTROL ROOM",
                new Vector3(1.4f, 2.1f, -0.3f), 9f),
            CreatePlate("Shooting room sign", "SHOOTING RANGE",
                new Vector3(-3.4f, 2.1f, -3.8f), 9f),
            CreatePlate("Specimen room sign", "SPECIMEN ROOM",
                new Vector3(6.1f, 2.1f, -3.8f), 9f)
        };
        interactor = FindAnyObjectByType<PlayerInteractor>();
    }

    private void LateUpdate()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (interactor == null) interactor = FindAnyObjectByType<PlayerInteractor>();
        GameplaySignals signals = GameplaySignals.Instance;
        bool gasFault = signals != null && signals.HasRaised("corridor_interlude_complete")
            && !signals.HasRaised("gas_restored");
        bool powerFault = signals != null && signals.HasRaised("ghost_interlude_complete")
            && !signals.HasRaised("power_restored");
        bool carryingWaste = interactor != null && interactor.HeldItem != null
            && interactor.HeldItem.Disposable;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);

        UpdatePlate(controls);
        UpdatePlate(disposal);
        if (roomSigns != null)
            foreach (Plate sign in roomSigns) UpdatePlate(sign);

        SetStatus(controls?.status, gasFault, pulse, new Color(0.55f, 0.85f, 0.95f));
        SetStatus(controls?.secondStatus, powerFault, pulse,
            new Color(0.87f, 0.76f, 0.54f));
        SetStatus(disposal?.status, carryingWaste, pulse,
            new Color(0.98f, 0.70f, 0.39f));
    }

    private void UpdatePlate(Plate plate)
    {
        if (plate?.canvas == null) return;
        bool show = ShiftStartMenu.ShiftStarted && playerCamera != null
            && Vector3.Distance(playerCamera.transform.position,
                plate.canvas.transform.position) < plate.visibleDistance;
        plate.canvas.enabled = show;
        if (!show) return;
        Vector3 facing = playerCamera.transform.position - plate.canvas.transform.position;
        facing.y = 0f;
        if (facing.sqrMagnitude > 0.01f)
            plate.canvas.transform.rotation = Quaternion.LookRotation(facing.normalized);
    }

    private static void SetStatus(Image lamp, bool active, float pulse, Color lit)
    {
        if (lamp == null) return;
        lamp.color = active
            ? new Color(lit.r, lit.g, lit.b, 0.50f + pulse * 0.45f)
            : new Color(0.28f, 0.37f, 0.42f, 0.48f);
    }

    private Plate CreatePlate(string objectName, string caption, Vector3 position,
        float visibleDistance)
    {
        GameObject root = new GameObject(objectName, typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(transform, true);
        root.transform.position = position;
        root.transform.localScale = Vector3.one * 0.002f;
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform rect = root.transform as RectTransform;
        rect.sizeDelta = new Vector2(440f, 82f);

        GameObject face = new GameObject("Metal plate", typeof(RectTransform), typeof(Image));
        face.transform.SetParent(root.transform, false);
        Stretch(face.transform as RectTransform);
        Image background = face.GetComponent<Image>();
        background.color = new Color(0.025f, 0.055f, 0.075f, 0.86f);
        background.raycastTarget = false;

        GameObject line = new GameObject("Upper edge", typeof(RectTransform), typeof(Image));
        line.transform.SetParent(root.transform, false);
        RectTransform lineRect = line.transform as RectTransform;
        lineRect.anchorMin = new Vector2(0f, 1f);
        lineRect.anchorMax = new Vector2(1f, 1f);
        lineRect.pivot = new Vector2(0.5f, 1f);
        lineRect.anchoredPosition = Vector2.zero;
        lineRect.sizeDelta = new Vector2(0f, 3f);
        line.GetComponent<Image>().color = new Color(0.42f, 0.70f, 0.80f, 0.72f);
        line.GetComponent<Image>().raycastTarget = false;

        GameObject textObject = new GameObject("Engraved label", typeof(RectTransform),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(root.transform, false);
        Stretch(textObject.transform as RectTransform);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = StationTypography.UIAsset;
        text.text = caption;
        text.fontSize = 31f;
        text.fontStyle = FontStyles.Bold;
        text.characterSpacing = 2.5f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.72f, 0.83f, 0.87f, 0.95f);
        text.raycastTarget = false;

        return new Plate { canvas = canvas, visibleDistance = visibleDistance };
    }

    private static Image AddStatus(Transform parent, float x)
    {
        GameObject dot = new GameObject("Status lamp", typeof(RectTransform), typeof(Image));
        dot.transform.SetParent(parent, false);
        RectTransform rect = dot.transform as RectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(13f, 13f);
        Image image = dot.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static Renderer FindRenderer(string exactName)
    {
        foreach (Renderer candidate in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if (candidate != null && candidate.name == exactName) return candidate;
        return null;
    }
}
