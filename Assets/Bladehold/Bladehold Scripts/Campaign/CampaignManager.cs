using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
///     Master state manager for the Castle Campaign progression.
///     Coordinates active, completed, and available nodes, manages stage deployment and transitions,
///     and syncs state across scenes with RunSession.
/// </summary>
public class CampaignManager : MonoBehaviour
{
    private static CampaignManager instance;

    public static CampaignManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<CampaignManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("CampaignManager");
                    instance = go.AddComponent<CampaignManager>();
                    if (Application.isPlaying)
                    {
                        DontDestroyOnLoad(go);
                    }
                }
            }
            return instance;
        }
    }

    /// <summary>
    ///     True when a manager exists, without auto-creating one. Use from OnDestroy/teardown code so
    ///     unsubscribing doesn't spawn a fresh manager while scenes unload or the app quits.
    /// </summary>
    public static bool HasInstance => instance != null;

    [Header("Campaign Graph")]
    [SerializeField] private CampaignGraphSO activeGraph;

    [Header("Scene Configuration")]
    [SerializeField] private string campaignMapSceneName = "Bladehold Campaign Map Scene";
    [SerializeField] private string metaAreaSceneName = "Bladehold Meta Area Scene";

    public CampaignGraphSO ActiveGraph
    {
        get
        {
            if (activeGraph == null)
            {
                activeGraph = Resources.Load<CampaignGraphSO>("CampaignGraph");
                if (activeGraph == null)
                {
                    Debug.LogError("[CampaignManager] No CampaignGraph asset found in Resources/. Run Bladehold/Campaign/Setup All Campaign Prefabs & Scene.");
                }
            }
            return activeGraph;
        }
        set => activeGraph = value;
    }

    public string CurrentNodeId { get; private set; }
    public HashSet<string> CompletedNodeIds { get; private set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> AvailableNodeIds { get; private set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public CampaignNodeSO CurrentNode => GetNode(CurrentNodeId);

    /// <summary>True when clearing the current node ends the campaign (final boss or demo cutoff).</summary>
    public bool CurrentNodeEndsCampaign => IsCampaignActive && CurrentNode != null && CurrentNode.EndsCampaign;
    public bool IsCampaignActive => RunSession.IsCampaignRun;

    public event Action<CampaignNodeSO> OnNodeSelected;
    public event Action<CampaignNodeSO> OnNodeCompleted;
    public event Action OnCampaignStateChanged;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (transform.parent == null && Application.isPlaying)
        {
            DontDestroyOnLoad(gameObject);
        }

        RestoreFromRunSession();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>
    ///     Restores campaign progression state from RunSession across scene transitions.
    /// </summary>
    public void RestoreFromRunSession()
    {
        CurrentNodeId = RunSession.CampaignCurrentNodeId;

        CompletedNodeIds.Clear();
        if (RunSession.CampaignCompletedNodeIds != null)
        {
            for (int i = 0; i < RunSession.CampaignCompletedNodeIds.Count; i++)
            {
                CompletedNodeIds.Add(RunSession.CampaignCompletedNodeIds[i]);
            }
        }

        AvailableNodeIds.Clear();
        if (RunSession.CampaignAvailableNodeIds != null && RunSession.CampaignAvailableNodeIds.Count > 0)
        {
            for (int i = 0; i < RunSession.CampaignAvailableNodeIds.Count; i++)
            {
                AvailableNodeIds.Add(RunSession.CampaignAvailableNodeIds[i]);
            }
        }
        else if (ActiveGraph != null && ActiveGraph.rootNode != null)
        {
            // Initial root start
            AvailableNodeIds.Add(ActiveGraph.rootNode.nodeId);
            SyncToRunSession();
        }
    }

    /// <summary>
    ///     Saves active state into RunSession static collections.
    /// </summary>
    private void SyncToRunSession()
    {
        RunSession.CampaignCurrentNodeId = CurrentNodeId;

        RunSession.CampaignCompletedNodeIds.Clear();
        foreach (string id in CompletedNodeIds)
        {
            RunSession.CampaignCompletedNodeIds.Add(id);
        }

        RunSession.CampaignAvailableNodeIds.Clear();
        foreach (string id in AvailableNodeIds)
        {
            RunSession.CampaignAvailableNodeIds.Add(id);
        }
    }

    /// <summary>
    ///     Initializes a brand new campaign run starting from Tier 1 Root Node.
    /// </summary>
    public void StartCampaignRun(CampaignGraphSO graph = null)
    {
        if (graph != null)
        {
            activeGraph = graph;
        }

        RunSession.IsCampaignRun = true;
        CurrentNodeId = null;
        CompletedNodeIds.Clear();
        AvailableNodeIds.Clear();

        if (ActiveGraph != null && ActiveGraph.rootNode != null)
        {
            AvailableNodeIds.Add(ActiveGraph.rootNode.nodeId);
        }

        SyncToRunSession();
        OnCampaignStateChanged?.Invoke();
        Debug.Log("[CampaignManager] Started brand new Castle Campaign run.");
    }

    /// <summary>
    ///     Selects and launches the destination node.
    /// </summary>
    public void SelectNode(CampaignNodeSO node)
    {
        if (node == null)
        {
            Debug.LogError("[CampaignManager] Cannot select null CampaignNodeSO.");
            return;
        }

        if (!AvailableNodeIds.Contains(node.nodeId))
        {
            Debug.LogWarning($"[CampaignManager] Node '{node.nodeId}' ({node.nodeTitle}) is not currently available to deploy.");
            return;
        }

        RunSession.IsCampaignRun = true;
        CurrentNodeId = node.nodeId;

        if (node.nodeType == CampaignNodeType.Combat || node.nodeType == CampaignNodeType.PreBoss)
        {
            // Each defence level node is 5 waves (Wave 1 to 5)
            RunSession.CurrentWave = 1;
        }

        SyncToRunSession();

        OnNodeSelected?.Invoke(node);
        Debug.Log($"[CampaignManager] Deploying to Node: {node.nodeTitle} (Scene: {node.sceneName}, Type: {node.nodeType})");

        // Load scene through LoadingScreenManager if available, otherwise SceneManager
        if (Bladehold.UI.LoadingScreenManager.Instance != null)
        {
            Bladehold.UI.LoadingScreenManager.Instance.LoadScene(
                node.sceneName,
                node.nodeTitle,
                node.subtitle,
                node.description,
                node.rewardIcon
            );
        }
        else
        {
            SceneManager.LoadScene(node.sceneName);
        }
    }

    /// <summary>
    ///     Directly unlocks and deploys the player to a specific node by its programmatic identifier.
    /// </summary>
    public void DeployToNode(string nodeId)
    {
        CampaignNodeSO node = GetNode(nodeId);
        if (node == null)
        {
            Debug.LogError($"[CampaignManager] DeployToNode could not find node '{nodeId}' in the campaign graph.");
            return;
        }

        AvailableNodeIds.Add(node.nodeId);
        SelectNode(node);
    }

    /// <summary>
    ///     Resolves a branching choice made inside a node's scene (the Crypt's Obey/Defy): completes the
    ///     current node, then makes <paramref name="nodeId" /> the current node. With
    ///     <paramref name="loadScene" /> it deploys there; without, the fight happens in the scene
    ///     that's already loaded, and whatever ends it completes the new node.
    /// </summary>
    public void EnterBranchNode(string nodeId, bool loadScene)
    {
        CampaignNodeSO node = GetNode(nodeId);
        if (node == null)
        {
            Debug.LogError($"[CampaignManager] EnterBranchNode could not find node '{nodeId}' in the campaign graph.");
            return;
        }

        if (!string.IsNullOrEmpty(CurrentNodeId))
        {
            CompleteCurrentNode();
        }

        if (loadScene)
        {
            DeployToNode(nodeId);
            return;
        }

        RunSession.IsCampaignRun = true;
        AvailableNodeIds.Add(node.nodeId);
        CurrentNodeId = node.nodeId;
        SyncToRunSession();
        OnNodeSelected?.Invoke(node);
        OnCampaignStateChanged?.Invoke();
        Debug.Log($"[CampaignManager] Entered branch node in place: {node.nodeTitle}");
    }

    /// <summary>
    ///     Marks the current active node as completed, unlocks forward connected child nodes,
    ///     awards currency bounties, and syncs with RunSession. Returns the completed node, or null
    ///     if there was no current node.
    /// </summary>
    public CampaignNodeSO CompleteCurrentNode()
    {
        if (string.IsNullOrEmpty(CurrentNodeId))
        {
            Debug.LogWarning("[CampaignManager] CompleteCurrentNode called but CurrentNodeId is null or empty.");
            return null;
        }

        CampaignNodeSO completedNode = CurrentNode;
        if (completedNode == null)
        {
            Debug.LogWarning($"[CampaignManager] Could not find node with ID '{CurrentNodeId}' to complete.");
            return null;
        }

        CompletedNodeIds.Add(completedNode.nodeId);
        AvailableNodeIds.Remove(completedNode.nodeId);

        // Unlock next forward child nodes
        if (completedNode.nextNodes != null)
        {
            for (int i = 0; i < completedNode.nextNodes.Count; i++)
            {
                CampaignNodeSO next = completedNode.nextNodes[i];
                if (next != null && !CompletedNodeIds.Contains(next.nodeId))
                {
                    AvailableNodeIds.Add(next.nodeId);
                }
            }
        }

        // Grant currency rewards
        if (completedNode.goldReward > 0)
        {
            RunSession.AddInRunGold(completedNode.goldReward);
        }

        if (completedNode.bloodReward > 0 || completedNode.metalReward > 0)
        {
            SaveData data = SaveSystem.Load();
            if (data != null)
            {
                if (completedNode.bloodReward > 0) data.goblinBlood += completedNode.bloodReward;
                if (completedNode.metalReward > 0) data.orcishMetal += completedNode.metalReward;
                SaveSystem.Save(data);
            }
        }

        Debug.Log($"[CampaignManager] Completed Node: {completedNode.nodeTitle}. Unlocked {completedNode.nextNodes?.Count ?? 0} next paths.");

        CurrentNodeId = null;
        SyncToRunSession();

        OnNodeCompleted?.Invoke(completedNode);
        OnCampaignStateChanged?.Invoke();
        return completedNode;
    }

    /// <summary>
    ///     The one exit from every node: completes the current node, then either returns to the
    ///     Campaign Map or, if that node ends the campaign (final boss, demo cutoff), ends the run.
    /// </summary>
    public void CompleteCurrentNodeAndContinue()
    {
        CampaignNodeSO completed = CompleteCurrentNode();
        if (completed != null && completed.EndsCampaign)
        {
            EndCampaign();
            return;
        }

        OpenOverviewMap();
    }

    /// <summary>
    ///     Ends the campaign: wipes the run (permanent currencies are already banked) and returns to
    ///     the Meta Area. Show the end screen before calling this.
    /// </summary>
    public void EndCampaign()
    {
        Debug.Log("[CampaignManager] Campaign complete. Clearing the run and returning to the Meta Area.");
        Time.timeScale = 1f;
        RunSession.ClearRun();
        RestoreFromRunSession();
        OnCampaignStateChanged?.Invoke();

        if (Bladehold.UI.LoadingScreenManager.Instance != null)
        {
            Bladehold.UI.LoadingScreenManager.Instance.LoadScene(
                metaAreaSceneName,
                "Sanctuary",
                "Safe Haven",
                "Prepare upgrades, forge weapons, and plan your next assault."
            );
        }
        else
        {
            SceneManager.LoadScene(metaAreaSceneName);
        }
    }

    /// <summary>
    ///     Transitions player to the Campaign Overview Map screen.
    /// </summary>
    public void OpenOverviewMap()
    {
        Time.timeScale = 1f;

        if (Bladehold.UI.LoadingScreenManager.Instance != null)
        {
            Bladehold.UI.LoadingScreenManager.Instance.LoadScene(
                campaignMapSceneName,
                "Castle Campaign",
                "War Room Map",
                "Select your tactical route through the fortress battlements and inner halls."
            );
        }
        else
        {
            SceneManager.LoadScene(campaignMapSceneName);
        }
    }

    /// <summary>
    ///     Resets campaign progress back to Tier 1 root.
    /// </summary>
    public void ResetCampaign()
    {
        CurrentNodeId = null;
        CompletedNodeIds.Clear();
        AvailableNodeIds.Clear();

        if (ActiveGraph != null && ActiveGraph.rootNode != null)
        {
            AvailableNodeIds.Add(ActiveGraph.rootNode.nodeId);
        }

        SyncToRunSession();
        OnCampaignStateChanged?.Invoke();
    }

    public CampaignNodeSO GetNode(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return ActiveGraph != null ? ActiveGraph.GetNodeById(id) : null;
    }
}
