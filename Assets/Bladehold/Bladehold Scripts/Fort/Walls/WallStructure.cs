using System;
using System.Collections;
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
///     - <b>One hand-placed model</b>: the wall lives (inactive at runtime) as a child of its
///       <see cref="WallPlot" />, holding a single building under <see cref="model" /> (stairs, walkway,
///       battlements, gate) plus rubble, spikes and element fixtures, all authored in place. Building clones
///       that template, so per-scene fitting carries over. Material upgrades swap every model slot that uses
///       a <see cref="WallConfigSO.tiers" /> material (Castle_Wall_01/02/03) to the new tier's.
///     - <b>Colliders</b>: the model keeps its own, on the layers authored in the prefab: stairs and floors on
///       Environment (walkable), walls and battlements on Fortification (player and tower shots pass). Decor
///       (door leaf, spikes, fixtures, rubble) is stripped. What blocks enemies is separate: the side blockers
///       and door blocker built from the plot width and <see cref="WallConfigSO" /> at the wall's origin.
///     - <b>Damage you can read from afar</b>: looping smoke/fire starts at the <see cref="WallConfigSO" />
///       light/medium/heavy thresholds (the model itself doesn't change). At 0 HP the model sinks into the
///       ground, leaving its rubble, and stops blocking (the plot can rebuild it during prep).
/// </summary>
public class WallStructure : MonoBehaviour, IUpgradeable
{
    private static readonly List<WallStructure> all = new List<WallStructure>();
    /// <summary>Every wall in the scene, standing or rubble.</summary>
    public static IReadOnlyList<WallStructure> All => all;
    /// <summary>Raised when a wall is built (HUD rows subscribe).</summary>
    public static event Action<WallStructure> OnAnyWallBuilt;

    [SerializeField] private Health health;
    [SerializeField] private WallDoor door;

    [Header("Art (hand-placed)")]
    [Tooltip("The wall building. Its colliders and layers are used as authored; tier materials are swapped on upgrade; it sinks away when the wall falls.")]
    [SerializeField] private Transform model;
    [Tooltip("The gate leaf inside the model (the portcullis). Moved onto the door's sliding mount at build.")]
    [SerializeField] private GameObject doorLeaf;
    [Tooltip("Shown when the wall falls, as the model sinks away.")]
    [SerializeField] private GameObject rubble;
    [Tooltip("Spike row along the outside face, shown when Spikes is bought.")]
    [SerializeField] private GameObject spikes;
    [Tooltip("Element fixtures on top of the wall: boiling-oil cauldron, icy-water barrels, lightning rod.")]
    [SerializeField] private GameObject fireFixture;
    [SerializeField] private GameObject iceFixture;
    [SerializeField] private GameObject lightningFixture;

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
    private readonly StructureUpgradeState upgrades = new StructureUpgradeState();
    private readonly List<GameObject> smoke = new List<GameObject>();
    // Every model material slot that uses a tier material, swapped on material upgrades.
    private readonly List<(Renderer renderer, int slot)> tierSlots = new List<(Renderer, int)>();
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

    /// <summary>
    ///     Local Z of the outside face enemies stop at: the shut door's blocker when the art puts the gate
    ///     further out than the wall line (a deep gatehouse), else half the wall's thickness. The door's
    ///     NavMesh obstacle doesn't carve, so an attack point behind the gate would have them walk through it.
    /// </summary>
    private float FaceDepth
    {
        get
        {
            float face = Thickness * 0.5f;
            BoxCollider blocker = door != null ? door.Blocker : null;
            if (blocker == null) return face;
            Vector3 halfDepth = new Vector3(0f, 0f, blocker.size.z * 0.5f);
            float front = transform.InverseTransformPoint(blocker.transform.TransformPoint(blocker.center + halfDepth)).z;
            float back = transform.InverseTransformPoint(blocker.transform.TransformPoint(blocker.center - halfDepth)).z;
            return Mathf.Max(face, front, back);
        }
    }

    /// <summary>
    ///     The stretch of the face attackers spread along (wall-local X): the shut door's blocker, so they
    ///     crowd the gate rather than the gatehouse's side walls, else the whole wall.
    /// </summary>
    private void GetDoorSpan(out float centre, out float half)
    {
        centre = 0f;
        half = width * 0.5f - 0.5f;
        BoxCollider blocker = door != null ? door.Blocker : null;
        if (blocker == null) return;
        Vector3 halfWidth = new Vector3(blocker.size.x * 0.5f, 0f, 0f);
        float a = transform.InverseTransformPoint(blocker.transform.TransformPoint(blocker.center + halfWidth)).x;
        float b = transform.InverseTransformPoint(blocker.transform.TransformPoint(blocker.center - halfWidth)).x;
        centre = (a + b) * 0.5f;
        half = Mathf.Max(0.25f, Mathf.Abs(a - b) * 0.5f - 0.4f);
    }

