using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Base class for all interactive battlefield defenses built on Tower Plots.
///     Consumes Supply as it attacks, breaks when reaching 0 Supply, and can be
///     repaired/resupplied or upgraded via player interaction [E].
///     In a defense scene (<see cref="DefenseSceneRules.UpgradeWheelActive" />) [E] opens the plan-17
///     upgrade wheel instead: Refill, Fire Rate tiers, Spikes, one element, Deconstruct. The tower stays
///     at level 1 there; what was bought lives in <see cref="Upgrades" />.
///     Ignored by enemy AI.
/// </summary>
public abstract class DefenseStructure : MonoBehaviour, IInteractable, IAffordableInteractable, IUpgradeable
{
    [Header("Defense Identity")]
    [SerializeField] protected FortDefenseType defenseType;
    [SerializeField] protected int currentLevel = 1;
    [SerializeField] protected int maxLevel = 3;

    [Header("Supply Settings")]
    [SerializeField] protected int currentSupply = 50;
    [SerializeField] protected int maxSupply = 50;
    [Tooltip("Supply per shot before Thrifty Gunners. Fractions build up and are paid a whole unit at a time. Each subclass sets its own in Awake.")]
    [SerializeField] protected float supplyPerAction = 2f;

    [Header("Targeting & Rotation")]
    [Tooltip("When true, the tower rotates horizontally to face its current target.")]
    [SerializeField] protected bool rotateToTarget = false;
    [Tooltip("Speed in degrees per second at which the tower rotates towards the target.")]
    [SerializeField] protected float rotationSpeed = 160f;
    [Tooltip("Blind spot radius (flat distance from the tower's centre). Enemies closer than this can't be targeted, so enemies hugging the tower are the hero's job. 0 = no blind spot.")]
    [Min(0f)]
    [SerializeField] protected float minRange = 0f;

    [Header("Audio & Feedback")]
    [Tooltip("Played at the tower when the player resupplies it (sound + wood burst).")]
    [SerializeField] protected MMF_Player repairFeedback;
    [Tooltip("Played at the tower when it levels up (sound + wood burst).")]
    [SerializeField] protected MMF_Player upgradeFeedback;
    [Tooltip("Played at the tower when it runs out of supply (break sound + wood burst).")]
    [SerializeField] protected MMF_Player breakFeedback;
    [SerializeField] protected DamageNumbersPro.DamageNumber supplyPopupPrefab;
    [SerializeField] protected float interactionRadius = 3.5f;

    public FortDefenseType DefenseType => defenseType;
    public int Level => currentLevel;
    public int MaxLevel => maxLevel;
    public int CurrentSupply => currentSupply;
    public int MaxSupply => maxSupply;
    /// <summary>Supply one shot costs on average, after Thrifty Gunners.</summary>
    public float SupplyPerAction => supplyPerAction * RunSession.TowerShotSupplyMultiplier;
    public bool IsDepleted => currentSupply <= 0;
    public float MinRange => minRange;

    private readonly System.Collections.Generic.HashSet<MonoBehaviour> hexers = new System.Collections.Generic.HashSet<MonoBehaviour>();

    /// <summary>
    ///     True while any Hexer is channelling on this tower: it stays standing but can't fire (turrets,
    ///     traps, Tesla Spire and Permafrost all check this). A hexer destroyed mid-channel is pruned.
    /// </summary>
    public bool IsHexed
    {
        get
        {
            if (hexers.Count == 0) return false;
            hexers.RemoveWhere(h => h == null);
            return hexers.Count > 0;
        }
    }

    /// <summary>Starts a hex from <paramref name="hexer" /> (idempotent). The tower stops firing until every hexer lets go.</summary>
    public void AddHexer(MonoBehaviour hexer)
    {
        if (hexer == null || !hexers.Add(hexer)) return;
        UpdatePrompt();
    }

    /// <summary>Ends <paramref name="hexer" />'s hex (idempotent).</summary>
    public void RemoveHexer(MonoBehaviour hexer)
    {
        if (hexer == null || !hexers.Remove(hexer)) return;
        UpdatePrompt();
    }
    /// <summary>Furthest this tower can target. 0 for traps, which only hit what walks over them.</summary>
    public virtual float MaxRange => 0f;
    /// <summary>Supply the player has paid to upgrade this tower (free level-ups via SetLevel/InitState don't count).</summary>
    public int UpgradeSupplySpent => upgradeSupplySpent;
    /// <summary>What dismantling this tower hands back: its remaining supply plus everything spent upgrading it.</summary>
    public int DismantleRefund => Mathf.Max(0, currentSupply) + upgradeSupplySpent + upgrades.supplySpent;

