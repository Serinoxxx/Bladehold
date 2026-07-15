using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Bladehold.BalanceSim
{
    /// <summary>
    ///     A live <see cref="PlayerStats" /> on a hidden editor GameObject — the sim runs the *real*
    ///     aggregation formula (<c>(base + Σflat) × (1 + Σpercent)</c>), so upgrade math can never
    ///     drift from the game. Base registration mirrors the runtime owners exactly:
    ///     the sword's DamageTrigger (DamageTrigger.cs:132-139), WaveSpawner (GoldDropMultiplier),
    ///     and Player (AllDamageMultiplier, HealthPackHealPercent — Player.cs:72).
    ///     Dispose after each trial (or reuse via <see cref="Reset" /> — cheaper for Monte-Carlo).
    /// </summary>
    public class SimStats : IDisposable
    {
        private GameObject go;
        public PlayerStats Stats { get; private set; }

        private readonly SimWorld world;

        public SimStats(SimWorld world)
        {
            this.world = world;
            go = new GameObject("BalanceSim.PlayerStats") { hideFlags = HideFlags.HideAndDontSave };
            Stats = go.AddComponent<PlayerStats>();
            RegisterBases();
        }

        /// <summary>Fresh stat sheet for a new trial without paying GameObject churn.</summary>
        public void Reset()
        {
            Object.DestroyImmediate(Stats);
            Stats = go.AddComponent<PlayerStats>();
            RegisterBases();
        }

        private void RegisterBases()
        {
            // Mirrors DamageTrigger.RegisterStatBases (DamageTrigger.cs:132-139).
            Stats.SetBase(StatType.SwordDamage, world.swordBaseDamage);
            Stats.SetBase(StatType.SwordRange, 1f);
            Stats.SetBase(StatType.CritChance, 0f);
            Stats.SetBase(StatType.CritMultiplier, world.baseCritMultiplier);
            Stats.SetBase(StatType.MaxHitsPerSwing, world.swordMaxHitsBase);
            Stats.SetBase(StatType.ChargeDamageBonus, 0f);
            Stats.SetBase(StatType.MaxChargeLevels, 0f);
            // Mirrors WaveSpawner.Start / Player.Awake base registrations.
            Stats.SetBase(StatType.GoldDropMultiplier, 1f);
            Stats.SetBase(StatType.AllDamageMultiplier, 1f);
            Stats.SetBase(StatType.HealthPackHealPercent, 0.10f); // Player.cs:72
            // Locked-by-default skill lines (base 0 = locked, the house convention).
            Stats.SetBase(StatType.LifeStealPercent, 0f);
            Stats.SetBase(StatType.BlockCooldown, 0f);
            Stats.SetBase(StatType.ParryChance, 0f);

            foreach (var kv in SimOverrides.PendingBaseOverrides)
            {
                Stats.SetBase(kv.Key, kv.Value);
            }
            foreach (SimOverrides.StatInjection injection in SimOverrides.PendingStatInjections)
            {
                Stats.AddModifier(injection.stat, injection.kind, injection.amount);
            }
        }

        public void Dispose()
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
                go = null;
                Stats = null;
            }
        }
    }
}
