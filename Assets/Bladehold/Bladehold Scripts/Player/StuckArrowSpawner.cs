using UnityEngine;

/// <summary>
///     Leaves a <see cref="StuckArrow" /> prop embedded in every enemy an arrow strikes: subscribes
///     to <see cref="PlayerBow.OnArrowImpact" /> (the <see cref="SwordHitFeedback" /> reactive
///     pattern — the bow stays unaware of what reacts to its hits) and plants the prop at a fixed
///     anchor on the target rather than the exact raycast point, so it never rides an odd limb
///     collider: a <see cref="VulnerableSpot" /> hit anchors to that spot itself, anything else
///     anchors to the target's chest bone (<see cref="HumanBodyBones.Chest" />, falling back to
///     <see cref="HumanBodyBones.Spine" />) via <see cref="FindChestBone" /> — both jittered by
///     <see cref="anchorOffsetRadius" /> so a volley doesn't stack every shaft in one spot. Aligned to
///     the arrow's flight direction and parented to the anchor so it follows the enemy's animation and
///     ragdoll. Penetration depth varies per hit: a random roll between
///     <see cref="minPenetration" />/<see cref="maxPenetration" />, deepened by
///     <see cref="penetrationPerChargeLevel" /> for charged draws.
/// </summary>
public class StuckArrowSpawner : MonoBehaviour
{
    [Tooltip("The PlayerBow whose impacts leave stuck arrows. Auto-wired from this object or its parents.")]
    [SerializeField] private PlayerBow bow;

    [Tooltip("StuckArrow prefab — authored with the tip at the origin, pointing down +Z (see StuckArrow).")]
    [SerializeField] private StuckArrow arrowPrefab;

    [Header("Penetration (metres the tip sinks past the surface)")]
    [SerializeField] private float minPenetration = 0.15f;
    [SerializeField] private float maxPenetration = 0.35f;
    [Tooltip("Extra depth per charge level of the draw that fired the arrow — a full draw buries the shaft deeper.")]
    [SerializeField] private float penetrationPerChargeLevel = 0.05f;

    [Header("Anchor (chest bone or vulnerable spot, see summary)")]
    [Tooltip("Random jitter radius (metres) around the chest bone / vulnerable spot so a volley doesn't stack every arrow in one spot.")]
    [SerializeField] private float anchorOffsetRadius = 0.08f;

    private bool anyError = false;

    private void OnValidate()
    {
        if (bow == null)
        {
            bow = GetComponentInParent<PlayerBow>();
        }
    }

    private void Start()
    {
        if (bow == null)
        {
            Debug.LogError("PlayerBow is not assigned or found in parents; no stuck arrows will spawn.");
            anyError = true;
        }
        if (arrowPrefab == null)
        {
            Debug.LogError("StuckArrow prefab is not assigned; no stuck arrows will spawn.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        bow.OnArrowImpact += HandleImpact;
    }

    private void OnDestroy()
    {
        if (bow != null)
        {
            bow.OnArrowImpact -= HandleImpact;
        }
    }

    private void HandleImpact(ArrowImpact impact)
    {
        float depth = Random.Range(minPenetration, maxPenetration)
            + impact.chargeLevel * penetrationPerChargeLevel;

        Transform anchor = impact.hitVulnerableSpot && impact.vulnerableSpot != null
            ? impact.vulnerableSpot.transform
            : FindChestBone(impact.target);
        if (anchor == null)
        {
            anchor = impact.hitCollider != null ? impact.hitCollider.transform : null;
        }

        Vector3 point = anchor != null
            ? anchor.position + Random.insideUnitSphere * anchorOffsetRadius
            : impact.point;

        StuckArrow arrow = Instantiate(arrowPrefab);
        arrow.Embed(point, impact.direction, depth, anchor);
    }

    /// <summary>The enemy's chest bone (falling back to spine), or null if the target has no Humanoid Animator — e.g. not an enemy, or ragdoll not yet built.</summary>
    private static Transform FindChestBone(IDamageable target)
    {
        if (target is not Component component)
        {
            return null;
        }
        Animator animator = component.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            return null;
        }
        Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        return chest != null ? chest : animator.GetBoneTransform(HumanBodyBones.Spine);
    }
}
