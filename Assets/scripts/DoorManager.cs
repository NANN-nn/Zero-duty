using System.Collections.Generic;
using UnityEngine;

public class DoorManager : MonoBehaviour
{
    [Header("左右门 Animator（请重新绑定原 Animation 引用）")]
    public Animator DoorAnim_Left;
    public Animator DoorAnim_Right;
    [SerializeField] private string openLeftState = "OpenDoorL";
    [SerializeField] private string openRightState = "OpenDoorR";
    [SerializeField] private string closeLeftState = "CloseDoorL";
    [SerializeField] private string closeRightState = "CloseDoorR";
    [SerializeField, Min(0f)] private float blendSeconds = 0.1f;
    private readonly HashSet<Collider> occupants = new HashSet<Collider>();
    private bool isOpen;
    private bool ready;

    private void Start()
    {
        ready = ValidateDoor(DoorAnim_Left, openLeftState, closeLeftState)
            & ValidateDoor(DoorAnim_Right, openRightState, closeRightState);
        if (!ready) { enabled = false; return; }
        isOpen = occupants.Count > 0;
        PlayDoors(isOpen, true);
    }

    private bool ValidateDoor(Animator animator, string open, string close)
    {
        if (animator != null && animator.runtimeAnimatorController != null
            && animator.HasState(0, Animator.StringToHash(open))
            && animator.HasState(0, Animator.StringToHash(close))) return true;
        Debug.LogError($"{name}: 请绑定左右门 Animator，并检查第 0 层的 {open} / {close} 状态。", this);
        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled || other.GetComponentInParent<playerController>() == null) return;
        occupants.Add(other);
        SetOpen(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (occupants.Remove(other) && occupants.Count == 0) SetOpen(false);
    }

    // Rebuild occupancy if this component is re-enabled while a player is inside.
    private void OnTriggerStay(Collider other) => OnTriggerEnter(other);

    private void Update()
    {
        // Destroying/disabling a collider does not always generate OnTriggerExit.
        occupants.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
        SetOpen(occupants.Count > 0);
    }

    private void SetOpen(bool open)
    {
        if (!ready || isOpen == open) return;
        isOpen = open;
        PlayDoors(open, false);
    }

    private void PlayDoors(bool open, bool immediate)
    {
        Play(DoorAnim_Left, open ? openLeftState : closeLeftState, immediate);
        Play(DoorAnim_Right, open ? openRightState : closeRightState, immediate);
    }

    private void Play(Animator animator, string state, bool immediate)
    {
        int hash = Animator.StringToHash(state);
        if (immediate || blendSeconds <= 0f) animator.Play(hash, 0, immediate ? 1f : 0f);
        else animator.CrossFadeInFixedTime(hash, blendSeconds, 0, 0f);
    }

    private void OnDisable()
    {
        occupants.Clear();
        if (ready && DoorAnim_Left != null && DoorAnim_Right != null) PlayDoors(false, true);
        isOpen = false;
    }
}
