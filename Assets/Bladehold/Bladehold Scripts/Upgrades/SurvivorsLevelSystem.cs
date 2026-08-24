using System;
using UnityEngine;

/// <summary>
///     Tracks player experience / gold progression in Survivors mode.
///     When cumulative gold collected reaches the target threshold for the current level,
///     triggers a level-up, calculates the next level threshold with configurable cost scaling,
///     and prompts the 3-card skill draft UI.
/// </summary>
public class SurvivorsLevelSystem : MonoBehaviour
{
    public static SurvivorsLevelSystem Instance { get; private set; }

    [Header("Leveling Tunables")]
    [Tooltip("Gold required to reach Level 2.")]
    [SerializeField] private int baseGoldTarget = 15;

    [Tooltip("Exponential multiplier for target gold per level (e.g., 1.25 = +25% per level).")]
    [SerializeField] private float goldCostMultiplier = 1.25f;

    [Tooltip("Linear extra gold cost added per level.")]
    [SerializeField] private int flatCostIncrement = 5;

    [Header("Dependencies")]
    [SerializeField] private Wallet wallet;

    private int currentLevel = 1;
    private int currentLevelXP = 0;
    private int targetXPForNextLevel;
    private int totalRunGoldTracked = 0;
    private int lastKnownWalletCoins = 0;
    private int pendingDrafts = 0;
    private bool anyError = false;

    public event Action<int, int, int> OnXPChanged; // (currentLevelXP, targetXPForNextLevel, currentLevel)
    public event Action<int> OnLevelUp; // (newLevel)
    public event Action<int> OnPendingDraftsChanged; // (pendingDrafts)

    public int CurrentLevel => currentLevel;
    public int CurrentLevelXP => currentLevelXP;
    public int TargetXPForNextLevel => targetXPForNextLevel;
    public int PendingDrafts => pendingDrafts;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        currentLevel = 1;
        currentLevelXP = 0;
        pendingDrafts = 0;
        totalRunGoldTracked = 0;
        targetXPForNextLevel = CalculateTargetXPForLevel(currentLevel);

        OnXPChanged?.Invoke(currentLevelXP, targetXPForNextLevel, currentLevel);
        OnPendingDraftsChanged?.Invoke(pendingDrafts);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    ///     Calculates the XP required to advance from level <paramref name="level"/> to level <paramref name="level"/> + 1.
    /// </summary>
    public int CalculateTargetXPForLevel(int level)
    {
        if (level <= 0) level = 1;
        float exponentialPart = baseGoldTarget * Mathf.Pow(goldCostMultiplier, level - 1);
        int linearPart = flatCostIncrement * (level - 1);
        return Mathf.Max(baseGoldTarget, Mathf.RoundToInt(exponentialPart + linearPart));
    }

    /// <summary>
    ///     Adds XP and increments pending skill drafts upon level up.
    /// </summary>
    public void AddXP(int amount)
    {
        if (amount <= 0 || anyError) return;

        currentLevelXP += amount;

        while (currentLevelXP >= targetXPForNextLevel)
        {
            currentLevelXP -= targetXPForNextLevel;
            currentLevel++;
            pendingDrafts++;
            targetXPForNextLevel = CalculateTargetXPForLevel(currentLevel);

            Debug.Log($"[SurvivorsLevelSystem] LEVEL UP! Reached Level {currentLevel}! Pending drafts: {pendingDrafts}. Next target: {targetXPForNextLevel} gold.");
            OnLevelUp?.Invoke(currentLevel);
            OnPendingDraftsChanged?.Invoke(pendingDrafts);
        }

        OnXPChanged?.Invoke(currentLevelXP, targetXPForNextLevel, currentLevel);
    }

    /// <summary>
    ///     Consumes one pending skill draft after a card has been picked.
    /// </summary>
    public void ConsumeDraft()
    {
        if (pendingDrafts > 0)
        {
            pendingDrafts--;
            OnPendingDraftsChanged?.Invoke(pendingDrafts);
        }
    }

    /// <summary>
    ///     Resets level and draft progress back to level 1 on run restart.
    /// </summary>
    public void ResetProgress()
    {
        currentLevel = 1;
        currentLevelXP = 0;
        pendingDrafts = 0;
        totalRunGoldTracked = 0;
        targetXPForNextLevel = CalculateTargetXPForLevel(currentLevel);
        OnXPChanged?.Invoke(currentLevelXP, targetXPForNextLevel, currentLevel);
        OnPendingDraftsChanged?.Invoke(pendingDrafts);
    }
}
