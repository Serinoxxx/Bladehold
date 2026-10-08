using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
///     Right-side sidebar component for Survivors Mode.
///     Displays player class, HP, core stats (melee/ranged damage, crit chance, move speed),
///     and an acquired skills list. Each skill row is hoverable and pad-focusable and shows a
///     <see cref="SidebarSkillTooltipUI"/> (name, level, description) built in code on first use.
///     Shared between <see cref="SurvivorsCardSelectUI"/> and <see cref="DeathScreen"/>; the draft modal
///     also calls <see cref="EnableDraftLayout"/> (big-icon rows) and <see cref="FitBeside"/> (scales the
///     panel up into the space beside the cards).
/// </summary>
public class SurvivorsPlayerInfoSidebarUI : MonoBehaviour
{
    [Header("Player Info")]
    [SerializeField] private TMP_Text classNameText;
    [SerializeField] private TMP_Text healthText;
    [Tooltip("Gate HP next to the player's, the number the wave choice hinges on. Hidden outside gate-defence scenes.")]
    [SerializeField] private TMP_Text gateHealthText;

    [Header("Core Stats")]
    [SerializeField] private TMP_Text meleeDamageText;
    [SerializeField] private TMP_Text rangedDamageText;
    [SerializeField] private TMP_Text critChanceText;
    [SerializeField] private TMP_Text moveSpeedText;

    [Header("Acquired Skills List")]
    [Tooltip("Container Transform with a LayoutGroup to hold acquired skill rows.")]
    [SerializeField] private Transform skillsListContainer;

    [Tooltip("Prefab for a single acquired skill row item.")]
    [SerializeField] private GameObject skillItemPrefab;

    [Header("Draft Modal Layout")]
    [Tooltip("Largest uniform scale the draft modal gives the panel; it shrinks toward 1 if the cards need the room (16:10).")]
    [SerializeField] private float draftMaxScale = 1.6f;

    [Tooltip("Space kept between the panel and the outermost card, in the modal's canvas units.")]
    [SerializeField] private float draftCardClearance = 48f;

    [Tooltip("Acquired-skill row height in the draft modal (panel units, before the panel scale).")]
    [SerializeField] private float draftSkillRowHeight = 66f;

    [Tooltip("Acquired-skill icon size in the draft modal (panel units, before the panel scale).")]
    [SerializeField] private float draftSkillIconSize = 58f;

    [Tooltip("Skill name font size in the draft modal (panel units).")]
    [SerializeField] private float draftSkillNameFontSize = 24f;

    [Tooltip("Level badge font size in the draft modal (panel units).")]
    [SerializeField] private float draftSkillLevelFontSize = 21f;

    private readonly List<GameObject> spawnedSkillItems = new List<GameObject>();

    private SidebarSkillTooltipUI tooltip;
    private bool draftLayout;
    private bool authoredRectCaptured;
    private Vector2 authoredSizeDelta;

    private void OnEnable()
    {
        RefreshSidebar();
    }

    private void OnDestroy()
    {
        if (tooltip != null)
        {
            Destroy(tooltip.gameObject);
        }
    }

    /// <summary>
    ///     Switches the acquired-skill rows to the draft modal's large size (big icons, wrapped names). Called
    ///     once by <see cref="SurvivorsCardSelectUI" />; the death screen keeps the prefab's compact rows.
    /// </summary>
    public void EnableDraftLayout()
    {
        if (draftLayout) return;
        draftLayout = true;
        ScrollRect scroll = skillsListContainer != null ? skillsListContainer.GetComponentInParent<ScrollRect>(true) : null;
        if (scroll != null && scroll.GetComponent<ScrollRectAutoScroll>() == null)
        {
            scroll.gameObject.AddComponent<ScrollRectAutoScroll>();
        }
        if (isActiveAndEnabled)
        {
            PopulateAcquiredSkills();
        }
    }

