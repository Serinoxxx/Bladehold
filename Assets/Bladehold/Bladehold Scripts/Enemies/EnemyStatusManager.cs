using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class EnemyStatusManager : MonoBehaviour
{
    private Health health;
    private Dictionary<string, float> activeStatuses = new Dictionary<string, float>();
    
    // Status visual elements
    private GameObject discordRingVisual;
    private GameObject fireVisual;
    private GameObject iceVisual;
    private GameObject frozenVisual;

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
            health.OnDied += HandleDeath;
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.ScaleDamageTaken -= HandleScaleDamageTaken;
            health.OnDamaged -= HandleDamageReceived;
            health.OnDied -= HandleDeath;
        }
    }

    private void HandleDeath()
    {
        CleanupAllVisuals();
    }

    private void CleanupAllVisuals()
    {
        if (discordRingVisual != null) Destroy(discordRingVisual);
        if (fireVisual != null) Destroy(fireVisual);
        if (iceVisual != null) Destroy(iceVisual);
        if (frozenVisual != null) Destroy(frozenVisual);
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

        UpdateDiscordVisual();
    }

    private void UpdateDiscordVisual()
    {
        if (GetUniqueElementCount() >= 2)
        {
            if (discordRingVisual == null && ElementalEffectsManager.Instance != null && ElementalEffectsManager.Instance.discordRingVfx != null)
            {
                discordRingVisual = Instantiate(ElementalEffectsManager.Instance.discordRingVfx, transform.position + Vector3.up * 1.5f, Quaternion.identity, transform);
                if (ElementalEffectsManager.Instance.discordAppliedSfx != null)
                {
                    AudioSource.PlayClipAtPoint(ElementalEffectsManager.Instance.discordAppliedSfx, transform.position);
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
                activeStatuses["Frozen"] = 2f; 
                SlowStatus.GetOrAdd(health)?.ApplySlow(1.0f, 2f); 
                
                if (frozenVisual == null && ElementalEffectsManager.Instance != null && ElementalEffectsManager.Instance.frozenStatusVfx != null)
                {
                    frozenVisual = Instantiate(ElementalEffectsManager.Instance.frozenStatusVfx, transform.position, Quaternion.identity, transform);
                    if (ElementalEffectsManager.Instance.frozenSfx != null)
                    {
                        AudioSource.PlayClipAtPoint(ElementalEffectsManager.Instance.frozenSfx, transform.position);
                    }
                }
            }
            else
            {
                activeStatuses["Ice"] = 3f;
                SlowStatus.GetOrAdd(health)?.ApplySlow(0.35f, 3f);
                if (iceVisual == null && ElementalEffectsManager.Instance != null && ElementalEffectsManager.Instance.iceStatusVfx != null)
                {
                    iceVisual = Instantiate(ElementalEffectsManager.Instance.iceStatusVfx, transform.position, Quaternion.identity, transform);
                    if (ElementalEffectsManager.Instance.statusAppliedSfx != null)
                    {
                        AudioSource.PlayClipAtPoint(ElementalEffectsManager.Instance.statusAppliedSfx, transform.position);
                    }
                }
            }
        }
        else if (elementId.Equals("Fire", System.StringComparison.OrdinalIgnoreCase))
        {
            activeStatuses["Fire"] = 4f;
            ignitedTickTimer = 0f;
            if (fireVisual == null && ElementalEffectsManager.Instance != null && ElementalEffectsManager.Instance.fireStatusVfx != null)
            {
                fireVisual = Instantiate(ElementalEffectsManager.Instance.fireStatusVfx, transform.position, Quaternion.identity, transform);
                if (ElementalEffectsManager.Instance.statusAppliedSfx != null)
                {
                    AudioSource.PlayClipAtPoint(ElementalEffectsManager.Instance.statusAppliedSfx, transform.position);
                }
            }
        }
        else if (elementId.Equals("Lightning", System.StringComparison.OrdinalIgnoreCase))
        {
            activeStatuses["Lightning"] = 5f;
        }
        
        UpdateDiscordVisual();
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
        if (statusId.Equals("Fire", System.StringComparison.OrdinalIgnoreCase) && fireVisual != null)
        {
            Destroy(fireVisual);
            fireVisual = null;
        }
        else if (statusId.Equals("Ice", System.StringComparison.OrdinalIgnoreCase) && iceVisual != null)
        {
            Destroy(iceVisual);
            iceVisual = null;
        }
        else if (statusId.Equals("Frozen", System.StringComparison.OrdinalIgnoreCase) && frozenVisual != null)
        {
            Destroy(frozenVisual);
            frozenVisual = null;
        }
    }

    private float HandleScaleDamageTaken(Damage damage)
    {
        if (GetUniqueElementCount() >= 2)
        {
            return 1.40f; 
        }
        return 1f;
    }

    private void HandleDamageReceived(Damage damage)
    {
        if (damage.type == DamageType.elemental) return;

        bool isFire = string.Equals(damage.elementId, "Fire", System.StringComparison.OrdinalIgnoreCase);
        bool isLightning = string.Equals(damage.elementId, "Lightning", System.StringComparison.OrdinalIgnoreCase);
        bool isIce = string.Equals(damage.elementId, "Ice", System.StringComparison.OrdinalIgnoreCase);

        // Check Synergies FIRST before applying new status
        if (damage.isPlayerDamage && Player.Instance != null && Player.Instance.Stats != null)
        {
            // Thermal Shock
            if (Player.Instance.Stats.GetValue(StatType.DuoThermalShock) > 0f)
            {
                if (isFire && (HasStatus("Ice") || HasStatus("Frozen")))
                {
                    RemoveStatus("Ice");
                    RemoveStatus("Frozen");
                    
                    float burstDamage = damage.value * 1.5f;
                    Damage burst = new Damage { value = burstDamage, type = DamageType.elemental, source = Player.Instance.Damageable, isPlayerDamage = true };
                    health.ReceiveDamage(burst);
                    
                    if (ElementalEffectsManager.Instance != null && ElementalEffectsManager.Instance.thermalShockVfx != null)
                    {
                        Instantiate(ElementalEffectsManager.Instance.thermalShockVfx, transform.position, Quaternion.identity);
                        if (ElementalEffectsManager.Instance.thermalShockSfx != null) AudioSource.PlayClipAtPoint(ElementalEffectsManager.Instance.thermalShockSfx, transform.position);
                    }
                    
                    Collider[] hits = Physics.OverlapSphere(transform.position, 4f);
                    foreach (var hit in hits)
                    {
                        Health target = hit.GetComponentInParent<Health>();
                        if (target != null && target != health && !target.IsDead && (target.transform.root != Player.Instance.transform.root))
                        {
                            SlowStatus.GetOrAdd(target)?.ApplySlow(0.5f, 3f);
                        }
                    }
                    return; // skip further element apply
                }
            }

            // Plasma Overload
            if (Player.Instance.Stats.GetValue(StatType.DuoPlasmaOverload) > 0f)
            {
                if (isLightning && HasStatus("Fire"))
                {
                    float remainingTicks = Mathf.Max(1f, activeStatuses["Fire"]);
                    float dotDamage = Player.Instance.Stats.GetValue(StatType.SwordDamage) * 0.15f * remainingTicks;
                    
                    RemoveStatus("Fire");
                    
                    if (ElementalEffectsManager.Instance != null && ElementalEffectsManager.Instance.plasmaOverloadVfx != null)
                    {
                        Instantiate(ElementalEffectsManager.Instance.plasmaOverloadVfx, transform.position, Quaternion.identity);
                        if (ElementalEffectsManager.Instance.plasmaOverloadSfx != null) AudioSource.PlayClipAtPoint(ElementalEffectsManager.Instance.plasmaOverloadSfx, transform.position);
                    }
                    
                    Collider[] hits = Physics.OverlapSphere(transform.position, 4f);
                    foreach (var hit in hits)
                    {
                        Health target = hit.GetComponentInParent<Health>();
                        if (target != null && target != health && !target.IsDead && (target.transform.root != Player.Instance.transform.root))
                        {
                            Damage explosion = new Damage { value = dotDamage, type = DamageType.elemental, source = Player.Instance.Damageable, isPlayerDamage = true };
                            target.ReceiveDamage(explosion);
                        }
                    }
                    return; 
                }
            }

            // Superconductor
            if (Player.Instance.Stats.GetValue(StatType.DuoSuperconductor) > 0f)
            {
                if (isLightning && HasStatus("Frozen"))
                {
                    float clDamage = 40f; 
                    
                    if (ElementalEffectsManager.Instance != null && ElementalEffectsManager.Instance.superconductorVfx != null)
                    {
                        Instantiate(ElementalEffectsManager.Instance.superconductorVfx, transform.position, Quaternion.identity);
                        if (ElementalEffectsManager.Instance.superconductorSfx != null) AudioSource.PlayClipAtPoint(ElementalEffectsManager.Instance.superconductorSfx, transform.position);
                    }
                    
                    Collider[] hits = Physics.OverlapSphere(transform.position, 6f);
                    int targetsHit = 0;
                    foreach (var hit in hits)
                    {
                        if (targetsHit >= 5) break;
                        Health target = hit.GetComponentInParent<Health>();
                        if (target != null && target != health && !target.IsDead && (target.transform.root != Player.Instance.transform.root))
                        {
                            Damage clDmg = new Damage { value = clDamage, type = DamageType.elemental, source = Player.Instance.Damageable, isPlayerDamage = true };
                            target.ReceiveDamage(clDmg);
                            targetsHit++;
                        }
                    }
                }
            }
        }

        // Apply new status
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
