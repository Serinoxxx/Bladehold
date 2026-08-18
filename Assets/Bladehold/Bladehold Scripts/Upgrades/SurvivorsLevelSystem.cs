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
    private bool anyError = false;

    public event Action<int, int, int> OnXPChanged; // (currentLevelXP, targetXPForNextLevel, currentLevel)
    public event Action<int> OnLevelUp; // (newLevel)

    public int CurrentLevel => currentLevel;
    public int CurrentLevelXP => currentLevelXP;
    public int TargetXPForNextLevel => targetXPForNextLevel;

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
        if (wallet == null && Player.Instance != null)
        {
            wallet = Player.Instance.Wallet;
        }

        if (wallet == null)
        {
            wallet = UnityEngine.Object.FindAnyObjectByType<Wallet>();
        }

        if (wallet == null)
        {
            Debug.LogError("[SurvivorsLevelSystem] Essential dependency Wallet not found!");
            anyError = true;
            return;
        }

        currentLevel = 1;
        currentLevelXP = 0;
        targetXPForNextLevel = CalculateTargetXPForLevel(currentLevel);
        lastKnownWalletCoins = wallet.Coins;

        wallet.OnCoinsChanged += HandleCoinsChanged;
        OnXPChanged?.Invoke(currentLevelXP, targetXPForNextLevel, currentLevel);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (wallet != null)
        {
            wallet.OnCoinsChanged -= HandleCoinsChanged;
        }
    }

    /// <summary>
    ///     Calculates the gold required to advance from level <paramref name="level"/> to level <paramref name="level"/> + 1.
    /// </summary>
    public int CalculateTargetXPForLevel(int level)
    {
        if (level <= 0) level = 1;
        float exponentialPart = baseGoldTarget * Mathf.Pow(goldCostMultiplier, level - 1);
        int linearPart = flatCostIncrement * (level - 1);
        return Mathf.Max(baseGoldTarget, Mathf.RoundToInt(exponentialPart + linearPart));
    }

    private void HandleCoinsChanged(int newTotalCoins)
    {
        if (anyError) return;

        // Calculate gold gained since last check
        int gained = newTotalCoins - lastKnownWalletCoins;
        lastKnownWalletCoins = newTotalCoins;

        if (gained <= 0)
        {
            return;
        }

        totalRunGoldTracked += gained;
        AddXP(gained);
    }

    /// <summary>
    ///     Adds XP (gold) and handles single or multi level-ups if enough gold was acquired at once.
    /// </summary>
    public void AddXP(int amount)
    {
        if (amount <= 0 || anyError) return;

        currentLevelXP += amount;

        while (currentLevelXP >= targetXPForNextLevel)
        {
            currentLevelXP -= targetXPForNextLevel;
            currentLevel++;
            targetXPForNextLevel = CalculateTargetXPForLevel(currentLevel);

            Debug.Log($"[SurvivorsLevelSystem] LEVEL UP! Reached Level {currentLevel}! Next target: {targetXPForNextLevel} gold.");
            OnLevelUp?.Invoke(currentLevel);

            if (SurvivorsGameManager.Instance != null)
            {
                SurvivorsGameManager.Instance.PauseForCardSelection();
            }
        }

        OnXPChanged?.Invoke(currentLevelXP, targetXPForNextLevel, currentLevel);
    }
}
