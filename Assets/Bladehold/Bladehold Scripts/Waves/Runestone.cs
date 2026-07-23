using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     A permanent elemental runestone — arena furniture placed near the gate. The Mage blasts it
///     (staff sweep or wand missile, from any range) to switch imbuement to its element, granting
///     <see cref="StatType.MageRunestoneCharges" /> charges; a same-element blast only refreshes the
///     timer (see <see cref="MageImbuement.CollectRunestone" /> — no charge farming by camping the
///     stone). Implements <see cref="IDamageable" /> so both weapons hit it for free (the Chest
///     precedent), but holds no health and never dies.
///
///     Only <b>player-owned</b> hits activate it: every player damage source stamps
///     <see cref="Damage.source" /> with the player's own damageable, and the stone requires that
///     exact identity — enemy melee stamps the attacker's Health and Storm Witch splash stamps
///     nothing, so neither can flip the Mage's element. Needs a <b>solid</b> (non-trigger) collider
///     so missiles visibly stop on it and the staff's BladeSweep raycasts register, on a layer
///     inside both weapons' hit masks but OUTSIDE <see cref="MageImbuement" />'s /
///     <see cref="ChainLightning" />'s enemy masks — chains, explosions, and flame zones must never
///     flip elements; switching is a deliberate, aimed act.
/// </summary>
public class Runestone : MonoBehaviour, IDamageable
{
    [Tooltip("The element this runestone grants.")]
    [SerializeField] private ElementType element;
    [Tooltip("Minimum seconds between activations — absorbs a multi-hit sweep landing several blows in one swing.")]
    [SerializeField] private float cooldownSeconds = 1f;
    [Tooltip("Played when a blast activates the stone (element granted / timer refreshed). Optional.")]
    [SerializeField] private MMF_Player activateFeedback;
    [Tooltip("Played when a non-Mage blasts the stone — a fizzle, so hitting it never feels ignored. Optional.")]
    [SerializeField] private MMF_Player fizzleFeedback;

    private float lastActivationTime = Mathf.NegativeInfinity;

    /// <summary>This runestone's element, for scene tooling.</summary>
    public ElementType Element => element;

    public void ReceiveDamage(Damage damage)
    {
        if (damage == null || Time.time - lastActivationTime < cooldownSeconds)
        {
            return;
        }

        // Player-owned hits only: player weapons/zones stamp their owner's damageable as source.
        // Enemy melee stamps the attacker's own Health; enemy AoE stamps nothing. Both fail here.
        if (Player.Instance == null || damage.source == null)
        {
            return;
        }

        bool isPlayerSource = ReferenceEquals(damage.source, Player.Instance.Damageable) ||
                             ReferenceEquals(damage.source, Player.Instance.Health) ||
                             (damage.source is Component comp && comp.transform.root == Player.Instance.transform.root);
        if (!isPlayerSource)
        {
            return;
        }

        lastActivationTime = Time.time;

        MageImbuement imbuement = Player.Instance.transform.root.GetComponentInChildren<MageImbuement>();
        if (imbuement != null && imbuement.CollectRunestone(element))
        {
            if (activateFeedback != null)
            {
                activateFeedback.PlayFeedbacks();
            }
        }
        else if (fizzleFeedback != null)
        {
            // A non-Mage (disabled imbuement refuses the grant) — acknowledge the hit anyway.
            fizzleFeedback.PlayFeedbacks();
        }
    }
}
