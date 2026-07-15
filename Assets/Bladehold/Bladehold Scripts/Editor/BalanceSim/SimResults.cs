using System;
using System.Collections.Generic;
using System.Linq;

namespace Bladehold.BalanceSim
{
    /// <summary>One cleared (or fatal) wave inside one trial.</summary>
    public class WaveRecord
    {
        public int wave;
        public float clearSeconds;
        public float damageTaken;
        public float damageDealt;
        public int kills;
        public int goldEarned;
        /// <summary>Player HP when the wave ended (or 0 on the death wave).</summary>
        public float hpEnd;
        /// <summary>Lowest HP fraction touched during this wave.</summary>
        public float minHpFraction;
        /// <summary>Gold left after the between-wave shopping that followed this wave.</summary>
        public int goldUnspent;
        /// <summary>Purchases made in the intermission after this wave ("id:level").</summary>
        public List<string> purchases = new List<string>();
        /// <summary>True when the wave hit the safety time cap instead of clearing.</summary>
        public bool stalled;
        /// <summary>True when the player died during this wave.</summary>
        public bool fatal;
    }

    /// <summary>One Monte-Carlo trial: a full run until death, stall, or the wave horizon.</summary>
    public class TrialResult
    {
        /// <summary>Wave the player died on; 0 = survived the whole horizon.</summary>
        public int deathWave;
        public bool stalled;
        public List<WaveRecord> waves = new List<WaveRecord>();
    }

    /// <summary>All trials for one profile, plus the aggregation the report and pacing rules read.</summary>
    public class ProfileResult
    {
        public PlayerProfile profile;
        public List<TrialResult> trials = new List<TrialResult>();

        public int TrialCount => trials.Count;

        /// <summary>Fraction of trials still alive at the *start* of the given wave.</summary>
        public float SurvivalRate(int wave)
        {
            if (trials.Count == 0) return 0f;
            int alive = trials.Count(t => t.deathWave == 0 || t.deathWave >= wave);
            return (float)alive / trials.Count;
        }

        /// <summary>Death waves with survivors counted as horizon+1 so percentiles stay meaningful.</summary>
        public List<float> DeathWaves(int horizon)
        {
            return trials.Select(t => (float)(t.deathWave == 0 ? horizon + 1 : t.deathWave)).ToList();
        }

        /// <summary>Pulls one per-wave metric across trials that reached the wave.</summary>
        public List<float> WaveMetric(int wave, Func<WaveRecord, float> selector, bool includeFatal = true)
        {
            var values = new List<float>();
            foreach (TrialResult trial in trials)
            {
                foreach (WaveRecord record in trial.waves)
                {
                    if (record.wave == wave && (includeFatal || !record.fatal))
                    {
                        values.Add(selector(record));
                    }
                }
            }
            return values;
        }

        /// <summary>The modal purchase set across trials for the intermission after the given wave.</summary>
        public string ModalPurchases(int wave)
        {
            var counts = new Dictionary<string, int>();
            foreach (TrialResult trial in trials)
            {
                foreach (WaveRecord record in trial.waves)
                {
                    if (record.wave == wave)
                    {
                        string key = string.Join(" ", record.purchases);
                        counts.TryGetValue(key, out int c);
                        counts[key] = c + 1;
                    }
                }
            }
            return counts.Count == 0 ? "" : counts.OrderByDescending(kv => kv.Value).First().Key;
        }
    }

    public static class Percentiles
    {
        /// <summary>Nearest-rank percentile; p in 0..100. Empty input returns NaN.</summary>
        public static float Of(List<float> values, float p)
        {
            if (values == null || values.Count == 0)
            {
                return float.NaN;
            }
            var sorted = values.OrderBy(v => v).ToList();
            int rank = (int)Math.Ceiling(p / 100f * sorted.Count);
            rank = Math.Clamp(rank, 1, sorted.Count);
            return sorted[rank - 1];
        }

        public static float Median(List<float> values) => Of(values, 50f);
    }
}
