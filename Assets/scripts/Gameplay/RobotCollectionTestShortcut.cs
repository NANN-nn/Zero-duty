#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Editor-only F11 test using the normal robot pickup and reward flow.</summary>
public sealed class RobotCollectionTestShortcut : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForPlayMode()
    {
        if (FindAnyObjectByType<RobotCollectionTestShortcut>() != null) return;
        new GameObject("Robot Collection Test (Editor Only)").AddComponent<RobotCollectionTestShortcut>();
    }

    private void Update()
    {
        if (!Application.isFocused || Keyboard.current == null
            || !Keyboard.current.f11Key.wasPressedThisFrame) return;
        if (!ShiftStartMenu.ShiftStarted || Time.timeScale <= 0f || ShiftAudioDirector.EndingActive)
        {
            Debug.Log("Robot test: start a shift and close the pause/ending screen first.", this);
            return;
        }
        PlayerInteractor actor = FindAnyObjectByType<PlayerInteractor>();
        if (actor == null || !actor.InputEnabled || OptionalPropActivities.Busy
            || MonitorViewManager.ActiveSession != null || ReferencePictureView.ActiveView != null)
        {
            Debug.Log("Robot test: stand up and close any note or equipment panel first.", this);
            return;
        }
        OptionalPropActivities activities = OptionalPropActivities.Instance;
        if (activities.RobotsCollected >= OptionalPropActivities.RobotGoal)
        {
            Debug.Log("Robot test: already collected all five. Restart Play to see the reward again.", this);
            return;
        }
        foreach (OptionalPropInteraction prop in FindObjectsByType<OptionalPropInteraction>(FindObjectsSortMode.None))
            if (prop.Kind == OptionalPropInteraction.PropKind.Robot && prop.CanInteract(actor))
                prop.Interact(actor);
        Debug.Log($"Robot test: collected {activities.RobotsCollected}/5; the handbook-desk reward shot will play automatically.", this);
    }
}
#endif