    /// <summary>
    ///     Scales the whole panel up (to <see cref="draftMaxScale" />) as far as the space beside
    ///     <paramref name="keepClearOf" />'s active children allows, keeps its authored on-screen height, and
    ///     stretches the skill list down to the panel's bottom. Call after the modal is active and its cards
    ///     are populated. Assumes the panel hugs the right edge (pivot x = 1), as in the HUD's draft modal.
    /// </summary>
    public void FitBeside(RectTransform keepClearOf)
    {
        RectTransform rt = (RectTransform)transform;
        RectTransform parent = rt.parent as RectTransform;
        if (parent == null) return;

        if (!authoredRectCaptured)
        {
            authoredSizeDelta = rt.sizeDelta;
            authoredRectCaptured = true;
        }

        float scale = Mathf.Max(1f, draftMaxScale);
        float panelWidth = rt.rect.width;
        if (keepClearOf != null && panelWidth > 0f)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(keepClearOf);
            float contentRight = float.NegativeInfinity;
            Vector3[] corners = new Vector3[4];
            for (int i = 0; i < keepClearOf.childCount; i++)
            {
                RectTransform child = keepClearOf.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf) continue;
                child.GetWorldCorners(corners);
                contentRight = Mathf.Max(contentRight, parent.InverseTransformPoint(corners[2]).x);
            }

