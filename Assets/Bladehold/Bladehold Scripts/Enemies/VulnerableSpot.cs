using UnityEngine;

/// <summary>
///     Marks a collider as a vulnerable part of an enemy (e.g. a small trigger sphere on the head
///     bone). The Precision Shot skill line makes arrows deal
///     <see cref="StatType.BowPrecisionDamageBonus" /> extra damage when their hitscan ray strikes a
///     collider carrying this marker (see <see cref="PlayerBow" />). Pure marker — the bonus numbers
///     live on the stat so skill nodes can raise them, per the "expose upgradeable numbers as stats"
///     convention.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class VulnerableSpot : MonoBehaviour
{
}
