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

public static class BuildAndCaptureCampaignMap
{
    public const string ScenePath = "Assets/Bladehold/Bladehold Scenes/Bladehold Campaign Map Scene.unity";
    public const string ArtifactsDir = "C:/Users/lance/.gemini/antigravity/brain/3e38f462-e87c-4c9b-9878-ffdd22d17f42";
    public const string ProjectScreenshotsDir = "Screenshots";

    [MenuItem("Bladehold/Campaign/Build Campaign Map Scene", priority = 30)]
    public static void BuildSceneMenuItem()
    {
        BuildScene();
    }

    [MenuItem("Bladehold/Campaign/Capture Progression Screenshots", priority = 31)]
    public static void CaptureScreenshotsMenuItem()
    {
        CaptureProgressionScreenshots();
    }

    [MenuItem("Bladehold/Campaign/Build Scene & Capture Screenshots", priority = 32)]
    public static void ExecuteAll()
    {
        Debug.Log("[BuildAndCaptureCampaignMap] === Starting Complete Build & Screenshot Capture ===");
        BuildScene();
        CaptureProgressionScreenshots();
        Debug.Log("[BuildAndCaptureCampaignMap] === Finished All Operations Successfully! ===");
    }

    public static void BuildScene()
    {
        Debug.Log($"[BuildAndCaptureCampaignMap] Building scene at: {ScenePath}");

        string dir = Path.GetDirectoryName(ScenePath);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 1. Camera & Background
        GameObject camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        Camera cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1f);
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        camGo.transform.position = new Vector3(0f, 0f, -10f);

        // 2. Event System
        GameObject eventSysGo = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