            if (!float.IsNegativeInfinity(contentRight))
            {
                rt.GetWorldCorners(corners);
                float panelRight = parent.InverseTransformPoint(corners[2]).x;
                float available = panelRight - contentRight - draftCardClearance;
                scale = Mathf.Clamp(available / panelWidth, 1f, scale);
            }
        }

        rt.localScale = new Vector3(scale, scale, 1f);

        // A vertically stretched panel keeps its authored on-screen height: shrink its local height to match.
        if (!Mathf.Approximately(rt.anchorMin.y, rt.anchorMax.y))
        {
            float parentHeight = parent.rect.height;
            float onScreenHeight = parentHeight * (rt.anchorMax.y - rt.anchorMin.y) + authoredSizeDelta.y;
            rt.sizeDelta = new Vector2(authoredSizeDelta.x, onScreenHeight / scale - parentHeight * (rt.anchorMax.y - rt.anchorMin.y));
        }

        FitSkillListToPanel(rt);
    }

    /// <summary>Gives the skill scroll view every pixel between its top and the panel's bottom padding.</summary>
    private void FitSkillListToPanel(RectTransform panel)
    {
        ScrollRect scroll = skillsListContainer != null ? skillsListContainer.GetComponentInParent<ScrollRect>(true) : null;
        if (scroll == null) return;
        RectTransform view = (RectTransform)scroll.transform;
        if (view.parent != panel || !Mathf.Approximately(view.anchorMin.y, view.anchorMax.y)) return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        Vector3[] corners = new Vector3[4];
        view.GetWorldCorners(corners);
        float top = panel.InverseTransformPoint(corners[1]).y;
        LayoutGroup group = panel.GetComponent<LayoutGroup>();
        float bottom = panel.rect.yMin + (group != null ? group.padding.bottom : 0f);
        float height = top - bottom;
        if (height <= 0f) return;

        view.sizeDelta = new Vector2(view.sizeDelta.x, height);

        // The authored content stretches with a centred pivot, so a short list would float mid-view in the
        // taller scroll area: pin it to the top so rows start right under the "Acquired Skills" header.
        RectTransform content = scroll.content;
        if (content != null)
        {
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = new Vector2(0f, 0f);
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
    }

    /// <summary>
    ///     Refreshes all player information, core stats, and the acquired skills list.
    /// </summary>
    public void RefreshSidebar()
    {
        PopulatePlayerInfo();
        PopulateCoreStats();
        PopulateAcquiredSkills();
    }

    private void PopulatePlayerInfo()
    {
        Player player = Player.Instance;

        if (classNameText != null)
        {
            classNameText.text = "HERO";
        }

        if (healthText != null)
        {
            if (player != null && player.Health != null)
            {
                int currentHp = Mathf.Max(0, Mathf.RoundToInt(player.Health.CurrentHealth));
                int maxHp = Mathf.RoundToInt(player.Health.MaxHealth);
                healthText.text = $"{currentHp} / {maxHp} HP";
            }
            else
            {
                healthText.text = "100 / 100 HP";
            }
        }

        if (gateHealthText != null)
        {
            // Gate mirrors its Health into RunSession on every change, so this is live mid-sector too.
            bool hasGate = RunSession.FortressGateMaxHealth > 0f;
            gateHealthText.gameObject.SetActive(hasGate);
            if (hasGate)
            {
                int gateHp = Mathf.Max(0, Mathf.RoundToInt(RunSession.FortressGateCurrentHealth));
                int gateMax = Mathf.RoundToInt(RunSession.FortressGateMaxHealth);
                gateHealthText.text = Loc.Get("sidebar.gate_hp", "Gate: {0} / {1} HP")
                    .Replace("{0}", gateHp.ToString()).Replace("{1}", gateMax.ToString());
            }
        }
    }

    private void PopulateCoreStats()
    {
        PlayerStats stats = Player.Instance != null ? Player.Instance.Stats : null;
        if (stats == null)
        {
            stats = UnityEngine.Object.FindAnyObjectByType<PlayerStats>();
        }

        if (stats == null) return;

        // Melee Damage
        if (meleeDamageText != null)
        {
            float meleeVal = stats.GetValue(StatType.SwordDamage);
            if (meleeVal <= 0f) meleeVal = stats.GetBase(StatType.SwordDamage);
            if (meleeVal <= 0f) meleeVal = 10f; // Swordsman fallback base
            meleeDamageText.text = StatDisplay.Value(StatType.SwordDamage, meleeVal);
        }

        // Ranged Damage
        if (rangedDamageText != null)
        {
            float bowVal = stats.GetValue(StatType.BowDamage);
            float wandVal = stats.GetValue(StatType.WandDamage);
            float rangedVal = Mathf.Max(bowVal, wandVal);
            if (rangedVal <= 0f) rangedVal = stats.GetBase(StatType.BowDamage);
            if (rangedVal <= 0f) rangedVal = stats.GetBase(StatType.WandDamage);
            rangedDamageText.text = rangedVal > 0f ? StatDisplay.Value(StatType.BowDamage, rangedVal) : "-";
        }

        // Crit Chance
        if (critChanceText != null)
        {
            float critVal = stats.GetValue(StatType.CritChance);
            critChanceText.text = StatDisplay.Value(StatType.CritChance, critVal);
        }

        // Move Speed
        if (moveSpeedText != null)
        {
            float speedVal = stats.GetValue(StatType.MoveSpeed);
            if (speedVal <= 0f) speedVal = 1f;
            moveSpeedText.text = StatDisplay.Value(StatType.MoveSpeed, speedVal);
        }
    }

    private void PopulateAcquiredSkills()
    {
        if (skillsListContainer == null) return;
        if (skillItemPrefab == null)
        {
            Debug.LogError("[SurvivorsPlayerInfoSidebarUI] skillItemPrefab is not assigned (UI/SidebarSkillRow.prefab).", this);
            return;
        }

        if (tooltip != null)
        {
            tooltip.Hide(null);
        }

        // Clear previous rows
        for (int i = spawnedSkillItems.Count - 1; i >= 0; i--)
        {
            if (spawnedSkillItems[i] != null)
            {
                Destroy(spawnedSkillItems[i]);
            }
        }
        spawnedSkillItems.Clear();

        PopulateFromDraftService();
    }

    private void PopulateFromDraftService()
    {
        DraftUpgradeService draftService = DraftUpgradeService.Instance ?? DraftUpgradeService.GetOrCreateInstance();
        if (draftService == null) return;

        foreach (var kvp in RunSession.InRunUpgradeLevels)
        {
            string upgradeId = kvp.Key;
            int level = kvp.Value;
            if (level <= 0) continue;

            DraftUpgradeDefinition def = draftService.GetById(upgradeId);
            if (def == null) continue;

            SkillNode node = draftService.ConvertToSkillNode(def);
            Sprite icon = draftService.GetIcon(def.iconName);

            GameObject itemGO = Instantiate(skillItemPrefab, skillsListContainer);
            spawnedSkillItems.Add(itemGO);
            Image itemIcon = SetupSkillItemData(itemGO, node, level, icon);

            // Elemental skills show their element's icon colour, matching the draft cards.
            if (itemIcon != null && icon != null && draftService.TryGetElementIconTint(node.element, out Color tint))
            {
                itemIcon.color = tint;
            }

            BindInspect(itemGO, BuildTooltipContent(def, node, level, icon, itemIcon != null ? itemIcon.color : Color.white));
        }
    }

    /// <summary>The acquired skill's tooltip: same CSV/localized text the draft card showed, plus its current level.</summary>
    private static SidebarSkillTooltipUI.Content BuildTooltipContent(DraftUpgradeDefinition def, SkillNode node, int level, Sprite icon, Color iconColor)
    {
        string levelLine;
        if (node.maxLevel > 1)
        {
            levelLine = Loc.Get("sidebar.skill_level", "Level {0} / {1}")
                .Replace("{0}", level.ToString()).Replace("{1}", node.maxLevel.ToString());
            if (level >= node.maxLevel)
            {
                levelLine += "  (" + Loc.Get("common.maxed", "Maxed") + ")";
            }
        }
        else
        {
            levelLine = Loc.Get("sidebar.skill_owned", "Acquired");
        }
        if (def.isUltimate)
        {
            levelLine = Loc.Get("sidebar.skill_ultimate", "Ultimate") + "  ·  " + levelLine;
        }

        // The card's own description (not ConvertToSkillNode's, which may carry a pick-time [Overwrite] warning).
        string description = string.IsNullOrEmpty(node.locKey) ? def.description : node.LocalizedDescription;
        string upgradeLine = "";
        if (level > 1 && !string.IsNullOrEmpty(node.LocalizedUpgradeText))
        {
            upgradeLine = Loc.Get("sidebar.skill_upgraded", "Upgraded {0}x: {1}")
                .Replace("{0}", (level - 1).ToString()).Replace("{1}", node.LocalizedUpgradeText);
        }

        return new SidebarSkillTooltipUI.Content
        {
            title = node.LocalizedDisplayName,
            subtitle = levelLine,
            description = description,
            upgradeLine = upgradeLine,
            icon = icon,
            iconColor = iconColor
        };
    }

    /// <summary>Makes a row hoverable and pad-focusable, showing its tooltip (built on first use).</summary>
    private void BindInspect(GameObject itemGO, SidebarSkillTooltipUI.Content content)
    {
        SidebarSkillTooltipUI skillTooltip = GetOrCreateTooltip();

        // The prefab's old EventTrigger swallowed scroll-wheel events over the rows; the entry replaces it.
        EventTrigger trigger = itemGO.GetComponent<EventTrigger>();
        if (trigger != null)
        {
            Destroy(trigger);
        }

        Selectable selectable = itemGO.GetComponent<Selectable>();
        if (selectable == null)
        {
            selectable = itemGO.AddComponent<Selectable>();
        }
        selectable.transition = Selectable.Transition.None;
        selectable.navigation = new Navigation { mode = Navigation.Mode.Automatic };

        SidebarSkillEntryUI entry = itemGO.GetComponent<SidebarSkillEntryUI>();
        if (entry == null)
        {
            entry = itemGO.AddComponent<SidebarSkillEntryUI>();
        }
        Image background = itemGO.GetComponent<Image>();
        entry.Bind(skillTooltip, content, background, UITheme.For(this).Get(UIColorRole.Accent, 0.28f));
    }

    private SidebarSkillTooltipUI GetOrCreateTooltip()
    {
        if (tooltip != null) return tooltip;

        UIThemeSO theme = UITheme.For(this);
        TMP_FontAsset headerFont = theme.GetFont(UIFontRole.Header);
        if (headerFont == null && classNameText != null) headerFont = classNameText.font;
        TMP_FontAsset bodyFont = theme.GetFont(UIFontRole.Body);
        if (bodyFont == null && healthText != null) bodyFont = healthText.font;

        tooltip = SidebarSkillTooltipUI.Create((RectTransform)transform, theme, headerFont, bodyFont);
        return tooltip;
    }

    /// <summary>Fills one row and, in the draft layout, resizes it; returns the row's icon image.</summary>
    private Image SetupSkillItemData(GameObject itemGO, SkillNode node, int level, Sprite icon)
    {
        Image iconImg = itemGO.transform.Find("Icon")?.GetComponent<Image>();
        TMP_Text nameLbl = itemGO.transform.Find("Name")?.GetComponent<TMP_Text>() ?? itemGO.GetComponentInChildren<TMP_Text>();
        TMP_Text levelBadge = itemGO.transform.Find("LevelBadge")?.GetComponent<TMP_Text>();

        if (iconImg != null && icon != null)
        {
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            iconImg.gameObject.SetActive(true);
        }

        if (nameLbl != null)
        {
            nameLbl.text = node.LocalizedDisplayName;
        }

        if (levelBadge != null)
        {
            levelBadge.text = node.maxLevel > 1 ? $"Lv. {level}/{node.maxLevel}" : $"Lv. {level}";
        }

        if (draftLayout)
        {
            ApplyDraftRowSize(itemGO, iconImg, nameLbl, levelBadge);
        }

        return iconImg;
    }

    /// <summary>Big-icon row for the draft modal: taller row, larger icon and text, names wrap instead of overflowing.</summary>
    private void ApplyDraftRowSize(GameObject itemGO, Image iconImg, TMP_Text nameLbl, TMP_Text levelBadge)
    {
        RectTransform row = (RectTransform)itemGO.transform;
        row.sizeDelta = new Vector2(row.sizeDelta.x, draftSkillRowHeight);
        LayoutElement rowLe = itemGO.GetComponent<LayoutElement>();
        if (rowLe != null)
        {
            rowLe.minHeight = draftSkillRowHeight;
        }

        HorizontalLayoutGroup rowLayout = itemGO.GetComponent<HorizontalLayoutGroup>();
        if (rowLayout != null)
        {
            rowLayout.padding = new RectOffset(8, 10, 4, 4);
            rowLayout.spacing = 12f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
        }

        if (iconImg != null)
        {
            LayoutElement iconLe = iconImg.GetComponent<LayoutElement>();
            if (iconLe == null) iconLe = iconImg.gameObject.AddComponent<LayoutElement>();
            iconLe.minWidth = iconLe.preferredWidth = draftSkillIconSize;
            iconLe.minHeight = iconLe.preferredHeight = draftSkillIconSize;
        }

        if (nameLbl != null)
        {
            nameLbl.fontSize = draftSkillNameFontSize;
            nameLbl.textWrappingMode = TextWrappingModes.Normal;
            LayoutElement nameLe = nameLbl.GetComponent<LayoutElement>();
            if (nameLe == null) nameLe = nameLbl.gameObject.AddComponent<LayoutElement>();
            nameLe.minWidth = 0f;
            nameLe.flexibleWidth = 1f;
        }

        if (levelBadge != null)
        {
            levelBadge.fontSize = draftSkillLevelFontSize;
            LayoutElement levelLe = levelBadge.GetComponent<LayoutElement>();
            if (levelLe == null) levelLe = levelBadge.gameObject.AddComponent<LayoutElement>();
            levelLe.preferredWidth = draftSkillLevelFontSize * 3.4f;
            levelLe.flexibleWidth = 0f;
        }
    }
}
