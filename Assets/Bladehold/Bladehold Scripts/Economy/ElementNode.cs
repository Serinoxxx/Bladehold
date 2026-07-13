using DamageNumbersPro;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     An elemental node pickup — the Mage's imbuement fuel, scattered each wave by
///     <see cref="ElementNodeSpawner" /> and rolled from chest loot. Walking over it grants its
///     element to the player's <see cref="MageImbuement" /> (a charge + timer reset, or an element
///     swap — see the buff's grant semantics); the wand's missiles also collect nodes they fly past
///     via <see cref="TryCollectRemote" /> (the "swap imbuement from a distance" half for ground
///     nodes). A fourth sibling of <see cref="Coin" />/<see cref="ImpulseOrb" />/<see cref="LightningOrb" />
///     rather than a shared base class — its contract differs: a failed grant does NOT consume the
///     node, so a non-Mage (whose disabled <see cref="MageImbuement" /> refuses the grant) walks
///     straight over it and a chest-rolled node just lies there until its lifetime expires.
///     Requires a trigger <see cref="Collider" />.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ElementNode : MonoBehaviour
{
    [Tooltip("The element this node grants.")]
    [SerializeField] private ElementType element;
    [Tooltip("Popup showing the new charge count on pickup. Optional.")]
    [SerializeField] private DamageNumber pickupPopup;
    [Tooltip("World-space offset from the node where the pickup popup spawns.")]
    [SerializeField] private Vector3 popupOffset = new Vector3(0f, 0.5f, 0f);
    [SerializeField] private MMF_Player pickupFeedback;
    [Tooltip("Seconds before an uncollected node expires. 0 = never expires.")]
    [SerializeField] private float lifetime = 60f;

    private bool collected;

    /// <summary>This node's element, for spawners/loot tables that inspect prefabs.</summary>
    public ElementType Element => element;

    private void Start()
    {
        if (lifetime > 0f)
        {
            // A pickup destroys the node first, making this pending destroy a harmless no-op.
            Destroy(gameObject, lifetime);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCollect(other.gameObject);
    }

    /// <summary>
    ///     Collects this node on behalf of <paramref name="collector" /> (anything holding an
    ///     enabled <see cref="MageImbuement" /> in its parents). Returns false — leaving the node in
    ///     the world — when there's no imbuement to grant to (a non-Mage touching a chest-rolled
    ///     node) or it was already collected.
    /// </summary>
    public bool TryCollect(GameObject collector)
    {
        if (collected || collector == null)
        {
            return false;
        }

        // A player-ridden horse collects on behalf of its rider (see HorsePickupProxy) — the buff
        // lands on the player, never on the horse.
        HorsePickupProxy proxy = collector.GetComponentInParent<HorsePickupProxy>();
        if (proxy != null && proxy.Target != null)
        {
            collector = proxy.Target;
        }

        MageImbuement imbuement = collector.GetComponentInParent<MageImbuement>();
        if (imbuement == null)
        {
            return false;
        }

        return TryCollectRemote(imbuement);
    }

    /// <summary>
    ///     Grants this node directly to <paramref name="imbuement" /> — the wand's missiles collect
    ///     nodes they fly past through here (the bow's Pickup Arrows precedent). Consumes the node
    ///     only when the grant succeeds (an enabled Mage imbuement), so it keeps lying there for
    ///     anyone else.
    /// </summary>
    public bool TryCollectRemote(MageImbuement imbuement)
    {
        if (collected || imbuement == null)
        {
            return false;
        }

        if (!imbuement.CollectNode(element))
        {
            return false;
        }

        collected = true;

        if (pickupPopup != null)
        {
            pickupPopup.Spawn(transform.position + popupOffset, imbuement.ChargeCount);
        }
        if (pickupFeedback != null)
        {
            pickupFeedback.PlayFeedbacks();
        }

        Destroy(gameObject);
        return true;
    }
}
