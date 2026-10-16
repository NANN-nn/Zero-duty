using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class GameFlowController : MonoBehaviour
{
    [Serializable]
    public class Stage
    {
        public string stageName;
        public GameObject[] activateOnStart;
        public GameObject[] deactivateOnStart;
        public string[] requiredSignals;
        public GameObject[] activateOnComplete;
        public GameObject[] deactivateOnComplete;
    }

    [SerializeField] private Stage[] stages;
    [SerializeField] private bool beginOnStart = true;
    [SerializeField, Min(0f)] private float nextStageDelay = 0.5f;

    public int CurrentStageIndex { get; private set; } = -1;
    public bool Finished => stages != null && CurrentStageIndex >= stages.Length;

    private readonly HashSet<string> received = new HashSet<string>();
    private bool advancing;

    private void OnEnable()
    {
        if (GameplaySignals.Instance != null)
            GameplaySignals.Instance.SignalRaised += ReceiveSignal;
    }

    private void Start()
    {
        if (GameplaySignals.Instance != null)
        {
            GameplaySignals.Instance.SignalRaised -= ReceiveSignal;
            GameplaySignals.Instance.SignalRaised += ReceiveSignal;
        }
        if (beginOnStart) Begin();
    }

    public void Begin()
    {
        if (CurrentStageIndex >= 0) return;
        EnterStage(0);
    }

    private void ReceiveSignal(string signalId)
    {
        if (Finished || CurrentStageIndex < 0 || advancing) return;
        received.Add(signalId);
        if (HasAllSignals())
        {
            advancing = true;
            Invoke(nameof(CompleteAndAdvance), nextStageDelay);
        }
    }

    private bool HasAllSignals()
    {
        string[] required = stages[CurrentStageIndex].requiredSignals;
        if (required == null || required.Length == 0) return true;
        foreach (string signal in required)
            if (!received.Contains(signal)) return false;
        return true;
    }

    private void CompleteAndAdvance()
    {
        Stage current = stages[CurrentStageIndex];
        SetActive(current.activateOnComplete, true);
        SetActive(current.deactivateOnComplete, false);
        EnterStage(CurrentStageIndex + 1);
    }

    private void EnterStage(int index)
    {
        received.Clear();
        advancing = false;
        CurrentStageIndex = index;
        if (Finished)
        {
            GameplaySignals.Instance?.Raise("game_complete");
            return;
        }

        Stage stage = stages[index];
        SetActive(stage.activateOnStart, true);
        SetActive(stage.deactivateOnStart, false);
        Debug.Log($"[任务阶段 {index + 1}] {stage.stageName}", this);
    }

    private static void SetActive(GameObject[] objects, bool active)
    {
        if (objects == null) return;
        foreach (GameObject target in objects)
            if (target != null) target.SetActive(active);
    }

    private void OnDisable()
    {
        if (GameplaySignals.Instance != null)
            GameplaySignals.Instance.SignalRaised -= ReceiveSignal;
        CancelInvoke();
    }
}
