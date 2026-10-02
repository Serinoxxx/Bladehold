using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     A wall across a bridge head (plan 17), built on a <see cref="WallPlot" />. Local +Z faces the
///     enemy (outside), local X runs across the bridge.
///
///     - <b>Health</b>: a plain <see cref="Health" /> (immune to the player), max HP per material tier
///       (wood/stone/metal on <see cref="FortUpgradeConfigSO" />). Enemies attack it through
///       <see cref="AITargetSelector.SetWallTarget" /> exactly as they attack the gate.
///     - <b>Blocking</b>: the two side sections carve the NavMesh; the doorway between them stays on the
///       plot's own NavMesh area, whose cost <see cref="WallNavCost" /> raises while the wall stands and the
///       <see cref="WallDoor" /> is shut. NavMeshAgents don't collide with colliders, so what stops an enemy
///       is its target: every <see cref="scanInterval" /> the wall gives each enemy on its outside face (and
///       the enemies round any siege unit there) itself as the target, so they walk to the face and attack.
///     - <b>Upgrades</b> (<see cref="IUpgradeable" />, opened from the <see cref="WallCraftingStation" />):
///       material tier, Repair +N, spikes (thorns on every melee hit), one element (boiling oil, icy water
///       or lightning arcs, triggered when attacked), and Deconstruct with a full refund.
///     - <b>Damage you can read from afar</b>: segments swap to damaged variants and looping smoke/fire
///       starts at the <see cref="WallConfigSO" /> light/medium/heavy thresholds. At 0 HP it collapses to
///       rubble and stops blocking (the plot can rebuild it during prep).
/// </summary>
public class WallStructure : MonoBehaviour, IUpgradeable
{
    private static readonly List<WallStructure> all = new List<WallStructure>();
    /// <summary>Every wall in the scene, standing or rubble.</summary>
    public static IReadOnlyList<WallStructure> All => all;
    /// <summary>Raised when a wall is built (HUD rows subscribe).</summary>
    public static event Action<WallStructure> OnAnyWallBuilt;

    [SerializeField] private Health health;
    [Tooltip("Spawned segment art goes under this child.")]
    [SerializeField] private Transform visualsRoot;
    [SerializeField] private WallDoor door;

    [Header("Enemy detection")]
    [Tooltip("Depth of the box in front of the outside face; enemies inside it attack the wall.")]
    [SerializeField] private float approachDepth = 5f;
    [Tooltip("Enemies this close to a siege unit attacking the wall join in (its escort clearing the path).")]
    [SerializeField] private float siegeRecruitRadius = 10f;
    [SerializeField] private float scanInterval = 0.25f;

    [Header("Feedback")]
    [Tooltip("Each hit the wall takes (thud, splinters).")]
    [SerializeField] private MMF_Player hitFeedback;
    [Tooltip("Each time the wall drops a damage stage (crack, debris burst).")]
    [SerializeField] private MMF_Player stageDropFeedback;
    [Tooltip("The wall falls (crash, dust, shake).")]
    [SerializeField] private MMF_Player collapseFeedback;
    [Tooltip("Upgrade bought, repaired or rebuilt (hammering, sparkle).")]
    [SerializeField] private MMF_Player upgradeFeedback;
    [Tooltip("Lightning element: played at each attacker an arc hits.")]
    [SerializeField] private MMF_Player lightningArcFeedback;
    [SerializeField] private DamageNumbersPro.DamageNumber popupPrefab;

    private WallPlot plot;
    private WallConfigSO art;
    private float width;
    private int navArea = -1;
    private bool collapsed;
    private bool initialised;
    private bool anyError;
    private int damageStage;
    private float nextScan;
    private float nextElementTime;
    private int enemyMask;
    private Transform fixture;
    private readonly StructureUpgradeState upgrades = new StructureUpgradeState();
    private readonly List<Transform> segments = new List<Transform>();
    private readonly List<GameObject> spikeProps = new List<GameObject>();
    private readonly List<GameObject> smoke = new List<GameObject>();
    private readonly List<BoxCollider> sideColliders = new List<BoxCollider>();
    private readonly List<NavMeshObstacle> sideObstacles = new List<NavMeshObstacle>();
    private readonly Collider[] scanBuffer = new Collider[64];
    private readonly Collider[] recruitBuffer = new Collider[64];

    public event Action<WallStructure> OnStateChanged;

