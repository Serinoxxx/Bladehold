using System;
using System.Collections.Generic;
using System.Globalization;

namespace Bladehold.BalanceSim
{
    /// <summary>
    ///     What-if overrides: flat dotted <c>key=value</c> lines applied to a <see cref="SimWorld" />
    ///     snapshot (never to real assets). Unknown keys throw — a silent typo in a balance experiment
    ///     is worse than a crash. Supported keys:
    ///     <code>
    ///     player.maxHealth=20            statBase.SwordDamage=7        statMod.CritChance.flat=0.1
    ///     wave.goblinsAddedPerWave=3     enemy.goblin_brute.health=30  node.sword_dmg=3
    ///     profile.good.avoidance=0.7     sim.spawnDistanceMeters=25
    ///     </code>
    /// </summary>
    public static class SimOverrides
    {
        /// <summary>Extra stat modifiers injected at run start (the <c>statMod.*</c> path).</summary>
        public class StatInjection
        {
            public StatType stat;
            public ModifierKind kind;
            public float amount;
        }

        public static readonly List<StatInjection> PendingStatInjections = new List<StatInjection>();
        public static readonly Dictionary<StatType, float> PendingBaseOverrides = new Dictionary<StatType, float>();

        /// <summary>Applies every line; call once per Load, after which Pending* hold the stat-layer overrides.</summary>
        public static void ApplyAll(SimWorld world, IEnumerable<string> lines)
        {
            PendingStatInjections.Clear();
            PendingBaseOverrides.Clear();
            foreach (string raw in lines)
            {
                string line = raw?.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                {
                    continue;
                }
                Apply(world, line);
            }
        }

        private static void Apply(SimWorld world, string line)
        {
            int eq = line.IndexOf('=');
            if (eq <= 0)
            {
                throw new InvalidOperationException($"Override '{line}' is not key=value.");
            }
            string key = line.Substring(0, eq).Trim();
            string value = line.Substring(eq + 1).Trim();
            string[] parts = key.Split('.');

            switch (parts[0].ToLowerInvariant())
            {
                case "player":
                    Expect(parts, 2, key);
                    if (!parts[1].Equals("maxHealth", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException($"Unknown override key '{key}' (only player.maxHealth).");
                    }
                    world.playerMaxHealth = F(value, key);
                    break;

                case "statbase":
                    Expect(parts, 2, key);
                    PendingBaseOverrides[Stat(parts[1], key)] = F(value, key);
                    break;

                case "statmod":
                    Expect(parts, 3, key);
                    PendingStatInjections.Add(new StatInjection
                    {
                        stat = Stat(parts[1], key),
                        kind = ParseKind(parts[2], key),
                        amount = F(value, key),
                    });
                    break;

                case "wave":
                    Expect(parts, 2, key);
                    ApplyWave(world, parts[1], value, key);
                    break;

                case "enemy":
                    Expect(parts, 3, key);
                    ApplyEnemy(world, parts[1], parts[2], value, key);
                    break;

                case "node":
                    Expect(parts, 2, key);
                    if (world.goldTree.GetById(parts[1]) == null)
                    {
                        throw new InvalidOperationException($"Override '{key}': no node '{parts[1]}' in the gold tree.");
                    }
                    world.prePurchasedNodes[parts[1]] = (int)F(value, key);
                    break;

                case "profile":
                    Expect(parts, 3, key);
                    if (!world.profiles.TryGetValue(parts[1], out PlayerProfile profile))
                    {
                        throw new InvalidOperationException($"Override '{key}': no profile '{parts[1]}'.");
                    }
                    profile.SetField(parts[2], value);
                    break;

                case "sim":
                    Expect(parts, 2, key);
                    ApplySim(world, parts[1], value, key);
                    break;

                default:
                    throw new InvalidOperationException($"Unknown override key '{key}'.");
            }
        }

        private static void ApplyWave(SimWorld world, string field, string value, string key)
        {
            switch (field.ToLowerInvariant())
            {
                case "basegoblincount": world.baseGoblinCount = (int)F(value, key); break;
                case "goblinsaddedperwave": world.goblinsAddedPerWave = (int)F(value, key); break;
                case "maxconcurrent": world.maxConcurrent = (int)F(value, key); break;
                case "timebetweenwaves": world.timeBetweenWaves = (int)F(value, key); break;
                case "spawninterval": world.spawnInterval = F(value, key); break;
                default: throw new InvalidOperationException($"Unknown override key '{key}'.");
            }
        }

        private static void ApplyEnemy(SimWorld world, string id, string field, string value, string key)
        {
            SimEnemyType e = world.FindEnemy(id)
                ?? throw new InvalidOperationException($"Override '{key}': no enemy '{id}' in the roster.");
            switch (field.ToLowerInvariant())
            {
                case "health": e.health = F(value, key); break;
                case "damage": e.damage = F(value, key); break;
                case "mingold": e.minGold = (int)F(value, key); break;
                case "maxgold": e.maxGold = (int)F(value, key); break;
                case "speed": e.speed = F(value, key); break;
                case "unlockwave": e.unlockWave = (int)F(value, key); break;
                // Authored as a percent, matching the CSV column (10 = 10%).
                case "spawnchance": e.spawnChance = Math.Clamp(F(value, key) / 100f, 0f, 1f); break;
                case "minspawn": e.minSpawn = (int)F(value, key); break;
                case "maxconcurrent": e.maxConcurrent = (int)F(value, key); break;
                default: throw new InvalidOperationException($"Unknown override key '{key}'.");
            }
        }

        private static void ApplySim(SimWorld world, string field, string value, string key)
        {
            switch (field.ToLowerInvariant())
            {
                case "spawndistancemeters": world.spawnDistanceMeters = F(value, key); break;
                case "bomberfuseseconds": world.bomberFuseSeconds = F(value, key); break;
                case "healthpackdropchance": world.healthPackDropChance = F(value, key); break;
                case "goldbagchance": world.goldBagChance = F(value, key); break;
                case "goldbagmultiplier": world.goldBagMultiplier = F(value, key); break;
                case "basecritmultiplier": world.baseCritMultiplier = F(value, key); break;
                case "chargetimeperlevel": world.chargeTimePerLevel = F(value, key); break;
                default: throw new InvalidOperationException($"Unknown override key '{key}'.");
            }
        }

        private static void Expect(string[] parts, int count, string key)
        {
            if (parts.Length != count)
            {
                throw new InvalidOperationException($"Override key '{key}' has {parts.Length} segments, expected {count}.");
            }
        }

        private static StatType Stat(string name, string key)
        {
            if (!Enum.TryParse(name, true, out StatType stat))
            {
                throw new InvalidOperationException($"Override '{key}': unknown StatType '{name}'.");
            }
            return stat;
        }

        private static ModifierKind ParseKind(string name, string key)
        {
            if (!Enum.TryParse(name, true, out ModifierKind kind))
            {
                throw new InvalidOperationException($"Override '{key}': unknown modifier kind '{name}' (flat/percent).");
            }
            return kind;
        }

        private static float F(string value, string key)
        {
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
            {
                throw new InvalidOperationException($"Override '{key}': invalid number '{value}'.");
            }
            return v;
        }
    }
}
