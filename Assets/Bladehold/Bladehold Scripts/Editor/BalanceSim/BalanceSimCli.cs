using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Bladehold.BalanceSim
{
    /// <summary>
    ///     Headless entry point (the BatchBuild precedent — no <c>-quit</c>, exit explicitly):
    ///     <code>
    ///     Unity.exe -batchmode -projectPath "&lt;proj&gt;" -executeMethod Bladehold.BalanceSim.BalanceSimCli.Run
    ///       -logFile BalanceReports\sim.log
    ///       -simProfiles bad,average,good -simTrials 200 -simWaves 20 -simSeed 12345
    ///       -simSet player.maxHealth=20 -simOverrides overrides.txt -simOut BalanceReports/hp20 -simEmitTrials
    ///     </code>
    ///     Exit codes: 0 = ran, all pacing rules pass/warn; 2 = ran, at least one <c>fail</c> verdict;
    ///     1 = the sim itself errored. A <c>[BalanceSim] started</c> line prints immediately so agents
    ///     can tell "sim failed" from "compile error meant -executeMethod never ran".
    /// </summary>
    public static class BalanceSimCli
    {
        public static void Run()
        {
            Console.WriteLine("[BalanceSim] started");
            Debug.Log("[BalanceSim] started");
            try
            {
                SimConfig cfg = ParseArgs(Environment.GetCommandLineArgs());

                if (cfg.calibrate)
                {
                    string calOut = string.IsNullOrEmpty(cfg.outDir)
                        ? Path.Combine("BalanceReports", $"calibration_{DateTime.Now:yyyyMMdd_HHmmss}")
                        : cfg.outDir;
                    Dictionary<string, float> mape =
                        CalibrationLoader.RunCalibration(cfg, cfg.calibrateProfile, cfg.telemetryDir, calOut);
                    string mapeLine = "[BalanceSim] calibration MAPE — "
                        + string.Join(", ", mape.Select(kv => $"{kv.Key}: {kv.Value:P0}"));
                    Console.WriteLine(mapeLine);
                    Debug.Log(mapeLine);
                    Console.WriteLine($"[BalanceSim] report: {Path.GetFullPath(calOut)}");
                    EditorApplication.Exit(0);
                    return;
                }

                SimRunOutput output = SimRunner.Run(cfg);

                string summaryLine = SummaryLine(output, cfg);
                Console.WriteLine(summaryLine);
                Debug.Log(summaryLine);
                Console.WriteLine($"[BalanceSim] report: {Path.GetFullPath(output.outDir)}");

                EditorApplication.Exit(output.AnyFail ? 2 : 0);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[BalanceSim] ERROR: {e.Message}");
                Debug.LogError($"[BalanceSim] ERROR: {e}");
                EditorApplication.Exit(1);
            }
        }

        private static string SummaryLine(SimRunOutput output, SimConfig cfg)
        {
            var parts = new List<string>();
            foreach (ProfileResult result in output.results)
            {
                float median = Percentiles.Median(result.DeathWaves(cfg.maxWaves));
                string deathText = median > cfg.maxWaves
                    ? $"survives past wave {cfg.maxWaves} (median)"
                    : $"death wave med {median:0.#}";
                parts.Add($"{result.profile.id}: {deathText}");
            }
            int fails = output.findings.Count(f => f.verdict == "fail");
            int warns = output.findings.Count(f => f.verdict == "warn");
            return $"[BalanceSim] done — {string.Join("; ", parts)} — {fails} fail, {warns} warn";
        }

        private static SimConfig ParseArgs(string[] args)
        {
            var cfg = new SimConfig();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-simProfiles":
                        cfg.profileIds = Next(args, ref i).Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                        break;
                    case "-simTrials":
                        cfg.trials = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture);
                        break;
                    case "-simWaves":
                        cfg.maxWaves = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture);
                        break;
                    case "-simSeed":
                        cfg.seed = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture);
                        break;
                    case "-simOut":
                        cfg.outDir = Next(args, ref i);
                        break;
                    case "-simSet":
                        cfg.overrides.Add(Next(args, ref i));
                        break;
                    case "-simOverrides":
                        string path = Next(args, ref i);
                        if (!File.Exists(path))
                        {
                            throw new InvalidOperationException($"-simOverrides file not found: {path}");
                        }
                        cfg.overrides.AddRange(File.ReadAllLines(path));
                        break;
                    case "-simEmitTrials":
                        cfg.emitTrials = true;
                        break;
                    case "-simCalibrate":
                        cfg.calibrate = true;
                        break;
                    case "-simCalibrateProfile":
                        cfg.calibrateProfile = Next(args, ref i);
                        break;
                    case "-simTelemetryDir":
                        cfg.telemetryDir = Next(args, ref i);
                        break;
                }
            }
            return cfg;
        }

        private static string Next(string[] args, ref int i)
        {
            if (i + 1 >= args.Length)
            {
                throw new InvalidOperationException($"Missing value after {args[i]}");
            }
            return args[++i];
        }
    }
}
