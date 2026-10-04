using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     One Moonlight Edge crescent: a wave of light that flies straight out from a sword swing, passing
///     through everything and damaging each enemy it crosses once. Spawned by
///     <see cref="SwordMoonlightEdgeUltimate" />, which decides damage and size at launch and owns the
///     hit feedback (reported back through the launch callback). The visual is authored on the prefab.
/// </summary>
public class MoonlightCrescent : MonoBehaviour
{
    public struct LaunchSpec
    {
        public Vector3 direction;
        public float speed;
        public float range;
        public float halfWidth;
        public float scale;
        public Damage damageTemplate;
        public Transform ownerRoot;
        /// <summary>Called once per enemy hit, with the hit point.</summary>
        public Action<Vector3> onHit;
    }

    [Tooltip("Seconds the crescent stays after reaching its range (stops damaging) so its trail can fade.")]
    [SerializeField] private float lingerSeconds = 0.4f;
    [Tooltip("Optional: visual children hidden the moment the crescent stops, leaving only trails to fade.")]
    [SerializeField] private GameObject[] hideOnEnd;
    [Tooltip("Half-height of the hit box, in metres.")]
    [SerializeField] private float halfHeight = 1.2f;

    private readonly Collider[] overlapBuffer = new Collider[64];
    private readonly HashSet<Health> hitTargets = new HashSet<Health>();

    private LaunchSpec spec;
    private float travelled;
    private bool flying;

    public void Launch(LaunchSpec launchSpec)
    {
        spec = launchSpec;
        travelled = 0f;
        hitTargets.Clear();
        flying = true;

        transform.rotation = Quaternion.LookRotation(spec.direction);
        transform.localScale = Vector3.one * Mathf.Max(0.1f, spec.scale);
    }

    private void FixedUpdate()
    {
        if (!flying) return;

        float step = Mathf.Min(spec.speed * Time.fixedDeltaTime, spec.range - travelled);
        Vector3 from = transform.position;
        Vector3 to = from + spec.direction * step;
        travelled += step;

        // Box covering this tick's travel, as wide as the crescent.
        Vector3 center = (from + to) * 0.5f;
        Vector3 halfExtents = new Vector3(spec.halfWidth * spec.scale, halfHeight, step * 0.5f + 0.5f);
        int count = Physics.OverlapBoxNonAlloc(center, halfExtents, overlapBuffer, transform.rotation, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null || (spec.ownerRoot != null && col.transform.root == spec.ownerRoot)) continue;

            Health health = col.GetComponentInParent<Health>();
            if (health == null || health.IsDead || !hitTargets.Add(health)) continue;
            if (Player.Instance != null && health == Player.Instance.Health) continue;

            Vector3 hitPoint = col.ClosestPointOnBounds(center);
            Damage d = spec.damageTemplate;
            Damage damage = new Damage
            {
                value = d.value,
                type = d.type,
                isCritical = d.isCritical,
                knockbackForce = d.knockbackForce,
                sourcePosition = from,
                direction = spec.direction,
                hitCollider = col,
                source = d.source,
                isPlayerDamage = true,
                elementId = d.elementId,
            };
            health.ReceiveDamage(damage);
            spec.onHit?.Invoke(hitPoint);
        }

        transform.position = to;

        if (travelled >= spec.range - 0.001f)
        {
            End();
        }
    }

    private void End()
    {
        flying = false;
        if (hideOnEnd != null)
        {
            foreach (GameObject go in hideOnEnd)
            {
                if (go != null) go.SetActive(false);
            }
        }
        Destroy(gameObject, lingerSeconds);
    }
}
