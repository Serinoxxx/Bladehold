using System;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     The "Hold the Line" greed economy. When the player skips the recovery intermission and dives
///     straight into the next wave, a small stacking gold bonus is banked: each consecutive Hold adds
///     <see cref="StatType.HoldTheLineGoldPerWave" /> (base 5%) as a Percent modifier onto
///     <see cref="StatType.GoldDropMultiplier" />, so gold drops climb the longer the player presses
///     on without recovering. Both loss conditions reset the whole stack — the player dying
///     (<see cref="Health.OnDied" />) or a gate falling (<see cref="Gate.OnAnyGateDestroyed" />, the
///     two-fail-state pattern <see cref="DeathScreen" />/<see cref="WaveSpawner" /> already use).
///     Choosing "Recover and Upgrade" keeps the banked stack but doesn't grow it.
///
///     <see cref="PlayerStats" /> has no timed modifiers, so this owns the total percent it has pushed
///     and cancels it with an exact negative modifier on reset (the <see cref="ImpulseBuff" />
///     add/remove idiom). The per-wave amount is a stat itself so a Reincarnate node ("Greedy Stand")
///     can deepen it, per the "expose upgradeable numbers as stats" convention. A scene singleton so
///     the intermission UI and any HUD indicator can reach it.
/// </summary>
public class HoldTheLineBonus : MonoBehaviour
{
    [Tooltip("Optional; defaults to Player.Instance.Stats.")]
    [SerializeField] private PlayerStats stats;
    [Tooltip("Base gold-drop bonus banked per consecutive Hold-the-Line wave (0.05 = +5%). A Reincarnate node raises it.")]
    [SerializeField] private float baseGoldPerWave = 0.05f;

    [Header("Feedback (cosmetic, optional)")]
    [Tooltip("Played when a Hold-the-Line stack is banked.")]
    [SerializeField] private MMF_Player extendFeedback;
    [Tooltip("Played when the streak is reset by a loss (death or a gate falling).")]
    [SerializeField] private MMF_Player resetFeedback;

    public static HoldTheLineBonus Instance { get; private set; }

    /// <summary>Raised whenever the stack count / multiplier changes (bank or reset).</summary>
    public event Action OnChanged;

    /// <summary>How many consecutive Hold-the-Line waves are currently banked.</summary>
    public int StackCount { get; private set; }

    /// <summary>The gold multiplier the streak currently contributes (1 = no bonus).</summary>
    public float Multiplier => 1f + appliedPercent;

    // The exact Percent total pushed onto GoldDropMultiplier, so a reset cancels it precisely.
    private float appliedPercent;
    private Health playerHealth;
    private bool anyError = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (stats == null)
        {
            stats = Player.Instance != null ? Player.Instance.Stats : null;
        }
        if (stats == null)
        {
            Debug.LogError("HoldTheLineBonus could not find PlayerStats (set it or ensure Player.Instance.Stats exists).");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        stats.SetBase(StatType.HoldTheLineGoldPerWave, baseGoldPerWave);

        // Reset the greed streak on either loss condition. On death/gate the scene reloads (which would
        // reset the fresh PlayerStats anyway), but resetting now drops the multiplier immediately so any
        // HUD indicator reads x1 the moment the run ends.
        Player player = Player.Instance;
        if (player != null && player.Health != null)
        {
            playerHealth = player.Health;
            playerHealth.OnDied += HandlePlayerDied;
        }
        Gate.OnAnyGateDestroyed += HandleGateDestroyed;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        if (playerHealth != null)
        {
            playerHealth.OnDied -= HandlePlayerDied;
        }
        Gate.OnAnyGateDestroyed -= HandleGateDestroyed;
    }

    /// <summary>
    ///     Banks one more Hold-the-Line wave: adds the current per-wave amount
    ///     (<see cref="StatType.HoldTheLineGoldPerWave" />) as a Percent modifier on the gold
    ///     multiplier. Called by the intermission UI when the player picks "Hold the Line".
    /// </summary>
    public void Extend()
    {
        if (anyError)
        {
            return;
        }

        float inc = stats.GetValue(StatType.HoldTheLineGoldPerWave);
        if (inc <= 0f)
        {
            return;
        }

        stats.AddModifier(StatType.GoldDropMultiplier, ModifierKind.Percent, inc);
        appliedPercent += inc;
        StackCount++;

        if (extendFeedback != null)
        {
            extendFeedback.PlayFeedbacks();
        }

        OnChanged?.Invoke();
    }

    /// <summary>Clears the whole streak, cancelling the exact percent it pushed onto the gold multiplier.</summary>
    public void ResetBonus()
    {
        if (anyError)
        {
            return;
        }

        bool had = StackCount > 0 || appliedPercent != 0f;

        if (appliedPercent != 0f)
        {
            stats.AddModifier(StatType.GoldDropMultiplier, ModifierKind.Percent, -appliedPercent);
            appliedPercent = 0f;
        }
        StackCount = 0;

        if (had && resetFeedback != null)
        {
            resetFeedback.PlayFeedbacks();
        }

        OnChanged?.Invoke();
    }

    private void HandlePlayerDied()
    {
        ResetBonus();
    }

    private void HandleGateDestroyed(Gate gate)
    {
        ResetBonus();
    }
}
