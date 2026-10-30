using UnityEngine;

/// <summary>Disperses a deadline scare without resolving or extending the anomaly.</summary>
[DisallowMultipleComponent]
public sealed class PressureGhostTarget : MonoBehaviour, IGunHitTarget
{
    private CorridorPressureSequence owner;

    public void Initialize(CorridorPressureSequence pressure) => owner = pressure;

    public void Hit()
    {
        if (!isActiveAndEnabled || owner == null) return;
        if (owner.DispelWarningGhost()) ShiftAudioDirector.PlayGhostHit();
    }
}
