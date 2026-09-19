using System;
using UnityEngine;

/// <summary>
///     Base class for all interactive battlefield defenses built on Tower Plots.
///     Consumes Supply as it attacks, breaks when reaching 0 Supply, and can be
///     repaired/resupplied or upgraded via player interaction [E].
///     Ignored by enemy AI.
/// </summary>
public abstract class DefenseStructure : MonoBehaviour, IInteractable
{
    [Header("Defense Identity")]
    [SerializeField] protected FortDefenseType defenseType;
    [SerializeField] protected int currentLevel = 1;
    [SerializeField] protected int maxLevel = 3;

    [Header("Supply Settings")]
    [SerializeField] protected int currentSupply = 50;
    [SerializeField] protected int maxSupply = 50;
    [SerializeField] protected int supplyPerAction = 2;

    [Header("Audio & Feedback")]
    [SerializeField] protected AudioClip repairSfx;
    [SerializeField] protected AudioClip upgradeSfx;
    [SerializeField] protected AudioClip breakSfx;
    [SerializeField] protected GameObject breakVfxPrefab;

    public FortDefenseType DefenseType => defenseType;
    public int Level => currentLevel;
    public int MaxLevel => maxLevel;
    public int CurrentSupply => currentSupply;
    public int MaxSupply => maxSupply;
    public int SupplyPerAction => supplyPerAction;

    public TowerPlot OwnerPlot { get; set; }
    public int PlotIndex { get; set; } = -1;

    public string PromptText { get; protected set; } = "Interact";
    public virtual bool CanInteract => true;
    public Vector3 InteractionPosition => transform.position;

    public event Action<int, int> OnSupplyChanged; // current, max
    public event Action<int> OnLevelChanged; // newLevel

    protected virtual void Awake()
    {
        UpdatePrompt();
    }

    protected virtual void Start()
    {
        UpdatePrompt();
        OnSupplyChanged?.Invoke(currentSupply, maxSupply);
    }

    public virtual void InitState(int level, int supply, int maxSup)
    {
        currentLevel = Mathf.Clamp(level, 1, maxLevel);
        maxSupply = maxSup > 0 ? maxSup : CalculateMaxSupplyForLevel(currentLevel);
        currentSupply = Mathf.Clamp(supply, 0, maxSupply);
        ApplyLevelStats(currentLevel);
        UpdatePrompt();
        OnSupplyChanged?.Invoke(currentSupply, maxSupply);
    }

    public virtual void SetLevel(int level)
    {
        currentLevel = Mathf.Clamp(level, 1, maxLevel);
        maxSupply = CalculateMaxSupplyForLevel(currentLevel);
        currentSupply = maxSupply;
        ApplyLevelStats(currentLevel);
        UpdatePrompt();
        OnLevelChanged?.Invoke(currentLevel);
        OnSupplyChanged?.Invoke(currentSupply, maxSupply);
    }

    public virtual void Upgrade()
    {
        if (currentLevel >= maxLevel) return;

        currentLevel++;
        maxSupply = CalculateMaxSupplyForLevel(currentLevel);
        currentSupply = maxSupply;
        ApplyLevelStats(currentLevel);

        if (upgradeSfx != null)
        {
            AudioSource.PlayClipAtPoint(upgradeSfx, transform.position);
        }

        UpdatePrompt();
        OnLevelChanged?.Invoke(currentLevel);
        OnSupplyChanged?.Invoke(currentSupply, maxSupply);
        Debug.Log($"[DefenseStructure] Upgraded {defenseType} to Level {currentLevel}!");
    }

    public virtual bool ConsumeSupply(int amount = -1)
    {
        int cost = amount > 0 ? amount : supplyPerAction;
        currentSupply = Mathf.Max(0, currentSupply - cost);
        OnSupplyChanged?.Invoke(currentSupply, maxSupply);
        UpdatePrompt();

        if (currentSupply <= 0)
        {
            OnSupplyDepleted();
            return false;
        }

        return true;
    }

    protected virtual void OnSupplyDepleted()
    {
        Debug.Log($"[DefenseStructure] {defenseType} ran out of Supply and broke!");

        if (breakSfx != null)
        {
            AudioSource.PlayClipAtPoint(breakSfx, transform.position);
        }

        if (breakVfxPrefab != null)
        {
            Instantiate(breakVfxPrefab, transform.position, Quaternion.identity);
        }

        if (OwnerPlot != null)
        {
            OwnerPlot.OnDefenseDestroyed(this);
        }

        Destroy(gameObject);
    }

    public virtual void Interact(Player player)
    {
        if (currentSupply < maxSupply)
        {
            // Resupply
            int needed = maxSupply - currentSupply;
            int available = RunSession.InRunSupply;
            int toProvide = Mathf.Min(needed, available);

            if (toProvide > 0 && RunSession.TrySpendInRunSupply(toProvide))
            {
                currentSupply += toProvide;
                if (repairSfx != null)
                {
                    AudioSource.PlayClipAtPoint(repairSfx, transform.position);
                }
                UpdatePrompt();
                OnSupplyChanged?.Invoke(currentSupply, maxSupply);
                Debug.Log($"[DefenseStructure] Resupplied {toProvide} Supply. Current: {currentSupply}/{maxSupply}");
            }
        }
        else if (currentLevel < maxLevel)
        {
            // Upgrade (must be fully supplied)
            int upgradeCost = GetUpgradeCost();
            if (RunSession.InRunSupply >= upgradeCost && RunSession.TrySpendInRunSupply(upgradeCost))
            {
                Upgrade();
            }
            else
            {
                Debug.Log($"[DefenseStructure] Need {upgradeCost} Supply to upgrade (have {RunSession.InRunSupply}).");
            }
        }
    }

    public virtual int GetUpgradeCost()
    {
        return 40 * currentLevel;
    }

    protected virtual int CalculateMaxSupplyForLevel(int level)
    {
        return 50 + (level - 1) * 25; // 50, 75, 100
    }

    public virtual void UpdatePrompt()
    {
        if (currentSupply < maxSupply)
        {
            int cost = maxSupply - currentSupply;
            PromptText = $"Resupply ({cost} Supply)";
        }
        else if (currentLevel < maxLevel)
        {
            int cost = GetUpgradeCost();
            PromptText = $"Upgrade to Lv {currentLevel + 1} ({cost} Supply)";
        }
        else
        {
            PromptText = $"Lv {currentLevel} Max";
        }
    }

    protected abstract void ApplyLevelStats(int level);
}