    private void OnValidate()
    {
        if (health == null) health = GetComponent<Health>();
        if (door == null) door = GetComponentInChildren<WallDoor>(true);
    }

    // Registered while enabled, so the inactive template under the plot never counts as a wall.
    private void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
    }

    private void OnDisable()
    {
        all.Remove(this);
    }

    private void Awake()
    {
        if (health == null) health = GetComponent<Health>();
        if (health != null) health.ImmuneToPlayerDamage = true;
        int mask = LayerMask.GetMask("Enemy");
        enemyMask = mask != 0 ? mask : 1 << 7;
    }

    private void Start()
    {
        if (health == null) { Debug.LogError($"[WallStructure] {name}: Health is not assigned.", this); anyError = true; }
        if (model == null) { Debug.LogError($"[WallStructure] {name}: model is not assigned.", this); anyError = true; }
        if (doorLeaf == null) Debug.LogError($"[WallStructure] {name}: doorLeaf is not assigned.", this);
        if (rubble == null) Debug.LogError($"[WallStructure] {name}: rubble is not assigned.", this);
        if (spikes == null) Debug.LogError($"[WallStructure] {name}: spikes is not assigned.", this);
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
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnHealthChanged -= HandleHealthChanged;
            health.OnDied -= HandleDied;
        }
        if (navArea >= 0) WallNavCost.SetCost(navArea, 1f);
    }

    /// <summary>Called by the plot right after cloning the template and activating the clone (before Start).</summary>
    public void Init(WallPlot owner, WallConfigSO wallArt, float plotWidth, int area, int buildCost)
    {
        plot = owner;
        art = wallArt;
        width = Mathf.Max(plotWidth, (wallArt != null ? wallArt.doorWidth : 3f) + 1f);
        navArea = area;
        BuildCostPaid = buildCost;

        PrepareAuthoredArt();
        FortUpgradeConfigSO config = Config;
        health.SetMaxHealth(config != null ? config.WallHealth(0) : 150f);
        health.ImmuneToPlayerDamage = true;
        health.OnDamaged += HandleDamaged;
        health.OnHealthChanged += HandleHealthChanged;
        health.OnDied += HandleDied;

        BuildSideBlockers();
        door.Init(this, art, doorLeaf);
        RefreshArt();
        RefreshFixture();
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

    /// <summary>
    ///     True when <paramref name="position" /> is out past the wall's face (<see cref="FaceDepth" />), on
    ///     the enemy side. A player inside a deep gatehouse, behind its gate, is not outside.
    /// </summary>
    public bool IsOutside(Vector3 position)
    {
        return transform.InverseTransformPoint(position).z > FaceDepth;
    }

    /// <summary>The spot on the outside face an attacker at <paramref name="from" /> should stand at.</summary>
    public Vector3 GetAttackPoint(Vector3 from)
    {
        Vector3 local = transform.InverseTransformPoint(from);
        GetDoorSpan(out float centre, out float half);
        local.x = Mathf.Clamp(local.x, centre - half, centre + half);
        local.y = 0f;
        local.z = FaceDepth + 0.6f;
        return transform.TransformPoint(local);
    }

    /// <summary>A blocking wall straight ahead of <paramref name="position" /> within <paramref name="range" /> (the battering ram's check).</summary>
    public static WallStructure FindBlockingAhead(Vector3 position, float range)
    {
        foreach (WallStructure wall in all)
        {
            if (wall == null || !wall.IsBlocking) continue;
            Vector3 local = wall.transform.InverseTransformPoint(position);
            if (local.z > 0f && local.z < range + wall.FaceDepth + wall.Thickness * 0.5f && Mathf.Abs(local.x) < wall.width * 0.5f + 1f)
            {
                return wall;
            }
        }
        return null;
    }

    /// <summary>Gives every enemy at the outside face (and every enemy round a siege unit there) this wall as its target.</summary>
    private void ClaimAttackers()
    {
        // From the wall line out past the face: also catches anyone the crowd shoves into a deep gatehouse.
        float near = Thickness * 0.5f;
        float far = FaceDepth + approachDepth;
        Vector3 centre = transform.position + transform.forward * ((near + far) * 0.5f) + Vector3.up * 1.5f;
        Vector3 halfExtents = new Vector3(width * 0.5f + 1f, 2.5f, (far - near) * 0.5f);
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
            RefreshArt();
            if (worse && stageDropFeedback != null) stageDropFeedback.PlayFeedbacks(transform.position + Vector3.up * 1.5f);
        }
        OnStateChanged?.Invoke(this);
    }

    private void HandleDied()
    {
        if (collapsed) return;
        collapsed = true;
        if (collapseFeedback != null) collapseFeedback.PlayFeedbacks(transform.position + Vector3.up);

        RefreshArt();
        RefreshFixture();
        foreach (BoxCollider c in sideColliders) if (c != null) c.enabled = false;
        foreach (NavMeshObstacle o in sideObstacles) if (o != null) o.enabled = false;
        door.Collapse();
        if (rubble != null) rubble.SetActive(true);
        if (model != null)
        {
            // Nobody stands on a sinking building: its stairs and walkway go at once.
            foreach (Collider c in model.GetComponentsInChildren<Collider>()) c.enabled = false;
            StartCoroutine(SinkModel());
        }

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

    /// <summary>
    ///     Readies the cloned template's hand-placed art. The model's colliders and layers are used as authored
    ///     (walkable stairs/floors on Environment, the rest on Fortification). Decor colliders (spikes, fixtures,
    ///     rubble) are stripped and everything outside the model goes on Fortification, which player and tower
    ///     shots ignore. Also finds the model's tier-material slots for upgrades.
    /// </summary>
    private void PrepareAuthoredArt()
    {
        int fort = LayerMask.NameToLayer(PlayerBarrier.FortificationLayerName);
        if (fort >= 0) gameObject.layer = fort;
        // The door's hand-placed blocker is the one collider outside the model that must survive.
        Collider doorBlocker = door != null ? door.Blocker : null;
        foreach (Transform child in transform)
        {
            if (child == model) continue;
            foreach (Collider c in child.GetComponentsInChildren<Collider>(true))
                if (c != doorBlocker) Destroy(c);
            SetLayerRecursive(child.gameObject, fort);
        }
        if (doorLeaf != null) SetLayerRecursive(doorLeaf, fort);
        if (rubble != null) rubble.SetActive(false);

        tierSlots.Clear();
        if (model == null || art == null) return;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (art.IsTierMaterial(mats[i])) tierSlots.Add((r, i));
            }
        }
    }

    /// <summary>Shows the current tier's material on the model, the spikes if bought, and the damage smoke.</summary>
    private void RefreshArt()
    {
        if (spikes != null) spikes.SetActive(upgrades.hasSpikes && !collapsed);
        if (collapsed) return;
        Material tierMaterial = art != null && art.Tier(upgrades.materialTier) != null ? art.Tier(upgrades.materialTier).material : null;
        if (tierMaterial != null)
        {
            foreach ((Renderer r, int slot) in tierSlots)
            {
                if (r == null) continue;
                Material[] mats = r.sharedMaterials;
                if (mats[slot] == tierMaterial) continue;
                mats[slot] = tierMaterial;
                r.sharedMaterials = mats;
            }
        }
        RefreshSmoke();
    }

    /// <summary>The fallen wall's model sinks by its own height into the ground, then hides; the rubble stays.</summary>
    private IEnumerator SinkModel()
    {
        float top = 0f;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            if (!(r is ParticleSystemRenderer)) top = Mathf.Max(top, r.bounds.max.y - model.position.y);
        }
        Vector3 from = model.localPosition;
        Vector3 to = from - transform.InverseTransformVector(Vector3.up) * (top + 0.3f);
        float duration = art != null ? art.collapseSinkSeconds : 1.4f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            // Ease in: it gives way slowly, then drops.
            float k = t / duration;
            model.localPosition = Vector3.Lerp(from, to, k * k);
            yield return null;
        }
        model.gameObject.SetActive(false);
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
        StructureElement element = collapsed ? StructureElement.None : upgrades.element;
        if (fireFixture != null) fireFixture.SetActive(element == StructureElement.Fire);
        if (iceFixture != null) iceFixture.SetActive(element == StructureElement.Ice);
        if (lightningFixture != null) lightningFixture.SetActive(element == StructureElement.Lightning);
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
                RefreshArt();
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
                RefreshArt();
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
