using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Three two-port rotary contacts in series. A contact conducts between its
/// left and right sockets only at 0 or 180 degrees; 90/270 breaks the route.
/// The shuffled signal colors tell the player which existing lever to pull.
/// </summary>
[DisallowMultipleComponent]
public sealed class CircuitRoutingUI : MonoBehaviour
{
    private const float PanelWidth = 1672f;
    private const float PanelHeight = 941f;
    private static readonly float[] TileX = { 550f, 835f, 1120f };
    private static readonly Color[] SignalColors =
    {
        new Color(0.97f, 0.63f, 0.22f, 1f),
        new Color(0.40f, 0.87f, 0.89f, 1f),
        new Color(0.75f, 0.49f, 0.90f, 1f)
    };

    private readonly int[] turns = new int[3];
    private readonly int[] signalOrder = new int[3];
    private readonly RectTransform[] contacts = new RectTransform[3];
    private readonly Image[] traces = new Image[3];
    private readonly Image[] lamps = new Image[3];
    private readonly Button[] tileButtons = new Button[3];
    private Button replayButton;
    private TMP_Text routeMessage;
    private Action<int[]> onRouted;
    private Coroutine playback;
    private bool roundActive;
    private bool routeSolved;

    public void Configure(CanvasGroup group, Action<int[]> completion)
    {
        onRouted = completion;
        if (group == null) return;
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            RectTransform tile = NewImage("Rotary contact " + (i + 1),
                group.transform, new Color(0.035f, 0.068f, 0.081f, 1f),
                TileX[i], 294f, 184f, 184f);
            Outline edge = tile.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0.24f, 0.40f, 0.45f, 0.8f);
            edge.effectDistance = new Vector2(2f, 2f);
            tileButtons[i] = tile.gameObject.AddComponent<Button>();
            tileButtons[i].targetGraphic = tile.GetComponent<Image>();
            tileButtons[i].navigation = new Navigation { mode = Navigation.Mode.None };
            tileButtons[i].onClick.AddListener(() => Turn(index));

            RectTransform contact = NewImage("Rotating two-port bridge", tile,
                Color.clear, PanelWidth * 0.5f, PanelHeight * 0.5f, 166f, 166f);
            contact.GetComponent<Image>().raycastTarget = false;
            contacts[i] = contact;
            RectTransform trace = NewImage("Conductive bar", contact,
                new Color(0.48f, 0.78f, 0.84f, 1f),
                PanelWidth * 0.5f, PanelHeight * 0.5f, 168f, 13f);
            traces[i] = trace.GetComponent<Image>();
            traces[i].raycastTarget = false;
            RectTransform hub = NewImage("Contact hub", contact,
                new Color(0.075f, 0.16f, 0.19f, 1f),
                PanelWidth * 0.5f, PanelHeight * 0.5f, 40f, 40f);
            hub.GetComponent<Image>().raycastTarget = false;