    public Health Health => health;
    public IDamageable Damageable => health;
    public WallPlot Plot => plot;
    public float Width => width;
    public StructureUpgradeState Upgrades => upgrades;
    public int BuildCostPaid { get; private set; }
    public bool IsStanding => initialised && !collapsed && health != null && !health.IsDead;
    /// <summary>Standing with the door shut: enemies can't pass, so they route round or attack.</summary>
    public bool IsBlocking => IsStanding && (door == null || !door.IsOpen);
    public float HealthFraction => health != null && health.MaxHealth > 0f ? Mathf.Clamp01(health.CurrentHealth / health.MaxHealth) : 0f;
    public string TierName => art != null && art.Tier(upgrades.materialTier) != null ? art.Tier(upgrades.materialTier).displayName : "Wall";
    private static FortUpgradeConfigSO Config => DefenseSceneRules.Config;
    private float Thickness => art != null ? art.wallThickness : 0.8f;

    private void OnValidate()
    {
        if (health == null) health = GetComponent<Health>();
        if (door == null) door = GetComponentInChildren<WallDoor>(true);
    }

    private void Awake()
    {
        all.Add(this);
        if (health == null) health = GetComponent<Health>();
        if (health != null) health.ImmuneToPlayerDamage = true;
        int mask = LayerMask.GetMask("Enemy");
        enemyMask = mask != 0 ? mask : 1 << 7;
    }

    private void Start()
    {
        if (health == null) { Debug.LogError($"[WallStructure] {name}: Health is not assigned.", this); anyError = true; }
        if (visualsRoot == null) { Debug.LogError($"[WallStructure] {name}: visualsRoot is not assigned.", this); anyError = true; }
        if (door == null) { Debug.LogError($"[WallStructure] {name}: door is not assigned.", this); anyError = true; }
        if (hitFeedback == null) Debug.LogError($"[WallStructure] {name}: hitFeedback is not assigned.", this);
        if (stageDropFeedback == null) Debug.LogError($"[WallStructure] {name}: stageDropFeedback is not assigned.", this);
        if (collapseFeedback == null) Debug.LogError($"[WallStructure] {name}: collapseFeedback is not assigned.", this);
        if (upgradeFeedback == null) Debug.LogError($"[WallStructure] {name}: upgradeFeedback is not assigned.", this);
        if (lightningArcFeedback == null) Debug.LogError($"[WallStructure] {name}: lightningArcFeedback is not assigned.", this);
        if (popupPrefab == null) Debug.LogError($"[WallStructure] {name}: popupPrefab is not assigned.", this);
        if (!initialised) { Debug.LogError($"[WallStructure] {name}: never initialised by a WallPlot.", this); anyError = true; }
    }

