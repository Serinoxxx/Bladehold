using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Drops a <see cref="LightningOrb" /> when this enemy's <see cref="Health" /> dies. Unlike
///     <see cref="ImpulseGoblin" />'s orb drop (a chance-rolled variant of a regular goblin), the Storm
///     Witch is her own roster type, so the drop always happens — no marked-instance flag needed.
///     Listens to <see cref="Health.OnDied" />; Health stays unaware of loot. The death feedback is optional and
///     purely cosmetic: a missing one never blocks the orb drop.
/// </summary>
public class LightningOrbDropper : MonoBehaviour
{
    [SerializeField] private Health health;
    [Tooltip("Optional: played at this enemy's position on death. Nothing is authored yet.")]
    [SerializeField] private MMF_Player deathFeedback;

    private bool anyError = false;

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
    }

    private void Start()
    {
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (anyError)
        {
            return;
        }

        health.OnDied += HandleDied;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
        }
    }

    private void HandleDied()
    {
        if (deathFeedback != null)
        {
            deathFeedback.PlayFeedbacks(transform.position);
        }

        if (Player.Instance != null)
        {
            Player.Instance.GetComponentInChildren<ChainLightningBuff>()?.CollectOrb();
        }
    }
}
