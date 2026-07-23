using System;
using System.Collections.Generic;
using System.Linq;

namespace Bladehold.BalanceSim
{
    /// <summary>
    ///     The coarse-tick projection engine — one call simulates one full run (trial) for one profile.
    ///     Positions are abstracted to timers: an enemy "approaches" for
    ///     <c>spawnDistance / speed × kiteFactor</c> seconds, then wants to engage; melee engagement is
    ///     capped at the profile's <c>maxAttackers</c> (positioning skill), ranged enemies bypass the
    ///     cap. Spawning replicates the real spawner's pacing and type-selection cascade
    ///     (<see cref="SpawnModel" />); damage and gold run through <see cref="CombatModel" /> against
    ///     the live <see cref="PlayerStats" /> sheet; between waves an <see cref="UpgradePolicy" />
    ///     spends gold on the real skill tree. Deterministic: one <see cref="Random" /> per trial.
    /// </summary>
    public static class SimEngine
    {
        private class EnemyState
        {
            public SpawnModel.TypeState type;
            public float hp;
            public float engageAt;
            public bool engaged;
            public float nextAttackAt;
            /// <summary>Suicide archetype only: when the bomb goes off.</summary>
            public float detonateAt = float.MaxValue;
            public int arrivalOrder;
        }

        public static TrialResult RunTrial(
            SimWorld world, PlayerProfile profile, SimStats simStats, SimConfig cfg, int trialIndex)
        {
            var rng = new Random(SeedFor(cfg.seed, profile.id, trialIndex));
            simStats.Reset();
            PlayerStats stats = simStats.Stats;
            var policy = new UpgradePolicy(world, stats, profile, cfg.purchaseScript);
            var spawner = new SpawnModel(world, rng);

            var result = new TrialResult();
            float hp = world.playerMaxHealth;
            int gold = 0;
            float nextBlockReadyAt = 0f;
            int purchasesBefore = 0;

            for (int wave = 1; wave <= cfg.maxWaves; wave++)
            {
                var record = new WaveRecord { wave = wave, minHpFraction = hp / world.playerMaxHealth };
                spawner.BeginWave();
                int total = world.GoblinsForWave(wave);
                int remainingToSpawn = total;
                int killed = 0;
                var alive = new List<EnemyState>();
                float t = 0f;
                float nextSpawnAt = 0f;
                float nextSwingAt = 0f;
                int arrivalCounter = 0;

                while (killed < total && t < cfg.maxWaveSeconds)
                {
                    // --- Spawning (mirrors WaveSpawner.SpawnLoop pacing: group periodic spawns) ---
                    if (remainingToSpawn > 0 && alive.Count < world.maxConcurrent && t >= nextSpawnAt)
                    {
                        int effectiveBatchSize = world.spawnBatchSize > 0 ? world.spawnBatchSize : world.maxConcurrent;
                        int batchTarget = Math.Min(effectiveBatchSize, world.maxConcurrent - alive.Count);
                        batchTarget = Math.Min(batchTarget, remainingToSpawn);

                        for (int i = 0; i < batchTarget; i++)
                        {
                            SpawnModel.TypeState type = spawner.Select(wave);
                            type.spawnedThisWave++;
                            type.alive++;
                            remainingToSpawn--;
                            float approach = world.spawnDistanceMeters / Math.Max(0.1f, type.def.speed);
                            if (type.def.archetype != EnemyArchetype.Ranged)
                            {
                                approach *= profile.kiteFactor; // kiting stretches melee walk-in; ranged just needs line of sight
                            }
                            alive.Add(new EnemyState
                            {
                                type = type,
                                hp = type.def.health,
                                engageAt = t + (i * world.spawnInterval) + approach,
                                arrivalOrder = arrivalCounter++,
                            });
                        }
                        nextSpawnAt = t + (world.spawnBatchInterval > 0f ? world.spawnBatchInterval : world.spawnInterval);
                    }

                    // --- Engagement ---
                    int meleeEngaged = alive.Count(e => e.engaged && e.type.def.archetype != EnemyArchetype.Ranged);
                    foreach (EnemyState e in alive)
                    {
                        if (e.engaged || t < e.engageAt)
                        {
                            continue;
                        }
                        bool isRanged = e.type.def.archetype == EnemyArchetype.Ranged;
                        if (!isRanged && meleeEngaged >= profile.maxAttackers)
                        {
                            continue; // queues behind the melee ring; re-checked every tick
                        }
                        e.engaged = true;
                        if (!isRanged)
                        {
                            meleeEngaged++;
                        }
                        e.nextAttackAt = t + e.type.def.windupToApex;
                        if (e.type.def.archetype == EnemyArchetype.Suicide)
                        {
                            e.detonateAt = t + world.bomberFuseSeconds;
                        }
                    }

                    // --- Enemy attacks ---
                    var suicided = new List<EnemyState>();
                    foreach (EnemyState e in alive)
                    {
                        if (!e.engaged)
                        {
                            continue;
                        }
                        if (e.type.def.archetype == EnemyArchetype.Suicide)
                        {
                            if (t >= e.detonateAt)
                            {
                                float taken = CombatModel.ResolveIncomingHit(
                                    e.type.def, profile, stats, rng, t, ref nextBlockReadyAt);
                                hp -= taken;
                                record.damageTaken += taken;
                                suicided.Add(e); // the bomb kills the bomber too — still a wave kill + drops
                            }
                            continue;
                        }
                        if (t >= e.nextAttackAt)
                        {
                            float taken = CombatModel.ResolveIncomingHit(
                                e.type.def, profile, stats, rng, t, ref nextBlockReadyAt);
                            hp -= taken;
                            record.damageTaken += taken;
                            e.nextAttackAt = t + e.type.def.attackCooldown;
                        }
                    }
                    foreach (EnemyState e in suicided)
                    {
                        killed++;
                        HandleKill(e, world, profile, stats, rng, record, ref gold, ref hp);
                        alive.Remove(e);
                        e.type.alive--;
                    }

                    if (hp <= 0f)
                    {
                        break;
                    }

                    // --- Player swings ---
                    List<EnemyState> engaged = alive.Where(e => e.engaged).ToList();
                    if (engaged.Count > 0 && t >= nextSwingAt)
                    {
                        bool charged = stats.GetValue(StatType.MaxChargeLevels) >= 1f
                            && rng.NextDouble() < profile.chargedRatio;
                        int chargeLevel = charged
                            ? (int)Math.Round(stats.GetValue(StatType.MaxChargeLevels))
                            : 0;
                        // ChargeLevel = floor(held / chargeTimePerLevel) (PlayerAttack.cs:189-192) — a
                        // full charge costs chargeLevel × chargeTimePerLevel of standing still.
                        float swingPeriod = 1f / Math.Max(0.05f, profile.swingsPerSecond * profile.attackUptime)
                            + chargeLevel * world.chargeTimePerLevel;
                        nextSwingAt = t + swingPeriod;

                        SortTargets(engaged, profile, stats, rng);
                        int maxHits = CombatModel.MaxHitsPerSwing(stats);
                        int hits = 0;
                        foreach (EnemyState target in engaged)
                        {
                            if (hits >= maxHits)
                            {
                                break;
                            }
                            hits++;
                            if (rng.NextDouble() >= profile.hitAccuracy)
                            {
                                continue; // whiffed this target
                            }
                            CombatModel.SwingHit hit = CombatModel.RollSwingHit(stats, chargeLevel, rng);
                            target.hp -= hit.damage;
                            record.damageDealt += hit.damage;
                            // VampiricBlade heals LifeStealPercent × damage dealt (VampiricBlade.cs:86).
                            float lifesteal = stats.GetValue(StatType.LifeStealPercent);
                            if (lifesteal > 0f)
                            {
                                hp = Math.Min(world.playerMaxHealth, hp + hit.damage * lifesteal);
                            }
                            if (target.hp <= 0f)
                            {
                                killed++;
                                HandleKill(target, world, profile, stats, rng, record, ref gold, ref hp);
                                alive.Remove(target);
                                target.type.alive--;
                            }
                        }
                    }

                    t += SimConfig.TickSeconds;
                    record.minHpFraction = Math.Min(record.minHpFraction, Math.Max(0f, hp) / world.playerMaxHealth);
                }

                record.clearSeconds = t;
                record.kills = killed;
                record.hpEnd = Math.Max(0f, hp);

                if (hp <= 0f)
                {
                    record.fatal = true;
                    record.minHpFraction = 0f;
                    result.waves.Add(record);
                    result.deathWave = wave;
                    return result;
                }
                if (killed < total)
                {
                    record.stalled = true;
                    result.waves.Add(record);
                    result.stalled = true;
                    return result;
                }

                // --- Between waves: shopping (WaveSpawner's intermission; no regen exists, so HP carries) ---
                gold = policy.Spend(gold, wave);
                record.goldUnspent = gold;
                record.purchases = policy.purchases.Skip(purchasesBefore).ToList();
                purchasesBefore = policy.purchases.Count;
                result.waves.Add(record);
            }

            return result; // survived the horizon: deathWave stays 0
        }

