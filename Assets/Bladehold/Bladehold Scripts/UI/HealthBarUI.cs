using MoreMountains.Tools;
using UnityEngine;

/// <summary>
///     Binds an <see cref="MMHealthBar" /> to a <see cref="Health" />: refreshes the bar whenever
///     health changes. It listens to <see cref="Health.OnHealthChanged" />; Health stays unaware of
///     the bar. Hiding at zero, lerping and bump-on-change are all handled by the MMHealthBar itself.
///     If an <see cref="EnemyRagdoll" /> is present, it will follow the ragdoll's pelvis.
/// </summary>
public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private MMHealthBar healthBar;
    [SerializeField] private EnemyRagdoll ragdoll;

    private bool anyError = false;
    private Vector3 initialLocalPosition;
    private Transform parentTransform;

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponentInParent<Health>();
        }

        if (healthBar == null)
        {
            healthBar = GetComponent<MMHealthBar>();
        }

        if (ragdoll == null)
        {
            ragdoll = GetComponentInParent<EnemyRagdoll>();
        }
    }

    private void Start()
    {
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }

        if (healthBar == null)
        {
            Debug.LogError("MMHealthBar component is not assigned or found on the GameObject.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        Collider col = GetComponentInParent<Collider>();
        if (col != null)
        {
            float topY = col.bounds.max.y;
            if (transform.position.y < topY + 0.2f)
            {
                transform.position = new Vector3(transform.position.x, topY + 0.2f, transform.position.z);
            }
        }

        initialLocalPosition = transform.localPosition;
        parentTransform = transform.parent;
        
        if (ragdoll == null)
        {
            ragdoll = GetComponentInParent<EnemyRagdoll>();
        }

        health.OnHealthChanged += Refresh;
        health.OnDied += HandleDied;

        // Start-order safety: if Health.Start already ran, its initial OnHealthChanged fired before we
        // subscribed — refresh now; if it hasn't run yet, its event will overwrite this shortly.
        Refresh();
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnHealthChanged -= Refresh;
            health.OnDied -= HandleDied;
        }
    }

    private void LateUpdate()
    {
        if (ragdoll != null && ragdoll.IsRagdolled && ragdoll.Pelvis != null)
        {
            // The ragdoll pelvis has moved away from the root.
            // Move this GameObject to track the Pelvis position + original offset.
            Vector3 worldOffset = parentTransform != null ? parentTransform.TransformVector(initialLocalPosition) : initialLocalPosition;
            transform.position = ragdoll.Pelvis.position + worldOffset;
        }
        else
        {
            // Restore local position when not ragdolled (or if pelvis is missing)
            transform.localPosition = initialLocalPosition;
        }
    }

    private void HandleDied()
    {
        if (healthBar != null)
        {
            healthBar.ShowBar(false);
        }
    }

    private void Refresh()
    {
        if (health != null && health.IsDead)
        {
            if (healthBar != null)
            {
                healthBar.ShowBar(false);
            }
            return;
        }

        healthBar.UpdateBar(health.CurrentHealth, 0f, health.MaxHealth, show: true);
    }
}
