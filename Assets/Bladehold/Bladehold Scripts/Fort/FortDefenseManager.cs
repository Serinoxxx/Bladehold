using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Scene manager for all fort defenses and sockets.
///     Listens to in-run skill tree card drafts and deploys / upgrades fort structures across scene sockets.
/// </summary>
public class FortDefenseManager : MonoBehaviour
{
    public static FortDefenseManager Instance { get; private set; }

    [Header("Defense Prefabs")]
    [SerializeField] private GameObject arrowSlitsPrefab;
    [SerializeField] private GameObject burningOilPrefab;
    [SerializeField] private GameObject spikesPrefab;

    [Header("Current Upgrade Levels")]
    [SerializeField] private int arrowSlitsLevel = 0;
    [SerializeField] private int burningOilLevel = 0;
    [SerializeField] private int spikesLevel = 0;

    private readonly List<FortDefenseSocket> allSockets = new List<FortDefenseSocket>();
    private readonly List<FortDefense> activeDefenses = new List<FortDefense>();
    private float nextTeslaDischargeTime = 0f;
    private float nextPermafrostAuraTime = 0f;
    private readonly Collider[] permafrostHitsBuffer = new Collider[64];
    private readonly HashSet<Health> permafrostProcessedEnemies = new HashSet<Health>();

