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
        private string calibrateProfile = "average";
        private string calibrationResult;

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

        private void DrawCalibration()
        {
            EditorGUILayout.BeginHorizontal();
            calibrateProfile = EditorGUILayout.TextField(new GUIContent("Calibrate as",
                "Profile whose combat model is compared against your real Telemetry runs (purchases replayed verbatim)"),
                calibrateProfile);
            if (GUILayout.Button("Calibrate vs Telemetry", GUILayout.Width(160)))
            {
                try
                {
                    EditorUtility.DisplayProgressBar("Balance Sim", "Replaying telemetry runs…", 0.5f);
                    string calOut = Path.Combine("BalanceReports", $"calibration_{DateTime.Now:yyyyMMdd_HHmmss}");
                    var cfg = new SimConfig { trials = Mathf.Max(1, trials), seed = seed };
                    var mape = CalibrationLoader.RunCalibration(cfg, calibrateProfile.Trim(), "", calOut);
                    calibrationResult = "MAPE — " + string.Join(", ", mape.Select(kv => $"{kv.Key}: {kv.Value:P0}"))
                        + $"  →  {calOut}\\calibration.csv";
                }
                catch (Exception e)
                {
                    calibrationResult = e.Message;
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(calibrationResult))
            {
                EditorGUILayout.HelpBox(calibrationResult, MessageType.Info);
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
                if (GUILayout.Button("Open report.html", GUILayout.Height(24)))
                {
                    Application.OpenURL("file:///" + Path.GetFullPath(Path.Combine(output.outDir, "report.html")).Replace('\\', '/'));
                }
            }
            EditorGUILayout.EndHorizontal();
            DrawCalibration();
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

            DrawCharts(result);

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

        /// <summary>Death-wave histogram + HP p10–p90 band with a median polyline, drawn with DrawRect
        /// (Handles for the line, Repaint only) — no dependencies, matches the report.html charts.</summary>
        private void DrawCharts(ProfileResult result)
        {
            float maxHp = output.world.playerMaxHealth;

            // --- Death histogram ---
            EditorGUILayout.LabelField("Death wave histogram (green = survived horizon)", EditorStyles.miniBoldLabel);
            Rect histRect = GUILayoutUtility.GetRect(10, 64, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(histRect, new Color(0.12f, 0.13f, 0.15f));
            var counts = new int[maxWaves + 2];
            foreach (TrialResult trial in result.trials)
            {
                counts[trial.deathWave == 0 ? maxWaves + 1 : Mathf.Clamp(trial.deathWave, 1, maxWaves)]++;
            }
            int maxCount = Mathf.Max(1, counts.Max());
            float barW = histRect.width / (maxWaves + 1);
            for (int i = 1; i < counts.Length; i++)
            {
                if (counts[i] == 0) continue;
                float h = counts[i] / (float)maxCount * (histRect.height - 4f);
                var bar = new Rect(histRect.x + (i - 1) * barW + 1, histRect.yMax - h - 2, barW - 2, h);
                EditorGUI.DrawRect(bar, i == counts.Length - 1
                    ? new Color(0.49f, 0.84f, 0.49f)
                    : new Color(0.94f, 0.44f, 0.44f));
            }

            // --- HP trajectory band + median line ---
            EditorGUILayout.LabelField("HP at wave end (median line, p10–p90 band)", EditorStyles.miniBoldLabel);
            Rect hpRect = GUILayoutUtility.GetRect(10, 72, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(hpRect, new Color(0.12f, 0.13f, 0.15f));
            int lastWave = 1;
            for (int w = maxWaves; w >= 1; w--) { if (result.SurvivalRate(w) > 0f) { lastWave = w; break; } }
            float colW = hpRect.width / Mathf.Max(1, lastWave);
            var medians = new List<Vector3>();
            for (int w = 1; w <= lastWave; w++)
            {
                List<float> hp = result.WaveMetric(w, r => r.hpEnd, includeFatal: false);
                if (hp.Count == 0) continue;
                float lo = Percentiles.Of(hp, 10f) / maxHp, hi = Percentiles.Of(hp, 90f) / maxHp;
                float med = Percentiles.Median(hp) / maxHp;
                float x = hpRect.x + (w - 0.5f) * colW;
                EditorGUI.DrawRect(new Rect(x - colW * 0.4f, hpRect.yMax - hi * hpRect.height,
                    colW * 0.8f, Mathf.Max(1f, (hi - lo) * hpRect.height)), new Color(0.29f, 0.56f, 0.85f, 0.33f));
                medians.Add(new Vector3(x, hpRect.yMax - med * hpRect.height, 0f));
            }
            if (Event.current.type == EventType.Repaint && medians.Count > 1)
            {
                Handles.color = new Color(0.29f, 0.56f, 0.85f);
                Handles.DrawAAPolyLine(2.5f, medians.ToArray());
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
