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

        // A player-ridden horse collects on behalf of its rider (see HorsePickupProxy): the heal
        // applies to the player, and — with the "Stable Diet" node — also to the horse itself.
        HorsePickupProxy proxy = collector.GetComponentInParent<HorsePickupProxy>();
        if (proxy != null && proxy.Target != null)
        {
            collector = proxy.Target;
        }
        else
        {
            proxy = null;
        }

        Player player = collector.GetComponentInParent<Player>();
        if (player == null || player.Health == null)
        {
            return false;
        }

        Health health = player.Health;

        float percent = player.Stats != null
            ? Mathf.Clamp01(player.Stats.GetValue(StatType.HealthPackHealPercent))
            : 0f;
        if (percent <= 0f)
        {
            return false;
        }

        bool playerNeedsHeal = !health.IsDead && health.CurrentHealth < health.MaxHealth;

        // Stable Diet: packs the ridden horse runs over also heal the horse.
        Health horseHealth = null;
        if (proxy != null && proxy.HorseHealth != null && player.Stats != null
            && player.Stats.GetValue(StatType.HorseHealFromPacks) >= 1f)
        {
            Health candidate = proxy.HorseHealth;
            if (!candidate.IsDead && candidate.CurrentHealth < candidate.MaxHealth)
            {
                horseHealth = candidate;
            }
        }

        // A pack that helps neither body stays on the ground until needed.
        if (!playerNeedsHeal && horseHealth == null)
        {
            return false;
        }

        collected = true;

        // The popup shows what was actually restored, which a near-full health bar caps.
        float healed = 0f;
        if (playerNeedsHeal)
        {
            float missing = health.MaxHealth - health.CurrentHealth;
            healed = Mathf.Min(health.MaxHealth * percent, missing);
            health.Heal(health.MaxHealth * percent);
        }

        if (horseHealth != null)
        {
            float horseMissing = horseHealth.MaxHealth - horseHealth.CurrentHealth;
            float horseHealed = Mathf.Min(horseHealth.MaxHealth * percent, horseMissing);
            horseHealth.Heal(horseHealth.MaxHealth * percent);
            if (healed <= 0f)
            {
                healed = horseHealed;
            }
        }

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
