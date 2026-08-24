using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bladehold.UI
{
    /// <summary>
    ///     Controls the Main Menu Meta-Progression Upgrades Screen.
    ///     Displays a grid of all permanent upgrades (marked with isMeta in the skill tree CSV),
    ///     styled with the Synty Fantasy Warrior parchment theme, Texturina typography,
    ///     and clear visual affordance/dimming for affordable vs unaffordable upgrades.
    /// </summary>
    public class MetaProgressionGridUI : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("The main skill tree ScriptableObject containing meta upgrade definitions.")]
        [SerializeField] private SkillTreeSO skillTree;

        [Header("UI References")]
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private Transform gridContent;
        [SerializeField] private GameObject metaSkillCardPrefab;
        [SerializeField] private Button backButton;
        [SerializeField] private MainMenuManager mainMenuManager;

        [Header("Theme Assets (Loaded dynamically if null)")]
        [SerializeField] private TMP_FontAsset texturinaFont;
        [SerializeField] private Sprite parchmentCardSprite;
        [SerializeField] private Sprite parchmentButtonSprite;
        [SerializeField] private Sprite dividerLineSprite;

        private SaveData currentSave;
        private readonly List<GameObject> spawnedCards = new List<GameObject>();

        private readonly Color parchmentNormal = new Color(0.773f, 0.745f, 0.659f, 1f); // #C5BEA8
        private readonly Color parchmentDimmed = new Color(0.48f, 0.45f, 0.40f, 0.95f);
        private readonly Color textDarkBrown = new Color(0.329f, 0.282f, 0.239f, 1f); // #54483D
        private readonly Color textBodyBrown = new Color(0.388f, 0.337f, 0.294f, 1f); // #63564B
        private readonly Color badgeLevelColor = new Color(0.55f, 0.28f, 0.15f, 1f); // #8C4726
        private readonly Color goldPriceColor = new Color(1f, 0.820f, 0.439f, 1f); // #FFD170

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
            LoadThemeAssets();
        }

        private void LoadThemeAssets()
        {
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
#endif
        }

        private void OnEnable()
        {
            LoadThemeAssets();
            RefreshUI();
        }

        private void HandleBackClicked()
        {
            if (mainMenuManager != null)
            {
                mainMenuManager.OnBackToTitle();
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        ///     Reloads save data from disk and rebuilds the meta-progression grid.
        /// </summary>
        public void RefreshUI()
        {
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

            // Ensure grid layout sizing
            GridLayoutGroup glg = gridContent.GetComponent<GridLayoutGroup>();
            if (glg != null)
            {
                glg.cellSize = new Vector2(270f, 245f);
                glg.spacing = new Vector2(16f, 16f);
                glg.padding = new RectOffset(20, 20, 20, 20);
                glg.childAlignment = TextAnchor.UpperCenter;
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

            // Populate all meta skills
            IReadOnlyList<SkillNode> allNodes = skillTree.Nodes;
            foreach (SkillNode node in allNodes)
            {
                if (node == null || !node.isMeta) continue;

                int level = metaLevels.TryGetValue(node.id, out int lvl) ? lvl : 0;
                GameObject cardGO = CreateDefaultMetaCard(node, level, skillTree.GetIcon(node.iconName));

                if (cardGO != null)
                {
                    spawnedCards.Add(cardGO);
                    SetupCardData(cardGO, node, level, skillTree.GetIcon(node.iconName));
                }
            }
        }

        private void SetupCardData(GameObject cardGO, SkillNode node, int level, Sprite icon)
        {
            CanvasGroup cg = cardGO.GetComponent<CanvasGroup>();
            Image cardBg = cardGO.GetComponent<Image>();

            TMP_Text titleTxt = cardGO.transform.Find("Inner/Header/TitleCol/Title")?.GetComponent<TMP_Text>();
            TMP_Text descTxt = cardGO.transform.Find("Inner/Description")?.GetComponent<TMP_Text>();
            TMP_Text levelTxt = cardGO.transform.Find("Inner/Header/TitleCol/LevelBadge")?.GetComponent<TMP_Text>();
            Image iconImg = cardGO.transform.Find("Inner/Header/Icon")?.GetComponent<Image>();
            Button buyBtn = cardGO.transform.Find("Inner/BuyButton")?.GetComponent<Button>() ?? cardGO.GetComponentInChildren<Button>();
            Image buyBtnBg = buyBtn != null ? buyBtn.GetComponent<Image>() : null;
            TMP_Text btnTxt = buyBtn != null ? buyBtn.GetComponentInChildren<TMP_Text>() : null;

            if (titleTxt != null)
            {
                titleTxt.text = node.LocalizedDisplayName;
                titleTxt.color = new Color(0.24f, 0.17f, 0.11f, 1f); // #3D2B1C
                if (texturinaFont != null) titleTxt.font = texturinaFont;
            }

            if (descTxt != null)
            {
                descTxt.text = level > 0 && !string.IsNullOrEmpty(node.LocalizedUpgradeText)
                    ? node.LocalizedUpgradeText
                    : node.LocalizedDescription;
                descTxt.color = new Color(0.32f, 0.25f, 0.19f, 1f); // #524030
                if (texturinaFont != null) descTxt.font = texturinaFont;
            }

            if (levelTxt != null)
            {
                levelTxt.text = node.maxLevel > 1 ? $"Lv. {level} / {node.maxLevel}" : (level >= 1 ? "UNLOCKED" : "LOCKED");
                levelTxt.color = new Color(0.58f, 0.25f, 0.10f, 1f); // #94401A
                if (texturinaFont != null) levelTxt.font = texturinaFont;
            }

            if (iconImg != null && icon != null)
            {
                iconImg.sprite = icon;
                iconImg.gameObject.SetActive(true);
            }

            bool isMaxed = level >= node.maxLevel;
            int nextCost = isMaxed ? 0 : node.CostForLevel(level + 1);
            bool canAfford = !isMaxed && currentSave.totalGold >= nextCost;

            // Visual Affordance Dimming
            if (isMaxed)
            {
                if (cg != null) cg.alpha = 0.40f;
                if (cardBg != null) cardBg.color = parchmentDimmed;
                if (buyBtn != null) buyBtn.interactable = false;
                if (buyBtnBg != null) buyBtnBg.color = new Color(0.42f, 0.38f, 0.35f, 0.9f);
                if (btnTxt != null)
                {
                    btnTxt.text = "MAXED";
                    btnTxt.color = new Color(0.60f, 0.58f, 0.55f, 1f);
                    if (texturinaFont != null) btnTxt.font = texturinaFont;
                }
            }
            else if (canAfford)
            {
                if (cg != null) cg.alpha = 1.0f;
                if (cardBg != null) cardBg.color = parchmentNormal;
                if (buyBtn != null)
                {
                    buyBtn.interactable = true;
                    buyBtn.onClick.RemoveAllListeners();
                    buyBtn.onClick.AddListener(() => OnBuyMetaSkill(node, nextCost));
                }
                if (buyBtnBg != null) buyBtnBg.color = new Color(0.92f, 0.74f, 0.40f, 1f); // Rich gold parchment
                if (btnTxt != null)
                {
                    btnTxt.text = $"<b>{nextCost:N0} Gold</b>";
                    btnTxt.color = new Color(0.22f, 0.15f, 0.08f, 1f); // Dark bold brown on gold button
                    if (texturinaFont != null) btnTxt.font = texturinaFont;
                }
            }
            else
            {
                // Unaffordable -> Dimmed/Darkened
                if (cg != null) cg.alpha = 0.48f;
                if (cardBg != null) cardBg.color = parchmentDimmed;
                if (buyBtn != null) buyBtn.interactable = false;
                if (buyBtnBg != null) buyBtnBg.color = new Color(0.38f, 0.33f, 0.30f, 0.95f);
                if (btnTxt != null)
                {
                    btnTxt.text = $"Need {nextCost:N0} Gold";
                    btnTxt.color = new Color(0.95f, 0.35f, 0.30f, 1f); // Red cost
                    if (texturinaFont != null) btnTxt.font = texturinaFont;
                }
            }
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

        private GameObject CreateDefaultMetaCard(SkillNode node, int level, Sprite icon)
        {
            LoadThemeAssets();

            GameObject card = new GameObject(node.id, typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            card.transform.SetParent(gridContent, false);

            Image cardBg = card.GetComponent<Image>();
            if (parchmentCardSprite != null)
            {
                cardBg.sprite = parchmentCardSprite;
                cardBg.type = Image.Type.Sliced;
            }
            cardBg.color = parchmentNormal;

            // Inner Content Vertical Layout
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

            // Header (Icon + TitleCol)
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
            titleTMP.color = textDarkBrown;
            if (texturinaFont != null) titleTMP.font = texturinaFont;

            GameObject lvlGO = new GameObject("LevelBadge", typeof(RectTransform), typeof(TextMeshProUGUI));
            lvlGO.transform.SetParent(titleCol.transform, false);
            TextMeshProUGUI lvlTMP = lvlGO.GetComponent<TextMeshProUGUI>();
            lvlTMP.text = $"Lv. {level} / {node.maxLevel}";
            lvlTMP.fontSize = 12;
            lvlTMP.color = badgeLevelColor;
            lvlTMP.alignment = TextAlignmentOptions.MidlineLeft;
            if (texturinaFont != null) lvlTMP.font = texturinaFont;

            // Divider Line
            if (dividerLineSprite != null)
            {
                GameObject lineGO = new GameObject("Divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                lineGO.transform.SetParent(inner.transform, false);
                Image lineImg = lineGO.GetComponent<Image>();
                lineImg.sprite = dividerLineSprite;
                lineImg.color = new Color(textDarkBrown.r, textDarkBrown.g, textDarkBrown.b, 0.45f);
                LayoutElement lineLE = lineGO.GetComponent<LayoutElement>();
                lineLE.preferredHeight = 4f;
                lineLE.flexibleHeight = 0f;
            }

            // Description
            GameObject descGO = new GameObject("Description", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            descGO.transform.SetParent(inner.transform, false);
            TextMeshProUGUI descTMP = descGO.GetComponent<TextMeshProUGUI>();
            descTMP.text = node.LocalizedDescription;
            descTMP.fontSize = 11.5f;
            descTMP.color = textBodyBrown;
            if (texturinaFont != null) descTMP.font = texturinaFont;
            LayoutElement descLE = descGO.GetComponent<LayoutElement>();
            descLE.preferredHeight = 75f;
            descLE.flexibleHeight = 1f;

            // Buy Button
            GameObject buyBtnGO = new GameObject("BuyButton", typeof(RectTransform), typeof(Button), typeof(Image), typeof(LayoutElement));
            buyBtnGO.transform.SetParent(inner.transform, false);
            Image btnBg = buyBtnGO.GetComponent<Image>();
            if (parchmentButtonSprite != null)
            {
                btnBg.sprite = parchmentButtonSprite;
                btnBg.type = Image.Type.Sliced;
            }
            btnBg.color = new Color(0.92f, 0.76f, 0.45f, 1f);
            LayoutElement btnLE = buyBtnGO.GetComponent<LayoutElement>();
            btnLE.preferredHeight = 36f;
            btnLE.flexibleHeight = 0f;

            GameObject btnTxtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            btnTxtGO.transform.SetParent(buyBtnGO.transform, false);
            TextMeshProUGUI btnTMP = btnTxtGO.GetComponent<TextMeshProUGUI>();
            btnTMP.text = "Upgrade";
            btnTMP.fontSize = 13;
            btnTMP.fontStyle = FontStyles.Bold;
            btnTMP.alignment = TextAlignmentOptions.Center;
            btnTMP.color = textDarkBrown;
            if (texturinaFont != null) btnTMP.font = texturinaFont;
            RectTransform btnTxtRT = btnTxtGO.GetComponent<RectTransform>();
            btnTxtRT.anchorMin = Vector2.zero;
            btnTxtRT.anchorMax = Vector2.one;
            btnTxtRT.sizeDelta = Vector2.zero;

            return card;
        }
    }
}
