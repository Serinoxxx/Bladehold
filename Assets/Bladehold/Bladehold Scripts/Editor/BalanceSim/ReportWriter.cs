using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Bladehold.BalanceSim
{
    /// <summary>
    ///     Writes a run's outputs to the report folder: <c>waves.csv</c> (per profile × wave
    ///     percentiles, the human/table view), <c>summary.json</c> (config echo + aggregates — the file
    ///     an agent reads), <c>findings.json</c> (pacing verdicts only, for cheap pass/fail polling),
    ///     and optionally <c>trials.csv</c> (raw per-trial rows, large).
    /// </summary>
    public static class ReportWriter
    {
        public static void WriteAll(
            string outDir, SimConfig cfg, SimWorld world,
            List<ProfileResult> results, List<Finding> findings)
        {
            Directory.CreateDirectory(outDir);

            File.WriteAllText(Path.Combine(outDir, "waves.csv"), BuildWavesCsv(cfg, results));
            File.WriteAllText(Path.Combine(outDir, "summary.json"), BuildSummaryJson(cfg, world, results, findings));
            File.WriteAllText(Path.Combine(outDir, "findings.json"),
                JsonConvert.SerializeObject(findings, Formatting.Indented));
            File.WriteAllText(Path.Combine(outDir, "report.html"), HtmlReport.Build(cfg, world, results, findings));
            if (cfg.emitTrials)
            {
                File.WriteAllText(Path.Combine(outDir, "trials.csv"), BuildTrialsCsv(results));
            }
        }

        private static string BuildWavesCsv(SimConfig cfg, List<ProfileResult> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine("profile,policy,wave,survival_rate,clear_seconds_med,clear_seconds_p10,clear_seconds_p90,"
                + "damage_taken_med,hp_end_med,hp_end_p10,min_hp_fraction_med,gold_earned_med,gold_unspent_med,dps_med,purchases_mode");
            foreach (ProfileResult result in results)
            {
                for (int wave = 1; wave <= cfg.maxWaves; wave++)
                {
                    List<float> clears = result.WaveMetric(wave, r => r.clearSeconds, includeFatal: false);
                    if (clears.Count == 0 && result.SurvivalRate(wave) <= 0f)
                    {
                        break; // nobody reached this wave
                    }
                    List<float> dps = result.WaveMetric(wave,
                        r => r.clearSeconds > 0f ? r.damageDealt / r.clearSeconds : 0f, includeFatal: false);
                    sb.AppendLine(string.Join(",",
                        result.profile.id,
                        result.profile.upgradePolicy.ToString(),
                        wave.ToString(CultureInfo.InvariantCulture),
                        N(result.SurvivalRate(wave)),
                        N(Percentiles.Median(clears)),
                        N(Percentiles.Of(clears, 10f)),
                        N(Percentiles.Of(clears, 90f)),
                        N(Percentiles.Median(result.WaveMetric(wave, r => r.damageTaken))),
                        N(Percentiles.Median(result.WaveMetric(wave, r => r.hpEnd, includeFatal: false))),
                        N(Percentiles.Of(result.WaveMetric(wave, r => r.hpEnd, includeFatal: false), 10f)),
                        N(Percentiles.Median(result.WaveMetric(wave, r => r.minHpFraction))),
                        N(Percentiles.Median(result.WaveMetric(wave, r => r.goldEarned))),
                        N(Percentiles.Median(result.WaveMetric(wave, r => r.goldUnspent, includeFatal: false))),
                        N(Percentiles.Median(dps)),
                        Quote(result.ModalPurchases(wave))));
                }
            }
            return sb.ToString();
        }

        private static string BuildSummaryJson(
            SimConfig cfg, SimWorld world, List<ProfileResult> results, List<Finding> findings)
        {
            var profiles = new List<object>();
            foreach (ProfileResult result in results)
            {
                List<float> deathWaves = result.DeathWaves(cfg.maxWaves);
                var histogram = new Dictionary<int, int>();
                foreach (TrialResult trial in result.trials)
                {
                    int key = trial.deathWave; // 0 = survived the horizon
                    histogram.TryGetValue(key, out int c);
                    histogram[key] = c + 1;
                }
                profiles.Add(new
                {
                    id = result.profile.id,
                    policy = result.profile.upgradePolicy.ToString(),
                    trials = result.TrialCount,
                    stalledTrials = result.trials.Count(t => t.stalled),
                    deathWave = new
                    {
                        median = Percentiles.Median(deathWaves),
                        p10 = Percentiles.Of(deathWaves, 10f),
                        p90 = Percentiles.Of(deathWaves, 90f),
                        survivedHorizon = result.trials.Count(t => t.deathWave == 0),
                        histogram,
                    },
                    parameters = result.profile,
                });
            }

            var summary = new
            {
                seed = cfg.seed,
                trials = cfg.trials,
                maxWaves = cfg.maxWaves,
                overrides = cfg.overrides,
                playerMaxHealth = world.playerMaxHealth,
                swordBaseDamage = world.swordBaseDamage,
                profiles,
                findings,
            };
            return JsonConvert.SerializeObject(summary, Formatting.Indented);
        }

        private static string BuildTrialsCsv(List<ProfileResult> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine("profile,trial,wave,clear_seconds,damage_taken,damage_dealt,kills,gold_earned,hp_end,min_hp_fraction,gold_unspent,fatal,stalled,purchases");
            foreach (ProfileResult result in results)
            {
                for (int i = 0; i < result.trials.Count; i++)
                {
                    foreach (WaveRecord r in result.trials[i].waves)
                    {
                        sb.AppendLine(string.Join(",",
                            result.profile.id, i.ToString(CultureInfo.InvariantCulture),
                            r.wave.ToString(CultureInfo.InvariantCulture),
                            N(r.clearSeconds), N(r.damageTaken), N(r.damageDealt),
                            r.kills.ToString(CultureInfo.InvariantCulture),
                            r.goldEarned.ToString(CultureInfo.InvariantCulture),
                            N(r.hpEnd), N(r.minHpFraction),
                            r.goldUnspent.ToString(CultureInfo.InvariantCulture),
                            r.fatal ? "1" : "0", r.stalled ? "1" : "0",
                            Quote(string.Join(" ", r.purchases))));
                    }
                }
            }
            return sb.ToString();
        }

        private static string N(float v)
        {
            return float.IsNaN(v) ? "" : v.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string Quote(string s)
        {
            return string.IsNullOrEmpty(s) ? "" : "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }
}
