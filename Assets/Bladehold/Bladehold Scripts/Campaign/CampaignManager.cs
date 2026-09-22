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
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    [Header("Campaign Graph")]
    [SerializeField] private CampaignGraphSO activeGraph;

    [Header("Scene Configuration")]
    [SerializeField] private string campaignMapSceneName = "Bladehold Campaign Map Scene";

    public CampaignGraphSO ActiveGraph
    {
        get
        {
            if (activeGraph == null)
            {
                activeGraph = Resources.Load<CampaignGraphSO>("CampaignGraph");
                if (activeGraph == null)
                {
                    activeGraph = CampaignGraphSO.CreateDefaultCampaignGraph();
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
        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }

        RestoreFromRunSession();
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
            int startingWave = node.tierIndex switch
            {
                1 => 1,
                2 => 4,
                4 => 7,
                5 => 10,
                7 => 13,
                _ => Mathf.Max(1, (node.tierIndex - 1) * 3 + 1)
            };
            RunSession.CurrentWave = startingWave;
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
        if (node != null)
        {
            if (!AvailableNodeIds.Contains(node.nodeId))
            {
                AvailableNodeIds.Add(node.nodeId);
            }
            SelectNode(node);
        }
        else
        {
            Debug.LogWarning($"[CampaignManager] DeployToNode could not find node '{nodeId}'. Attempting direct scene fallback.");
            string targetScene = nodeId.Contains("princess") ? "Bladehold Princess Sanctuary" : "Bladehold Survivors Scene";
            if (Bladehold.UI.LoadingScreenManager.Instance != null)
            {
                Bladehold.UI.LoadingScreenManager.Instance.LoadScene(
                    targetScene,
                    "Princess Sanctuary",
                    "Royal Bower",
                    "Confront Princess Katherine in her sanctuary."
                );
            }
            else
            {
                SceneManager.LoadScene(targetScene);
            }
        }
    }


    /// <summary>
    ///     Marks the current active node as completed, unlocks forward connected child nodes,
    ///     awards currency bounties, and syncs with RunSession.
    /// </summary>
    public void CompleteCurrentNode()
    {
        if (string.IsNullOrEmpty(CurrentNodeId))
        {
            Debug.LogWarning("[CampaignManager] CompleteCurrentNode called but CurrentNodeId is null or empty.");
            return;
        }

        CampaignNodeSO completedNode = CurrentNode;
        if (completedNode == null)
        {
            Debug.LogWarning($"[CampaignManager] Could not find node with ID '{CurrentNodeId}' to complete.");
            return;
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
    }

    /// <summary>
    ///     Completes current node and navigates player back to the Campaign Overview Map scene.
    /// </summary>
    public void CompleteCurrentNodeAndOpenMap()
    {
        CompleteCurrentNode();
        OpenOverviewMap();
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
