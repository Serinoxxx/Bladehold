using System.Collections.Generic;

namespace Bladehold.BalanceSim
{
    /// <summary>
    ///     One simulation request: which profiles to run, how many Monte-Carlo trials each, how deep,
    ///     the RNG seed, output folder, and any what-if overrides. Built from CLI args by
    ///     <see cref="BalanceSimCli" /> or from the window by <see cref="BalanceSimWindow" />.
    /// </summary>
    public class SimConfig
    {
        public List<string> profileIds = new List<string> { "bad", "bad_noupgrades", "average", "good" };
        public int trials = 200;
        /// <summary>Waves simulated per trial before the run is declared "survived past the horizon".</summary>
        public int maxWaves = 20;
        public int seed = 12345;
        public string outDir = "";
        /// <summary>Raw <c>key=value</c> override lines, applied by <see cref="SimOverrides" />.</summary>
        public List<string> overrides = new List<string>();
        public bool emitTrials = false;

        /// <summary>Safety cap on simulated seconds per wave — a wave taking longer counts as stalled.</summary>
        public float maxWaveSeconds = 600f;
        /// <summary>Sim tick length in seconds.</summary>
        public const float TickSeconds = 0.25f;
    }
}