            RectTransform lamp = NewImage("Signal color " + (i + 1), tile,
                Color.clear, PanelWidth * 0.5f, PanelHeight * 0.5f - 63f,
                24f, 24f);
            lamps[i] = lamp.GetComponent<Image>();
            lamps[i].raycastTarget = false;
        }

        RectTransform replayArea = NewImage("Replay signal hit area",
            group.transform, new Color(1f, 1f, 1f, 0.001f),
            1430f, 161f, 86f, 86f);
        replayButton = replayArea.gameObject.AddComponent<Button>();
        replayButton.targetGraphic = replayArea.GetComponent<Image>();
        replayButton.navigation = new Navigation { mode = Navigation.Mode.None };
        replayButton.onClick.AddListener(ReplaySignal);

        GameObject label = new GameObject("Circuit response",
            typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(group.transform, false);
        RectTransform labelRect = label.transform as RectTransform;
        Place(labelRect, 835f, 402f, 1100f, 36f);
        routeMessage = label.GetComponent<TextMeshProUGUI>();
        routeMessage.font = StationTypography.UIAsset;
        routeMessage.fontSize = 25f;
        routeMessage.fontStyle = FontStyles.Bold;
        routeMessage.alignment = TextAlignmentOptions.Center;
        routeMessage.color = new Color(0.65f, 0.81f, 0.86f, 1f);
        routeMessage.raycastTarget = false;
    }

    public void BeginRound()
    {
        EndRound();
        roundActive = true;
        routeSolved = false;

        for (int i = 0; i < 3; i++) signalOrder[i] = i;
        for (int i = signalOrder.Length - 1; i > 0; i--)
        {
            int other = UnityEngine.Random.Range(0, i + 1);
            int swap = signalOrder[i];
            signalOrder[i] = signalOrder[other];
            signalOrder[other] = swap;
        }

        for (int i = 0; i < turns.Length; i++)
        {
            // Every contact begins vertical, so the player must rotate all three.
            turns[i] = UnityEngine.Random.Range(0, 2) == 0 ? 1 : 3;
        }

        for (int i = 0; i < turns.Length; i++)
        {
            tileButtons[i].interactable = true;
            ShowContact(i, false);
        }
        replayButton.interactable = false;
        routeMessage.text = string.Empty;
    }

    public void EndRound()
    {
        roundActive = false;
        if (playback != null)
        {
            StopCoroutine(playback);
            playback = null;
        }
        for (int i = 0; i < lamps.Length; i++)
            if (lamps[i] != null) lamps[i].color = Dim(SignalColors[signalOrder[i]]);
    }

    // These are actual socket orientations, not a cosmetic angle test:
    // 0/180 = left+right; 90/270 = top+bottom.
    public static bool HasThroughConnection(int quarterTurns)
        => (quarterTurns & 1) == 0;

    public static bool RouteConnected(int[] orientations)
    {
        if (orientations == null || orientations.Length != 3) return false;
        for (int i = 0; i < 3; i++)
            if (!HasThroughConnection(orientations[i])) return false;
        return true;
    }

    private void Turn(int index)
    {
        if (!roundActive || routeSolved || playback != null || Time.timeScale <= 0f) return;
        turns[index] = (turns[index] + 1) & 3;
        ShowContact(index, false);
        ShiftAudioDirector.PlayUiSelect();

        if (!RouteConnected(turns))
        {
            routeMessage.text = string.Empty;
            return;
        }
        routeSolved = true;
        foreach (Button button in tileButtons) button.interactable = false;
        routeMessage.text = string.Empty;
        playback = StartCoroutine(PlaySignal(true));
    }

    private void ReplaySignal()
    {
        if (!roundActive || !routeSolved || playback != null || Time.timeScale <= 0f) return;
        ShiftAudioDirector.PlayUiSelect();
        playback = StartCoroutine(PlaySignal(false));
    }

    private IEnumerator PlaySignal(bool firstCompletion)
    {
        replayButton.interactable = false;
        for (int i = 0; i < 3; i++)
        {
            lamps[i].color = Dim(SignalColors[signalOrder[i]]);
            ShowContact(i, true);
        }
        yield return new WaitForSeconds(0.35f);
        for (int i = 0; i < 3; i++)
        {
            lamps[i].color = SignalColors[signalOrder[i]];
            ShiftAudioDirector.PlayUiSelect();
            yield return new WaitForSeconds(0.58f);
            lamps[i].color = Dim(SignalColors[signalOrder[i]]);
            yield return new WaitForSeconds(0.12f);
        }
        routeMessage.text = string.Empty;
        replayButton.interactable = true;
        playback = null;
        if (firstCompletion)
        {
            ShiftAudioDirector.PlayUiConfirm();
            onRouted?.Invoke((int[])signalOrder.Clone());
        }
    }

    private void ShowContact(int index, bool powered)
    {
        contacts[index].localRotation = Quaternion.Euler(0f, 0f, turns[index] * 90f);
        traces[index].color = powered
            ? new Color(0.49f, 0.91f, 0.98f, 1f)
            : HasThroughConnection(turns[index])
                ? new Color(0.31f, 0.62f, 0.69f, 1f)
                : new Color(0.17f, 0.30f, 0.34f, 1f);
        lamps[index].color = Dim(SignalColors[signalOrder[index]]);
    }

    public void ShowLeverMistake()
    {
        if (roundActive && routeSolved)
            routeMessage.text = "Wrong order. Try again";
    }

    private static Color Dim(Color color)
        => new Color(color.r * 0.4f, color.g * 0.4f, color.b * 0.4f, 0.75f);

    private static RectTransform NewImage(string label, Transform parent,
        Color color, float imageX, float imageY, float width, float height)
    {
        GameObject child = new GameObject(label, typeof(RectTransform), typeof(Image));
        child.transform.SetParent(parent, false);
        RectTransform rect = child.transform as RectTransform;
        Place(rect, imageX, imageY, width, height);
        child.GetComponent<Image>().color = color;
        return rect;
    }

    private static void Place(RectTransform rect, float imageX, float imageY,
        float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(imageX - PanelWidth * 0.5f,
            PanelHeight * 0.5f - imageY);
        rect.sizeDelta = new Vector2(width, height);
    }

    private void OnDisable() => EndRound();
}
