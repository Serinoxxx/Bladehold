using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bladehold.UI
{
    /// <summary>
    ///     Controls the Main Menu Meta-Progression Upgrades Screen.
    ///     Displays a grid of all permanent upgrades (marked with isMeta in the skill tree CSV),
    ///     ordered from cheapest to most expensive, instantiated via MetaSkillCardUI prefab.
    /// </summary>
    public class MetaProgressionGridUI : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("The main skill tree ScriptableObject containing meta upgrade definitions.")]
        [SerializeField] private SkillTreeSO skillTree;

        [Header("UI References")]
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private Transform gridContent;
        [SerializeField] private MetaSkillCardUI metaSkillCardPrefab;
        [SerializeField] private Button backButton;
        [SerializeField] private MainMenuManager mainMenuManager;
        [SerializeField] private SkillTooltip skillTooltip;

        [Header("Theme Assets")]
        [SerializeField] private TMP_FontAsset texturinaFont;
        [SerializeField] private Sprite parchmentCardSprite;
        [SerializeField] private Sprite parchmentButtonSprite;
        [SerializeField] private Sprite dividerLineSprite;

        private SaveData currentSave;
        private readonly List<GameObject> spawnedCards = new List<GameObject>();

        private void Awake()
        {
            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(HandleBackClicked);
            }
            if (mainMenuManager == null)
            {
                mainMenuManager = GetComponentInParent<MainMenuManager>() ?? UnityEngine.Object.FindAnyObjectByType<MainMenuManager>();
            }
            EnsureTooltipReference();
            LoadThemeAssets();
        }

        private void EnsureTooltipReference()
        {
            if (skillTooltip == null)
            {
                skillTooltip = GetComponentInChildren<SkillTooltip>(true) ?? UnityEngine.Object.FindAnyObjectByType<SkillTooltip>();
            }
#if UNITY_EDITOR
            if (skillTooltip == null)
            {
                var tooltipPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/UI/Tooltip.prefab");
                if (tooltipPrefab != null)
                {
                    Transform canvasTr = transform.root.GetComponentInChildren<Canvas>()?.transform ?? transform;
                    Transform existing = canvasTr.Find("Tooltip");
                    if (existing != null)
                    {
                        skillTooltip = existing.GetComponent<SkillTooltip>();
                    }
                    else
                    {
                        GameObject ttGO = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(tooltipPrefab, canvasTr);
                        ttGO.name = "Tooltip";
                        skillTooltip = ttGO.GetComponent<SkillTooltip>();
                        ttGO.SetActive(false);
                    }
                }
            }
#endif
        }

        private void LoadThemeAssets()
        {
            EnsureTooltipReference();
#if UNITY_EDITOR
            if (texturinaFont == null)
            {
                texturinaFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Synty/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF.asset");
            }
            if (parchmentCardSprite == null)
            {
                parchmentCardSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Box_Medium_ParchmentGradient_04.png");
            }
            if (parchmentButtonSprite == null)
            {
                parchmentButtonSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Box_Small_Parchment_01.png");
            }
            if (dividerLineSprite == null)
            {
                dividerLineSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Line_01.png");
            }
            if (metaSkillCardPrefab == null)
            {
                metaSkillCardPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<MetaSkillCardUI>("Assets/Bladehold/Bladehold Prefabs/UI/MetaSkillCard.prefab");
            }
#endif
        }

        private void OnEnable()
        {
            LoadThemeAssets();
            RefreshUI();
        }

        private void OnDisable()
        {
            if (skillTooltip != null)
            {
                skillTooltip.Hide();
            }
        }

        private void HandleBackClicked()
        {
            if (skillTooltip != null)
            {
                skillTooltip.Hide();
            }
            if (mainMenuManager != null)
            {
                mainMenuManager.ShowScreen(mainMenuManager.titleScreen);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void HandleCardHoverEnter(SkillNode node, int level, int cost, bool isMaxed)
        {
            if (skillTooltip != null)
            {
                skillTooltip.ShowDirect(node, level, cost, isMaxed);
            }
        }

        private void HandleCardHoverExit()
        {
            if (skillTooltip != null)
            {
                skillTooltip.Hide();
            }
        }

        /// <summary>
        ///     Reloads save data from disk and rebuilds the meta-progression grid ordered from cheapest to most expensive.
        /// </summary>
        public void RefreshUI()
        {
            LoadThemeAssets();
            currentSave = SaveSystem.Load();

            if (goldText != null)
            {
                goldText.text = $"<color=#FFD700>Gold: {currentSave.totalGold:N0}</color>";
                if (texturinaFont != null) goldText.font = texturinaFont;
            }

            if (skillTree == null)
            {
#if UNITY_EDITOR
                skillTree = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillTreeSO>("Assets/Bladehold/Bladehold Scripts/Upgrades/SkillTreeSO.asset");
#endif
            }

            if (skillTree == null || gridContent == null)
            {
                return;
            }

            // Clean all existing child cards in content
            for (int i = gridContent.childCount - 1; i >= 0; i--)
            {
                var child = gridContent.GetChild(i);
                if (child != null)
                {
                    DestroyImmediate(child.gameObject);
                }
            }
            spawnedCards.Clear();

            // Calculate current levels for all nodes
            Dictionary<string, int> metaLevels = new Dictionary<string, int>();
            if (currentSave.purchasedNodeIds != null)
            {
                foreach (string id in currentSave.purchasedNodeIds)
                {
                    if (metaLevels.TryGetValue(id, out int cur))
                    {
                        metaLevels[id] = cur + 1;
                    }
                    else
                    {
                        metaLevels[id] = 1;
                    }
                }
            }

            // Gather and sort all meta skills: cheapest to most expensive (with maxed nodes placed at the end)
            var metaNodes = skillTree.Nodes.Where(n => n != null && n.isMeta).ToList();

            var sortedNodes = metaNodes.OrderBy(n =>
            {
                int lvl = metaLevels.TryGetValue(n.id, out int l) ? l : 0;
                bool isMaxed = lvl >= n.maxLevel;
                int cost = isMaxed ? int.MaxValue : n.CostForLevel(lvl + 1);
                return (isMaxed ? 1 : 0, cost, n.displayName);
            }).ToList();

            // Instantiate card prefabs (using PrefabUtility.InstantiatePrefab in edit mode to preserve prefab link)
            foreach (SkillNode node in sortedNodes)
            {
                int level = metaLevels.TryGetValue(node.id, out int lvl) ? lvl : 0;
                Sprite icon = skillTree.GetIcon(node.iconName);

                MetaSkillCardUI cardUI = null;
#if UNITY_EDITOR
                if (!Application.isPlaying && metaSkillCardPrefab != null)
                {
                    GameObject instanceGO = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(metaSkillCardPrefab.gameObject, gridContent);
                    cardUI = instanceGO.GetComponent<MetaSkillCardUI>();
                }
                else
#endif
                {
                    if (metaSkillCardPrefab != null)
                    {
                        cardUI = Instantiate(metaSkillCardPrefab, gridContent);
                    }
                    else
                    {
                        cardUI = CreateFallbackMetaCard(node, level, icon);
                    }
                }

                if (cardUI != null)
                {
                    cardUI.name = $"Card_{node.id}";
                    spawnedCards.Add(cardUI.gameObject);
                    cardUI.OnCardHoverEnter += HandleCardHoverEnter;
                    cardUI.OnCardHoverExit += HandleCardHoverExit;
                    int nextCost = level >= node.maxLevel ? 0 : node.CostForLevel(level + 1);
                    cardUI.SetData(node, level, icon, currentSave.totalGold, texturinaFont, () => OnBuyMetaSkill(node, nextCost));
                }
            }
        }

        [ContextMenu("Rebuild Grid (Prefab Instances)")]
        public void RebuildGridInEditMode()
        {
            RefreshUI();
        }

        [ContextMenu("Clear Grid")]
        public void ClearGrid()
        {
            if (gridContent == null) return;
            for (int i = gridContent.childCount - 1; i >= 0; i--)
            {
                var child = gridContent.GetChild(i);
                if (child != null)
                {
                    DestroyImmediate(child.gameObject);
                }
            }
            spawnedCards.Clear();
        }

        private void OnBuyMetaSkill(SkillNode node, int cost)
        {
            if (node == null || currentSave.totalGold < cost) return;

            currentSave.totalGold -= cost;
            if (currentSave.purchasedNodeIds == null)
            {
                currentSave.purchasedNodeIds = new List<string>();
            }
            currentSave.purchasedNodeIds.Add(node.id);
            SaveSystem.Save(currentSave);

            RefreshUI();
        }

        private MetaSkillCardUI CreateFallbackMetaCard(SkillNode node, int level, Sprite icon)
        {
            GameObject card = new GameObject(node.id, typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(MetaSkillCardUI));
            card.transform.SetParent(gridContent, false);

            Image cardBg = card.GetComponent<Image>();
            if (parchmentCardSprite != null)
            {
                cardBg.sprite = parchmentCardSprite;
                cardBg.type = Image.Type.Sliced;
            }

            GameObject inner = new GameObject("Inner", typeof(RectTransform), typeof(VerticalLayoutGroup));
            inner.transform.SetParent(card.transform, false);
            RectTransform innerRT = inner.GetComponent<RectTransform>();
            innerRT.anchorMin = Vector2.zero;
            innerRT.anchorMax = Vector2.one;
            innerRT.sizeDelta = Vector2.zero;

            VerticalLayoutGroup vlg = inner.GetComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 6f;
            vlg.padding = new RectOffset(14, 14, 14, 14);

            GameObject header = new GameObject("Header", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            header.transform.SetParent(inner.transform, false);
            HorizontalLayoutGroup hlg = header.GetComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.spacing = 10f;
            LayoutElement headerLE = header.GetComponent<LayoutElement>();
            headerLE.preferredHeight = 40f;
            headerLE.flexibleHeight = 0f;

            GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(header.transform, false);
            Image img = iconGO.GetComponent<Image>();
            img.sprite = icon;
            img.preserveAspect = true;
            RectTransform iconRT = iconGO.GetComponent<RectTransform>();
            iconRT.sizeDelta = new Vector2(40f, 40f);

            GameObject titleCol = new GameObject("TitleCol", typeof(RectTransform), typeof(VerticalLayoutGroup));
            titleCol.transform.SetParent(header.transform, false);
            VerticalLayoutGroup vlgCol = titleCol.GetComponent<VerticalLayoutGroup>();
            vlgCol.childControlWidth = true;
            vlgCol.childControlHeight = false;
            vlgCol.childForceExpandWidth = true;
            vlgCol.childForceExpandHeight = false;
            vlgCol.spacing = 2f;
            RectTransform titleColRT = titleCol.GetComponent<RectTransform>();
            titleColRT.sizeDelta = new Vector2(185f, 40f);

            GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGO.transform.SetParent(titleCol.transform, false);
            TextMeshProUGUI titleTMP = titleGO.GetComponent<TextMeshProUGUI>();
            titleTMP.text = node.LocalizedDisplayName;
            titleTMP.fontSize = 14;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.alignment = TextAlignmentOptions.MidlineLeft;
            if (texturinaFont != null) titleTMP.font = texturinaFont;

            GameObject lvlGO = new GameObject("LevelBadge", typeof(RectTransform), typeof(TextMeshProUGUI));
            lvlGO.transform.SetParent(titleCol.transform, false);
            TextMeshProUGUI lvlTMP = lvlGO.GetComponent<TextMeshProUGUI>();
            lvlTMP.text = $"Lv. {level} / {node.maxLevel}";
            lvlTMP.fontSize = 12;
            lvlTMP.alignment = TextAlignmentOptions.MidlineLeft;
            if (texturinaFont != null) lvlTMP.font = texturinaFont;

            if (dividerLineSprite != null)
            {
                GameObject lineGO = new GameObject("Divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                lineGO.transform.SetParent(inner.transform, false);
                Image lineImg = lineGO.GetComponent<Image>();
                lineImg.sprite = dividerLineSprite;
                LayoutElement lineLE = lineGO.GetComponent<LayoutElement>();
                lineLE.preferredHeight = 4f;
                lineLE.flexibleHeight = 0f;
            }

            GameObject descGO = new GameObject("Description", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            descGO.transform.SetParent(inner.transform, false);
            TextMeshProUGUI descTMP = descGO.GetComponent<TextMeshProUGUI>();
            descTMP.text = node.LocalizedDescription;
            descTMP.fontSize = 11.5f;
            if (texturinaFont != null) descTMP.font = texturinaFont;
            LayoutElement descLE = descGO.GetComponent<LayoutElement>();
            descLE.preferredHeight = 75f;
            descLE.flexibleHeight = 1f;

            GameObject buyBtnGO = new GameObject("BuyButton", typeof(RectTransform), typeof(Button), typeof(Image), typeof(LayoutElement));
            buyBtnGO.transform.SetParent(inner.transform, false);
            Image btnBg = buyBtnGO.GetComponent<Image>();
            if (parchmentButtonSprite != null)
            {
                btnBg.sprite = parchmentButtonSprite;
                btnBg.type = Image.Type.Sliced;
            }
            LayoutElement btnLE = buyBtnGO.GetComponent<LayoutElement>();
            btnLE.preferredHeight = 36f;
            btnLE.flexibleHeight = 0f;

            GameObject btnTxtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            btnTxtGO.transform.SetParent(buyBtnGO.transform, false);
            TextMeshProUGUI btnTMP = btnTxtGO.GetComponent<TextMeshProUGUI>();
            btnTMP.fontSize = 13;
            btnTMP.fontStyle = FontStyles.Bold;
            btnTMP.alignment = TextAlignmentOptions.Center;
            if (texturinaFont != null) btnTMP.font = texturinaFont;
            RectTransform btnTxtRT = btnTxtGO.GetComponent<RectTransform>();
            btnTxtRT.anchorMin = Vector2.zero;
            btnTxtRT.anchorMax = Vector2.one;
            btnTxtRT.sizeDelta = Vector2.zero;

            MetaSkillCardUI cardUI = card.GetComponent<MetaSkillCardUI>();
            cardUI.AutoWireReferences();
            return cardUI;
        }
    }
}
