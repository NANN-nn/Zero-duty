#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Editor-only previews: Ctrl+Alt+1/2/3 after starting a shift.</summary>
public sealed class NativeEndingPreviewShortcut : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (FindAnyObjectByType<NativeEndingPreviewShortcut>() == null)
            new GameObject("Native ending preview shortcuts").AddComponent<NativeEndingPreviewShortcut>();
    }

    private void Update()
    {
        Keyboard k = Keyboard.current;
        if (!Application.isFocused || k == null || !ShiftStartMenu.ShiftStarted
            || !k.ctrlKey.isPressed || !k.altKey.isPressed || ShiftAudioDirector.EndingActive) return;
        if (k.digit1Key.wasPressedThisFrame) ShiftEndingScreen.Instance.ShowDataTransfer();
        else if (k.digit2Key.wasPressedThisFrame) ShiftEndingScreen.Instance.ShowLockdown();
        else if (k.digit3Key.wasPressedThisFrame) ShiftEndingScreen.Instance.ShowFailure();
    }
}
#endif