        private static void HandleKill(
            EnemyState enemy, SimWorld world, PlayerProfile profile, PlayerStats stats,
            Random rng, WaveRecord record, ref int gold, ref float hp)
        {
            // Instant kill-gold goes straight to the wallet — no pickup roll (CoinDropper.cs:108-115).
            int killGold = CombatModel.RollKillGold(enemy.type.def, stats, rng);
            gold += killGold;
            record.goldEarned += killGold;

            // Gold bags land on the ground; collection depends on the player bothering.
            int bag = CombatModel.RollGoldBag(enemy.type.def, world, stats, rng);
            if (bag > 0 && rng.NextDouble() < profile.goldPickup)
            {
                gold += bag;
                record.goldEarned += bag;
            }

            // Health pack: per-kill drop roll (HealthpackPowerupDropSO), collected with packPickup,
            // heals HealthPackHealPercent × max (HealthPack.cs:76). Not consumed at full HP.
            if (world.healthPackDropChance > 0f
                && rng.NextDouble() < world.healthPackDropChance
                && rng.NextDouble() < profile.packPickup
                && hp < world.playerMaxHealth)
            {
                float heal = world.playerMaxHealth * Math.Clamp(stats.GetValue(StatType.HealthPackHealPercent), 0f, 1f);
                hp = Math.Min(world.playerMaxHealth, hp + heal);
            }
        }

