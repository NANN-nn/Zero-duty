#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Editor-only shortcut to jump straight to the gas-valve fault.</summary>
public sealed class GasFaultTestShortcut : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForPlayMode()
    {
        if (FindAnyObjectByType<GasFaultTestShortcut>() != null) return;
        new GameObject("Gas Fault Test (Editor Only)").AddComponent<GasFaultTestShortcut>();
    }

    private void Update()
    {
        if (!Application.isFocused || Keyboard.current == null
            || !Keyboard.current.f4Key.wasPressedThisFrame) return;

        GameplaySignals signals = GameplaySignals.Instance;
        if (signals == null)
        {
            Debug.LogWarning("Gas test: GameplaySignals was not found in this scene.");
            return;
        }
        if (signals.HasRaised("gas_restored"))
        {
            Debug.LogWarning("Gas test: gas was already repaired. Restart Play mode to test again.");
            return;
        }

        // Clear the previous corridor countdown, then enable the gas puzzle and leak.
        if (!signals.HasRaised("corridor_disposed")) signals.Raise("corridor_disposed");
        if (!signals.HasRaised("corridor_interlude_complete"))
            signals.Raise("corridor_interlude_complete");
        Debug.Log("Gas test ready: go to the control-room valve, aim at its interaction area, then press E.");
    }
}
#endif
