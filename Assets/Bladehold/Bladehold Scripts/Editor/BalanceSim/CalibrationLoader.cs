using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Bladehold.BalanceSim
{
    /// <summary>One parsed RunTelemetry CSV (Analytics/RunTelemetry.cs writes them per run).</summary>
    public class TelemetryRun
    {
        public string file;
        public string playerClass = "";
        public List<TelemetryWave> waves = new List<TelemetryWave>();
        /// <summary>wave → gold-tree node ids bought during that wave/intermission (Reincarnate purchases excluded).</summary>
        public Dictionary<int, List<string>> purchasesByWave = new Dictionary<int, List<string>>();
        /// <summary>Wave of the death row, 0 = the run file has none (quit instead).</summary>
        public int deathWave;
    }

    public class TelemetryWave
    {
        public int wave;
        public float waveSeconds;
        public float damageTaken;
        public float damageDealt;
        public int kills;
        public int goldEarned;
        public bool fatal;
    }

    /// <summary>
    ///     Calibration: parse real play data (RunTelemetry CSVs under
    ///     <c>persistentDataPath/Telemetry/</c>), re-simulate each run with the *same purchases replayed*
    ///     (so spending behaviour is held fixed and only the combat/economy model is judged), and report
    ///     per-wave drift + per-metric MAPE. Where drift concentrates is where the model — or the game's
    ///     balance assumptions — need attention.
    /// </summary>
    public static class CalibrationLoader
    {
        // RunTelemetry.cs header: event,wave,run_seconds,wave_seconds,kills,gold_earned,damage_taken,
        // hits_taken,damage_dealt,hits_dealt,crits,quick_attacks,charged_attacks,sprint_seconds,cost,detail
        private const int ColEvent = 0, ColWave = 1, ColWaveSeconds = 3, ColKills = 4, ColGold = 5,
            ColDamageTaken = 6, ColDamageDealt = 8, ColDetail = 15;

        public static string DefaultTelemetryDir =>
            Path.Combine(Application.persistentDataPath, "Telemetry");

        public static List<TelemetryRun> LoadRuns(string dir)
        {
            var runs = new List<TelemetryRun>();
            if (!Directory.Exists(dir))
            {
                return runs;
            }
            foreach (string file in Directory.GetFiles(dir, "run_*.csv").OrderBy(f => f))
            {
                TelemetryRun run = ParseRun(file);
                if (run != null && run.waves.Count > 0)
                {
                    runs.Add(run);
                }
            }
            return runs;
        }

        private static TelemetryRun ParseRun(string file)
        {
            var run = new TelemetryRun { file = Path.GetFileName(file) };
            foreach (string line in File.ReadAllLines(file).Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                List<string> f = CsvUtil.SplitLine(line.TrimEnd('\r'));
                if (f.Count <= ColDetail)
                {
                    continue;
                }
                string evt = f[ColEvent].Trim();
                switch (evt)
                {
                    case "run_start":
                        // detail: startWave=..;class=..;gold=..;...
                        foreach (string part in f[ColDetail].Split(';'))
                        {
                            if (part.StartsWith("class="))
                            {
                                run.playerClass = part.Substring("class=".Length).Trim();
                            }
                        }
                        break;
                    case "wave_clear":
                    case "death":
                        run.waves.Add(new TelemetryWave
                        {
                            wave = I(f[ColWave]),
                            waveSeconds = F(f[ColWaveSeconds]),
                            kills = I(f[ColKills]),
                            goldEarned = I(f[ColGold]),
                            damageTaken = F(f[ColDamageTaken]),
                            damageDealt = F(f[ColDamageDealt]),
                            fatal = evt == "death",
                        });
                        if (evt == "death")
                        {
                            run.deathWave = I(f[ColWave]);
                        }
                        break;
                    case "purchase":
                        // detail: {tree}:{node.id}:{node.displayName}; only gold-tree buys concern the sim.
                        string[] parts = f[ColDetail].Split(':');
                        if (parts.Length >= 2 && parts[0].Trim() == "gold")
                        {
                            int wave = I(f[ColWave]);
                            if (!run.purchasesByWave.TryGetValue(wave, out List<string> list))
                            {
                                run.purchasesByWave[wave] = list = new List<string>();
                            }
                            list.Add(parts[1].Trim());
                        }
                        break;
                }
            }
            return run;
        }

        /// <summary>
        ///     Runs the calibration comparison for every telemetry run and writes
        ///     <c>calibration.csv</c> (per run × wave × metric: real vs sim median vs drift) plus a MAPE
        ///     block per metric. Returns the per-metric MAPE (0.20 = sim is off by 20% on average).
        /// </summary>
        public static Dictionary<string, float> RunCalibration(
            SimConfig baseCfg, string profileId, string telemetryDir, string outDir)
        {
            List<TelemetryRun> runs = LoadRuns(string.IsNullOrEmpty(telemetryDir) ? DefaultTelemetryDir : telemetryDir);
            if (runs.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No telemetry runs found in '{(string.IsNullOrEmpty(telemetryDir) ? DefaultTelemetryDir : telemetryDir)}'. "
                    + "Play a run first (RunTelemetry writes them automatically).");
            }

            var sb = new StringBuilder();
            sb.AppendLine("run,wave,metric,real,sim_median,drift_pct");
            // metric → list of |real−sim|/real
            var absErrors = new Dictionary<string, List<float>>();

            foreach (TelemetryRun run in runs)
            {
                // Only Swordsman runs are modeled in v1; runs with no class tag predate classes = Swordsman.
                if (!string.IsNullOrEmpty(run.playerClass)
                    && !run.playerClass.Equals("swordsman", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var cfg = new SimConfig
                {
                    profileIds = new List<string> { profileId },
                    trials = Math.Max(50, baseCfg.trials / 4),
                    maxWaves = run.waves.Max(w => w.wave),
                    seed = baseCfg.seed,
                    overrides = baseCfg.overrides,
                    purchaseScript = run.purchasesByWave,
                };
                SimWorld world = SimWorld.Load();
                SimOverrides.ApplyAll(world, cfg.overrides);
                var result = new ProfileResult { profile = world.profiles[profileId] };
                using (var simStats = new SimStats(world))
                {
                    for (int trial = 0; trial < cfg.trials; trial++)
                    {
                        result.trials.Add(SimEngine.RunTrial(world, result.profile, simStats, cfg, trial));
                    }
                }

                foreach (TelemetryWave real in run.waves)
                {
                    Compare(sb, absErrors, run, real, "wave_seconds", real.waveSeconds,
                        Percentiles.Median(result.WaveMetric(real.wave, r => r.clearSeconds, includeFatal: false)));
                    Compare(sb, absErrors, run, real, "damage_taken", real.damageTaken,
                        Percentiles.Median(result.WaveMetric(real.wave, r => r.damageTaken)));
                    Compare(sb, absErrors, run, real, "gold_earned", real.goldEarned,
                        Percentiles.Median(result.WaveMetric(real.wave, r => r.goldEarned)));
                    Compare(sb, absErrors, run, real, "kills", real.kills,
                        Percentiles.Median(result.WaveMetric(real.wave, r => r.kills)));
                }
            }

            if (absErrors.Count == 0)
            {
                throw new InvalidOperationException("Telemetry runs found, but none were Swordsman runs the v1 model covers.");
            }

            var mape = absErrors.ToDictionary(kv => kv.Key, kv => kv.Value.Average());
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "calibration.csv"), sb.ToString());
            File.WriteAllText(Path.Combine(outDir, "calibration_mape.json"),
                Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    profile = profileId,
                    runsCompared = runs.Count,
                    mape,
                    note = "MAPE per metric across all real waves (0.2 = sim off by 20% on average). "
                        + "Drift concentrating on specific waves points at the enemies unlocking there.",
                }, Newtonsoft.Json.Formatting.Indented));
            return mape;
        }

        private static void Compare(
            StringBuilder sb, Dictionary<string, List<float>> absErrors,
            TelemetryRun run, TelemetryWave real, string metric, float realValue, float simMedian)
        {
            if (float.IsNaN(simMedian))
            {
                return; // sim never reached this wave — the death-wave drift itself will show it
            }
            float drift = realValue != 0f ? (simMedian - realValue) / Math.Abs(realValue) : 0f;
            sb.AppendLine(string.Join(",",
                run.file, real.wave.ToString(CultureInfo.InvariantCulture), metric,
                realValue.ToString("0.###", CultureInfo.InvariantCulture),
                simMedian.ToString("0.###", CultureInfo.InvariantCulture),
                (drift * 100f).ToString("0.#", CultureInfo.InvariantCulture)));
            if (realValue != 0f)
            {
                if (!absErrors.TryGetValue(metric, out List<float> list))
                {
                    absErrors[metric] = list = new List<float>();
                }
                list.Add(Math.Abs(drift));
            }
        }

        private static int I(string s) => int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
        private static float F(string s) => float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
    }
}
