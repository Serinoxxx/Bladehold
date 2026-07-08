using DamageNumbersPro;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     A Health Pack powerup rarely dropped by any dying enemy (see <see cref="PowerupDropper" />).
///     When the player walks over it, it heals <see cref="StatType.HealthPackHealPercent" /> of the
///     player's max health (base 10%, raised by the "Field Medic" skill line), a DamageNumbersPro
///     popup shows the health restored, and the pack is consumed. A pack is <b>not</b> consumed at
///     full health — it stays on the ground until needed (or its lifetime expires). Requires a
///     trigger <see cref="Collider" />. A sibling of <see cref="ImpulseOrb" /> rather than sharing a
///     base class, per that script's own precedent.
/// </summary>
[RequireComponent(typeof(Collider))]
public class HealthPack : MonoBehaviour
{
    [SerializeField] private DamageNumber pickupPopup;
    [Tooltip("World-space offset from the pack where the pickup popup spawns.")]
    [SerializeField] private Vector3 popupOffset = new Vector3(0f, 0.5f, 0f);
    [SerializeField] private MMF_Player pickupFeedback;
    [Tooltip("Seconds before an uncollected pack expires. 0 = never expires.")]
    [SerializeField] private float lifetime = 60f;

    private bool collected;

    private void Start()
    {
        if (lifetime > 0f)
        {
            // A pickup destroys the pack first, making this pending destroy a harmless no-op.
            Destroy(gameObject, lifetime);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCollect(other.gameObject);
    }

    /// <summary>
    ///     Collects this pack on behalf of <paramref name="collector" /> (anything holding the
    ///     <see cref="Player" /> singleton component in its parents — enemies have a Health too, so
    ///     gating on Health alone would let goblins eat med packs). Walk-over pickup routes through
    ///     here; remote collectors (the bow's Pickup Arrows) may call it directly. Returns false if
    ///     already collected, the collector isn't the player, or the player doesn't need healing —
    ///     an unneeded pack stays on the ground.
    /// </summary>
    public bool TryCollect(GameObject collector)
    {
        if (collected || collector == null)
        {
            return false;
        }

        Player player = collector.GetComponentInParent<Player>();
        if (player == null || player.Health == null)
        {
            return false;
        }

        Health health = player.Health;
        if (health.IsDead || health.CurrentHealth >= health.MaxHealth)
        {
            return false;
        }

        float percent = player.Stats != null
            ? Mathf.Clamp01(player.Stats.GetValue(StatType.HealthPackHealPercent))
            : 0f;
        if (percent <= 0f)
        {
            return false;
        }

        collected = true;

        // The popup shows what was actually restored, which a near-full health bar caps.
        float missing = health.MaxHealth - health.CurrentHealth;
        float healed = Mathf.Min(health.MaxHealth * percent, missing);
        health.Heal(health.MaxHealth * percent);

        if (pickupPopup != null && healed > 0f)
        {
            pickupPopup.Spawn(transform.position + popupOffset, Mathf.Max(1, Mathf.RoundToInt(healed)));
        }

        if (pickupFeedback != null)
        {
            pickupFeedback.PlayFeedbacks();
        }

        Destroy(gameObject);
        return true;
    }
}