    private int upgradeSupplySpent;
    private float shotSupplyCarry; // Part-paid supply from fractional shot costs, spent once it reaches a whole unit.

    private readonly StructureUpgradeState upgrades = new StructureUpgradeState();
    private TowerSpikeRing spikeRing;
    /// <summary>Supply paid on the build wheel for this tower (refunded in full by Deconstruct).</summary>
    public int BuildCostPaid { get; set; }
    /// <summary>Supply paid refilling from the upgrade wheel; Deconstruct refunds what's still unfired.</summary>
    private int refillSupplyPaid;

    /// <summary>What the upgrade wheel has bought on this tower.</summary>
    public StructureUpgradeState Upgrades => upgrades;
    /// <summary>This tower's element (plan 17), None until one is bought.</summary>
    public StructureElement Element => upgrades.element;
    /// <summary>True when [E] opens the upgrade wheel (defense scenes) rather than refill/level-up.</summary>
    public static bool UsesUpgradeWheel => DefenseSceneRules.UpgradeWheelActive;

    /// <summary>Shots-per-second multiplier from the Fire Rate tiers; divide intervals by it.</summary>
    protected float FireRateMultiplier
    {
        get
        {
            FortUpgradeConfigSO config = DefenseSceneRules.Config;
            return config != null ? config.FireRateMultiplier(upgrades.fireRateTier) : 1f;
        }
    }

    /// <summary>Applies this tower's element status to a target it just hit (no-op for None).</summary>
    protected void ApplyElementTo(Health target)
    {
        if (target == null || target.IsDead || Element == StructureElement.None) return;
        EnemyStatusManager status = EnemyStatusManager.GetOrAdd(target);
        if (status == null) return;
        if (Element == StructureElement.Ice)
        {
            FortUpgradeConfigSO config = DefenseSceneRules.Config;
            status.ApplyStatus("Ice", config != null ? config.towerIceStacks : 0.5f);
        }
        else
        {
            status.ApplyStatus(Element.StatusId());
        }
    }

    /// <summary>
    ///     What Deconstruct hands back: the build cost, every upgrade, and whatever paid-for refill ammo
    ///     is still unfired. Never more than was paid, so build-and-deconstruct can't farm supply.
    /// </summary>
    public int DeconstructRefund => BuildCostPaid + upgrades.supplySpent + upgradeSupplySpent + Mathf.Min(refillSupplyPaid, Mathf.Max(0, currentSupply));
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

