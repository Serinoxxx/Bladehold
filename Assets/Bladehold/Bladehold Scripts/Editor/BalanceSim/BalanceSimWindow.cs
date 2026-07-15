using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Bladehold.BalanceSim
{
    /// <summary>
    ///     <b>Bladehold &gt; Balance Simulator</b> — the interactive face of the projection sim
    ///     (IMGUI, the SkillTreeCsvEditorWindow house style). Config strip on top (profiles, trials,
    ///     waves, seed, overrides textarea), then per-wave results tables per profile and the pacing
    ///     findings tinted green/amber/red. The heavy lifting all goes through <see cref="SimRunner" />,
    ///     the same path the headless CLI uses.
    /// </summary>
    public class BalanceSimWindow : EditorWindow
    {
        private string profilesText = "bad,average,good";
        private int trials = 200;
        private int maxWaves = 20;
        private int seed = 12345;
        private string overridesText = "";
        private bool emitTrials;

        private SimRunOutput output;
        private string error;
        private Vector2 scroll;
        private int selectedProfileTab;

        [MenuItem("Bladehold/Balance Simulator")]
        public static void Open()
        {
            GetWindow<BalanceSimWindow>("Balance Sim");
        }

        private void OnGUI()
        {
            DrawConfig();
            EditorGUILayout.Space();
            if (!string.IsNullOrEmpty(error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
            if (output != null)
            {
                scroll = EditorGUILayout.BeginScrollView(scroll);
                DrawFindings();
                EditorGUILayout.Space();
                DrawResultsTable();
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawConfig()
        {
            EditorGUILayout.LabelField("Run configuration", EditorStyles.boldLabel);
            profilesText = EditorGUILayout.TextField(new GUIContent("Profiles", "Comma-separated ids from SimProfiles.csv"), profilesText);
            EditorGUILayout.BeginHorizontal();
            trials = EditorGUILayout.IntField("Trials", trials);
            maxWaves = EditorGUILayout.IntField("Max waves", maxWaves);
            seed = EditorGUILayout.IntField("Seed", seed);
            EditorGUILayout.EndHorizontal();
            emitTrials = EditorGUILayout.Toggle(new GUIContent("Emit trials.csv", "Raw per-trial rows (large)"), emitTrials);

            EditorGUILayout.LabelField(new GUIContent("What-if overrides",
                "One key=value per line, e.g. player.maxHealth=20 or enemy.bomber.damage=15. '#' comments."));
            overridesText = EditorGUILayout.TextArea(overridesText, GUILayout.MinHeight(48));

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Run", GUILayout.Height(24)))
            {
                RunSim();
            }
            using (new EditorGUI.DisabledScope(output == null))
            {
                if (GUILayout.Button("Open report folder", GUILayout.Height(24)))
                {
                    EditorUtility.RevealInFinder(Path.Combine(Path.GetFullPath(output.outDir), "summary.json"));
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void RunSim()
        {
            error = null;
            output = null;
            var cfg = new SimConfig
            {
                profileIds = profilesText.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList(),
                trials = Mathf.Max(1, trials),
                maxWaves = Mathf.Max(1, maxWaves),
                seed = seed,
                emitTrials = emitTrials,
                overrides = overridesText
                    .Split('\n')
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 0)
                    .ToList(),
            };
            try
            {
                EditorUtility.DisplayProgressBar("Balance Sim", "Running trials…", 0.5f);
                output = SimRunner.Run(cfg);
                selectedProfileTab = 0;
            }
            catch (Exception e)
            {
                error = e.Message;
                Debug.LogError($"[BalanceSim] {e}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void DrawFindings()
        {
            EditorGUILayout.LabelField("Pacing verdicts", EditorStyles.boldLabel);
            if (output.findings.Count == 0)
            {
                EditorGUILayout.LabelField("(no pacing rules)");
                return;
            }
            foreach (Finding f in output.findings)
            {
                Color tint = f.verdict switch
                {
                    "fail" => new Color(1f, 0.45f, 0.45f),
                    "warn" => new Color(1f, 0.8f, 0.4f),
                    "pass" => new Color(0.55f, 1f, 0.55f),
                    _ => Color.gray,
                };
                Color old = GUI.color;
                GUI.color = tint;
                string band = $"{(f.bandMin.HasValue ? f.bandMin.Value.ToString("0.##") : "")}..{(f.bandMax.HasValue ? f.bandMax.Value.ToString("0.##") : "")}";
                EditorGUILayout.LabelField(
                    $"[{f.verdict.ToUpperInvariant()}] {f.ruleId} ({f.profile}) — {f.metric} = {f.observed:0.##} (band {band})  {f.message}",
                    EditorStyles.wordWrappedLabel);
                GUI.color = old;
            }
        }

        private void DrawResultsTable()
        {
            string[] tabs = output.results.Select(r => r.profile.id).ToArray();
            selectedProfileTab = GUILayout.Toolbar(Mathf.Clamp(selectedProfileTab, 0, tabs.Length - 1), tabs);
            ProfileResult result = output.results[selectedProfileTab];

            List<float> deaths = result.DeathWaves(maxWaves);
            EditorGUILayout.LabelField(
                $"Death wave: median {Percentiles.Median(deaths):0.#}, p10 {Percentiles.Of(deaths, 10f):0.#}, "
                + $"p90 {Percentiles.Of(deaths, 90f):0.#} ({result.trials.Count(t => t.deathWave == 0)}/{result.TrialCount} survived the horizon)",
                EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            Cell("Wave", 42, true);
            Cell("Alive%", 52, true);
            Cell("Clear s", 56, true);
            Cell("Dmg tkn", 58, true);
            Cell("HP end", 52, true);
            Cell("Min HP%", 58, true);
            Cell("Gold", 46, true);
            Cell("Purchases (modal)", 0, true);
            EditorGUILayout.EndHorizontal();

            for (int wave = 1; wave <= maxWaves; wave++)
            {
                float survival = result.SurvivalRate(wave);
                if (survival <= 0f)
                {
                    break;
                }
                EditorGUILayout.BeginHorizontal();
                Cell(wave.ToString(), 42);
                Cell((survival * 100f).ToString("0"), 52);
                Cell(Fmt(Percentiles.Median(result.WaveMetric(wave, r => r.clearSeconds, includeFatal: false))), 56);
                Cell(Fmt(Percentiles.Median(result.WaveMetric(wave, r => r.damageTaken))), 58);
                Cell(Fmt(Percentiles.Median(result.WaveMetric(wave, r => r.hpEnd, includeFatal: false))), 52);
                Cell(Fmt(Percentiles.Median(result.WaveMetric(wave, r => r.minHpFraction)) * 100f), 58);
                Cell(Fmt(Percentiles.Median(result.WaveMetric(wave, r => r.goldEarned))), 46);
                Cell(result.ModalPurchases(wave), 0);
                EditorGUILayout.EndHorizontal();
            }
        }

        private static string Fmt(float v) => float.IsNaN(v) ? "-" : v.ToString("0.#");

        private static void Cell(string text, float width, bool bold = false)
        {
            GUIStyle style = bold ? EditorStyles.miniBoldLabel : EditorStyles.miniLabel;
            if (width > 0f)
            {
                EditorGUILayout.LabelField(text, style, GUILayout.Width(width));
            }
            else
            {
                EditorGUILayout.LabelField(text, style);
            }
        }
    }
}
