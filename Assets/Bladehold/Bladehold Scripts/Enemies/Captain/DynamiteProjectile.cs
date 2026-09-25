using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Dynamite stick projectile thrown by Captain Kombusta. Lives on an authored prefab (CaptainKombustaSO.dynamitePrefab) that carries the stick mesh.
///     Lobs along a parabolic arc toward a target ground position, accompanied by a telegraphed
///     ground circle. On landing, detonates into a 2m radius fiery explosion dealing 20 damage.
/// </summary>
public class DynamiteProjectile : MonoBehaviour
{
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float flightTime = 0.8f;
    private float arcHeight = 2.5f;
    private float explosionRadius = 2.0f;
    private float explosionDamage = 20.0f;
    private float knockbackForce = 8.0f;
    private IDamageable owner;

    private GameObject explosionVfxPrefab;
    private AudioClip explosionSfx;
    private GameObject telegraphInstance;

    private float elapsedTime = 0f;
    private bool isLaunched = false;
    private bool hasDetonated = false;

    /// <summary>
    ///     Initializes and launches the dynamite toward the targeted ground location.
    /// </summary>
    public void Launch(
        Vector3 start,
        Vector3 target,
        float duration,
        float arc,
        float radius,
        float damage,
        float knockback,
        IDamageable sourceOwner,
        GameObject telegraphPrefab,
        GameObject vfxPrefab,
        AudioClip sfxExplosion,
        AudioClip sfxFuse = null,
        Vector3 groundNormal = default)
    {
        startPosition = start;
        targetPosition = target;
        flightTime = Mathf.Max(0.1f, duration);
        arcHeight = arc;
        explosionRadius = radius;
        explosionDamage = damage;
        knockbackForce = knockback;
        owner = sourceOwner;
        explosionVfxPrefab = vfxPrefab;
        explosionSfx = sfxExplosion;

        if (groundNormal == Vector3.zero) groundNormal = Vector3.up;

        transform.position = startPosition;

        // Visual telegraph on the ground marking the 2m radius blast zone, aligned with ground normal
        if (telegraphPrefab != null)
        {
            Quaternion groundRot = Quaternion.FromToRotation(Vector3.up, groundNormal);
            telegraphInstance = Instantiate(telegraphPrefab, targetPosition + groundNormal * 0.05f, groundRot);
            Vector3 currentScale = telegraphInstance.transform.localScale;
            telegraphInstance.transform.localScale = new Vector3(explosionRadius * 2f, currentScale.y, explosionRadius * 2f);
        }
        else
        {
            // The blast still lands, just untelegraphed.
            Debug.LogError("[DynamiteProjectile] No telegraph prefab: assign CaptainKombustaSO.telegraphPrefab.", this);
        }

        if (sfxFuse != null)
        {
            AudioSource.PlayClipAtPoint(sfxFuse, transform.position, 0.6f);
        }

        isLaunched = true;
    }

    private void Update()
    {
        if (!isLaunched || hasDetonated) return;

        elapsedTime += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsedTime / flightTime);

        // Parabolic arc interpolation
        Vector3 currentPos = Vector3.Lerp(startPosition, targetPosition, progress);
        currentPos.y += Mathf.Sin(progress * Mathf.PI) * arcHeight;
        transform.position = currentPos;

        // Dynamic tumble spin in air
        transform.Rotate(Vector3.right, 720f * Time.deltaTime, Space.Self);
        transform.Rotate(Vector3.up, 360f * Time.deltaTime, Space.World);

        if (progress >= 1.0f)
        {
            Detonate();
        }
    }

    /// <summary>
    ///     Triggers the 2m radius explosion, deals damage, spawns VFX, and cleans up.
    /// </summary>
    public void Detonate()
    {
        if (hasDetonated) return;
        hasDetonated = true;

        if (telegraphInstance != null)
        {
            Destroy(telegraphInstance);
        }

        Vector3 blastCenter = targetPosition;

        // Spawn explosion VFX
        if (explosionVfxPrefab != null)
        {
            GameObject vfx = Instantiate(explosionVfxPrefab, blastCenter, Quaternion.identity);
            Destroy(vfx, 2.5f);
        }

        // Play explosion sound
        if (explosionSfx != null)
        {
            AudioSource.PlayClipAtPoint(explosionSfx, blastCenter, 0.9f);
        }

        // Overlap sphere dealing 20 unparryable elemental damage in 2m radius
        Collider[] hits = Physics.OverlapSphere(blastCenter, explosionRadius);
        HashSet<IDamageable> hitEntities = new HashSet<IDamageable>();

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;

            // Don't damage the captain himself from his own dynamite
            if (owner != null && hit.transform.root == (owner as Component)?.transform.root) continue;

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable != null && !hitEntities.Contains(damageable))
            {
                hitEntities.Add(damageable);

                Damage dmg = new Damage
                {
                    value = explosionDamage,
                    type = DamageType.elemental,
                    elementId = "FIRE",
                    unparryable = true,
                    isProjectile = true,
                    knockbackForce = knockbackForce,
                    sourcePosition = blastCenter,
                    source = owner
                };

                damageable.ReceiveDamage(dmg);
            }
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (telegraphInstance != null)
        {
            Destroy(telegraphInstance);
        }
    }
}