        private static void SortTargets(List<EnemyState> engaged, PlayerProfile profile, PlayerStats stats, Random rng)
        {
            TargetPriority priority = profile.targetPriority;
            if (priority == TargetPriority.Mixed)
            {
                priority = rng.NextDouble() < 0.5 ? TargetPriority.Threat : TargetPriority.Arrival;
            }
            if (priority == TargetPriority.Arrival)
            {
                engaged.Sort((a, b) => a.arrivalOrder.CompareTo(b.arrivalOrder));
                return;
            }
            // Threat: suicide types first (the bomb is the one-shot), then damage per remaining HP.
            float swingDamage = Math.Max(0.01f, stats.GetValue(StatType.SwordDamage));
            float Score(EnemyState e)
            {
                float bombRush = e.type.def.archetype == EnemyArchetype.Suicide ? 1000f : 0f;
                return bombRush + e.type.def.damage / Math.Max(1f, e.hp / swingDamage);
            }
            engaged.Sort((a, b) => Score(b).CompareTo(Score(a)));
        }

        /// <summary>Stable FNV-1a seed so trials are bit-reproducible across editor sessions and platforms.</summary>
        private static int SeedFor(int masterSeed, string profileId, int trialIndex)
        {
            unchecked
            {
                uint hash = 2166136261u;
                void Mix(uint v)
                {
                    hash ^= v;
                    hash *= 16777619u;
                }
                Mix((uint)masterSeed);
                foreach (char c in profileId)
                {
                    Mix(c);
                }
                Mix((uint)trialIndex);
                return (int)hash;
            }
        }
    }
}
