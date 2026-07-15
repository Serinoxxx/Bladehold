using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace Bladehold.BalanceSim
{
    /// <summary>One row of <c>Config/SimPacingRules.csv</c> — a band a metric must land in.</summary>
    public class PacingRule
    {
        public string id;
        /// <summary>Profile the rule applies to; blank = any.</summary>
        public string profile;
        /// <summary>Upgrade policy the rule applies to; blank = any. Lets "bad with no upgrades" coexist with "bad playing normally".</summary>
        public string policy;
        public string metric;
        public int? waveMin;
        public int? waveMax;
        public float? min;
        public float? max;
        public bool isFail; // else warn
        public string message;
    }

    /// <summary>A rule's outcome against one profile's aggregated results.</summary>
    public class Finding
    {
        public string ruleId;
        public string profile;
        public string metric;
        public string verdict; // pass | warn | fail | skipped
        public float observed;
        public float? bandMin;
        public float? bandMax;
        public string message;
    }

    /// <summary>
    ///     Loads the designer-owned pacing contract and evaluates it against sim results. Metrics:
    ///     <c>death_wave_median</c>, <c>death_wave_p10</c>, <c>death_wave_p90</c>,
    ///     <c>survival_rate</c> (at wave <c>waveMin</c>), <c>clear_seconds_med</c>,
    ///     <c>hp_end_fraction_med</c>, <c>min_hp_fraction_med</c>, <c>gold_unspent_med</c>
    ///     (wave-scoped metrics aggregate over waves [waveMin..waveMax]).
    /// </summary>
    public static class PacingRules
    {
        public const string CsvAssetPath = "Assets/Bladehold/Config/SimPacingRules.csv";

        public static List<PacingRule> Load()
        {
            TextAsset csv = AssetDatabase.LoadAssetAtPath<TextAsset>(CsvAssetPath);
            if (csv == null)
            {
                Debug.LogWarning($"BalanceSim: no pacing rules at {CsvAssetPath}; skipping verdicts.");
                return new List<PacingRule>();
            }

            var rules = new List<PacingRule>();
            string[] lines = csv.text.Split('\n');
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                List<string> f = CsvUtil.SplitLine(line);
                if (f.Count < 10)
                {
                    throw new InvalidOperationException($"SimPacingRules.csv line {i + 1}: {f.Count} columns, expected 10.");
                }
                rules.Add(new PacingRule
                {
                    id = f[0].Trim(),
                    profile = f[1].Trim(),
                    policy = f[2].Trim(),
                    metric = f[3].Trim().ToLowerInvariant(),
                    waveMin = OptInt(f[4]),
                    waveMax = OptInt(f[5]),
                    min = OptFloat(f[6]),
                    max = OptFloat(f[7]),
                    isFail = f[8].Trim().Equals("fail", StringComparison.OrdinalIgnoreCase),
                    message = f[9].Trim(),
                });
            }
            return rules;
        }

        public static List<Finding> Evaluate(
            List<PacingRule> rules, List<ProfileResult> results, SimConfig cfg, float playerMaxHealth)
        {
            var findings = new List<Finding>();
            foreach (PacingRule rule in rules)
            {
                bool matchedAny = false;
                foreach (ProfileResult result in results)
                {
                    if (!string.IsNullOrEmpty(rule.profile)
                        && !rule.profile.Equals(result.profile.id, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    if (!string.IsNullOrEmpty(rule.policy)
                        && !rule.policy.Equals(result.profile.upgradePolicy.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    matchedAny = true;
                    findings.Add(EvaluateOne(rule, result, cfg, playerMaxHealth));
                }
                if (!matchedAny)
                {
                    // A rule that silently matches nothing looks like a pass — surface it instead.
                    findings.Add(new Finding
                    {
                        ruleId = rule.id,
                        profile = rule.profile,
                        metric = rule.metric,
                        verdict = "skipped",
                        observed = float.NaN,
                        bandMin = rule.min,
                        bandMax = rule.max,
                        message = $"no run matched profile '{rule.profile}' / policy '{rule.policy}' — {rule.message}",
                    });
                }
            }
            return findings;
        }

        private static Finding EvaluateOne(PacingRule rule, ProfileResult result, SimConfig cfg, float playerMaxHealth)
        {
            float observed = Observe(rule, result, cfg, playerMaxHealth);
            string verdict;
            if (float.IsNaN(observed))
            {
                verdict = "skipped";
            }
            else if ((rule.min.HasValue && observed < rule.min.Value)
                || (rule.max.HasValue && observed > rule.max.Value))
            {
                verdict = rule.isFail ? "fail" : "warn";
            }
            else
            {
                verdict = "pass";
            }
            return new Finding
            {
                ruleId = rule.id,
                profile = result.profile.id,
                metric = rule.metric,
                verdict = verdict,
                observed = observed,
                bandMin = rule.min,
                bandMax = rule.max,
                message = rule.message,
            };
        }

        private static float Observe(PacingRule rule, ProfileResult result, SimConfig cfg, float playerMaxHealth)
        {
            switch (rule.metric)
            {
                case "death_wave_median":
                    return Percentiles.Median(result.DeathWaves(cfg.maxWaves));
                case "death_wave_p10":
                    return Percentiles.Of(result.DeathWaves(cfg.maxWaves), 10f);
                case "death_wave_p90":
                    return Percentiles.Of(result.DeathWaves(cfg.maxWaves), 90f);
                case "survival_rate":
                    return result.SurvivalRate(rule.waveMin ?? cfg.maxWaves);
                case "clear_seconds_med":
                    return MedianOverWaves(rule, result, cfg, r => r.clearSeconds, includeFatal: false);
                case "hp_end_fraction_med":
                    return MedianOverWaves(rule, result, cfg,
                        r => r.hpEnd / Math.Max(1f, playerMaxHealth), includeFatal: false);
                case "min_hp_fraction_med":
                    // The *lowest point* across the wave window, per trial, then the median of those:
                    // "does a good run dip below 60% somewhere in waves 5-10".
                    return MedianOfPerTrialMin(rule, result, cfg);
                case "gold_unspent_med":
                    return MedianOverWaves(rule, result, cfg, r => r.goldUnspent, includeFatal: false);
                default:
                    throw new InvalidOperationException($"SimPacingRules.csv: unknown metric '{rule.metric}'.");
            }
        }

        private static float MedianOverWaves(
            PacingRule rule, ProfileResult result, SimConfig cfg,
            Func<WaveRecord, float> selector, bool includeFatal)
        {
            int from = rule.waveMin ?? 1;
            int to = rule.waveMax ?? cfg.maxWaves;
            var values = new List<float>();
            for (int wave = from; wave <= to; wave++)
            {
                values.AddRange(result.WaveMetric(wave, selector, includeFatal));
            }
            return Percentiles.Median(values);
        }

        private static float MedianOfPerTrialMin(PacingRule rule, ProfileResult result, SimConfig cfg)
        {
            int from = rule.waveMin ?? 1;
            int to = rule.waveMax ?? cfg.maxWaves;
            var perTrial = new List<float>();
            foreach (TrialResult trial in result.trials)
            {
                float min = float.NaN;
                foreach (WaveRecord record in trial.waves)
                {
                    if (record.wave >= from && record.wave <= to && !record.fatal)
                    {
                        min = float.IsNaN(min) ? record.minHpFraction : Math.Min(min, record.minHpFraction);
                    }
                }
                if (!float.IsNaN(min))
                {
                    perTrial.Add(min);
                }
            }
            return Percentiles.Median(perTrial);
        }

        private static int? OptInt(string s)
        {
            s = s.Trim();
            return string.IsNullOrEmpty(s) ? (int?)null : int.Parse(s, CultureInfo.InvariantCulture);
        }

        private static float? OptFloat(string s)
        {
            s = s.Trim();
            return string.IsNullOrEmpty(s) ? (float?)null : float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
