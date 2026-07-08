using UnityEngine;

/// <summary>
///     A cosmetic arrow prop left embedded where an arrow struck, spawned by
///     <see cref="StuckArrowSpawner" />. Parented to the collider the arrow hit so it rides the
///     enemy's animation (and ragdoll bones, once <see cref="EnemyRagdoll" /> takes over the rig)
///     and is destroyed along with the corpse when <see cref="CorpseDespawner" /> cleans up — the
///     <see cref="lifetime" /> timer is just a backstop so arrows in long-lived targets don't
///     accumulate. Author the prefab with the arrow's <b>tip at the origin, pointing down +Z</b>:
///     <see cref="Embed" /> sinks the origin <c>penetrationDepth</c> metres along the flight
///     direction, so the tip ends up that deep inside the surface.
/// </summary>
public class StuckArrow : MonoBehaviour
{
    [Tooltip("Seconds before the arrow disappears on its own. 0 = never — it still goes when whatever it's stuck in is destroyed.")]
    [SerializeField] private float lifetime = 20f;

    private void Start()
    {
        if (lifetime > 0f)
        {
            Destroy(gameObject, lifetime);
        }
    }

    /// <summary>
    ///     Plants the arrow at <paramref name="point" /> facing along <paramref name="direction" />,
    ///     sunk in by <paramref name="penetrationDepth" /> with a random roll around the shaft so a
    ///     cluster of arrows doesn't fletch-align, then parents it to <paramref name="parent" />
    ///     (world pose preserved, so scaled enemies don't distort the prop).
    /// </summary>
    public void Embed(Vector3 point, Vector3 direction, float penetrationDepth, Transform parent)
    {
        transform.SetPositionAndRotation(
            point + direction * penetrationDepth,
            Quaternion.LookRotation(direction) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
        if (parent != null)
        {
            transform.SetParent(parent, worldPositionStays: true);
        }
    }
}