        // 3. Canvas Hierarchy
        GameObject canvasGo = new GameObject("CampaignMapCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Background dark gradient / stone image
        GameObject bgGo = new GameObject("BackgroundPanel", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(canvasGo.transform, false);
        RectTransform bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;
        Image bgImg = bgGo.GetComponent<Image>();
        bgImg.color = new Color(0.08f, 0.09f, 0.12f, 1f);

        // Map Title & Subtitle
        GameObject titleGo = new GameObject("MapHeaderTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(canvasGo.transform, false);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -20f);
        titleRt.sizeDelta = new Vector2(1800f, 70f);
        TextMeshProUGUI titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
        titleTmp.text = "BLADEHOLD CASTLE INVASION MAP";
        titleTmp.fontSize = 28;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.TopLeft;
        titleTmp.color = new Color(0.95f, 0.85f, 0.55f);

        GameObject subtitleGo = new GameObject("MapSubtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        subtitleGo.transform.SetParent(titleGo.transform, false);
        RectTransform subRt = subtitleGo.GetComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0f, 0f);
        subRt.anchorMax = new Vector2(1f, 0.4f);
        subRt.offsetMin = Vector2.zero;
        subRt.offsetMax = Vector2.zero;
        TextMeshProUGUI subTmp = subtitleGo.GetComponent<TextMeshProUGUI>();
        subTmp.text = "Choose your assault route through the castle bastions to the Inner Sanctum";
        subTmp.fontSize = 15;
        subTmp.fontStyle = FontStyles.Italic;
        subTmp.color = new Color(0.7f, 0.75f, 0.82f);

        // Currencies Bar (Top Right)
        GameObject currsGo = new GameObject("CurrenciesBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        currsGo.transform.SetParent(canvasGo.transform, false);
        RectTransform currsRt = currsGo.GetComponent<RectTransform>();
        currsRt.anchorMin = new Vector2(1f, 1f);
        currsRt.anchorMax = new Vector2(1f, 1f);
        currsRt.pivot = new Vector2(1f, 1f);
        currsRt.anchoredPosition = new Vector2(-40f, -24f);
        currsRt.sizeDelta = new Vector2(500f, 44f);
        HorizontalLayoutGroup hlg = currsGo.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 25f;
        hlg.childAlignment = TextAnchor.MiddleRight;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        TextMeshProUGUI goldTmp = CreateCurrencyBadge(currsGo.transform, "GoldBadge", "Gold: 250g", new Color(1f, 0.85f, 0.25f));
        TextMeshProUGUI bloodTmp = CreateCurrencyBadge(currsGo.transform, "BloodBadge", "Blood: 45", new Color(0.95f, 0.3f, 0.3f));
        TextMeshProUGUI metalTmp = CreateCurrencyBadge(currsGo.transform, "MetalBadge", "Metal: 12", new Color(0.45f, 0.85f, 1f));

        // Return Button (Bottom Left)
        GameObject returnBtnGo = new GameObject("ReturnToMetaButton", typeof(RectTransform), typeof(Image), typeof(Button));
        returnBtnGo.transform.SetParent(canvasGo.transform, false);
        RectTransform retRt = returnBtnGo.GetComponent<RectTransform>();
        retRt.anchorMin = new Vector2(0f, 0f);
        retRt.anchorMax = new Vector2(0f, 0f);
        retRt.pivot = new Vector2(0f, 0f);
        retRt.anchoredPosition = new Vector2(40f, 30f);
        retRt.sizeDelta = new Vector2(220f, 50f);
        Image retImg = returnBtnGo.GetComponent<Image>();
        retImg.color = new Color(0.25f, 0.22f, 0.28f, 0.95f);
        Button retBtn = returnBtnGo.GetComponent<Button>();

        GameObject retTextGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        retTextGo.transform.SetParent(returnBtnGo.transform, false);
        RectTransform retTextRt = retTextGo.GetComponent<RectTransform>();
        retTextRt.anchorMin = Vector2.zero;
        retTextRt.anchorMax = Vector2.one;
        retTextRt.sizeDelta = Vector2.zero;
        TextMeshProUGUI retTmp = retTextGo.GetComponent<TextMeshProUGUI>();
        retTmp.text = "< Return to Stronghold";
        retTmp.fontSize = 14;
        retTmp.alignment = TextAlignmentOptions.Center;
        retTmp.color = Color.white;

        // ScrollRect & Viewport for Nodes
        GameObject scrollGo = new GameObject("MapScrollRect", typeof(RectTransform), typeof(ScrollRect));
        scrollGo.transform.SetParent(canvasGo.transform, false);
        RectTransform sRt = scrollGo.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0f, 0f);
        sRt.anchorMax = new Vector2(1f, 1f);
        sRt.offsetMin = new Vector2(40f, 100f);
        sRt.offsetMax = new Vector2(-40f, -90f);
        ScrollRect scrollRect = scrollGo.GetComponent<ScrollRect>();
        scrollRect.horizontal = true;
        scrollRect.vertical = false;

        GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        RectTransform vpRt = viewportGo.GetComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.sizeDelta = Vector2.zero;
        viewportGo.GetComponent<Mask>().showMaskGraphic = false;
        viewportGo.GetComponent<Image>().color = Color.clear;
        scrollRect.viewport = vpRt;

        // Content Container for Nodes (Sized for horizontal graph)
        GameObject contentGo = new GameObject("NodesContent", typeof(RectTransform));
        contentGo.transform.SetParent(viewportGo.transform, false);
        RectTransform contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 0f);
        contentRt.anchorMax = new Vector2(0f, 1f);
        contentRt.pivot = new Vector2(0f, 0.5f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(2500f, 0f);
        scrollRect.content = contentRt;

        // Paths Container (Drawn beneath nodes)
        GameObject pathsGo = new GameObject("PathsContainer", typeof(RectTransform));
        pathsGo.transform.SetParent(contentGo.transform, false);
        RectTransform pathsRt = pathsGo.GetComponent<RectTransform>();
        pathsRt.anchorMin = Vector2.zero;
        pathsRt.anchorMax = Vector2.one;
        pathsRt.sizeDelta = Vector2.zero;

        // Nodes Container
        GameObject nodesGo = new GameObject("NodesContainer", typeof(RectTransform));
        nodesGo.transform.SetParent(contentGo.transform, false);
        RectTransform nodesRt = nodesGo.GetComponent<RectTransform>();
        nodesRt.anchorMin = Vector2.zero;
        nodesRt.anchorMax = Vector2.one;
        nodesRt.sizeDelta = Vector2.zero;

        // Tooltip UI Panel (Sized 380 x 480)
        GameObject tooltipGo = CreateTooltipUI(canvasGo.transform);
        CampaignTooltipUI tooltipComp = tooltipGo.GetComponent<CampaignTooltipUI>();

        // CampaignMapUI Controller
        CampaignMapUI mapUI = canvasGo.AddComponent<CampaignMapUI>();
        CampaignGraphSO defaultGraph = CampaignGraphSO.CreateDefaultCampaignGraph();

        mapUI.InitializeReferences(
            defaultGraph,
            nodesRt,
            pathsRt,
            scrollRect,
            tooltipComp,
            titleTmp,
            goldTmp,
            bloodTmp,
            metalTmp,
            retBtn
        );

        // Save Scene
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[BuildAndCaptureCampaignMap] Successfully saved scene to {ScenePath}");
    }

    private static TextMeshProUGUI CreateCurrencyBadge(Transform parent, string name, string initialText, Color textColor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = initialText;
        tmp.fontSize = 16;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Right;
        tmp.color = textColor;
        return tmp;
    }

    private static GameObject CreateTooltipUI(Transform parent)
    {
        GameObject root = new GameObject("CampaignTooltip", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(CampaignTooltipUI));
        root.transform.SetParent(parent, false);
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(380f, 480f);

        Image bg = root.GetComponent<Image>();
        bg.color = new Color(0.07f, 0.08f, 0.12f, 0.96f);
        CanvasGroup cg = root.GetComponent<CanvasGroup>();

        // Border Frame
        GameObject borderGo = new GameObject("TooltipBorder", typeof(RectTransform), typeof(Image));
        borderGo.transform.SetParent(root.transform, false);
        RectTransform bRt = borderGo.GetComponent<RectTransform>();
        bRt.anchorMin = Vector2.zero;
        bRt.anchorMax = Vector2.one;
        bRt.sizeDelta = new Vector2(3f, 3f);
        Image bImg = borderGo.GetComponent<Image>();
        bImg.color = new Color(0.95f, 0.8f, 0.25f, 0.85f);
        borderGo.transform.SetAsFirstSibling();

        // 1. Title
        GameObject titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(root.transform, false);
        RectTransform tRt = titleGo.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0.05f, 0.88f);
        tRt.anchorMax = new Vector2(0.95f, 0.98f);
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;
        TextMeshProUGUI titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
        titleTmp.fontSize = 20;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = Color.white;

        // Subtitle
        GameObject subGo = new GameObject("SubtitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        subGo.transform.SetParent(root.transform, false);
        RectTransform sRt = subGo.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0.05f, 0.82f);
        sRt.anchorMax = new Vector2(0.95f, 0.88f);
        sRt.offsetMin = Vector2.zero;
        sRt.offsetMax = Vector2.zero;
        TextMeshProUGUI subTmp = subGo.GetComponent<TextMeshProUGUI>();
        subTmp.fontSize = 13;
        subTmp.fontStyle = FontStyles.Italic;
        subTmp.color = new Color(0.7f, 0.75f, 0.85f);

        // Badge
        GameObject badgeGo = new GameObject("NodeTypeBadge", typeof(RectTransform), typeof(Image));
        badgeGo.transform.SetParent(root.transform, false);
        RectTransform bgRt = badgeGo.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0.05f, 0.74f);
        bgRt.anchorMax = new Vector2(0.95f, 0.81f);
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        Image badgeImg = badgeGo.GetComponent<Image>();
        badgeImg.color = new Color(0.85f, 0.35f, 0.2f, 0.85f);

        GameObject badgeTextGo = new GameObject("BadgeText", typeof(RectTransform), typeof(TextMeshProUGUI));
        badgeTextGo.transform.SetParent(badgeGo.transform, false);
        RectTransform btRt = badgeTextGo.GetComponent<RectTransform>();
        btRt.anchorMin = Vector2.zero;
        btRt.anchorMax = Vector2.one;
        btRt.sizeDelta = Vector2.zero;
        TextMeshProUGUI badgeTmp = badgeTextGo.GetComponent<TextMeshProUGUI>();
        badgeTmp.fontSize = 12;
        badgeTmp.fontStyle = FontStyles.Bold;
        badgeTmp.alignment = TextAlignmentOptions.Center;
        badgeTmp.color = Color.white;

        // 2. Captain Section
        GameObject captSecGo = new GameObject("CaptainSection", typeof(RectTransform));
        captSecGo.transform.SetParent(root.transform, false);
        RectTransform csRt = captSecGo.GetComponent<RectTransform>();
        csRt.anchorMin = new Vector2(0.05f, 0.48f);
        csRt.anchorMax = new Vector2(0.95f, 0.72f);
        csRt.offsetMin = Vector2.zero;
        csRt.offsetMax = Vector2.zero;

        GameObject captNameGo = new GameObject("CaptainName", typeof(RectTransform), typeof(TextMeshProUGUI));
        captNameGo.transform.SetParent(captSecGo.transform, false);
        RectTransform cnRt = captNameGo.GetComponent<RectTransform>();
        cnRt.anchorMin = new Vector2(0f, 0.65f);
        cnRt.anchorMax = new Vector2(1f, 1f);
        cnRt.offsetMin = Vector2.zero;
        cnRt.offsetMax = Vector2.zero;
        TextMeshProUGUI captNameTmp = captNameGo.GetComponent<TextMeshProUGUI>();
        captNameTmp.fontSize = 15;
        captNameTmp.fontStyle = FontStyles.Bold;
        captNameTmp.color = new Color(1f, 0.45f, 0.35f);

        GameObject diffTierGo = new GameObject("DiffTier", typeof(RectTransform), typeof(TextMeshProUGUI));
        diffTierGo.transform.SetParent(captSecGo.transform, false);
        RectTransform dtRt = diffTierGo.GetComponent<RectTransform>();
        dtRt.anchorMin = new Vector2(0f, 0.32f);
        dtRt.anchorMax = new Vector2(0.5f, 0.65f);
        dtRt.offsetMin = Vector2.zero;
        dtRt.offsetMax = Vector2.zero;
        TextMeshProUGUI diffTierTmp = diffTierGo.GetComponent<TextMeshProUGUI>();
        diffTierTmp.fontSize = 13;
        diffTierTmp.fontStyle = FontStyles.Bold;

        GameObject diffSkullsGo = new GameObject("DiffSkulls", typeof(RectTransform), typeof(TextMeshProUGUI));
        diffSkullsGo.transform.SetParent(captSecGo.transform, false);
        RectTransform dsRt = diffSkullsGo.GetComponent<RectTransform>();
        dsRt.anchorMin = new Vector2(0.5f, 0.32f);
        dsRt.anchorMax = new Vector2(1f, 0.65f);
        dsRt.offsetMin = Vector2.zero;
        dsRt.offsetMax = Vector2.zero;
        TextMeshProUGUI diffSkullsTmp = diffSkullsGo.GetComponent<TextMeshProUGUI>();
        diffSkullsTmp.fontSize = 13;
        diffSkullsTmp.alignment = TextAlignmentOptions.Right;

        GameObject clanBuffGo = new GameObject("ClanBuff", typeof(RectTransform), typeof(TextMeshProUGUI));
        clanBuffGo.transform.SetParent(captSecGo.transform, false);
        RectTransform cbRt = clanBuffGo.GetComponent<RectTransform>();
        cbRt.anchorMin = new Vector2(0f, 0f);
        cbRt.anchorMax = new Vector2(1f, 0.32f);
        cbRt.offsetMin = Vector2.zero;
        cbRt.offsetMax = Vector2.zero;
        TextMeshProUGUI clanBuffTmp = clanBuffGo.GetComponent<TextMeshProUGUI>();
        clanBuffTmp.fontSize = 12;
        clanBuffTmp.fontStyle = FontStyles.Italic;
        clanBuffTmp.color = new Color(0.85f, 0.85f, 0.9f);

        // 3. Rewards Section
        GameObject rewSecGo = new GameObject("RewardsSection", typeof(RectTransform));
        rewSecGo.transform.SetParent(root.transform, false);
        RectTransform rsRt = rewSecGo.GetComponent<RectTransform>();
        rsRt.anchorMin = new Vector2(0.05f, 0.28f);
        rsRt.anchorMax = new Vector2(0.95f, 0.46f);
        rsRt.offsetMin = Vector2.zero;
        rsRt.offsetMax = Vector2.zero;

        GameObject rewSummGo = new GameObject("RewardsSummary", typeof(RectTransform), typeof(TextMeshProUGUI));
        rewSummGo.transform.SetParent(rewSecGo.transform, false);
        RectTransform rwRt = rewSummGo.GetComponent<RectTransform>();
        rwRt.anchorMin = new Vector2(0f, 0.5f);
        rwRt.anchorMax = new Vector2(1f, 1f);
        rwRt.offsetMin = Vector2.zero;
        rwRt.offsetMax = Vector2.zero;
        TextMeshProUGUI rewSummTmp = rewSummGo.GetComponent<TextMeshProUGUI>();
        rewSummTmp.fontSize = 14;
        rewSummTmp.fontStyle = FontStyles.Bold;
        rewSummTmp.color = new Color(1f, 0.85f, 0.25f);

        GameObject goldBox = new GameObject("GoldBox", typeof(RectTransform));
        goldBox.transform.SetParent(rewSecGo.transform, false);
        RectTransform gbRt = goldBox.GetComponent<RectTransform>();
        gbRt.anchorMin = new Vector2(0f, 0f);
        gbRt.anchorMax = new Vector2(0.33f, 0.5f);
        gbRt.offsetMin = Vector2.zero;
        gbRt.offsetMax = Vector2.zero;
        GameObject goldTxtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        goldTxtGo.transform.SetParent(goldBox.transform, false);
        TextMeshProUGUI goldTxt = goldTxtGo.GetComponent<TextMeshProUGUI>();
        goldTxt.fontSize = 12;
        goldTxt.color = new Color(1f, 0.85f, 0.25f);

        GameObject bloodBox = new GameObject("BloodBox", typeof(RectTransform));
        bloodBox.transform.SetParent(rewSecGo.transform, false);
        RectTransform bbRt = bloodBox.GetComponent<RectTransform>();
        bbRt.anchorMin = new Vector2(0.34f, 0f);
        bbRt.anchorMax = new Vector2(0.66f, 0.5f);
        bbRt.offsetMin = Vector2.zero;
        bbRt.offsetMax = Vector2.zero;
        GameObject bloodTxtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        bloodTxtGo.transform.SetParent(bloodBox.transform, false);
        TextMeshProUGUI bloodTxt = bloodTxtGo.GetComponent<TextMeshProUGUI>();
        bloodTxt.fontSize = 12;
        bloodTxt.color = new Color(0.95f, 0.35f, 0.35f);

        GameObject metalBox = new GameObject("MetalBox", typeof(RectTransform));
        metalBox.transform.SetParent(rewSecGo.transform, false);
        RectTransform mbRt = metalBox.GetComponent<RectTransform>();
        mbRt.anchorMin = new Vector2(0.67f, 0f);
        mbRt.anchorMax = new Vector2(1f, 0.5f);
        mbRt.offsetMin = Vector2.zero;
        mbRt.offsetMax = Vector2.zero;
        GameObject metalTxtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        metalTxtGo.transform.SetParent(metalBox.transform, false);
        TextMeshProUGUI metalTxt = metalTxtGo.GetComponent<TextMeshProUGUI>();
        metalTxt.fontSize = 12;
        metalTxt.color = new Color(0.45f, 0.85f, 1f);

        // 4. Lore Text
        GameObject loreGo = new GameObject("LoreText", typeof(RectTransform), typeof(TextMeshProUGUI));
        loreGo.transform.SetParent(root.transform, false);
        RectTransform lRt = loreGo.GetComponent<RectTransform>();
        lRt.anchorMin = new Vector2(0.05f, 0.10f);
        lRt.anchorMax = new Vector2(0.95f, 0.27f);
        lRt.offsetMin = Vector2.zero;
        lRt.offsetMax = Vector2.zero;
        TextMeshProUGUI loreTmp = loreGo.GetComponent<TextMeshProUGUI>();
        loreTmp.fontSize = 12;
        loreTmp.textWrappingMode = TextWrappingModes.Normal;
        loreTmp.color = new Color(0.85f, 0.88f, 0.92f);

        // 5. Action Prompt
        GameObject promptGo = new GameObject("ActionPrompt", typeof(RectTransform), typeof(TextMeshProUGUI));
        promptGo.transform.SetParent(root.transform, false);
        RectTransform pRt = promptGo.GetComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0.05f, 0.02f);
        pRt.anchorMax = new Vector2(0.95f, 0.09f);
        pRt.offsetMin = Vector2.zero;
        pRt.offsetMax = Vector2.zero;
        TextMeshProUGUI promptTmp = promptGo.GetComponent<TextMeshProUGUI>();
        promptTmp.fontSize = 13;
        promptTmp.fontStyle = FontStyles.Bold;
        promptTmp.alignment = TextAlignmentOptions.Center;

