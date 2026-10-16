using UnityEngine;

[DisallowMultipleComponent]
public class RandomAnomalySelector : MonoBehaviour
{
    [SerializeField] private GameObject[] choices;
    [Tooltip("只有玩家点开指定监控后才开始计时。")]
    [SerializeField] private MonitorViewManager monitorView;
    [SerializeField, Range(0, 2)] private int requiredScreenIndex = 1;
    [Tooltip("盯着对应监控后，异常出现的等待时间（秒）。")]
    [SerializeField] private Vector2 appearAfterSeconds = new Vector2(3f, 6f);
    [SerializeField] private string appearedSignal;

    private float remainingSeconds;
    private float focusDelay;
    private bool selectionPending;

    private void OnEnable()
    {
        if (Application.isPlaying) BeginSelection();
    }

    private void BeginSelection()
    {
        if (choices == null || choices.Length == 0) return;
        SetAllChoicesActive(false);
        // Override older serialized 2–3 second waits without touching the open scene.
        appearAfterSeconds = new Vector2(9f, 11f);
        float first = appearAfterSeconds.x;
        float last = appearAfterSeconds.y;
        focusDelay = Random.Range(first, last);
        remainingSeconds = focusDelay;
        selectionPending = true;
    }

    private void Update()
    {
        if (!selectionPending) return;
        if (!ShiftStartMenu.ShiftStarted || Time.timeScale <= 0f) return;
        if (GameplaySignals.Instance == null
            || !GameplaySignals.Instance.HasRaised("handbook_read"))
        {
            remainingSeconds = focusDelay;
            return;
        }
        if (monitorView == null)
            monitorView = FindAnyObjectByType<MonitorViewManager>();
        if (monitorView == null || !monitorView.IsSeated)
        {
            remainingSeconds = focusDelay;
            return;
        }

        remainingSeconds -= Time.deltaTime;
        if (remainingSeconds > 0f) return;
        selectionPending = false;

        int selected = Random.Range(0, choices.Length);
        for (int i = 0; i < choices.Length; i++)
            if (choices[i] != null) choices[i].SetActive(i == selected);
        GameObject anomaly = choices[selected];
        // The anomaly is now visible on the feed and in the corridor. It must
        // already be interactable when the player reaches it.
        if (anomaly != null)
            anomaly.GetComponent<MonitorDiscoveryTarget>()?.Discover();
        Debug.Log($"[Anomaly] Activated {anomaly?.name} after CCTV {requiredScreenIndex + 1}", this);
        GameplaySignals.Instance?.Raise(appearedSignal);
    }

    private void OnDisable()
    {
        selectionPending = false;
    }

    private void SetAllChoicesActive(bool active)
    {
        if (choices == null) return;
        foreach (GameObject choice in choices)
            if (choice != null) choice.SetActive(active);
    }
}
