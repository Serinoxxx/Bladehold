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

    [Header("Targeting & Rotation")]
    [Tooltip("When true, the tower rotates horizontally to face its current target.")]
    [SerializeField] protected bool rotateToTarget = false;
    [Tooltip("Speed in degrees per second at which the tower rotates towards the target.")]
    [SerializeField] protected float rotationSpeed = 160f;

    [Header("Audio & Feedback")]
    [SerializeField] protected AudioClip repairSfx;
    [SerializeField] protected AudioClip upgradeSfx;
    [SerializeField] protected AudioClip breakSfx;
    [SerializeField] protected GameObject breakVfxPrefab;
    [SerializeField] protected DamageNumbersPro.DamageNumber supplyPopupPrefab;
    [SerializeField] protected float interactionRadius = 3.5f;

    public FortDefenseType DefenseType => defenseType;
    public int Level => currentLevel;
    public int MaxLevel => maxLevel;
    public int CurrentSupply => currentSupply;
    public int MaxSupply => maxSupply;
    public int SupplyPerAction => supplyPerAction;
    public bool IsDepleted => currentSupply <= 0;
    /// <summary>Supply the player has paid to upgrade this tower (free level-ups via SetLevel/InitState don't count).</summary>
    public int UpgradeSupplySpent => upgradeSupplySpent;
    /// <summary>What dismantling this tower hands back: its remaining supply plus everything spent upgrading it.</summary>
    public int DismantleRefund => Mathf.Max(0, currentSupply) + upgradeSupplySpent;

    private int upgradeSupplySpent;
    public bool RotateToTarget
    {
        get => rotateToTarget;
        set => rotateToTarget = value;
    }
    public float RotationSpeed
    {
        get => rotationSpeed;
        set => rotationSpeed = value;
    }

    private static readonly System.Collections.Generic.List<DefenseStructure> allActive = new System.Collections.Generic.List<DefenseStructure>();
    public static System.Collections.Generic.IReadOnlyList<DefenseStructure> AllActive => allActive;

    public TowerPlot OwnerPlot { get; set; }
    public int PlotIndex { get; set; } = -1;

    public string PromptText { get; protected set; } = "Interact";
    public virtual bool CanInteract => true;
    public Vector3 InteractionPosition => transform.position;
    public virtual float InteractionRadius => interactionRadius;

    public event Action<int, int> OnSupplyChanged; // current, max
    public event Action<int> OnLevelChanged; // newLevel

    protected virtual void Awake()
    {
        UpdatePrompt();
    }

    protected virtual void OnEnable()
    {
        if (!allActive.Contains(this))
        {
            allActive.Add(this);
        }
        InteractableRegistry.Register(this);
    }

    protected virtual void OnDisable()
    {
        allActive.Remove(this);
        InteractableRegistry.Unregister(this);
    }

    protected virtual void Start()
    {
        ValidateFeedbackReferences();
        if (maxSupply <= 0) maxSupply = CalculateMaxSupplyForLevel(currentLevel);
        if (currentSupply <= 0) currentSupply = maxSupply;
        UpdatePrompt();
        OnSupplyChanged?.Invoke(currentSupply, maxSupply);
    }

    private void ValidateFeedbackReferences()
    {
        if (supplyPopupPrefab == null) Debug.LogError($"{name}: DefenseStructure.supplyPopupPrefab is not assigned.", this);
        if (repairSfx == null) Debug.LogError($"{name}: DefenseStructure.repairSfx is not assigned.", this);
        if (breakVfxPrefab == null) Debug.LogError($"{name}: DefenseStructure.breakVfxPrefab is not assigned.", this);
    }

    public virtual void InitState(int level, int supply, int maxSup)
    {
        currentLevel = Mathf.Clamp(level, 1, maxLevel);
        maxSupply = maxSup > 0 ? maxSup : CalculateMaxSupplyForLevel(currentLevel);
        currentSupply = supply >= 0 ? Mathf.Clamp(supply, 0, maxSupply) : maxSupply;
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
        if (currentSupply <= 0)
        {
            return false;
        }

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
        Debug.Log($"[DefenseStructure] {defenseType} ran out of Supply and stopped firing!");

        if (breakSfx != null)
        {
            AudioSource.PlayClipAtPoint(breakSfx, transform.position);
        }

        if (breakVfxPrefab != null)
        {
            Instantiate(breakVfxPrefab, transform.position, Quaternion.identity);
        }
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

                if (breakVfxPrefab != null)
                {
                    GameObject vfx = Instantiate(breakVfxPrefab, transform.position + Vector3.up * 0.8f, Quaternion.identity);
                    Destroy(vfx, 2.5f);
                }

                if (supplyPopupPrefab != null)
                {
                    supplyPopupPrefab.Spawn(transform.position + Vector3.up * 2.2f, $"-{toProvide} Supply");
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
                upgradeSupplySpent += upgradeCost;
                Upgrade();

                if (breakVfxPrefab != null)
                {
                    GameObject vfx = Instantiate(breakVfxPrefab, transform.position + Vector3.up * 1.0f, Quaternion.identity);
                    Destroy(vfx, 2.5f);
                }

                if (supplyPopupPrefab != null)
                {
                    supplyPopupPrefab.Spawn(transform.position + Vector3.up * 2.5f, $"-{upgradeCost} Supply");
                }
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
        if (currentSupply <= 0)
        {
            PromptText = $"[NO SUPPLY] Resupply ({maxSupply} Supply)";
        }
        else if (currentSupply < maxSupply)
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

    public virtual void RotateTowardsTarget(Vector3 worldTargetPos, float speedMultiplier = 1f)
    {
        if (!rotateToTarget) return;

        Vector3 toTarget = worldTargetPos - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * speedMultiplier * Time.deltaTime);
        }
    }

    protected abstract void ApplyLevelStats(int level);
}