        CampaignTooltipUI tooltipComp = root.GetComponent<CampaignTooltipUI>();
        tooltipComp.InitializeReferences(
            root, rt, cg,
            titleTmp, subTmp, badgeTmp, badgeImg,
            captSecGo, captNameTmp, diffTierTmp, diffSkullsTmp, clanBuffTmp,
            rewSecGo, rewSummTmp,
            goldBox, goldTxt,
            bloodBox, bloodTxt,
            metalBox, metalTxt,
            loreTmp, promptTmp
        );

        root.SetActive(false);
        return root;
    }

    public static void CaptureProgressionScreenshots()
    {
        Debug.Log("[BuildAndCaptureCampaignMap] === Capturing Campaign Progression Screenshots ===");

        if (!Directory.Exists(ArtifactsDir)) Directory.CreateDirectory(ArtifactsDir);
        if (!Directory.Exists(ProjectScreenshotsDir)) Directory.CreateDirectory(ProjectScreenshotsDir);

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        CampaignMapUI mapUI = UnityEngine.Object.FindAnyObjectByType<CampaignMapUI>(FindObjectsInactive.Include);
        if (mapUI == null)
        {
            Debug.LogError("[BuildAndCaptureCampaignMap] CampaignMapUI not found in scene!");
            return;
        }

        Camera cam = Camera.main;
        if (cam == null) cam = UnityEngine.Object.FindAnyObjectByType<Camera>(FindObjectsInactive.Include);

        // Ensure CampaignManager exists
        GameObject mgrGo = new GameObject("CampaignManager", typeof(CampaignManager));
        CampaignManager mgr = mgrGo.GetComponent<CampaignManager>();

        CampaignGraphSO graph = CampaignGraphSO.CreateDefaultCampaignGraph();
        mgr.ActiveGraph = graph;
        mgr.StartCampaignRun(graph);

        CampaignTooltipUI tooltip = UnityEngine.Object.FindAnyObjectByType<CampaignTooltipUI>(FindObjectsInactive.Include);

        // Stage 1: Initial Start (Tier 1 Courtyard available, others locked)
        mgr.StartCampaignRun(graph);
        mapUI.BuildMap();
        PositionTooltipAtNode(tooltip, graph.FindNode("tier1_courtyard"), new Vector2(380f, 620f));
        CaptureCameraToPNG(cam, "campaign_map_01_tier1_start.png");

        // Stage 2: Tier 1 Cleared -> Tier 2 Choices (North Ramparts vs Armory Barracks), Hover Ramparts
        mgr.CompletedNodeIds.Add("tier1_courtyard");
        mgr.AvailableNodeIds.Clear();
        mgr.AvailableNodeIds.Add("tier2_ramparts");
        mgr.AvailableNodeIds.Add("tier2_armory");
        RunSession.InRunGold = 350;
        mapUI.RefreshMap();
        PositionTooltipAtNode(tooltip, graph.FindNode("tier2_ramparts"), new Vector2(580f, 750f));
        CaptureCameraToPNG(cam, "campaign_map_02_tier2_branch_fraglob.png");

        // Stage 3: Same Tier 2, Hover Armory Barracks (Captain Kombusta)
        PositionTooltipAtNode(tooltip, graph.FindNode("tier2_armory"), new Vector2(580f, 480f));
        CaptureCameraToPNG(cam, "campaign_map_03_tier2_branch_kombusta.png");

        // Stage 4: Tier 2 Cleared -> Tier 3 Stop Scenes (Rest Area vs Supply Room), Hover Supply Room
        mgr.CompletedNodeIds.Add("tier2_ramparts");
        mgr.AvailableNodeIds.Clear();
        mgr.AvailableNodeIds.Add("tier3_rest_area");
        mgr.AvailableNodeIds.Add("tier3_supply_room");
        RunSession.InRunGold = 600;
        mapUI.RefreshMap();
        PositionTooltipAtNode(tooltip, graph.FindNode("tier3_supply_room"), new Vector2(850f, 500f));
        CaptureCameraToPNG(cam, "campaign_map_04_tier3_supply_room.png");

        // Stage 5: Tier 3 Cleared -> Tier 4 Center Merge (Great Banqueting Hall)
        mgr.CompletedNodeIds.Add("tier3_supply_room");
        mgr.AvailableNodeIds.Clear();
        mgr.AvailableNodeIds.Add("tier4_great_hall");
        RunSession.InRunGold = 750;
        mapUI.RefreshMap();
        PositionTooltipAtNode(tooltip, graph.FindNode("tier4_great_hall"), new Vector2(1100f, 620f));
        CaptureCameraToPNG(cam, "campaign_map_05_tier4_center_merge.png");

        // Stage 6: Tier 4 Cleared -> Tier 5 Split (Dungeon Oubliette vs Royal Conservatory), Hover Oubliette
        mgr.CompletedNodeIds.Add("tier4_great_hall");
        mgr.AvailableNodeIds.Clear();
        mgr.AvailableNodeIds.Add("tier5_dungeons");
        mgr.AvailableNodeIds.Add("tier5_conservatory");
        RunSession.InRunGold = 950;
        mapUI.RefreshMap();
        PositionTooltipAtNode(tooltip, graph.FindNode("tier5_dungeons"), new Vector2(1350f, 750f));
        CaptureCameraToPNG(cam, "campaign_map_06_tier5_split.png");

        // Stage 7: Advances through Tier 6 to Tier 7 Pre-Boss & Highlights Tier 8 Revelation
        mgr.CompletedNodeIds.Add("tier5_dungeons");
        mgr.CompletedNodeIds.Add("tier6_inner_rest");
        mgr.AvailableNodeIds.Clear();
        mgr.AvailableNodeIds.Add("tier7_throne_antechamber");
        RunSession.InRunGold = 1300;
        mapUI.RefreshMap();
        PositionTooltipAtNode(tooltip, graph.FindNode("tier8_crypt_sanctum"), new Vector2(1480f, 620f));
        CaptureCameraToPNG(cam, "campaign_map_07_tier7_and_revelation.png");

        // Cleanup
        UnityEngine.Object.DestroyImmediate(mgrGo);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BuildAndCaptureCampaignMap] Captured all 7 progression screenshots successfully!");
    }

    private static void PositionTooltipAtNode(CampaignTooltipUI tooltip, CampaignNodeSO node, Vector2 screenPos)
    {
        if (tooltip == null || node == null) return;
        tooltip.Show(node, CampaignNodeButtonUI.NodeVisualStatus.Available, screenPos);
    }

    private static void CaptureCameraToPNG(Camera cam, string filename)
    {
        int width = 1920;
        int height = 1080;

        RenderTexture rt = new RenderTexture(width, height, 24);
        RenderTexture prevActive = RenderTexture.active;
        RenderTexture prevTarget = cam.targetTexture;

        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        cam.targetTexture = prevTarget;
        RenderTexture.active = prevActive;
        UnityEngine.Object.DestroyImmediate(rt);

        byte[] pngBytes = tex.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(tex);

        // Save to brain artifact directory
        string brainPath = Path.Combine(ArtifactsDir, filename);
        File.WriteAllBytes(brainPath, pngBytes);

        // Save to project directory
        string projPath = Path.Combine(ProjectScreenshotsDir, filename);
        File.WriteAllBytes(projPath, pngBytes);

        Debug.Log($"[BuildAndCaptureCampaignMap] Saved screenshot: {brainPath} and {projPath} ({pngBytes.Length / 1024} KB)");
    }
}
#endif
