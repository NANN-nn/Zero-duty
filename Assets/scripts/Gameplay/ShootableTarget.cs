using UnityEngine;

[DisallowMultipleComponent]
public class ShootableTarget : MonoBehaviour
{
    [SerializeField, Min(1)] private int hitPoints = 1;
    [SerializeField] private string destroyedSignal = "ghost_destroyed";
    [SerializeField] private GameObject effectOnDestroyed;

    private void Awake() => RuntimeColliderUtility.EnsureBoxCollider(gameObject);

    public void Hit()
    {
        if (!enabled) return;
        ShiftAudioDirector.PlayGhostHit();
        hitPoints--;
        if (hitPoints > 0)
        {
            GetComponent<GhostFlickerTeleport>()?.ReactToHit();
            return;
        }
        if (effectOnDestroyed != null)
            Instantiate(effectOnDestroyed, transform.position, transform.rotation);
        GameplaySignals.Instance?.Raise(destroyedSignal);
        gameObject.SetActive(false);
    }
}
