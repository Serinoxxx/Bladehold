using System;
using System.Collections.Generic;
using System.Linq;

namespace Bladehold.BalanceSim
{
    /// <summary>
    ///     The sim's between-wave gold spender. Operates on the real <see cref="SkillTreeSO" /> data —
    ///     reveal rules, per-level cost ladders, and per-level effect amounts all come from
    ///     <see cref="SkillNode" /> — and applies purchases to the live <see cref="PlayerStats" />,
    ///     mirroring SkillTreeService (reveal: SkillTreeService.IsRevealed; purchase: TryPurchase).
    /// </summary>
    public class UpgradePolicy
    {
        private readonly SkillTreeSO tree;
        private readonly PlayerStats stats;
        private readonly UpgradePolicyKind kind;
        private readonly PlayerProfile profile;
        private readonly Dictionary<string, int> levels = new Dictionary<string, int>();
        /// <summary>Calibration: wave → node ids to replay instead of the policy (see SimConfig.purchaseScript).</summary>
        private readonly Dictionary<int, List<string>> purchaseScript;
        public readonly List<string> purchases = new List<string>();

        /// <summary>Stats a survival-minded player prioritizes.</summary>
        private static readonly HashSet<StatType> SurvivalStats = new HashSet<StatType>
        {
            StatType.LifeStealPercent,
            StatType.BlockCooldown,
            StatType.ParryChance,
            StatType.HealthPackHealPercent,
        };

        private bool nextPickIsSurvival = true; // Balanced alternation state

        public UpgradePolicy(SimWorld world, PlayerStats stats, PlayerProfile profile,
            Dictionary<int, List<string>> purchaseScript = null)
        {
            tree = world.goldTree;
            this.stats = stats;
            this.profile = profile;
            this.purchaseScript = purchaseScript;
            kind = profile.upgradePolicy;

            // node.<id>=<level> overrides: granted free at run start, effects applied per level
            // exactly as SkillTreeService.Start re-applies a save.
            foreach (var kv in world.prePurchasedNodes)
            {
                SkillNode node = tree.GetById(kv.Key);
                int target = Math.Min(kv.Value, node.maxLevel);
                for (int level = 1; level <= target; level++)
                {
                    levels[kv.Key] = level;
                    ApplyLevel(node, level);
                }
            }
        }

        public int GetLevel(string id) => levels.TryGetValue(id, out int level) ? level : 0;

        /// <summary>Spends as much gold as the policy wants (or replays the purchase script for this wave); returns the remaining gold.</summary>
        public int Spend(int gold, int wave)
        {
            if (purchaseScript != null)
            {
                // Replay mode: apply exactly what the real run bought after this wave, spending sim
                // gold (allowed to go negative — the real player evidently could afford it, and the
                // point is to hold the upgrade trajectory fixed, not to re-judge affordability).
                if (purchaseScript.TryGetValue(wave, out List<string> ids))
                {
                    foreach (string id in ids)
                    {
                        SkillNode node = tree.GetById(id);
                        if (node == null)
                        {
                            continue; // node renamed since the telemetry run
                        }
                        int level = GetLevel(id) + 1;
                        if (level > node.maxLevel)
                        {
                            continue;
                        }
                        gold -= node.CostForLevel(level);
                        levels[id] = level;
                        ApplyLevel(node, level);
                        purchases.Add($"{id}:{level}");
                    }
                }
                return gold;
            }

            if (kind == UpgradePolicyKind.None)
            {
                return gold;
            }

            while (true)
            {
                List<SkillNode> affordable = AffordableNodes(gold - profile.spendReserve);
                if (affordable.Count == 0)
                {
                    return gold;
                }

                SkillNode pick = Pick(affordable);
                int level = GetLevel(pick.id) + 1;
                int price = pick.CostForLevel(level);
                gold -= price;
                levels[pick.id] = level;
                ApplyLevel(pick, level);
                purchases.Add($"{pick.id}:{level}");
            }
        }

        private void ApplyLevel(SkillNode node, int level)
        {
            foreach (SkillEffect effect in node.effects)
            {
                stats.AddModifier(effect.stat, effect.kind, effect.AmountForLevel(level));
            }
        }

