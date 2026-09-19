using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Lobbed ballistic projectile for Catapult defenses.
///     Arcs toward the target position and explodes into a fiery AOE burst on impact.
/// </summary>
public class CatapultProjectile : MonoBehaviour
{
    [SerializeField] private float arcHeight = 6f;
    [SerializeField] private float flightDuration = 1.2f;
    [SerializeField] private float splashRadius = 4.5f;
    [SerializeField] private GameObject explosionVfxPrefab;
    [SerializeField] private AudioClip explosionSfx;

    private Vector3 startPoint;
    private Vector3 targetPoint;
    private float damageAmount;
    private float elapsedTime = 0f;
    private bool hasExploded = false;

    public void Launch(Vector3 start, Vector3 target, float damage, float duration = 1.2f, float radius = 4.5f)
    {
        startPoint = start;
        targetPoint = target;
        damageAmount = damage;
        flightDuration = Mathf.Max(0.2f, duration);
        splashRadius = radius;
        transform.position = start;
        elapsedTime = 0f;
        hasExploded = false;
    }

    private void Update()
    {
        if (hasExploded) return;

        elapsedTime += Time.deltaTime;
        float t = Mathf.Clamp01(elapsedTime / flightDuration);

        // Parabolic arc: linear horizontal + parabola vertical
        Vector3 currentPos = Vector3.Lerp(startPoint, targetPoint, t);
        float heightOffset = 4f * arcHeight * t * (1f - t);
        currentPos.y += heightOffset;

        Vector3 moveDir = currentPos - transform.position;
        if (moveDir != Vector3.zero)
        {
            transform.forward = moveDir.normalized;
        }
        transform.position = currentPos;

        if (t >= 1f)
        {
            Explode();
        }
    }

    public void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;

        Vector3 impactPos = targetPoint;

        // Visual and Sound effects
        if (explosionVfxPrefab != null)
        {
            Instantiate(explosionVfxPrefab, impactPos, Quaternion.identity);
        }
        else if (ElementalEffectsManager.Instance != null && ElementalEffectsManager.Instance.fireStatusVfx != null)
        {
            Instantiate(ElementalEffectsManager.Instance.fireStatusVfx, impactPos, Quaternion.identity);
        }

        if (explosionSfx != null)
        {
            AudioSource.PlayClipAtPoint(explosionSfx, impactPos);
        }

        // Damage & Ignite in splash radius
        Collider[] hits = Physics.OverlapSphere(impactPos, splashRadius);
        HashSet<Health> damagedEntities = new HashSet<Health>();

        foreach (Collider hit in hits)
        {
            if (hit == null) continue;
            Health h = hit.GetComponentInParent<Health>();
            if (h == null || damagedEntities.Contains(h) || h.IsDead) continue;
            if (Player.Instance != null && h.transform.root == Player.Instance.transform.root) continue;

            damagedEntities.Add(h);

            Damage dmg = new Damage
            {
                value = damageAmount,
                type = DamageType.elemental,
                elementId = "Fire",
                isPlayerDamage = true,
                sourcePosition = impactPos,
                source = Player.Instance != null ? Player.Instance.Damageable : null
            };

            h.ReceiveDamage(dmg);
            EnemyStatusManager.GetOrAdd(h)?.ApplyStatus("Fire");
        }

        Destroy(gameObject);
    }
}