    protected virtual void ValidateFeedbackReferences()
    {
        if (supplyPopupPrefab == null) Debug.LogError($"{name}: DefenseStructure.supplyPopupPrefab is not assigned.", this);
        if (repairFeedback == null) Debug.LogError($"{name}: DefenseStructure.repairFeedback is not assigned.", this);
        if (upgradeFeedback == null) Debug.LogError($"{name}: DefenseStructure.upgradeFeedback is not assigned.", this);
        if (breakFeedback == null) Debug.LogError($"{name}: DefenseStructure.breakFeedback is not assigned.", this);
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

        if (upgradeFeedback != null)
        {
            upgradeFeedback.PlayFeedbacks(transform.position);
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

        int cost = amount;
        if (amount <= 0)
        {
            // A shot: fractional costs carry over, so a 0.75 shot is free three times in four.
            shotSupplyCarry += SupplyPerAction;
            cost = Mathf.FloorToInt(shotSupplyCarry + 0.0001f);
            shotSupplyCarry = Mathf.Max(0f, shotSupplyCarry - cost);
            if (cost <= 0) return true;
        }
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

        if (breakFeedback != null)
        {
            breakFeedback.PlayFeedbacks(transform.position);
        }
    }

    /// <summary>Whether [Interact] can pay for what it would do now: a resupply needs any supply, an upgrade its full cost. The upgrade wheel handles its own costs.</summary>
    public virtual bool CanAfford
    {
        get
        {
            if (UsesUpgradeWheel) return true;
            if (currentSupply < maxSupply) return RunSession.InRunSupply > 0;
            if (currentLevel < maxLevel) return RunSession.InRunSupply >= GetUpgradeCost();
            return true;
        }
    }

    public virtual void Interact(Player player)
    {
        if (UsesUpgradeWheel)
        {
            BuildWheelUI wheel = BuildWheelUI.Instance;
            if (wheel != null) wheel.OpenUpgrades(this);
            else Debug.LogWarning("[DefenseStructure] BuildWheelUI.Instance is not found in the scene.");
            return;
        }

        if (currentSupply < maxSupply)
        {
            // Resupply
            int needed = maxSupply - currentSupply;
            int available = RunSession.InRunSupply;
            int toProvide = Mathf.Min(needed, available);

            if (toProvide > 0 && RunSession.TrySpendInRunSupply(toProvide))
            {
                currentSupply += toProvide;
                if (repairFeedback != null)
                {
                    repairFeedback.PlayFeedbacks(transform.position);
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
        if (UsesUpgradeWheel)
        {
            PromptText = currentSupply <= 0 ? "[NO SUPPLY] Upgrade / Refill" : "Upgrade";
            if (IsHexed) PromptText = $"[HEXED] {PromptText}";
            return;
        }

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

        if (IsHexed) PromptText = $"[HEXED] {PromptText}";
    }

    /// <summary>True when a point is inside this tower's blind spot (flat distance under <see cref="MinRange"/>).</summary>
    protected bool IsInsideMinRange(Vector3 worldPos)
    {
        if (minRange <= 0f) return false;
        Vector3 offset = worldPos - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude < minRange * minRange;
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

    // ---- Upgrade wheel (plan 17) -------------------------------------------------------------

    public virtual string UpgradeTitle => $"{TowerDisplayName}  ({currentSupply}/{maxSupply} ammo)";

    protected virtual string TowerDisplayName => defenseType switch
    {
        FortDefenseType.ArrowSlits => "Arrow Tower",
        FortDefenseType.NetThrower => "Net Thrower",
        _ => defenseType.ToString()
    };

    public Vector3 UpgradeAnchor => transform.position + Vector3.up * 2.2f;
    public bool IsUpgradeTargetAlive => this != null && isActiveAndEnabled;

    public virtual void BuildUpgradeOptions(List<UpgradeOption> options)
    {
        FortUpgradeConfigSO config = DefenseSceneRules.Config;
        if (config == null) return;

        // Refill: partial when the player can't cover all of it.
        int missing = maxSupply - currentSupply;
        int refillCost = Mathf.Min(missing, RunSession.InRunSupply);
        options.Add(new UpgradeOption
        {
            label = "Refill Ammo",
            description = "Top this tower's ammo back up from your supply.",
            icon = config.refillIcon,
            supplyCost = Mathf.Max(1, refillCost),
            blockedReason = missing <= 0 ? "Full" : RunSession.InRunSupply <= 0 ? "No Supply" : null,
            onPurchase = () => Refill(refillCost)
        });

        int tier = upgrades.fireRateTier;
        bool maxed = tier >= config.FireRateTiers;
        options.Add(new UpgradeOption
        {
            label = maxed ? "Fire Rate (Max)" : $"Fire Rate {ToRoman(tier + 1)}",
            description = maxed
                ? $"Firing {Mathf.RoundToInt((config.FireRateMultiplier(tier) - 1f) * 100f)}% faster."
                : $"Fire {Mathf.RoundToInt((config.FireRateMultiplier(tier + 1) - 1f) * 100f)}% faster than base.",
            icon = config.fireRateIcon,
            supplyCost = maxed ? 0 : config.fireRateCosts[tier],
            blockedReason = maxed ? "Max" : null,
            onPurchase = () =>
            {
                upgrades.fireRateTier++;
                upgrades.RecordSupply(config.fireRateCosts[tier]);
                OnUpgradeBought();
                return true;
            }
        });

        options.Add(new UpgradeOption
        {
            label = "Spikes",
            description = "A ring of stakes round the base that stabs enemies inside the blind spot.",
            icon = config.spikesIcon,
            supplyCost = config.towerSpikesCost,
            blockedReason = upgrades.hasSpikes ? "Built" : config.towerSpikeRingPrefab == null ? "Unavailable" : null,
            onPurchase = () =>
            {
                upgrades.hasSpikes = true;
                upgrades.RecordSupply(config.towerSpikesCost);
                AttachSpikeRing(config);
                OnUpgradeBought();
                return true;
            }
        });

        AddElementOptions(options, upgrades, config, TowerElementBlurb, _ =>
        {
            OnUpgradeBought();
            OnElementChanged();
        });

        int refund = DeconstructRefund;
        options.Add(new UpgradeOption
        {
            label = "Deconstruct",
            description = "Take the tower down and get back everything you paid (unfired refill ammo included).",
            icon = config.deconstructIcon,
            costOverride = $"<color=#7CFC7C>{upgrades.DescribeRefund(refund)}</color>",
            closesWheel = true,
            onPurchase = Deconstruct
        });
    }

    /// <summary>The wheel's Fire / Ice / Storm slices, shared by towers and walls: one element, locked once bought.</summary>
    public static void AddElementOptions(List<UpgradeOption> options, StructureUpgradeState state, FortUpgradeConfigSO config,
        Func<StructureElement, string> blurb, Action<StructureElement> onBought)
    {
        foreach (StructureElement element in StructureElements.All)
        {
            StructureElement e = element;
            string blocked = null;
            if (state.element == e) blocked = "Active";
            else if (state.HasElement) blocked = "Locked";

            options.Add(new UpgradeOption
            {
                label = e == StructureElement.Lightning ? "Storm" : e.ToString(),
                description = blurb(e) + (state.HasElement ? "" : "\n<size=85%>One element per structure, locked once chosen.</size>"),
                icon = config.ElementIcon(e),
                crystalElement = e,
                crystalCost = config.elementCrystalCost,
                blockedReason = blocked,
                onPurchase = () =>
                {
                    if (state.HasElement) return false;
                    state.element = e;
                    state.RecordCrystals(e, config.elementCrystalCost);
                    onBought(e);
                    return true;
                }
            });
        }
    }

    protected virtual string TowerElementBlurb(StructureElement element)
    {
        return element switch
        {
            StructureElement.Fire => "Shots set enemies on fire.",
            StructureElement.Ice => "Shots chill and slow; enough chill freezes.",
            StructureElement.Lightning => "Shots shock their target.",
            _ => ""
        };
    }

    /// <summary>Subclass hook for element-specific setup.</summary>
    protected virtual void OnElementChanged() { }

    private void OnUpgradeBought()
    {
        if (upgradeFeedback != null) upgradeFeedback.PlayFeedbacks(transform.position);
        UpdatePrompt();
    }

    private bool Refill(int cost)
    {
        if (cost <= 0) return false;
        currentSupply = Mathf.Min(maxSupply, currentSupply + cost);
        refillSupplyPaid += cost;
        if (repairFeedback != null) repairFeedback.PlayFeedbacks(transform.position);
        if (supplyPopupPrefab != null) supplyPopupPrefab.Spawn(transform.position + Vector3.up * 2.2f, $"-{cost} Supply");
        UpdatePrompt();
        OnSupplyChanged?.Invoke(currentSupply, maxSupply);
        return true;
    }

    private void AttachSpikeRing(FortUpgradeConfigSO config)
    {
        if (spikeRing != null || config.towerSpikeRingPrefab == null) return;
        spikeRing = Instantiate(config.towerSpikeRingPrefab, transform.position, transform.rotation, transform);
        spikeRing.Init(this, config);
    }

    /// <summary>Upgrade-wheel Deconstruct: refunds <see cref="DeconstructRefund" /> and every crystal, then clears the plot.</summary>
    public bool Deconstruct()
    {
        int refund = DeconstructRefund;
        string popup = upgrades.DescribeRefund(refund);
        RunSession.AddInRunSupply(refund);
        upgrades.RefundCrystals();
        if (supplyPopupPrefab != null && !string.IsNullOrEmpty(popup))
        {
            supplyPopupPrefab.Spawn(transform.position + Vector3.up * 2.5f, popup);
        }
        if (breakFeedback != null) breakFeedback.PlayFeedbacks(transform.position);

        if (OwnerPlot != null) OwnerPlot.ClearDefense();
        else Destroy(gameObject);
        return true;
    }

    /// <summary>Sector-end dismantle: crystals spent on this tower go back too.</summary>
    public void RefundCrystals()
    {
        upgrades.RefundCrystals();
    }

    private static string ToRoman(int n)
    {
        return n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => n.ToString() };
    }
}
