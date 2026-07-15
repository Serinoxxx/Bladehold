using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Bladehold.BalanceSim
{
    /// <summary>Everything one sim run produced, for the window/CLI to display or exit-code on.</summary>
    public class SimRunOutput
    {
        public SimWorld world;
        public List<ProfileResult> results = new List<ProfileResult>();
        public List<Finding> findings = new List<Finding>();
        public string outDir;
        public bool AnyFail => findings.Any(f => f.verdict == "fail");
    }

    /// <summary>
    ///     The one entry point both the CLI and the EditorWindow call: load the world snapshot, apply
    ///     overrides, run every requested profile's Monte-Carlo trials, evaluate pacing rules, write
    ///     the report folder.
    /// </summary>
    public static class SimRunner
    {
        public static SimRunOutput Run(SimConfig cfg)
        {
            SimWorld world = SimWorld.Load();
            SimOverrides.ApplyAll(world, cfg.overrides);

            var output = new SimRunOutput { world = world };
            using (var simStats = new SimStats(world))
            {
                foreach (string profileId in cfg.profileIds)
                {
                    if (!world.profiles.TryGetValue(profileId, out PlayerProfile profile))
                    {
                        throw new InvalidOperationException(
                            $"No profile '{profileId}' in {PlayerProfile.CsvAssetPath} "
                            + $"(have: {string.Join(", ", world.profiles.Keys)}).");
                    }
                    var result = new ProfileResult { profile = profile };
                    for (int trial = 0; trial < cfg.trials; trial++)
                    {
                        result.trials.Add(SimEngine.RunTrial(world, profile, simStats, cfg, trial));
                    }
                    output.results.Add(result);
                }
            }

            output.findings = PacingRules.Evaluate(PacingRules.Load(), output.results, cfg, world.playerMaxHealth);

            output.outDir = string.IsNullOrEmpty(cfg.outDir)
                ? Path.Combine("BalanceReports", $"run_{DateTime.Now:yyyyMMdd_HHmmss}")
                : cfg.outDir;
            ReportWriter.WriteAll(output.outDir, cfg, world, output.results, output.findings);
            return output;
        }
    }
}