    private void OnDestroy()
    {
        all.Remove(this);
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnHealthChanged -= HandleHealthChanged;
            health.OnDied -= HandleDied;
        }
        if (navArea >= 0) WallNavCost.SetCost(navArea, 1f);
    }

    /// <summary>Called by the plot right after Instantiate (before Start).</summary>
    public void Init(WallPlot owner, WallConfigSO wallArt, float plotWidth, int area, int buildCost)
    {
        plot = owner;
        art = wallArt;
        width = Mathf.Max(plotWidth, (wallArt != null ? wallArt.doorWidth : 3f) + 1f);
        navArea = area;
        BuildCostPaid = buildCost;

        SetLayerRecursive(gameObject, LayerMask.NameToLayer(PlayerBarrier.FortificationLayerName));
        FortUpgradeConfigSO config = Config;
        health.SetMaxHealth(config != null ? config.WallHealth(0) : 150f);
        health.ImmuneToPlayerDamage = true;
        health.OnDamaged += HandleDamaged;
        health.OnHealthChanged += HandleHealthChanged;
        health.OnDied += HandleDied;

        BuildSideBlockers();
        RebuildSegments();
        door.Init(this, art);
        initialised = true;
        RefreshNavCost();
        OnAnyWallBuilt?.Invoke(this);
    }

    private void Update()
    {
        if (anyError || !IsBlocking || Time.time < nextScan) return;
        nextScan = Time.time + scanInterval;
        ClaimAttackers();
    }

    // ---- Targeting ---------------------------------------------------------------------------

    /// <summary>True when <paramref name="position" /> is on the enemy side of the wall.</summary>
    public bool IsOutside(Vector3 position)
    {
        return Vector3.Dot(position - transform.position, transform.forward) > 0f;
    }

    /// <summary>The spot on the outside face an attacker at <paramref name="from" /> should stand at.</summary>
    public Vector3 GetAttackPoint(Vector3 from)
    {
        Vector3 local = transform.InverseTransformPoint(from);
        float half = width * 0.5f - 0.5f;
        local.x = Mathf.Clamp(local.x, -half, half);
        local.y = 0f;
        local.z = Thickness * 0.5f + 0.6f;
        return transform.TransformPoint(local);
    }

    /// <summary>A blocking wall straight ahead of <paramref name="position" /> within <paramref name="range" /> (the battering ram's check).</summary>
    public static WallStructure FindBlockingAhead(Vector3 position, float range)
    {
        foreach (WallStructure wall in all)
        {
            if (wall == null || !wall.IsBlocking) continue;
            Vector3 local = wall.transform.InverseTransformPoint(position);
            if (local.z > 0f && local.z < range + wall.Thickness && Mathf.Abs(local.x) < wall.width * 0.5f + 1f)
            {
                return wall;
            }
        }
        return null;
    }

    /// <summary>Gives every enemy at the outside face (and every enemy round a siege unit there) this wall as its target.</summary>
    private void ClaimAttackers()
    {
        Vector3 centre = transform.position + transform.forward * (Thickness * 0.5f + approachDepth * 0.5f) + Vector3.up * 1.5f;
        Vector3 halfExtents = new Vector3(width * 0.5f + 1f, 2.5f, approachDepth * 0.5f);
        int count = Physics.OverlapBoxNonAlloc(centre, halfExtents, scanBuffer, transform.rotation, enemyMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider col = scanBuffer[i];
            scanBuffer[i] = null;
            AITargetSelector selector = col != null ? col.GetComponentInParent<AITargetSelector>() : null;
            if (selector == null || selector.WallTarget == this) continue;
            Health h = selector.GetComponent<Health>();
            if (h != null && h.IsDead) continue;
            selector.SetWallTarget(this);

            if (WallNavCost.IsSiege(selector.gameObject)) RecruitEscorts(selector.transform.position);
        }
    }

    private void RecruitEscorts(Vector3 around)
    {
        int count = Physics.OverlapSphereNonAlloc(around, siegeRecruitRadius, recruitBuffer, enemyMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider col = recruitBuffer[i];
            recruitBuffer[i] = null;
            AITargetSelector selector = col != null ? col.GetComponentInParent<AITargetSelector>() : null;
            if (selector == null || selector.WallTarget == this || !IsOutside(selector.transform.position)) continue;
            selector.SetWallTarget(this);
        }
    }

    // ---- Damage -------------------------------------------------------------------------------

    private void HandleDamaged(Damage damage)
    {
        if (anyError || collapsed || damage == null) return;
        if (hitFeedback != null) hitFeedback.PlayFeedbacks(GetAttackPoint(damage.sourcePosition) + Vector3.up * 1.2f);

        FortUpgradeConfigSO config = Config;
        if (config == null) return;

        // Spikes: every melee hit costs the attacker (projectiles and status ticks don't).
        if (upgrades.hasSpikes && !damage.isProjectile && !damage.isStatusEffect && damage.source is Health attacker &&
            attacker != health && !attacker.IsDead)
        {
            attacker.ReceiveDamage(new Damage
            {
                value = config.wallSpikeDamage,
                type = DamageType.sharp,
                isDefenseDamage = true,
                isPlayerDamage = true,
                sourcePosition = transform.position,
                source = Player.Instance != null ? Player.Instance.Damageable : null
            });
        }

        if (upgrades.HasElement && Time.time >= nextElementTime)
        {
            nextElementTime = Time.time + config.wallElementCooldown;
            TriggerElement(config, damage.sourcePosition);
        }
    }

    private void TriggerElement(FortUpgradeConfigSO config, Vector3 attackerPos)
    {
        Vector3 point = GetAttackPoint(attackerPos) + transform.forward * 1.2f;
        switch (upgrades.element)
        {
            case StructureElement.Fire:
                if (config.boilingOilPrefab != null)
                {
                    BurningOilZone oil = Instantiate(config.boilingOilPrefab, point, Quaternion.identity);
                    oil.Init(config.wallElementReach, 4.5f, 8f, 0.5f);
                }
                break;
            case StructureElement.Ice:
                if (config.icyWaterPrefab != null)
                {
                    WallIcyWaterZone ice = Instantiate(config.icyWaterPrefab, point, Quaternion.identity);
                    ice.Init(config.wallElementReach);
                }
                break;
            case StructureElement.Lightning:
                DischargeArcs(config, point);
                break;
        }
    }

    private void DischargeArcs(FortUpgradeConfigSO config, Vector3 point)
    {
        int count = Physics.OverlapSphereNonAlloc(point, config.wallElementReach, recruitBuffer, enemyMask, QueryTriggerInteraction.Collide);
        var struck = new HashSet<Health>();
        float dmg = config.wallLightningDamage;
        if (Player.Instance != null && Player.Instance.Stats != null) dmg *= Player.Instance.Stats.GetValue(StatType.AllDamageMultiplier);

        for (int i = 0; i < count && struck.Count < config.wallLightningTargets; i++)
        {
            Collider col = recruitBuffer[i];
            recruitBuffer[i] = null;
            Health h = col != null ? col.GetComponentInParent<Health>() : null;
            if (h == null || h.IsDead || h == health || !struck.Add(h)) continue;
            if (Player.Instance != null && h.transform.root == Player.Instance.transform.root) continue;
            h.ReceiveDamage(new Damage
            {
                value = dmg,
                type = DamageType.elemental,
                elementId = "Lightning",
                isDefenseDamage = true,
                isPlayerDamage = true,
                sourcePosition = transform.position,
                source = Player.Instance != null ? Player.Instance.Damageable : null
            });
            EnemyStatusManager.GetOrAdd(h)?.ApplyStatus("Lightning");
            if (lightningArcFeedback != null) lightningArcFeedback.PlayFeedbacks(h.transform.position + Vector3.up);
        }
        for (int i = 0; i < count; i++) recruitBuffer[i] = null;
    }

    private void HandleHealthChanged()
    {
        if (!initialised || collapsed || art == null) return;
        int stage = art.DamageStage(HealthFraction);
        if (stage != damageStage)
        {
            bool worse = stage > damageStage;
            damageStage = stage;
            RebuildSegments();
            if (worse && stageDropFeedback != null) stageDropFeedback.PlayFeedbacks(transform.position + Vector3.up * 1.5f);
        }
        OnStateChanged?.Invoke(this);
    }

    private void HandleDied()
    {
        if (collapsed) return;
        collapsed = true;
        if (collapseFeedback != null) collapseFeedback.PlayFeedbacks(transform.position + Vector3.up);

        WallConfigSO.TierArt tierArt = art != null ? art.Tier(upgrades.materialTier) : null;
        foreach (Transform seg in segments)
        {
            if (seg == null) continue;
            if (tierArt != null && tierArt.rubble != null)
            {
                GameObject rubble = Instantiate(tierArt.rubble, seg.position, seg.rotation, transform);
                StripColliders(rubble);
            }
            Destroy(seg.gameObject);
        }
        segments.Clear();
        foreach (GameObject spike in spikeProps) if (spike != null) Destroy(spike);
        spikeProps.Clear();
        if (fixture != null) Destroy(fixture.gameObject);
        foreach (BoxCollider c in sideColliders) if (c != null) c.enabled = false;
        foreach (NavMeshObstacle o in sideObstacles) if (o != null) o.enabled = false;
        door.Collapse();

        // Heavy smoke lingers over the rubble, then dies down.
        foreach (GameObject fx in smoke)
        {
            if (fx == null) continue;
            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        RefreshNavCost();
        OnStateChanged?.Invoke(this);
        if (plot != null) plot.OnWallCollapsed(this);
    }

    // ---- Door ---------------------------------------------------------------------------------

    /// <summary>Called by the door when it opens or closes.</summary>
    public void OnDoorChanged()
    {
        RefreshNavCost();
        OnStateChanged?.Invoke(this);
    }

    private void RefreshNavCost()
    {
        if (navArea < 0) return;
        FortUpgradeConfigSO config = Config;
        WallNavCost.SetCost(navArea, IsBlocking && config != null ? config.wallAreaCost : 1f);
    }

    // ---- Build / visuals ----------------------------------------------------------------------

    private void BuildSideBlockers()
    {
        float doorWidth = art != null ? art.doorWidth : 3f;
        float height = art != null ? art.wallHeight : 3.5f;
        float sideLength = (width - doorWidth) * 0.5f;
        foreach (int side in new[] { -1, 1 })
        {
            var go = new GameObject(side < 0 ? "Side_L" : "Side_R");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(side * (doorWidth * 0.5f + sideLength * 0.5f), 0f, 0f);

            BoxCollider box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, height * 0.5f, 0f);
            box.size = new Vector3(sideLength, height, Thickness);
            sideColliders.Add(box);

            NavMeshObstacle obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = box.center;
            obstacle.size = box.size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
            sideObstacles.Add(obstacle);
        }
    }

    /// <summary>(Re)spawns the segment art for the current tier and damage stage across both sides of the doorway.</summary>
    private void RebuildSegments()
    {
        foreach (Transform seg in segments) if (seg != null) Destroy(seg.gameObject);
        segments.Clear();
        foreach (GameObject spike in spikeProps) if (spike != null) Destroy(spike);
        spikeProps.Clear();
        if (art == null || visualsRoot == null) return;

        WallConfigSO.TierArt tierArt = art.Tier(upgrades.materialTier);
        GameObject prefab = art.SegmentFor(upgrades.materialTier, damageStage);
        if (tierArt == null || prefab == null) return;

        float sideLength = (width - art.doorWidth) * 0.5f;
        int perSide = Mathf.Max(1, Mathf.RoundToInt(sideLength / tierArt.segmentLength));
        float scaleX = sideLength / (perSide * tierArt.segmentLength);
        int fort = gameObject.layer;
        foreach (int side in new[] { -1, 1 })
        {
            for (int k = 0; k < perSide; k++)
            {
                float x = side * (art.doorWidth * 0.5f + (k + 0.5f) * tierArt.segmentLength * scaleX);
                GameObject seg = Instantiate(prefab, visualsRoot);
                seg.transform.localPosition = new Vector3(x, 0f, 0f);
                seg.transform.localRotation = Quaternion.identity;
                seg.transform.localScale = new Vector3(scaleX, 1f, 1f);
                StripColliders(seg);
                SetLayerRecursive(seg, fort);
                segments.Add(seg.transform);

                if (upgrades.hasSpikes && art.spikesProp != null)
                {
                    GameObject spike = Instantiate(art.spikesProp, visualsRoot);
                    spike.transform.localPosition = new Vector3(x, 0f, Thickness * 0.5f + 0.4f);
                    spike.transform.localRotation = Quaternion.identity;
                    spike.transform.localScale = new Vector3(scaleX, 1f, 1f);
                    StripColliders(spike);
                    SetLayerRecursive(spike, fort);
                    spikeProps.Add(spike);
                }
            }
        }

        RefreshSmoke();
    }

    /// <summary>Looping smoke/fire for the current damage stage, one emitter per side so it reads from a distance.</summary>
    private void RefreshSmoke()
    {
        foreach (GameObject fx in smoke) if (fx != null) Destroy(fx);
        smoke.Clear();
        GameObject prefab = art != null ? art.DamageVfx(damageStage) : null;
        if (prefab == null) return;
        float sideCentre = art.doorWidth * 0.5f + (width - art.doorWidth) * 0.25f;
        foreach (int side in new[] { -1, 1 })
        {
            GameObject fx = Instantiate(prefab, transform);
            fx.transform.localPosition = new Vector3(side * sideCentre, art.wallHeight * 0.6f, 0f);
            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>()) ps.Play();
            smoke.Add(fx);
        }
    }

    private void RefreshFixture()
    {
        if (fixture != null) Destroy(fixture.gameObject);
        fixture = null;
        GameObject prefab = art != null ? art.ElementFixture(upgrades.element) : null;
        if (prefab == null) return;
        GameObject go = Instantiate(prefab, transform);
        go.transform.localPosition = new Vector3(0f, art.wallHeight, 0f);
        go.transform.localRotation = Quaternion.identity;
        StripColliders(go);
        SetLayerRecursive(go, gameObject.layer);
        fixture = go.transform;
    }

    private static void StripColliders(GameObject go)
    {
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        if (layer < 0) return;
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }

    // ---- Upgrade wheel ------------------------------------------------------------------------

    public string UpgradeTitle => $"{TierName} Wall  ({Mathf.CeilToInt(health.CurrentHealth)}/{Mathf.CeilToInt(health.MaxHealth)} HP)";
    public Vector3 UpgradeAnchor => transform.position + Vector3.up * ((art != null ? art.wallHeight : 3.5f) + 0.5f);
    public bool IsUpgradeTargetAlive => this != null && IsStanding;

    public void BuildUpgradeOptions(List<UpgradeOption> options)
    {
        FortUpgradeConfigSO config = Config;
        if (config == null) return;

        int tier = upgrades.materialTier;
        bool maxed = tier >= config.WallMaxTier;
        string nextName = !maxed && art != null && art.Tier(tier + 1) != null ? art.Tier(tier + 1).displayName : "";
        int materialCost = !maxed && config.wallMaterialCosts != null && tier < config.wallMaterialCosts.Length ? config.wallMaterialCosts[tier] : 0;
        options.Add(new UpgradeOption
        {
            label = maxed ? $"{TierName} (Max)" : $"{nextName} Wall",
            description = maxed
                ? $"Strongest wall: {Mathf.RoundToInt(config.WallHealth(tier))} HP."
                : $"Rebuild in {nextName.ToLowerInvariant()}: {Mathf.RoundToInt(config.WallHealth(tier))} to {Mathf.RoundToInt(config.WallHealth(tier + 1))} max HP (damage carries over).",
            icon = config.materialIcon,
            supplyCost = materialCost,
            blockedReason = maxed ? "Max" : null,
            onPurchase = () =>
            {
                upgrades.materialTier++;
                upgrades.RecordSupply(materialCost);
                health.SetMaxHealth(config.WallHealth(upgrades.materialTier), true);
                RebuildSegments();
                door.SetArt(art);
                OnUpgraded();
                return true;
            }
        });

        float missing = health.MaxHealth - health.CurrentHealth;
        options.Add(new UpgradeOption
        {
            label = $"Repair +{config.wallRepairAmount}",
            description = $"Patch up the wall: +{config.wallRepairAmount} HP.",
            icon = config.repairIcon,
            supplyCost = config.wallRepairCost,
            blockedReason = missing <= 0.01f ? "Full HP" : null,
            onPurchase = () =>
            {
                health.Heal(config.wallRepairAmount);
                upgrades.RecordSupply(config.wallRepairCost);
                OnUpgraded();
                return true;
            }
        });

        options.Add(new UpgradeOption
        {
            label = "Spikes",
            description = $"Stakes along the outside: every melee hit on the wall costs the attacker {config.wallSpikeDamage:0.#} HP.",
            icon = config.spikesIcon,
            supplyCost = config.wallSpikesCost,
            blockedReason = upgrades.hasSpikes ? "Built" : null,
            onPurchase = () =>
            {
                upgrades.hasSpikes = true;
                upgrades.RecordSupply(config.wallSpikesCost);
                RebuildSegments();
                OnUpgraded();
                return true;
            }
        });

        DefenseStructure.AddElementOptions(options, upgrades, config, WallElementBlurb, _ =>
        {
            RefreshFixture();
            OnUpgraded();
        });

        int refund = DeconstructRefund;
        options.Add(new UpgradeOption
        {
            label = "Deconstruct",
            description = "Take the wall down and get back everything you paid for it.",
            icon = config.deconstructIcon,
            costOverride = $"<color=#7CFC7C>{upgrades.DescribeRefund(refund)}</color>",
            closesWheel = true,
            onPurchase = () =>
            {
                if (plot != null) plot.DeconstructWall();
                return true;
            }
        });
    }

    private static string WallElementBlurb(StructureElement element)
    {
        return element switch
        {
            StructureElement.Fire => "Boiling oil pours on attackers: a burning pool that slows and scorches.",
            StructureElement.Ice => "Icy water spills on attackers: slows them, and enough of it freezes them solid.",
            StructureElement.Lightning => "The wall discharges into attackers, arcing through several at once.",
            _ => ""
        };
    }

    private void OnUpgraded()
    {
        if (upgradeFeedback != null) upgradeFeedback.PlayFeedbacks(UpgradeAnchor);
        OnStateChanged?.Invoke(this);
    }

    /// <summary>Everything paid for this wall: the build, each upgrade and repair. Crystals are refunded separately.</summary>
    public int DeconstructRefund => BuildCostPaid + upgrades.supplySpent;

    /// <summary>Refund popup at the wall (used by the plot on deconstruct).</summary>
    public void ShowPopup(string text)
    {
        if (popupPrefab != null && !string.IsNullOrEmpty(text)) popupPrefab.Spawn(UpgradeAnchor, text);
    }
}
