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
    ///     allowing players to spend persistent Gold to upgrade stats that persist across all runs.
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
        }

        private void OnEnable()
        {
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
            }

            if (skillTree == null)
            {
#if UNITY_EDITOR
                skillTree = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillTreeSO>("Assets/Bladehold/Bladehold Scriptable Objects/SkillTree.asset");
#endif
            }

            if (skillTree == null || gridContent == null)
            {
                return;
            }

            // Clean previous cards
            for (int i = spawnedCards.Count - 1; i >= 0; i--)
            {
                if (spawnedCards[i] != null)
                {
                    Destroy(spawnedCards[i]);
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
                GameObject cardGO;

                if (metaSkillCardPrefab != null)
                {
                    cardGO = Instantiate(metaSkillCardPrefab, gridContent);
                }
                else
                {
                    cardGO = CreateDefaultMetaCard(node, level, skillTree.GetIcon(node.iconName));
                }

                if (cardGO != null)
                {
                    spawnedCards.Add(cardGO);
                    SetupCardData(cardGO, node, level, skillTree.GetIcon(node.iconName));
                }
            }
        }

        private void SetupCardData(GameObject cardGO, SkillNode node, int level, Sprite icon)
        {
            TMP_Text titleTxt = cardGO.transform.Find("Title")?.GetComponent<TMP_Text>();
            TMP_Text descTxt = cardGO.transform.Find("Description")?.GetComponent<TMP_Text>();
            TMP_Text levelTxt = cardGO.transform.Find("LevelBadge")?.GetComponent<TMP_Text>();
            Image iconImg = cardGO.transform.Find("Icon")?.GetComponent<Image>();
            Button buyBtn = cardGO.transform.Find("BuyButton")?.GetComponent<Button>() ?? cardGO.GetComponentInChildren<Button>();
            TMP_Text btnTxt = buyBtn != null ? buyBtn.GetComponentInChildren<TMP_Text>() : null;

            if (titleTxt != null) titleTxt.text = node.LocalizedDisplayName;
            if (descTxt != null)
            {
                descTxt.text = level > 0 && !string.IsNullOrEmpty(node.LocalizedUpgradeText)
                    ? node.LocalizedUpgradeText
                    : node.LocalizedDescription;
            }
            if (levelTxt != null)
            {
                levelTxt.text = node.maxLevel > 1 ? $"Lv. {level} / {node.maxLevel}" : (level >= 1 ? "UNLOCKED" : "LOCKED");
            }
            if (iconImg != null && icon != null)
            {
                iconImg.sprite = icon;
                iconImg.gameObject.SetActive(true);
            }

            bool isMaxed = level >= node.maxLevel;
            int nextCost = isMaxed ? 0 : node.CostForLevel(level + 1);
            bool canAfford = !isMaxed && currentSave.totalGold >= nextCost;

            if (buyBtn != null)
            {
                buyBtn.onClick.RemoveAllListeners();
                if (isMaxed)
                {
                    buyBtn.interactable = false;
                    if (btnTxt != null) btnTxt.text = "<color=#888888>MAXED</color>";
                }
                else
                {
                    buyBtn.interactable = canAfford;
                    if (btnTxt != null)
                    {
                        btnTxt.text = canAfford
                            ? $"<color=#FFD700>{nextCost:N0} Gold</color>"
                            : $"<color=#FF5555>{nextCost:N0} Gold</color>";
                    }
                    buyBtn.onClick.AddListener(() => OnBuyMetaSkill(node, nextCost));
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
            GameObject card = new GameObject(node.id, typeof(RectTransform), typeof(Image));
            card.transform.SetParent(gridContent, false);

            Image cardBg = card.GetComponent<Image>();
            cardBg.color = new Color(0.11f, 0.13f, 0.17f, 0.95f);

            // Inner Content Vertical Layout
            GameObject inner = new GameObject("Inner", typeof(RectTransform), typeof(VerticalLayoutGroup));
            inner.transform.SetParent(card.transform, false);
            RectTransform innerRT = inner.GetComponent<RectTransform>();
            innerRT.anchorMin = Vector2.zero;
            innerRT.anchorMax = Vector2.one;
            innerRT.sizeDelta = Vector2.zero;

            VerticalLayoutGroup vlg = inner.GetComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 6f;
            vlg.padding = new RectOffset(10, 10, 10, 10);

            // Header (Icon + Title + Level)
            GameObject header = new GameObject("Header", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            header.transform.SetParent(inner.transform, false);
            HorizontalLayoutGroup hlg = header.GetComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.spacing = 8f;

            GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(header.transform, false);
            Image img = iconGO.GetComponent<Image>();
            img.sprite = icon;
            RectTransform iconRT = iconGO.GetComponent<RectTransform>();
            iconRT.sizeDelta = new Vector2(36f, 36f);

            GameObject titleCol = new GameObject("TitleCol", typeof(RectTransform), typeof(VerticalLayoutGroup));
            titleCol.transform.SetParent(header.transform, false);
            VerticalLayoutGroup vlgCol = titleCol.GetComponent<VerticalLayoutGroup>();
            vlgCol.childControlWidth = true;
            vlgCol.childControlHeight = true;
            vlgCol.childForceExpandWidth = true;
            vlgCol.childForceExpandHeight = false;
            RectTransform titleColRT = titleCol.GetComponent<RectTransform>();
            titleColRT.sizeDelta = new Vector2(175f, 36f);

            GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGO.transform.SetParent(titleCol.transform, false);
            TextMeshProUGUI titleTMP = titleGO.GetComponent<TextMeshProUGUI>();
            titleTMP.text = node.LocalizedDisplayName;
            titleTMP.fontSize = 14;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.alignment = TextAlignmentOptions.MidlineLeft;

            GameObject lvlGO = new GameObject("LevelBadge", typeof(RectTransform), typeof(TextMeshProUGUI));
            lvlGO.transform.SetParent(titleCol.transform, false);
            TextMeshProUGUI lvlTMP = lvlGO.GetComponent<TextMeshProUGUI>();
            lvlTMP.text = $"Lv. {level} / {node.maxLevel}";
            lvlTMP.fontSize = 12;
            lvlTMP.color = new Color(0.95f, 0.8f, 0.3f);
            lvlTMP.alignment = TextAlignmentOptions.MidlineLeft;

            // Description
            GameObject descGO = new GameObject("Description", typeof(RectTransform), typeof(TextMeshProUGUI));
            descGO.transform.SetParent(inner.transform, false);
            TextMeshProUGUI descTMP = descGO.GetComponent<TextMeshProUGUI>();
            descTMP.text = node.LocalizedDescription;
            descTMP.fontSize = 11;
            descTMP.color = new Color(0.8f, 0.8f, 0.8f);
            LayoutElement descLE = descGO.AddComponent<LayoutElement>();
            descLE.preferredHeight = 70f;

            // Buy Button
            GameObject buyBtnGO = new GameObject("BuyButton", typeof(RectTransform), typeof(Button), typeof(Image));
            buyBtnGO.transform.SetParent(inner.transform, false);
            Image btnBg = buyBtnGO.GetComponent<Image>();
            btnBg.color = new Color(0.18f, 0.45f, 0.22f, 1f);
            LayoutElement btnLE = buyBtnGO.AddComponent<LayoutElement>();
            btnLE.preferredHeight = 34f;

            GameObject btnTxtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            btnTxtGO.transform.SetParent(buyBtnGO.transform, false);
            TextMeshProUGUI btnTMP = btnTxtGO.GetComponent<TextMeshProUGUI>();
            btnTMP.text = "Upgrade";
            btnTMP.fontSize = 13;
            btnTMP.fontStyle = FontStyles.Bold;
            btnTMP.alignment = TextAlignmentOptions.Center;
            RectTransform btnTxtRT = btnTxtGO.GetComponent<RectTransform>();
            btnTxtRT.anchorMin = Vector2.zero;
            btnTxtRT.anchorMax = Vector2.one;
            btnTxtRT.sizeDelta = Vector2.zero;

            return card;
        }
    }
}
