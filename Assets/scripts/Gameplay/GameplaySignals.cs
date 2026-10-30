using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-local event bus and completion history shared by incidents and presentation.
/// Late subscribers query HasRaised instead of replaying an action or changing its state.
/// </summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public class GameplaySignals : MonoBehaviour
{
    public static GameplaySignals Instance { get; private set; }
    public event Action<string> SignalRaised;

    private readonly HashSet<string> history = new HashSet<string>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("场景里只能有一个 GameplaySignals。", this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    public void Raise(string signalId)
    {
        if (string.IsNullOrWhiteSpace(signalId)) return;
        // History is idempotent; listeners still receive each deliberate notification.
        history.Add(signalId);
        SignalRaised?.Invoke(signalId);
        Debug.Log($"[玩法信号] {signalId}", this);
    }

    public bool HasRaised(string signalId) => history.Contains(signalId);

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
