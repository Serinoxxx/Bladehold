#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SetupCampaignPrefabsAndAssets
{
    public const string NodePrefabPath = "Assets/Bladehold/Bladehold Prefabs/UI/CampaignNodeButton.prefab";
    public const string PathLinePrefabPath = "Assets/Bladehold/Bladehold Prefabs/UI/CampaignPathLine.prefab";
    public const string GraphAssetPath = "Assets/Bladehold/Resources/CampaignGraph.asset";
    public const string NodesFolder = "Assets/Bladehold/Resources/CampaignNodes";
    public const string ScenePath = "Assets/Bladehold/Bladehold Scenes/Bladehold Campaign Map Scene.unity";

    [InitializeOnLoadMethod]
    private static void OnProjectLoaded()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(NodePrefabPath) || !File.Exists(PathLinePrefabPath) || !File.Exists(GraphAssetPath))
            {
                Debug.Log("[SetupCampaignPrefabsAndAssets] Missing campaign prefabs or graph asset. Generating now...");
                ExecuteAll();
            }
        };
    }

    [MenuItem("Bladehold/Campaign/Setup All Campaign Prefabs & Scene", priority = 10)]
    public static void ExecuteAll()
    {
        Debug.Log("[SetupCampaignPrefabsAndAssets] === Starting Campaign Prefabs & Assets Setup ===");
        var graph = CreateOrUpdateCampaignGraph();
        var nodePrefab = CreateOrUpdateNodeButtonPrefab();
        var pathPrefab = CreateOrUpdatePathLinePrefab();
        UpdateCampaignMapScene(graph, nodePrefab, pathPrefab);
        Debug.Log("[SetupCampaignPrefabsAndAssets] === Campaign Setup Complete! ===");
    }

    public static CampaignGraphSO CreateOrUpdateCampaignGraph()
    {
        if (!Directory.Exists("Assets/Bladehold/Resources"))
        {
            Directory.CreateDirectory("Assets/Bladehold/Resources");
        }

        if (!Directory.Exists(NodesFolder))
        {
            Directory.CreateDirectory(NodesFolder);
        }

        CampaignGraphSO graph = AssetDatabase.LoadAssetAtPath<CampaignGraphSO>(GraphAssetPath);
        if (graph == null)
        {
            graph = ScriptableObject.CreateInstance<CampaignGraphSO>();
            AssetDatabase.CreateAsset(graph, GraphAssetPath);
        }

        graph.BuildDefaultGraph();

        // Save each node as a persistent asset file in CampaignNodes/
        for (int i = 0; i < graph.allNodes.Count; i++)
        {
            CampaignNodeSO node = graph.allNodes[i];
            if (node == null) continue;

            string nodeAssetPath = $"{NodesFolder}/Node_{node.nodeId}.asset";
            CampaignNodeSO existingNode = AssetDatabase.LoadAssetAtPath<CampaignNodeSO>(nodeAssetPath);

            if (existingNode == null)
            {
                AssetDatabase.CreateAsset(node, nodeAssetPath);
            }
            else
            {
                EditorUtility.CopySerialized(node, existingNode);
                graph.allNodes[i] = existingNode;
                EditorUtility.SetDirty(existingNode);
            }
        }

        // Re-link rootNode and tier references to persistent assets
        if (graph.allNodes.Count > 0)
        {
            graph.rootNode = graph.allNodes[0];
        }

        // Re-link nextNodes on persistent assets
        for (int i = 0; i < graph.allNodes.Count; i++)
        {
            CampaignNodeSO node = graph.allNodes[i];
            if (node == null || node.nextNodes == null) continue;

            for (int n = 0; n < node.nextNodes.Count; n++)
            {
                if (node.nextNodes[n] != null)
                {
                    string targetId = node.nextNodes[n].nodeId;
                    CampaignNodeSO persistentTarget = graph.allNodes.Find(x => x != null && x.nodeId == targetId);
                    if (persistentTarget != null)
                    {
                        node.nextNodes[n] = persistentTarget;
                    }
                }
            }
            EditorUtility.SetDirty(node);
        }

        EditorUtility.SetDirty(graph);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[SetupCampaignPrefabsAndAssets] Saved CampaignGraph.asset with {graph.allNodes.Count} nodes.");
        return graph;
    }

    public static CampaignNodeButtonUI CreateOrUpdateNodeButtonPrefab()
    {
        string dir = Path.GetDirectoryName(NodePrefabPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        // Load Synty sprites if available
        Sprite bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/SPR_FantasyWarrior_Box_Background_01.png");
        Sprite frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/SPR_FantasyWarrior_Frame_Box_03.png");
        Sprite fishingSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/Icons_Map/ICON_FantasyWarrior_Map_Fishing_01_Clean.png");
        Sprite lockSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/Icons_Map/ICON_FantasyWarrior_Map_Lock_01_Clean.png");
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Synty/InterfaceFantasyWarriorHUD/Fonts/Grenze/Grenze-SemiBold SDF.asset");

        GameObject root = new GameObject("CampaignNodeButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(CampaignNodeButtonUI));
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(170f, 95f);

        Image bgImg = root.GetComponent<Image>();
        if (bgSprite != null)
        {
            bgImg.sprite = bgSprite;
            bgImg.type = Image.Type.Sliced;
        }
        bgImg.color = new Color(0.18f, 0.20f, 0.25f, 0.95f);
        Button btn = root.GetComponent<Button>();

        // 1. Border Frame
        GameObject borderGo = new GameObject("BorderFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        borderGo.transform.SetParent(root.transform, false);
        RectTransform borderRt = borderGo.GetComponent<RectTransform>();
        borderRt.anchorMin = Vector2.zero;
        borderRt.anchorMax = Vector2.one;
        borderRt.sizeDelta = new Vector2(4f, 4f);
        Image borderImg = borderGo.GetComponent<Image>();
        if (frameSprite != null)
        {
            borderImg.sprite = frameSprite;
            borderImg.type = Image.Type.Sliced;
        }
        borderImg.color = new Color(0.35f, 0.35f, 0.40f, 0.8f);
        borderGo.transform.SetAsFirstSibling();

        // 2. Active Pulse Glow
        GameObject glowGo = new GameObject("ActiveGlow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        glowGo.transform.SetParent(root.transform, false);
        RectTransform glowRt = glowGo.GetComponent<RectTransform>();
        glowRt.anchorMin = Vector2.zero;
        glowRt.anchorMax = Vector2.one;
        glowRt.sizeDelta = new Vector2(8f, 8f);
        Image glowImg = glowGo.GetComponent<Image>();
        if (frameSprite != null)
        {
            glowImg.sprite = frameSprite;
            glowImg.type = Image.Type.Sliced;
        }
        glowImg.color = new Color(1f, 0.85f, 0.2f, 0.35f);
        glowGo.SetActive(false);

        // 3. Tier Label (Top)
        GameObject tierGo = new GameObject("TierText", typeof(RectTransform), typeof(TextMeshProUGUI));
        tierGo.transform.SetParent(root.transform, false);
        RectTransform tierRt = tierGo.GetComponent<RectTransform>();
        tierRt.anchorMin = new Vector2(0f, 0.72f);
        tierRt.anchorMax = new Vector2(1f, 0.98f);
        tierRt.offsetMin = Vector2.zero;
        tierRt.offsetMax = Vector2.zero;
        TextMeshProUGUI tierTmp = tierGo.GetComponent<TextMeshProUGUI>();
        if (font != null) tierTmp.font = font;
        tierTmp.fontSize = 11;
        tierTmp.alignment = TextAlignmentOptions.Center;
        tierTmp.fontStyle = FontStyles.Bold;
        tierTmp.color = new Color(0.75f, 0.85f, 1f, 0.85f);
        tierTmp.text = "Tier 1";

        // 4. Title Label (Middle)
        GameObject titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(root.transform, false);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.05f, 0.28f);
        titleRt.anchorMax = new Vector2(0.95f, 0.72f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;
        TextMeshProUGUI titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
        if (font != null) titleTmp.font = font;
        titleTmp.fontSize = 13;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.textWrappingMode = TextWrappingModes.Normal;
        titleTmp.color = Color.white;
        titleTmp.text = "Sector Title";

        // 5. Captain Badge (Bottom)
        GameObject captGo = new GameObject("CaptainText", typeof(RectTransform), typeof(TextMeshProUGUI));
        captGo.transform.SetParent(root.transform, false);
        RectTransform captRt = captGo.GetComponent<RectTransform>();
        captRt.anchorMin = new Vector2(0f, 0.04f);
        captRt.anchorMax = new Vector2(1f, 0.28f);
        captRt.offsetMin = Vector2.zero;
        captRt.offsetMax = Vector2.zero;
        TextMeshProUGUI captTmp = captGo.GetComponent<TextMeshProUGUI>();
        if (font != null) captTmp.font = font;
        captTmp.fontSize = 10;
        captTmp.alignment = TextAlignmentOptions.Center;
        captTmp.fontStyle = FontStyles.Italic;
        captTmp.color = new Color(1f, 0.45f, 0.35f, 0.95f);
        captTmp.text = "Captain Name";

        // 6. Node Icon (e.g. Fishing Pond, Boss)
        GameObject iconGo = new GameObject("NodeIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGo.transform.SetParent(root.transform, false);
        RectTransform iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.04f, 0.65f);
        iconRt.anchorMax = new Vector2(0.24f, 0.95f);
        iconRt.offsetMin = Vector2.zero;
        iconRt.offsetMax = Vector2.zero;
        Image iconImg = iconGo.GetComponent<Image>();
        if (fishingSprite != null) iconImg.sprite = fishingSprite;
        iconGo.SetActive(false);

        // 7. Lock Overlay
        GameObject lockGo = new GameObject("LockOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        lockGo.transform.SetParent(root.transform, false);
        RectTransform lockRt = lockGo.GetComponent<RectTransform>();
        lockRt.anchorMin = Vector2.zero;
        lockRt.anchorMax = Vector2.one;
        lockRt.sizeDelta = Vector2.zero;
        Image lockImg = lockGo.GetComponent<Image>();
        lockImg.color = new Color(0.05f, 0.05f, 0.08f, 0.65f);

        if (lockSprite != null)
        {
            GameObject lockIconGo = new GameObject("LockIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            lockIconGo.transform.SetParent(lockGo.transform, false);
            RectTransform liRt = lockIconGo.GetComponent<RectTransform>();
            liRt.anchorMin = new Vector2(0.5f, 0.5f);
            liRt.anchorMax = new Vector2(0.5f, 0.5f);
            liRt.sizeDelta = new Vector2(24f, 24f);
            Image liImg = lockIconGo.GetComponent<Image>();
            liImg.sprite = lockSprite;
            liImg.color = new Color(0.8f, 0.8f, 0.85f, 0.85f);
        }
        lockGo.SetActive(false);

        // 8. Completed Checkmark
        GameObject checkGo = new GameObject("CompletedBadge", typeof(RectTransform), typeof(TextMeshProUGUI));
        checkGo.transform.SetParent(root.transform, false);
        RectTransform checkRt = checkGo.GetComponent<RectTransform>();
        checkRt.anchorMin = new Vector2(0.75f, 0.65f);
        checkRt.anchorMax = new Vector2(0.98f, 0.95f);
        checkRt.offsetMin = Vector2.zero;
        checkRt.offsetMax = Vector2.zero;
        TextMeshProUGUI checkTmp = checkGo.GetComponent<TextMeshProUGUI>();
        checkTmp.text = "[V]";
        checkTmp.fontSize = 12;
        checkTmp.fontStyle = FontStyles.Bold;
        checkTmp.color = new Color(0.35f, 0.9f, 0.45f);
        checkGo.SetActive(false);

        CampaignNodeButtonUI btnComp = root.GetComponent<CampaignNodeButtonUI>();
        btnComp.InitializeReferences(
            rt, btn, bgImg, borderImg, titleTmp, tierTmp, captTmp,
            lockGo, checkGo, glowGo, iconImg, fishingSprite);

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, NodePrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        Debug.Log($"[SetupCampaignPrefabsAndAssets] Saved CampaignNodeButton prefab to {NodePrefabPath}");
        return savedPrefab.GetComponent<CampaignNodeButtonUI>();
    }

    public static GameObject CreateOrUpdatePathLinePrefab()
    {
        string dir = Path.GetDirectoryName(PathLinePrefabPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        GameObject root = new GameObject("CampaignPathLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(100f, 4f);

        Image img = root.GetComponent<Image>();
        img.color = Color.white;

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PathLinePrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        Debug.Log($"[SetupCampaignPrefabsAndAssets] Saved CampaignPathLine prefab to {PathLinePrefabPath}");
        return savedPrefab;
    }

    public static void UpdateCampaignMapScene(CampaignGraphSO graph, CampaignNodeButtonUI nodePrefab, GameObject pathPrefab)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        CampaignMapUI mapUI = UnityEngine.Object.FindAnyObjectByType<CampaignMapUI>(FindObjectsInactive.Include);
        if (mapUI == null)
        {
            Debug.LogError($"[SetupCampaignPrefabsAndAssets] CampaignMapUI not found in {ScenePath}");
            return;
        }

        // Configure Containers pivots
        Transform contentTr = mapUI.transform.Find("MapScrollRect/Viewport/NodesContent");
        if (contentTr != null)
        {
            RectTransform contentRt = contentTr as RectTransform;
            contentRt.anchoredPosition = Vector2.zero;

            Transform nodesTr = contentTr.Find("NodesContainer");
            if (nodesTr != null)
            {
                RectTransform nodesRt = nodesTr as RectTransform;
                nodesRt.pivot = new Vector2(0f, 0.5f);
                nodesRt.anchorMin = new Vector2(0f, 0f);
                nodesRt.anchorMax = new Vector2(1f, 1f);
                nodesRt.offsetMin = Vector2.zero;
                nodesRt.offsetMax = Vector2.zero;

                // Clear all stale child nodes
                for (int i = nodesRt.childCount - 1; i >= 0; i--)
                {
                    UnityEngine.Object.DestroyImmediate(nodesRt.GetChild(i).gameObject);
                }
            }

            Transform pathsTr = contentTr.Find("PathsContainer");
            if (pathsTr != null)
            {
                RectTransform pathsRt = pathsTr as RectTransform;
                pathsRt.pivot = new Vector2(0f, 0.5f);
                pathsRt.anchorMin = new Vector2(0f, 0f);
                pathsRt.anchorMax = new Vector2(1f, 1f);
                pathsRt.offsetMin = Vector2.zero;
                pathsRt.offsetMax = Vector2.zero;

                // Clear all stale paths
                for (int i = pathsRt.childCount - 1; i >= 0; i--)
                {
                    UnityEngine.Object.DestroyImmediate(pathsRt.GetChild(i).gameObject);
                }
            }
        }

        // Assign serialized prefabs & graph via SerializedObject to ensure scene serialization
        SerializedObject so = new SerializedObject(mapUI);
        so.FindProperty("campaignGraph").objectReferenceValue = graph;
        so.FindProperty("nodeButtonPrefab").objectReferenceValue = nodePrefab;
        so.FindProperty("pathLinePrefab").objectReferenceValue = pathPrefab;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(mapUI);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[SetupCampaignPrefabsAndAssets] Configured and saved {ScenePath} with prefabs and persistent graph.");
    }
}
#endif
