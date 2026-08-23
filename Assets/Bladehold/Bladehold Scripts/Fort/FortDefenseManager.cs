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

        if (SkillTreeService.Instance != null)
        {
            SkillTreeService.Instance.OnNodePurchased += HandleSkillNodePurchasedEvent;
            SkillTreeService.Instance.OnTreeChanged += SyncFromSkillTree;
            SyncFromSkillTree();
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
        SkillNode arrowNode = tree.GetById("fort_arrow_slit");
        SkillNode oilNode = tree.GetById("fort_burning_oil");
        SkillNode spikeNode = tree.GetById("fort_spikes");

        int arrowLvl = arrowNode != null ? SkillTreeService.Instance.GetLevel(arrowNode) : 0;
        int oilLvl = oilNode != null ? SkillTreeService.Instance.GetLevel(oilNode) : 0;
        int spikeLvl = spikeNode != null ? SkillTreeService.Instance.GetLevel(spikeNode) : 0;

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
            case "fort_arrow_slit":
                UpgradeOrDeploy(FortDefenseType.ArrowSlits);
                break;
            case "fort_burning_oil":
                UpgradeOrDeploy(FortDefenseType.BurningOil);
                break;
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