    public int ArrowSlitsLevel => arrowSlitsLevel;
    public int BurningOilLevel => burningOilLevel;
    public int SpikesLevel => spikesLevel;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        RefreshSockets();
    }

    private void Start()
    {
        RefreshSockets();

        if (Player.Instance != null && Player.Instance.Stats != null)
        {
            Player.Instance.Stats.SetBase(StatType.FortArrowSlitsCount, 0f);
            Player.Instance.Stats.SetBase(StatType.FortSniperNestUnlocked, 0f);
            Player.Instance.Stats.SetBase(StatType.FortFocusFireBonus, 0f);
            Player.Instance.Stats.SetBase(StatType.FortFieryPitchUnlocked, 0f);
            Player.Instance.Stats.SetBase(StatType.FortScaldingHeatBonus, 0f);
            Player.Instance.Stats.SetBase(StatType.FortExpandedVatsPercent, 0f);
            Player.Instance.Stats.SetBase(StatType.FortConcussiveSpikesDuration, 0f);
            Player.Instance.Stats.SetBase(StatType.FortShoveInterval, 0f);
            Player.Instance.Stats.SetBase(StatType.FortVulnerabilityFieldBonus, 0f);
            Player.Instance.Stats.SetBase(StatType.FortElectrifiedOil, 0f);
            Player.Instance.Stats.SetBase(StatType.FortPermafrostSpikes, 0f);
        }

        if (SkillTreeService.Instance != null)
        {
            SkillTreeService.Instance.OnNodePurchased += HandleSkillNodePurchasedEvent;
            SkillTreeService.Instance.OnTreeChanged += SyncFromSkillTree;
            SyncFromSkillTree();
        }
    }

    private void Update()
    {
        if (Player.Instance != null && Player.Instance.Stats != null)
        {
            float teslaDamage = Player.Instance.Stats.GetValue(StatType.LightningTeslaSpireDamage);
            if (teslaDamage > 0f && Time.time >= nextTeslaDischargeTime)
            {
                nextTeslaDischargeTime = Time.time + 5.0f;

                Vector3 originPos = transform.position;
                for (int i = 0; i < activeDefenses.Count; i++)
                {
                    if (activeDefenses[i] != null)
                    {
                        originPos = activeDefenses[i].transform.position;
                        break;
                    }
                }

                Collider[] hits = Physics.OverlapSphere(originPos, 35f);
                Health target = null;
                float closestDistSqr = float.MaxValue;
                HashSet<Health> processed = new HashSet<Health>();

                for (int i = 0; i < hits.Length; i++)
                {
                    Collider hit = hits[i];
                    if (hit == null) continue;
                    Health h = hit.GetComponentInParent<Health>();
                    if (h == null || processed.Contains(h)) continue;
                    processed.Add(h);

                    if (h.IsDead) continue;
                    if (Player.Instance != null && (h.gameObject == Player.Instance.gameObject || h.CompareTag("Player"))) continue;

                    float distSqr = (h.transform.position - originPos).sqrMagnitude;
                    if (distSqr < closestDistSqr)
                    {
                        closestDistSqr = distSqr;
                        target = h;
                    }
                }

                if (target != null)
                {
                    float finalDmg = teslaDamage;
                    float allMult = Player.Instance.Stats.GetValue(StatType.AllDamageMultiplier);
                    if (allMult > 0f)
                    {
                        finalDmg *= allMult;
                    }

                    float pyreBonus = Player.Instance.Stats.GetValue(StatType.FireFortressPyreBonus);
                    if (pyreBonus > 0f && target.GetComponent<EnemyStatusManager>()?.HasStatus("Fire") == true)
                    {
                        finalDmg *= (1f + pyreBonus);
                    }

                    Damage dmg = new Damage
                    {
                        value = finalDmg,
                        type = DamageType.elemental,
                        elementId = "Lightning",
                        isPlayerDamage = true,
                        sourcePosition = originPos,
                        source = Player.Instance != null ? Player.Instance.Damageable : null
                    };

                    target.ReceiveDamage(dmg);
                    EnemyStatusManager.GetOrAdd(target)?.ApplyStatus("Lightning");

                    Vector3 targetPos = target.transform.position;
                    if (ElementalEffectsManager.Instance != null)
                    {
                        if (ElementalEffectsManager.Instance.superconductorVfx != null)
                        {
                            Instantiate(ElementalEffectsManager.Instance.superconductorVfx, targetPos, Quaternion.identity);
                        }
                        AudioClip zapClip = ElementalEffectsManager.Instance.superconductorSfx != null
                            ? ElementalEffectsManager.Instance.superconductorSfx
                            : ElementalEffectsManager.Instance.statusAppliedSfx;
                        if (zapClip != null)
                        {
                            AudioSource.PlayClipAtPoint(zapClip, targetPos);
                        }
                    }
                }
            }

            // Ice Permafrost Wall Aura (Task 26)
            if (Player.Instance.Stats.GetValue(StatType.IcePermafrostUnlocked) > 0f && Time.time >= nextPermafrostAuraTime)
            {
                nextPermafrostAuraTime = Time.time + 1.0f;
                permafrostProcessedEnemies.Clear();

                bool emittedFromDefense = false;
                for (int i = 0; i < activeDefenses.Count; i++)
                {
                    if (activeDefenses[i] != null)
                    {
                        emittedFromDefense = true;
                        ApplyPermafrostAuraAt(activeDefenses[i].transform.position);
                    }
                }

                if (!emittedFromDefense)
                {
                    bool emittedFromSocket = false;
                    for (int i = 0; i < allSockets.Count; i++)
                    {
                        if (allSockets[i] != null)
                        {
                            emittedFromSocket = true;
                            ApplyPermafrostAuraAt(allSockets[i].transform.position);
                        }
                    }

                    if (!emittedFromSocket)
                    {
                        ApplyPermafrostAuraAt(transform.position);
                    }
                }
            }
        }
    }

    private void ApplyPermafrostAuraAt(Vector3 center)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(center, 8f, permafrostHitsBuffer);
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = permafrostHitsBuffer[i];
            permafrostHitsBuffer[i] = null;
            if (hit == null) continue;

            Health targetHealth = hit.GetComponentInParent<Health>();
            if (targetHealth == null || permafrostProcessedEnemies.Contains(targetHealth)) continue;
            permafrostProcessedEnemies.Add(targetHealth);

            if (targetHealth.IsDead) continue;
            if (Player.Instance != null && (targetHealth.gameObject == Player.Instance.gameObject || targetHealth.CompareTag("Player"))) continue;

            SlowStatus.GetOrAdd(targetHealth)?.ApplySlow(0.35f, 2.0f);
            EnemyStatusManager.GetOrAdd(targetHealth)?.ApplyStatus("Ice");
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (SkillTreeService.Instance != null)
        {
            SkillTreeService.Instance.OnNodePurchased -= HandleSkillNodePurchasedEvent;
            SkillTreeService.Instance.OnTreeChanged -= SyncFromSkillTree;
        }
    }

    public void RegisterSocket(FortDefenseSocket socket)
    {
        if (socket != null && !allSockets.Contains(socket))
        {
            allSockets.Add(socket);
        }
    }

    public void UnregisterSocket(FortDefenseSocket socket)
    {
        if (socket != null)
        {
            allSockets.Remove(socket);
        }
    }

    /// <summary>
    ///     Finds and indexes all sockets present in the active scene.
    /// </summary>
    public void RefreshSockets()
    {
        FortDefenseSocket[] found = FindObjectsByType<FortDefenseSocket>(FindObjectsSortMode.None);
        foreach (FortDefenseSocket s in found)
        {
            if (s != null && !allSockets.Contains(s))
            {
                allSockets.Add(s);
            }
        }
    }

    private void HandleSkillNodePurchasedEvent(SkillNode node, int price)
    {
        if (node != null)
        {
            HandleSkillNodePurchased(node.id);
        }
    }

    /// <summary>
    ///     Synchronizes fort levels with SkillTreeService state (e.g. from save data on load).
    /// </summary>
    public void SyncFromSkillTree()
    {
        if (SkillTreeService.Instance == null || SkillTreeService.Instance.Tree == null) return;

        SkillTreeSO tree = SkillTreeService.Instance.Tree;
        SkillNode arrowNode1 = tree.GetById("fort_arrow_slits");
        SkillNode arrowNode2 = tree.GetById("fort_arrow_slit");
        SkillNode oilNode1 = tree.GetById("fort_boiling_oil");
        SkillNode oilNode2 = tree.GetById("fort_burning_oil");
        SkillNode spikeNode1 = tree.GetById("fort_spike_barricades");
        SkillNode spikeNode2 = tree.GetById("fort_spikes");

        int arrowLvl = Mathf.Max(
            arrowNode1 != null ? SkillTreeService.Instance.GetLevel(arrowNode1) : 0,
            arrowNode2 != null ? SkillTreeService.Instance.GetLevel(arrowNode2) : 0);

        int oilLvl = Mathf.Max(
            oilNode1 != null ? SkillTreeService.Instance.GetLevel(oilNode1) : 0,
            oilNode2 != null ? SkillTreeService.Instance.GetLevel(oilNode2) : 0);

        int spikeLvl = Mathf.Max(
            spikeNode1 != null ? SkillTreeService.Instance.GetLevel(spikeNode1) : 0,
            spikeNode2 != null ? SkillTreeService.Instance.GetLevel(spikeNode2) : 0);

        if (arrowLvl > 0 && arrowLvl != arrowSlitsLevel)
        {
            arrowSlitsLevel = arrowLvl;
            DeployOrUpgradeType(FortDefenseType.ArrowSlits, FortSocketType.WallSlit, arrowSlitsPrefab, arrowSlitsLevel);
        }

        if (oilLvl > 0 && oilLvl != burningOilLevel)
        {
            burningOilLevel = oilLvl;
            DeployOrUpgradeType(FortDefenseType.BurningOil, FortSocketType.GateOverhead, burningOilPrefab, burningOilLevel);
        }

        if (spikeLvl > 0 && spikeLvl != spikesLevel)
        {
            spikesLevel = spikeLvl;
            DeployOrUpgradeType(FortDefenseType.Spikes, FortSocketType.GroundBarricade, spikesPrefab, spikesLevel);
        }
    }

    /// <summary>
    ///     Handles an upgrade card draft from SurvivorsCardSelector or SkillTreeService.
    /// </summary>
    public void HandleSkillNodePurchased(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return;

        switch (nodeId.ToLowerInvariant())
        {
            case "fort_arrow_slits":
            case "fort_arrow_slit":
                UpgradeOrDeploy(FortDefenseType.ArrowSlits);
                break;

            case "fort_boiling_oil":
            case "fort_burning_oil":
                UpgradeOrDeploy(FortDefenseType.BurningOil);
                break;

            case "fort_spike_barricades":
            case "fort_spikes":
                UpgradeOrDeploy(FortDefenseType.Spikes);
                break;
        }
    }

    /// <summary>
    ///     Deploys a defense to available sockets or upgrades all existing instances if already deployed.
    /// </summary>
    public void UpgradeOrDeploy(FortDefenseType type)
    {
        switch (type)
        {
            case FortDefenseType.ArrowSlits:
                arrowSlitsLevel++;
                DeployOrUpgradeType(FortDefenseType.ArrowSlits, FortSocketType.WallSlit, arrowSlitsPrefab, arrowSlitsLevel);
                break;

            case FortDefenseType.BurningOil:
                burningOilLevel++;
                DeployOrUpgradeType(FortDefenseType.BurningOil, FortSocketType.GateOverhead, burningOilPrefab, burningOilLevel);
                break;

            case FortDefenseType.Spikes:
                spikesLevel++;
                DeployOrUpgradeType(FortDefenseType.Spikes, FortSocketType.GroundBarricade, spikesPrefab, spikesLevel);
                break;
        }
    }

    private void DeployOrUpgradeType(FortDefenseType type, FortSocketType preferredSocket, GameObject prefab, int newLevel)
    {
        RefreshSockets();

        // Upgrade all currently installed defenses of this type
        foreach (FortDefense def in activeDefenses)
        {
            if (def != null && def.DefenseType == type)
            {
                def.SetLevel(newLevel);
            }
        }

        // If level 1 (first unlock) or no defenses currently active, deploy to matching sockets!
        if (newLevel >= 1)
        {
            if (prefab == null)
            {
                Debug.LogWarning($"[FortDefenseManager] Cannot deploy {type}: prefab is null!");
                return;
            }

            foreach (FortDefenseSocket socket in allSockets)
            {
                if (socket == null) continue;

                // Match socket type
                bool matches = socket.SocketType == preferredSocket;

                if (matches)
                {
                    if (!socket.IsOccupied)
                    {
                        FortDefense installed = socket.InstallDefense(prefab, newLevel);
                        if (installed != null)
                        {
                            activeDefenses.Add(installed);
                            Debug.Log($"[FortDefenseManager] Deployed {type} (Level {newLevel}) on socket '{socket.name}'.");
                        }
                    }
                    else if (socket.CurrentDefense != null && socket.CurrentDefense.DefenseType == type)
                    {
                        socket.CurrentDefense.SetLevel(newLevel);
                    }
                }
            }
        }
    }
}