        private List<SkillNode> AffordableNodes(int budget)
        {
            var result = new List<SkillNode>();
            if (budget <= 0)
            {
                return result;
            }
            foreach (SkillNode node in tree.Nodes)
            {
                int level = GetLevel(node.id);
                if (level >= node.maxLevel || !IsRevealed(node))
                {
                    continue;
                }
                if (node.CostForLevel(level + 1) <= budget)
                {
                    result.Add(node);
                }
            }
            return result;
        }

        /// <summary>Mirrors SkillTreeService.IsRevealed: root, or any linked node at level ≥ 1 (links symmetric).</summary>
        private bool IsRevealed(SkillNode node)
        {
            if (node.isRoot)
            {
                return true;
            }
            foreach (string p in node.prereqs)
            {
                if (GetLevel(p) >= 1)
                {
                    return true;
                }
            }
            foreach (string dependentId in tree.GetDependents(node.id))
            {
                if (GetLevel(dependentId) >= 1)
                {
                    return true;
                }
            }
            return false;
        }

        private SkillNode Pick(List<SkillNode> affordable)
        {
            switch (kind)
            {
                case UpgradePolicyKind.Cheapest:
                    return Cheapest(affordable);
                case UpgradePolicyKind.DpsGreedy:
                    return DpsGreedy(affordable);
                case UpgradePolicyKind.SurvivalFirst:
                    return SurvivalPick(affordable) ?? DpsGreedy(affordable);
                case UpgradePolicyKind.Balanced:
                    SkillNode pick = nextPickIsSurvival
                        ? SurvivalPick(affordable) ?? DpsGreedy(affordable)
                        : DpsGreedy(affordable);
                    nextPickIsSurvival = !nextPickIsSurvival;
                    return pick;
                default:
                    return Cheapest(affordable);
            }
        }

        private SkillNode Cheapest(List<SkillNode> affordable)
        {
            return affordable.OrderBy(n => n.CostForLevel(GetLevel(n.id) + 1)).First();
        }

        private SkillNode SurvivalPick(List<SkillNode> affordable)
        {
            List<SkillNode> survival = affordable
                .Where(n => n.effects.Any(e => SurvivalStats.Contains(e.stat)))
                .ToList();
            return survival.Count > 0 ? Cheapest(survival) : null;
        }

        /// <summary>Best marginal sim-DPS per gold; connector nodes score 0 but still buy when nothing scores (they open branches).</summary>
        private SkillNode DpsGreedy(List<SkillNode> affordable)
        {
            SkillNode best = null;
            float bestScore = 0f;
            float before = DpsScore(null);
            foreach (SkillNode node in affordable)
            {
                int price = node.CostForLevel(GetLevel(node.id) + 1);
                float gain = DpsScore(node) - before;
                float score = gain / Math.Max(1, price);
                if (best == null || score > bestScore)
                {
                    best = node;
                    bestScore = score;
                }
            }
            // Nothing raised DPS (all connectors / defense nodes): buy the cheapest to keep opening the tree.
            return bestScore > 0f ? best : Cheapest(affordable);
        }

        /// <summary>
        ///     A DPS proxy from the stat sheet, optionally previewing one node's next level via the real
        ///     <see cref="PlayerStats.PreviewValue" /> (per-stat — node effects land on distinct stats).
        /// </summary>
        private float DpsScore(SkillNode candidate)
        {
            int nextLevel = candidate != null ? GetLevel(candidate.id) + 1 : 0;

            float Value(StatType stat)
            {
                if (candidate != null)
                {
                    foreach (SkillEffect effect in candidate.effects)
                    {
                        if (effect.stat == stat)
                        {
                            return stats.PreviewValue(stat, effect.kind, effect.AmountForLevel(nextLevel));
                        }
                    }
                }
                return stats.GetValue(stat);
            }

            float allDamage = Value(StatType.AllDamageMultiplier);
            if (allDamage <= 0f) allDamage = 1f;
            float critChance = Math.Clamp(Value(StatType.CritChance), 0f, 1f);
            float critFactor = 1f + critChance * Math.Max(0f, Value(StatType.CritMultiplier) - 1f);
            float chargeFactor = 1f + profile.chargedRatio
                * Value(StatType.MaxChargeLevels) * Value(StatType.ChargeDamageBonus);
            float hits = Math.Max(1f, Value(StatType.MaxHitsPerSwing));
            // Multi-hit only pays off with crowds; weight extra hits at half value.
            float hitFactor = 1f + (hits - 1f) * 0.5f;

            return Value(StatType.SwordDamage) * allDamage * critFactor * chargeFactor * hitFactor;
        }
    }
}
