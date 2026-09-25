using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
///     Right-side sidebar component for Survivors Mode.
///     Displays player class, HP, core stats (melee/ranged damage, crit chance, move speed),
///     and an acquired skills list with interactive hover tooltips.
///     Shared between <see cref="SurvivorsCardSelectUI"/> and <see cref="DeathScreen"/>.
/// </summary>
public class SurvivorsPlayerInfoSidebarUI : MonoBehaviour
{
    [Header("Player Info")]
    [SerializeField] private TMP_Text classNameText;
    [SerializeField] private TMP_Text healthText;

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

    [Header("Tooltip Reference")]
    [Tooltip("SkillTooltip component to show when hovering over an acquired skill.")]
    [SerializeField] private SkillTooltip tooltip;

    private readonly List<GameObject> spawnedSkillItems = new List<GameObject>();

    private void Awake()
    {
        if (tooltip == null)
        {
            tooltip = GetComponentInChildren<SkillTooltip>(true);
            if (tooltip == null)
            {
                tooltip = UnityEngine.Object.FindAnyObjectByType<SkillTooltip>(FindObjectsInactive.Include);
            }
        }
    }

    private void OnEnable()
    {
        RefreshSidebar();
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
            SetupSkillItemData(itemGO, node, level, icon);
        }
    }

    private void SetupSkillItemData(GameObject itemGO, SkillNode node, int level, Sprite icon)
    {
        Image iconImg = itemGO.transform.Find("Icon")?.GetComponent<Image>() ?? itemGO.GetComponentInChildren<Image>();
        TMP_Text nameLbl = itemGO.transform.Find("Name")?.GetComponent<TMP_Text>() ?? itemGO.GetComponentInChildren<TMP_Text>();
        TMP_Text levelBadge = itemGO.transform.Find("LevelBadge")?.GetComponent<TMP_Text>();

        if (iconImg != null && icon != null)
        {
            iconImg.sprite = icon;
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

        // Attach Hover Trigger
        EventTrigger trigger = itemGO.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = itemGO.AddComponent<EventTrigger>();
        }
        trigger.triggers.Clear();

        // Pointer Enter
        EventTrigger.Entry enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener((data) =>
        {
            if (tooltip != null)
            {
                tooltip.ShowDirect(node.LocalizedDisplayName, node.LocalizedDescription);
            }
        });
        trigger.triggers.Add(enterEntry);

        // Pointer Exit
        EventTrigger.Entry exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener((data) =>
        {
            if (tooltip != null)
            {
                tooltip.Hide();
            }
        });
        trigger.triggers.Add(exitEntry);
    }
}
