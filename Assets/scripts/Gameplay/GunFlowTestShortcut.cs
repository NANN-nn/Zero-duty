#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Editor-only shortcut for testing the gun without replaying earlier events.</summary>
public class GunFlowTestShortcut : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForPlayMode()
    {
        if (FindAnyObjectByType<GunFlowTestShortcut>() != null) return;
        new GameObject("Gun Flow Test (Editor Only)").AddComponent<GunFlowTestShortcut>();
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.f8Key.wasPressedThisFrame) return;

        GameplaySignals signals = GameplaySignals.Instance;
        if (signals == null)
        {
            Debug.LogWarning("Gun test: GameplaySignals was not found in this scene.");
            return;
        }

        signals.Raise("gas_restored");
        signals.Raise("gas_interlude_complete");

        foreach (MonitorDiscoveryTarget target in
                 FindObjectsByType<MonitorDiscoveryTarget>(FindObjectsSortMode.None))
        {
            if (target.name != "GhostTarget") continue;
            target.Discover();
            Debug.Log("Gun test ready: pick up the gun with E, shoot with left click, then return it with E.");
            return;
        }

        Debug.LogWarning("Gun test: GhostTarget was not found after gas_interlude_complete.");
    }
}
#endif
