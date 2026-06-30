using System.Collections.Generic;
using UnityEngine;

public class DamageTrigger : MonoBehaviour
{
    [SerializeField] DamageTriggerSO damageTriggerSO;
    [SerializeField] DamageSO damageSO;

    [Tooltip("The attacker that wields this trigger; it is never damaged by it. Leave empty to use the nearest IDamageable up the parent hierarchy (e.g. the character this weapon is attached to).")]
    [SerializeField] GameObject owner;

    readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
    readonly Collider[] overlapBuffer = new Collider[32];

    IDamageable ownerDamageable;

    bool isActive;
    float deactivateTime;

    bool anyError = false;

    private void Start()
    {
        if (damageTriggerSO == null)
        {
            Debug.LogError("DamageTriggerSO is not assigned in the inspector.");
            anyError = true;
        }

        if (damageSO == null)
        {
            Debug.LogError("DamageSO is not assigned in the inspector.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        // Resolve the wielder so the trigger never damages whoever swings it. The weapon is usually
        // a child of the attacker, so default to the nearest IDamageable up the hierarchy.
        GameObject ownerRoot = owner != null ? owner : gameObject;
        ownerDamageable = ownerRoot.GetComponentInParent<IDamageable>();
    }

    public void Activate()
    {
        if (anyError) return;

        isActive = true;
        deactivateTime = Time.time + damageTriggerSO.duration;
        hitTargets.Clear();
    }

    void Update()
    {
        if (anyError) return;
        if (!isActive) return;

        ApplyDamageInRadius();

        if (hitTargets.Count >= damageTriggerSO.maxHits || Time.time >= deactivateTime)
        {
            isActive = false;
        }
    }

    void ApplyDamageInRadius()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, damageTriggerSO.radius, overlapBuffer);

        for (int i = 0; i < count; i++)
        {
            if (hitTargets.Count >= damageTriggerSO.maxHits) return;

            if (!overlapBuffer[i].TryGetComponent(out IDamageable damageable))
            {
                damageable = overlapBuffer[i].GetComponentInParent<IDamageable>();
            }

            if (damageable == null) continue;
            // Never damage the wielder of this trigger.
            if (damageable == ownerDamageable) continue;
            if (!hitTargets.Add(damageable)) continue;

            damageable.ReceiveDamage(new Damage
            {
                value = damageSO.baseDamage,
            });
        }
    }
}
