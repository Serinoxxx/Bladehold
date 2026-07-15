using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Bladehold.BalanceSim
{
    /// <summary>How the sim orders kill targets among engaged enemies.</summary>
    public enum TargetPriority
    {
        /// <summary>Whatever reached the player first (a panicking/button-mashing player).</summary>
        Arrival,
        /// <summary>Threat order with probability 0.5 per swing, else arrival.</summary>
        Mixed,
        /// <summary>Highest damage-per-time-to-kill first; suicide archetypes (bomber) always first.</summary>
        Threat,
    }

    /// <summary>Between-wave gold-spending behaviour (see <see cref="UpgradePolicy" />).</summary>
    public enum UpgradePolicyKind
    {
        None,
        Cheapest,
        DpsGreedy,
        SurvivalFirst,
        Balanced,
    }

    /// <summary>
    ///     One row of <c>Config/SimProfiles.csv</c> — the knobs that turn the projection into a "bad",
    ///     "average", or "good" player. A bad player runs at enemies and trades damage (low avoidance,
    ///     many attackers); a good player kites, prioritizes threats, and clears faster — but avoidance
    ///     stays below 1 and maxAttackers at least 1, so chip damage always exists and no profile is
    ///     invincible by construction.
    /// </summary>
    public class PlayerProfile
    {
        public string id;
        /// <summary>Attempted swing cadence while at least one enemy is engaged.</summary>
        public float swingsPerSecond = 1f;
        /// <summary>Fraction of engaged time spent attacking (the rest is repositioning/hesitating).</summary>
        public float attackUptime = 0.75f;
        /// <summary>Probability a swing connects with each selected target.</summary>
        public float hitAccuracy = 0.75f;
        /// <summary>Fraction of swings held to full charge (inert until MaxChargeLevels &gt; 0).</summary>
        public float chargedRatio = 0f;
        /// <summary>Probability an incoming hit attempt misses because the player moved.</summary>
        public float avoidance = 0.45f;
        /// <summary>Multiplier on enemy approach time (kiting stretches the walk-in).</summary>
        public float kiteFactor = 1.5f;
        /// <summary>Max enemies able to engage (melee range) at once — positioning skill.</summary>
        public int maxAttackers = 4;
        public TargetPriority targetPriority = TargetPriority.Mixed;
        /// <summary>Probability the player faces an attacker when hit (parry eligibility).</summary>
        public float facingRatio = 0.7f;
        /// <summary>Fraction of *ground* gold (gold bags; instant kill-gold is always banked) collected.</summary>
        public float goldPickup = 0.85f;
        /// <summary>Fraction of dropped health packs collected.</summary>
        public float packPickup = 0.8f;
        public UpgradePolicyKind upgradePolicy = UpgradePolicyKind.Balanced;
        /// <summary>Gold kept unspent as a buffer by the upgrade policy.</summary>
        public int spendReserve = 0;

        public const string CsvAssetPath = "Assets/Bladehold/Config/SimProfiles.csv";

        /// <summary>Loads every profile row from <see cref="CsvAssetPath" />, keyed by id.</summary>
        public static Dictionary<string, PlayerProfile> LoadAll()
        {
            TextAsset csv = AssetDatabase.LoadAssetAtPath<TextAsset>(CsvAssetPath);
            if (csv == null)
            {
                throw new InvalidOperationException($"SimProfiles CSV not found at {CsvAssetPath}");
            }

            var profiles = new Dictionary<string, PlayerProfile>(StringComparer.OrdinalIgnoreCase);
            string[] lines = csv.text.Split('\n');
            for (int i = 1; i < lines.Length; i++) // skip header
            {
                string line = lines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                PlayerProfile p = ParseRow(line, i + 1);
                profiles[p.id] = p;
            }
            return profiles;
        }

        private static PlayerProfile ParseRow(string line, int lineNumber)
        {
            List<string> f = CsvUtil.SplitLine(line);
            if (f.Count < 14)
            {
                throw new InvalidOperationException($"SimProfiles.csv line {lineNumber}: {f.Count} columns, expected 14.");
            }
            return new PlayerProfile
            {
                id = f[0].Trim(),
                swingsPerSecond = F(f[1], lineNumber),
                attackUptime = F(f[2], lineNumber),
                hitAccuracy = F(f[3], lineNumber),
                chargedRatio = F(f[4], lineNumber),
                avoidance = F(f[5], lineNumber),
                kiteFactor = F(f[6], lineNumber),
                maxAttackers = Mathf.Max(1, (int)F(f[7], lineNumber)),
                targetPriority = ParseEnum<TargetPriority>(f[8], lineNumber),
                facingRatio = F(f[9], lineNumber),
                goldPickup = F(f[10], lineNumber),
                packPickup = F(f[11], lineNumber),
                upgradePolicy = ParseEnum<UpgradePolicyKind>(f[12], lineNumber),
                spendReserve = (int)F(f[13], lineNumber),
            };
        }

        private static float F(string s, int lineNumber)
        {
            if (!float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
            {
                throw new InvalidOperationException($"SimProfiles.csv line {lineNumber}: invalid number '{s}'.");
            }
            return v;
        }

        private static T ParseEnum<T>(string s, int lineNumber) where T : struct
        {
            if (!Enum.TryParse(s.Trim(), true, out T v))
            {
                throw new InvalidOperationException($"SimProfiles.csv line {lineNumber}: invalid {typeof(T).Name} '{s}'.");
            }
            return v;
        }

        /// <summary>Sets one profile field by CSV column name (the <c>profile.&lt;id&gt;.&lt;field&gt;</c> override path).</summary>
        public void SetField(string field, string value)
        {
            float V() => float.Parse(value, CultureInfo.InvariantCulture);
            switch (field.ToLowerInvariant())
            {
                case "swingspersecond": swingsPerSecond = V(); break;
                case "attackuptime": attackUptime = V(); break;
                case "hitaccuracy": hitAccuracy = V(); break;
                case "chargedratio": chargedRatio = V(); break;
                case "avoidance": avoidance = V(); break;
                case "kitefactor": kiteFactor = V(); break;
                case "maxattackers": maxAttackers = Mathf.Max(1, (int)V()); break;
                case "targetpriority": targetPriority = (TargetPriority)Enum.Parse(typeof(TargetPriority), value, true); break;
                case "facingratio": facingRatio = V(); break;
                case "goldpickup": goldPickup = V(); break;
                case "packpickup": packPickup = V(); break;
                case "upgradepolicy": upgradePolicy = (UpgradePolicyKind)Enum.Parse(typeof(UpgradePolicyKind), value, true); break;
                case "spendreserve": spendReserve = (int)V(); break;
                default: throw new InvalidOperationException($"Unknown profile field '{field}'.");
            }
        }
    }
}
