#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Editor-only shortcut to jump straight to the breaker fault.</summary>
public sealed class PowerFaultTestShortcut : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForPlayMode()
    {
        if (FindAnyObjectByType<PowerFaultTestShortcut>() != null) return;
        new GameObject("Power Fault Test (Editor Only)").AddComponent<PowerFaultTestShortcut>();
    }

    private void Update()
    {
        if (!Application.isFocused || Keyboard.current == null
            || !Keyboard.current.f3Key.wasPressedThisFrame) return;

        GameplaySignals signals = GameplaySignals.Instance;
        if (signals == null)
        {
            Debug.LogWarning("Power test: GameplaySignals was not found in this scene.");
            return;
        }
        if (signals.HasRaised("power_restored"))
        {
            Debug.LogWarning("Power test: breaker was already repaired. Restart Play mode to test again.");
            return;
        }

        if (!signals.HasRaised("ghost_interlude_complete"))
            signals.Raise("ghost_interlude_complete");
        Debug.Log("Power test ready: go to the control-room breaker, aim at its interaction area, then press E.");
    }
}
#endif
