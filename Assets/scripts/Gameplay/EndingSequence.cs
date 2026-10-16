using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class EndingSequence : MonoBehaviour
{
    [SerializeField] private string triggerSignal = "game_complete";
    [SerializeField] private CanvasGroup endingCanvas;
    [SerializeField] private TMP_Text endingText;
    [SerializeField, TextArea] private string message = "Shift complete";
    [SerializeField, Min(0.1f)] private float fadeDuration = 2f;

    private void Start()
    {
        if (endingCanvas != null)
        {
            endingCanvas.alpha = 0f;
            endingCanvas.gameObject.SetActive(false);
        }
        if (GameplaySignals.Instance != null)
            GameplaySignals.Instance.SignalRaised += OnSignal;
    }

    private void OnSignal(string signalId)
    {
        if (signalId != triggerSignal) return;
        if (GameplaySignals.Instance != null)
            GameplaySignals.Instance.SignalRaised -= OnSignal;
        StartCoroutine(PlayEnding());
    }

    private IEnumerator PlayEnding()
    {
        if (endingCanvas == null) yield break;
        endingCanvas.gameObject.SetActive(true);
        if (endingText != null) endingText.text = message;
        float time = 0f;
        while (time < fadeDuration)
        {
            time += Time.unscaledDeltaTime;
            endingCanvas.alpha = Mathf.Clamp01(time / fadeDuration);
            yield return null;
        }
    }

    private void OnDestroy()
    {
        if (GameplaySignals.Instance != null)
            GameplaySignals.Instance.SignalRaised -= OnSignal;
    }
}
