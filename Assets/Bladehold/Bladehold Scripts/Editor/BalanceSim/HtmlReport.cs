using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Bladehold.BalanceSim
{
    /// <summary>
    ///     Renders <c>report.html</c> — fully self-contained (inline CSS + static SVG, zero external
    ///     requests) so it opens from disk, in CI artifacts, or via SendUserFile. One section per
    ///     profile: survival curve, HP trajectory band, clear-time curve, death-wave histogram; findings
    ///     table up top.
    /// </summary>
    public static class HtmlReport
    {
        private const int W = 640, H = 220, PadL = 44, PadB = 26, PadT = 12, PadR = 12;

        public static string Build(SimConfig cfg, SimWorld world, List<ProfileResult> results, List<Finding> findings)
        {
            var sb = new StringBuilder();
            sb.Append("<title>Bladehold Balance Report</title>");
            sb.Append("<style>"
                + "body{font-family:system-ui,sans-serif;max-width:960px;margin:2rem auto;padding:0 1rem;"
                + "background:#14161a;color:#e6e6e6}"
                + "h1,h2{font-weight:600}table{border-collapse:collapse;width:100%;font-size:.85rem}"
                + "td,th{border:1px solid #333;padding:4px 8px;text-align:left}"
                + ".pass{color:#7cd67c}.warn{color:#e6c35a}.fail{color:#ef7070}.skipped{color:#888}"
                + ".chart{background:#1c1f24;border-radius:8px;margin:.75rem 0;padding:4px}"
                + "code{background:#22262c;padding:1px 5px;border-radius:4px}"
                + "svg text{fill:#9aa0a8;font-size:10px}</style>");

            sb.Append("<h1>Bladehold balance projection</h1>");
            sb.Append($"<p>seed <code>{cfg.seed}</code> · {cfg.trials} trials/profile · horizon {cfg.maxWaves} waves"
                + $" · player HP {world.playerMaxHealth} · sword {world.swordBaseDamage}</p>");
            if (cfg.overrides.Count > 0)
            {
                sb.Append("<p>overrides: " + string.Join(" ", cfg.overrides.Select(o => $"<code>{Escape(o)}</code>")) + "</p>");
            }

            sb.Append("<h2>Pacing verdicts</h2><table><tr><th>Verdict</th><th>Rule</th><th>Profile</th><th>Metric</th><th>Observed</th><th>Band</th><th>Intent</th></tr>");
            foreach (Finding f in findings)
            {
                string band = $"{(f.bandMin?.ToString("0.##", CultureInfo.InvariantCulture) ?? "")}..{(f.bandMax?.ToString("0.##", CultureInfo.InvariantCulture) ?? "")}";
                sb.Append($"<tr><td class=\"{f.verdict}\">{f.verdict.ToUpperInvariant()}</td><td>{Escape(f.ruleId)}</td>"
                    + $"<td>{Escape(f.profile)}</td><td>{Escape(f.metric)}</td>"
                    + $"<td>{(float.IsNaN(f.observed) ? "—" : f.observed.ToString("0.##", CultureInfo.InvariantCulture))}</td>"
                    + $"<td>{band}</td><td>{Escape(f.message)}</td></tr>");
            }
            sb.Append("</table>");

            foreach (ProfileResult result in results)
            {
                AppendProfile(sb, result, cfg, world);
            }
            return sb.ToString();
        }

        private static void AppendProfile(StringBuilder sb, ProfileResult result, SimConfig cfg, SimWorld world)
        {
            List<float> deaths = result.DeathWaves(cfg.maxWaves);
            int survived = result.trials.Count(t => t.deathWave == 0);
            sb.Append($"<h2>{Escape(result.profile.id)} <small>({result.profile.upgradePolicy})</small></h2>");
            sb.Append($"<p>death wave median <b>{Percentiles.Median(deaths):0.#}</b>, "
                + $"p10 {Percentiles.Of(deaths, 10f):0.#}, p90 {Percentiles.Of(deaths, 90f):0.#} · "
                + $"{survived}/{result.TrialCount} survived the horizon</p>");

            int lastWave = LastReachedWave(result, cfg.maxWaves);
            float[] waves = Enumerable.Range(1, lastWave).Select(w => (float)w).ToArray();

            sb.Append(LineChart("Survival (fraction of trials alive at wave start)", waves,
                new[] { waves.Select(w => result.SurvivalRate((int)w)).ToArray() }, null, null, 0f, 1f));

            float[] hpMed = waves.Select(w => Percentiles.Median(result.WaveMetric((int)w, r => r.hpEnd, includeFatal: false))).ToArray();
            float[] hpLo = waves.Select(w => Percentiles.Of(result.WaveMetric((int)w, r => r.hpEnd, includeFatal: false), 10f)).ToArray();
            float[] hpHi = waves.Select(w => Percentiles.Of(result.WaveMetric((int)w, r => r.hpEnd, includeFatal: false), 90f)).ToArray();
            sb.Append(LineChart("HP at wave end (median, p10–p90 band)", waves, new[] { hpMed }, hpLo, hpHi, 0f, world.playerMaxHealth));

            float[] clrMed = waves.Select(w => Percentiles.Median(result.WaveMetric((int)w, r => r.clearSeconds, includeFatal: false))).ToArray();
            float[] clrLo = waves.Select(w => Percentiles.Of(result.WaveMetric((int)w, r => r.clearSeconds, includeFatal: false), 10f)).ToArray();
            float[] clrHi = waves.Select(w => Percentiles.Of(result.WaveMetric((int)w, r => r.clearSeconds, includeFatal: false), 90f)).ToArray();
            sb.Append(LineChart("Clear seconds (median, p10–p90 band)", waves, new[] { clrMed }, clrLo, clrHi, 0f, float.NaN));

            // Death histogram
            var histogram = new int[cfg.maxWaves + 2]; // index maxWaves+1 = survived
            foreach (TrialResult trial in result.trials)
            {
                histogram[trial.deathWave == 0 ? cfg.maxWaves + 1 : trial.deathWave]++;
            }
            sb.Append(Histogram("Death wave histogram (last bar = survived horizon)", histogram));
        }

        private static int LastReachedWave(ProfileResult result, int maxWaves)
        {
            for (int wave = maxWaves; wave >= 1; wave--)
            {
                if (result.SurvivalRate(wave) > 0f)
                {
                    return wave;
                }
            }
            return 1;
        }

        private static string LineChart(
            string title, float[] xs, float[][] seriesList, float[] bandLo, float[] bandHi, float yMin, float yMax)
        {
            var valid = seriesList.SelectMany(s => s).Concat(bandHi ?? Array.Empty<float>())
                .Where(v => !float.IsNaN(v)).ToList();
            if (valid.Count == 0)
            {
                return "";
            }
            if (float.IsNaN(yMax))
            {
                yMax = valid.Max() * 1.1f;
            }
            if (Math.Abs(yMax - yMin) < 0.001f)
            {
                yMax = yMin + 1f;
            }
            float xMin = xs.First(), xMax = Math.Max(xs.Last(), xMin + 1f);

            float X(float x) => PadL + (x - xMin) / (xMax - xMin) * (W - PadL - PadR);
            float Y(float y) => H - PadB - (y - yMin) / (yMax - yMin) * (H - PadB - PadT);

            var sb = new StringBuilder();
            sb.Append($"<div class=\"chart\"><svg viewBox=\"0 0 {W} {H}\" width=\"100%\">");
            sb.Append($"<text x=\"{PadL}\" y=\"{PadT}\">{Escape(title)}</text>");
            // Axes + gridlines
            for (int g = 0; g <= 4; g++)
            {
                float gy = Y(yMin + (yMax - yMin) * g / 4f);
                sb.Append($"<line x1=\"{PadL}\" y1=\"{S(gy)}\" x2=\"{W - PadR}\" y2=\"{S(gy)}\" stroke=\"#2c3138\" stroke-width=\"1\"/>");
                sb.Append($"<text x=\"4\" y=\"{S(gy + 3f)}\">{(yMin + (yMax - yMin) * g / 4f):0.#}</text>");
            }
            foreach (float x in xs.Where((_, i) => i % Math.Max(1, xs.Length / 10) == 0))
            {
                sb.Append($"<text x=\"{S(X(x) - 4f)}\" y=\"{H - 8}\">{x:0}</text>");
            }
            // p10–p90 band
            if (bandLo != null && bandHi != null)
            {
                var points = new List<string>();
                for (int i = 0; i < xs.Length; i++)
                {
                    if (!float.IsNaN(bandHi[i])) points.Add($"{S(X(xs[i]))},{S(Y(Clamp(bandHi[i], yMin, yMax)))}");
                }
                for (int i = xs.Length - 1; i >= 0; i--)
                {
                    if (!float.IsNaN(bandLo[i])) points.Add($"{S(X(xs[i]))},{S(Y(Clamp(bandLo[i], yMin, yMax)))}");
                }
                if (points.Count > 2)
                {
                    sb.Append($"<polygon points=\"{string.Join(" ", points)}\" fill=\"#4a90d955\" stroke=\"none\"/>");
                }
            }
            // Series lines
            string[] colors = { "#4a90d9", "#e6c35a", "#7cd67c" };
            for (int s = 0; s < seriesList.Length; s++)
            {
                var points = new List<string>();
                for (int i = 0; i < xs.Length; i++)
                {
                    if (!float.IsNaN(seriesList[s][i]))
                    {
                        points.Add($"{S(X(xs[i]))},{S(Y(Clamp(seriesList[s][i], yMin, yMax)))}");
                    }
                }
                sb.Append($"<polyline points=\"{string.Join(" ", points)}\" fill=\"none\" stroke=\"{colors[s % colors.Length]}\" stroke-width=\"2\"/>");
            }
            sb.Append("</svg></div>");
            return sb.ToString();
        }

        private static string Histogram(string title, int[] counts)
        {
            int max = Math.Max(1, counts.Max());
            float barW = (W - PadL - PadR) / (float)counts.Length;
            var sb = new StringBuilder();
            sb.Append($"<div class=\"chart\"><svg viewBox=\"0 0 {W} {H}\" width=\"100%\">");
            sb.Append($"<text x=\"{PadL}\" y=\"{PadT}\">{Escape(title)}</text>");
            for (int i = 1; i < counts.Length; i++)
            {
                float h = counts[i] / (float)max * (H - PadB - PadT - 10);
                float x = PadL + (i - 1) * barW;
                bool survivedBar = i == counts.Length - 1;
                sb.Append($"<rect x=\"{S(x + 1f)}\" y=\"{S(H - PadB - h)}\" width=\"{S(barW - 2f)}\" height=\"{S(h)}\" fill=\"{(survivedBar ? "#7cd67c" : "#ef7070")}\"/>");
                if (counts[i] > 0)
                {
                    sb.Append($"<text x=\"{S(x + barW * 0.3f)}\" y=\"{S(H - PadB - h - 3f)}\">{counts[i]}</text>");
                }
                sb.Append($"<text x=\"{S(x + barW * 0.3f)}\" y=\"{H - 8}\">{(survivedBar ? ">" : (i).ToString())}</text>");
            }
            sb.Append("</svg></div>");
            return sb.ToString();
        }

        private static float Clamp(float v, float lo, float hi) => Math.Min(hi, Math.Max(lo, v));
        private static string S(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);

        private static string Escape(string s) =>
            s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
