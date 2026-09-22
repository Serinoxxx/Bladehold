using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Dynamite stick projectile thrown by Captain Kombusta.
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
        AudioClip sfxFuse = null)
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

        transform.position = startPosition;

        // Visual telegraph on the ground marking the 2m radius blast zone
        if (telegraphPrefab != null)
        {
            telegraphInstance = Instantiate(telegraphPrefab, targetPosition + Vector3.up * 0.05f, Quaternion.identity);
            Vector3 currentScale = telegraphInstance.transform.localScale;
            telegraphInstance.transform.localScale = new Vector3(explosionRadius * 2f, currentScale.y, explosionRadius * 2f);
        }
        else
        {
            // Fallback ground indicator if no prefab assigned
            telegraphInstance = CreateFallbackTelegraph(targetPosition, explosionRadius);
        }

        if (sfxFuse != null)
        {
            AudioSource.PlayClipAtPoint(sfxFuse, transform.position, 0.6f);
        }

        EnsureVisualStick();
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

    private void EnsureVisualStick()
    {
        // If the projectile doesn't have a mesh or renderer, build a red cylinder dynamite stick
        if (GetComponentInChildren<Renderer>() == null)
        {
            GameObject stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stick.name = "DynamiteMesh";
            stick.transform.SetParent(transform, false);
            stick.transform.localScale = new Vector3(0.12f, 0.25f, 0.12f);

            Collider col = stick.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer ren = stick.GetComponent<Renderer>();
            if (ren != null)
            {
                Material redMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                redMat.color = new Color(0.85f, 0.15f, 0.1f);
                ren.material = redMat;
            }
        }
    }

    private GameObject CreateFallbackTelegraph(Vector3 center, float radius)
    {
        GameObject go = new GameObject("DynamiteFallbackTelegraph");
        go.transform.position = center + Vector3.up * 0.05f;

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.startWidth = 0.08f;
        lr.endWidth = 0.08f;
        lr.startColor = new Color(1f, 0.2f, 0f, 0.7f);
        lr.endColor = new Color(1f, 0.2f, 0f, 0.7f);

        Shader spriteShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
        if (spriteShader != null) lr.material = new Material(spriteShader);

        int segments = 32;
        lr.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }

        return go;
    }

    private void OnDestroy()
    {
        if (telegraphInstance != null)
        {
            Destroy(telegraphInstance);
        }
    }
}
