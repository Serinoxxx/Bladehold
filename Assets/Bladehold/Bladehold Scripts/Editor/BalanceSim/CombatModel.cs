using System;

namespace Bladehold.BalanceSim
{
    /// <summary>
    ///     The sim's damage/economy arithmetic. Player swing damage mirrors the Swordsman path of
    ///     DamageTrigger.BuildDamage (DamageTrigger.cs:388-458) — SwordDamage × AllDamageMultiplier →
    ///     per-target crit roll → charge multiplier — deliberately skipping the Berserker-only
    ///     impulse/rage/pain terms (v1 scope is the Swordsman, the only fully authored class).
    ///     Gold mirrors CoinDropper.HandleDied (CoinDropper.cs:97-130).
    /// </summary>
    public static class CombatModel
    {
        public struct SwingHit
        {
            public float damage;
            public bool crit;
        }

        /// <summary>One target's damage from one swing. Crit is rolled per target, like the real sweep.</summary>
        public static SwingHit RollSwingHit(PlayerStats stats, int chargeLevel, Random rng)
        {
            float allDamage = stats.GetValue(StatType.AllDamageMultiplier);
            if (allDamage <= 0f)
            {
                allDamage = 1f; // GlobalDamageMultiplier treats an unregistered/zero stat as 1 (DamageTrigger.cs:371-379)
            }
            float value = stats.GetValue(StatType.SwordDamage) * allDamage;

            bool crit = rng.NextDouble() < stats.GetValue(StatType.CritChance);
            if (crit)
            {
                value *= stats.GetValue(StatType.CritMultiplier);
            }

            // AttackDamageMultiplier = 1 + ChargeLevel × ChargeDamageBonus (PlayerAttack.cs:193).
            value *= 1f + chargeLevel * stats.GetValue(StatType.ChargeDamageBonus);

            return new SwingHit { damage = value, crit = crit };
        }

        /// <summary>How many unique targets one swing can damage (EffectiveMaxHits, DamageTrigger.cs:362-365).</summary>
        public static int MaxHitsPerSwing(PlayerStats stats)
        {
            return Math.Max(1, (int)Math.Round(stats.GetValue(StatType.MaxHitsPerSwing)));
        }

        /// <summary>Instant gold granted on a kill — mirrors CoinDropper.HandleDied (CoinDropper.cs:97-106).</summary>
        public static int RollKillGold(SimEnemyType enemy, PlayerStats stats, Random rng)
        {
            float multiplier = stats.GetValue(StatType.GoldDropMultiplier);
            if (multiplier <= 0f)
            {
                multiplier = 1f;
            }
            int rolled = rng.Next(enemy.minGold, enemy.maxGold + 1);
            return Math.Max(1, (int)Math.Round(rolled * multiplier));
        }

        /// <summary>
        ///     The rare gold-bag pickup on top of the instant gold (CoinDropper.cs:128-132). Returns 0
        ///     when the roll fails. Unlike the instant gold, this lands on the ground — the caller
        ///     applies the profile's pickup efficiency.
        /// </summary>
        public static int RollGoldBag(SimEnemyType enemy, SimWorld world, PlayerStats stats, Random rng)
        {
            if (world.goldBagChance <= 0f || rng.NextDouble() >= world.goldBagChance)
            {
                return 0;
            }
            float multiplier = stats.GetValue(StatType.GoldDropMultiplier);
            if (multiplier <= 0f)
            {
                multiplier = 1f;
            }
            int rolled = rng.Next(enemy.minGold, enemy.maxGold + 1);
            return Math.Max(1, (int)Math.Round(rolled * world.goldBagMultiplier * multiplier));
        }

        /// <summary>
        ///     Resolves one incoming enemy hit attempt against the player's defences, in the real
        ///     pipeline order (Health.ReceiveDamage, Health.cs:108-149): avoidance (the sim's stand-in
        ///     for "the swing whiffed because the player moved") → TryBlockDamage handlers (Parry, then
        ///     the Solid auto-block) → HP loss. Returns the damage actually taken (0 = avoided/blocked).
        /// </summary>
        public static float ResolveIncomingHit(
            SimEnemyType enemy,
            PlayerProfile profile,
            PlayerStats stats,
            Random rng,
            float now,
            ref float nextBlockReadyAt)
        {
            if (rng.NextDouble() < profile.avoidance)
            {
                return 0f;
            }

            bool parryable = enemy.archetype != EnemyArchetype.Ranged; // elemental hits always pass Parry (Parry.cs)
            if (parryable)
            {
                // Parry: ParryChance rolled per hit, only while facing the attacker (Parry.cs:76-105).
                float parryChance = stats.GetValue(StatType.ParryChance) * profile.facingRatio;
                if (parryChance > 0f && rng.NextDouble() < parryChance)
                {
                    return 0f;
                }
            }

            // Solid auto-block: negates one hit every BlockCooldown seconds (DamageBlocker.cs:71-84).
            float blockCooldown = stats.GetValue(StatType.BlockCooldown);
            if (blockCooldown > 0f && now >= nextBlockReadyAt)
            {
                nextBlockReadyAt = now + blockCooldown;
                return 0f;
            }

            return enemy.damage;
        }
    }
}
