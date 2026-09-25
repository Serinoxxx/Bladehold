using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Applies an <see cref="EnemyRosterSO" /> row (<see cref="EnemyDefinition" />) to a spawned enemy
///     instance: health/damage/gold/speed/knockback/scale overrides, set right after Instantiate and
///     before the enemy's Start, so the shared ScriptableObjects are never mutated.
/// </summary>
public static class EnemyDefinitionApplier
{
    /// <summary>Applies a roster row's stat overrides to a freshly spawned instance. Blank CSV cells
    /// leave the prefab's own ScriptableObject values in effect; the shared SOs are never mutated.
    /// Shared by <see cref="SurvivorsSpawner" />, <see cref="MinionSpawner" />, the DevConsole and
    /// <c>EnemyZoo</c> so every spawn path is roster-faithful through one source of truth.</summary>
    public static void Apply(GameObject enemy, EnemyDefinition def)
    {
        ApplyDefinitionInternal(enemy, def, preserveHealthFraction: false);
    }

    /// <summary>Re-applies a roster row's overrides to an already-running instance (the Enemy
    /// Manager tweaking a live zoo enemy). Identical to <see cref="Apply" /> except the
    /// health override keeps the instance's current damage fraction instead of refilling it.</summary>
    public static void ApplyLive(GameObject enemy, EnemyDefinition def)
    {
        ApplyDefinitionInternal(enemy, def, preserveHealthFraction: true);
    }

    private static void ApplyDefinitionInternal(GameObject enemy, EnemyDefinition def, bool preserveHealthFraction)
    {
        if (def.health.HasValue)
        {
            enemy.GetComponent<Health>()?.SetMaxHealth(def.health.Value, preserveHealthFraction);
        }
        if (def.damage.HasValue)
        {
            enemy.GetComponent<AIAttack>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<LightningBallAttack>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<HomingOrbAttack>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<RadialBurstAttack>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<LightningStormAttack>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<TrollSlamAttack>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<BomberAttack>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<MountedKnightBrain>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<HookProjectileAttack>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<WhirlwindAttack>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<SlayerDashAttack>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<LeapSlamAttack>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<PinballCharge>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<BubblerCaster>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<AssassinAttack>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<PowderKegAttack>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<BannermanAura>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<BulwarkAttack>()?.SetDamage(def.damage.Value);
            enemy.GetComponent<CaptainKombustaController>()?.SetDamage(def.damage.Value);
        }
        if (def.minGold.HasValue)
        {
            enemy.GetComponent<CoinDropper>()?.SetCoinDrop(def.minGold.Value, def.maxGold.Value);
        }
        if (def.speed.HasValue)
        {
            enemy.GetComponent<AIMovement>()?.SetSpeed(def.speed.Value);
        }
        if (def.knockbackResistance.HasValue)
        {
            enemy.GetComponent<KnockbackReceiver>()?.SetResistance(def.knockbackResistance.Value);
        }
        if (!Mathf.Approximately(def.scale, 1f) || enemy.transform.localScale.y != 1f)
        {
            float targetScale = def.scale;
            enemy.transform.localScale = Vector3.one * targetScale;

            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                // Reset agent dimensions relative to base goblin prefab defaults (0.5 radius, 2.0 height)
                // and scale them, rather than multiplying the active agent size which would double-scale.
                agent.radius = 0.5f * targetScale;
                agent.height = 2f * targetScale;
                
                // baseOffset is local-space: the agent already applies the transform scale.
                // Scaling it here again buries large enemies below the NavMesh.
                agent.baseOffset = -0.08f;
            }
        }
        else
        {
            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.baseOffset = -0.08f;
            }
        }
    }
}
