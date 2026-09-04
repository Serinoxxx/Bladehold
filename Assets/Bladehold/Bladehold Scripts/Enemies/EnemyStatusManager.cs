using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class EnemyStatusManager : MonoBehaviour
{
    private Health health;
    private Dictionary<string, float> activeStatuses = new Dictionary<string, float>();
    
    // Status visual elements
    private GameObject discordRingVisual;
    private float discordRingTimer = 0f;
    private float ignitedTickTimer = 0f;

    public static EnemyStatusManager GetOrAdd(Component target)
    {
        if (target == null) return null;
        Health health = target.GetComponentInParent<Health>();
        if (health == null || health.IsDead) return null;
        if (!health.gameObject.TryGetComponent(out EnemyStatusManager status))
        {
            status = health.gameObject.AddComponent<EnemyStatusManager>();
        }
        return status;
    }

    public bool HasStatus(string statusId)
    {
        return activeStatuses.ContainsKey(statusId);
    }

    public int GetUniqueElementCount()
    {
        int count = 0;
        if (HasStatus("Fire")) count++;
        if (HasStatus("Ice")) count++;
        if (HasStatus("Lightning")) count++;
        return count;
    }

    private void Awake()
    {
        health = GetComponent<Health>();
        if (health != null)
        {
            health.ScaleDamageTaken += HandleScaleDamageTaken;
            health.OnDamaged += HandleDamageReceived;
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.ScaleDamageTaken -= HandleScaleDamageTaken;
            health.OnDamaged -= HandleDamageReceived;
        }
    }

    private void Update()
    {
        if (health != null && health.IsDead) return;

        List<string> expired = new List<string>();
        foreach (var kvp in activeStatuses)
        {
            float newTime = kvp.Value - Time.deltaTime;
            if (newTime <= 0f) expired.Add(kvp.Key);
            else activeStatuses[kvp.Key] = newTime;
        }

        foreach (var id in expired)
        {
            activeStatuses.Remove(id);
            OnStatusExpired(id);
        }

        if (HasStatus("Fire"))
        {
            ignitedTickTimer += Time.deltaTime;
            if (ignitedTickTimer >= 1f)
            {
                ignitedTickTimer -= 1f;
                float dotDamage = (Player.Instance != null && Player.Instance.Stats != null) ? Player.Instance.Stats.GetValue(StatType.SwordDamage) * 0.15f : 10f;
                Damage dot = new Damage { value = dotDamage, type = DamageType.elemental, source = Player.Instance?.Damageable, isPlayerDamage = true };
                health.ReceiveDamage(dot);
            }
        }

        if (GetUniqueElementCount() >= 2)
        {
            if (discordRingVisual == null)
            {
                discordRingVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                discordRingVisual.transform.SetParent(transform);
                discordRingVisual.transform.localPosition = Vector3.up * 2f;
                discordRingVisual.transform.localScale = Vector3.one * 0.5f;
                Destroy(discordRingVisual.GetComponent<Collider>());
                
                if (TryGetComponent(out Animator anim))
                {
                    anim.SetTrigger("HitReact");
                }
            }
        }
        else if (discordRingVisual != null)
        {
            Destroy(discordRingVisual);
            discordRingVisual = null;
        }
    }

    public void ApplyStatus(string elementId)
    {
        if (string.IsNullOrEmpty(elementId)) return;

        if (elementId.Equals("Ice", System.StringComparison.OrdinalIgnoreCase))
        {
            if (HasStatus("Ice") && activeStatuses["Ice"] > 0f)
            {
                activeStatuses["Frozen"] = 2f; // Hard stun
                SlowStatus.GetOrAdd(health)?.ApplySlow(1.0f, 2f); // 100% slow
            }
            else
            {
                activeStatuses["Ice"] = 3f;
                SlowStatus.GetOrAdd(health)?.ApplySlow(0.35f, 3f);
            }
        }
        else if (elementId.Equals("Fire", System.StringComparison.OrdinalIgnoreCase))
        {
            activeStatuses["Fire"] = 4f;
            ignitedTickTimer = 0f;
        }
        else if (elementId.Equals("Lightning", System.StringComparison.OrdinalIgnoreCase))
        {
            activeStatuses["Lightning"] = 5f;
        }
    }

    public void RemoveStatus(string statusId)
    {
        if (activeStatuses.Remove(statusId))
        {
            OnStatusExpired(statusId);
        }
    }

    private void OnStatusExpired(string statusId)
    {
        // cleanup effects if any
    }

    private float HandleScaleDamageTaken(Damage damage)
    {
        if (GetUniqueElementCount() >= 2)
        {
            return 1.40f; // +40% from Discord
        }
        return 1f;
    }

    private void HandleDamageReceived(Damage damage)
    {
        // Ignore dot/environmental if player damage
        if (damage.isPlayerDamage && !string.IsNullOrEmpty(damage.elementId))
        {
            ApplyStatus(damage.elementId);
        }

        // Conductive trigger
        if (damage.isPlayerDamage && HasStatus("Lightning") && string.IsNullOrEmpty(damage.elementId)) 
        {
            RemoveStatus("Lightning");
            
            float clDamage = (Player.Instance != null && Player.Instance.Stats != null) ? Player.Instance.Stats.GetValue(StatType.SwordDamage) : 10f;
            
            Collider[] hits = Physics.OverlapSphere(transform.position, 5f);
            int targetsHit = 0;
            foreach (Collider hit in hits)
            {
                if (targetsHit >= 2) break;
                Health targetHealth = hit.GetComponentInParent<Health>();
                if (targetHealth != null && targetHealth != health && !targetHealth.IsDead && (Player.Instance == null || targetHealth.transform.root != Player.Instance.transform.root))
                {
                    Damage clDmg = new Damage { value = clDamage, type = DamageType.elemental, source = Player.Instance?.Damageable, isPlayerDamage = true };
                    targetHealth.ReceiveDamage(clDmg);
                    targetsHit++;
                }
            }
        }
    }
}
