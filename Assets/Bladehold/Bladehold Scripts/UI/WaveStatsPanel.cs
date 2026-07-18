using System.Collections;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     The animated end-of-wave stats readout shown during the between-wave intermission (driven by
///     <see cref="WaveIntermissionUI" />). Snapshots the run scoreboard when each wave starts
///     (<see cref="WaveSpawner.WaveStarted" />) so it can show this wave's deltas — goblins slain and
///     gold earned — alongside the total gold and the current Hold-the-Line multiplier.
///     <see cref="PlayReveal" /> cascades the lines in: each number counts up (on unscaled time, so it
///     plays under a slow-mo or paused game), fills its optional bar, and plays a per-line
///     <see cref="MMF_Player" /> for the juice (pop / tick / text reveal). A pure display — it reads
///     <see cref="GameStats" />/<see cref="Wallet" />, never mutates anything.
/// </summary>
public class WaveStatsPanel : MonoBehaviour
{
    [SerializeField] private WaveSpawner spawner;

    [Header("Labels")]
    [SerializeField] private TMP_Text waveLabel;
    [SerializeField] private TMP_Text goblinsSlainText;
    [SerializeField] private TMP_Text goldEarnedText;
    [SerializeField] private TMP_Text totalGoldText;
    [Tooltip("Optional: shows the current Hold-the-Line gold multiplier (e.g. \"x1.15 Gold Bonus\"). Hidden when there's no streak.")]
    [SerializeField] private TMP_Text bonusMultiplierText;

    [Header("Optional fill bars (wipe 0..1 as the line reveals)")]
    [SerializeField] private Image goblinsBar;
    [SerializeField] private Image goldBar;

    [Header("Reveal timing")]
    [Tooltip("Seconds each number spends counting up to its value.")]
    [SerializeField] private float countUpDuration = 0.6f;
    [Tooltip("Seconds between each stat line revealing.")]
    [SerializeField] private float lineStagger = 0.35f;

    [Header("Per-line feedback (optional)")]
    [Tooltip("Played as the goblins-slain line reveals.")]
    [SerializeField] private MMF_Player goblinsRevealFeedback;
    [Tooltip("Played as the gold-earned line reveals.")]
    [SerializeField] private MMF_Player goldRevealFeedback;
    [Tooltip("Played as the total-gold line reveals.")]
    [SerializeField] private MMF_Player totalRevealFeedback;

    // Scoreboard snapshot taken at the start of the wave being summarised, so PlayReveal shows just
    // this wave's contribution rather than the running totals.
    private int killedAtWaveStart;
    private int goldAtWaveStart;

    private bool anyError = false;

    private void OnValidate()
    {
        if (spawner == null)
        {
            spawner = FindObjectOfType<WaveSpawner>();
        }
    }

    private void Start()
    {
        if (spawner == null)
        {
            Debug.LogError("WaveStatsPanel has no WaveSpawner to read wave stats from.");
            anyError = true;
            return;
        }

        spawner.WaveStarted += HandleWaveStarted;
    }

    private void OnDestroy()
    {
        if (spawner != null)
        {
            spawner.WaveStarted -= HandleWaveStarted;
        }
    }

    private void HandleWaveStarted(int wave)
    {
        killedAtWaveStart = GameStats.Instance != null ? GameStats.Instance.GoblinsKilled : 0;
        goldAtWaveStart = GameStats.Instance != null ? GameStats.Instance.GoldEarnedThisRun : 0;
    }

    /// <summary>
    ///     Cascades the stat lines in for the wave that just cleared. Runs on unscaled time so it plays
    ///     under a slow-mo or fully paused intermission. Safe to call with any label/bar left unassigned
    ///     — each is guarded.
    /// </summary>
    public IEnumerator PlayReveal(int clearedWave)
    {
        if (anyError)
        {
            yield break;
        }

        int killed = (GameStats.Instance != null ? GameStats.Instance.GoblinsKilled : 0) - killedAtWaveStart;
        int gold = (GameStats.Instance != null ? GameStats.Instance.GoldEarnedThisRun : 0) - goldAtWaveStart;
        int total = Player.Instance != null && Player.Instance.Wallet != null ? Player.Instance.Wallet.Coins : 0;
        killed = Mathf.Max(0, killed);
        gold = Mathf.Max(0, gold);

        if (waveLabel != null)
        {
            waveLabel.text = Loc.Format("wavestats.cleared", clearedWave);
        }

        // Bonus multiplier line — hidden unless a Hold-the-Line streak is banked.
        if (bonusMultiplierText != null)
        {
            float mult = HoldTheLineBonus.Instance != null ? HoldTheLineBonus.Instance.Multiplier : 1f;
            bool show = mult > 1.0001f;
            bonusMultiplierText.gameObject.SetActive(show);
            if (show)
            {
                bonusMultiplierText.text = Loc.Format("wavestats.gold_bonus", mult.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        yield return CountUp(goblinsSlainText, goblinsBar, Loc.Get("wavestats.goblins_slain"), killed, goblinsRevealFeedback);
        yield return new WaitForSecondsRealtime(lineStagger);
        yield return CountUp(goldEarnedText, goldBar, Loc.Get("wavestats.gold_earned"), gold, goldRevealFeedback);
        yield return new WaitForSecondsRealtime(lineStagger);
        yield return CountUp(totalGoldText, null, Loc.Get("wavestats.total_gold"), total, totalRevealFeedback);
    }

    private IEnumerator CountUp(TMP_Text label, Image bar, string format, int target, MMF_Player feedback)
    {
        if (feedback != null)
        {
            feedback.PlayFeedbacks();
        }

        float elapsed = 0f;
        while (elapsed < countUpDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = countUpDuration > 0f ? Mathf.Clamp01(elapsed / countUpDuration) : 1f;
            int shown = Mathf.RoundToInt(Mathf.Lerp(0f, target, t));
            if (label != null)
            {
                label.text = string.Format(format, shown);
            }
            if (bar != null)
            {
                bar.fillAmount = t;
            }
            yield return null;
        }

        if (label != null)
        {
            label.text = string.Format(format, target);
        }
        if (bar != null)
        {
            bar.fillAmount = 1f;
        }
    }
}
