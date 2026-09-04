using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
///     Service responsible for parsing DraftUpgrades.csv, evaluating mid-run draft candidate cards,
///     and applying chosen upgrades to PlayerStats and RunSession.
/// </summary>
public class DraftUpgradeService : MonoBehaviour
{
    public static DraftUpgradeService Instance { get; private set; }

    [SerializeField] private TextAsset draftUpgradesCsv;

    private readonly List<DraftUpgradeDefinition> allDefinitions = new List<DraftUpgradeDefinition>();
    private readonly Dictionary<string, DraftUpgradeDefinition> byId = new Dictionary<string, DraftUpgradeDefinition>(StringComparer.OrdinalIgnoreCase);
    private bool isInitialized = false;

    public IReadOnlyList<DraftUpgradeDefinition> AllDefinitions
    {
        get
        {
            EnsureInitialized();
            return allDefinitions;
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            EnsureInitialized();
        }
        else if (Instance != this)
        {
            Destroy(this);
        }
    }

    public static DraftUpgradeService GetOrCreateInstance()
    {
        if (Instance != null) return Instance;
        Instance = FindAnyObjectByType<DraftUpgradeService>();
        if (Instance != null)
        {
            Instance.EnsureInitialized();
            return Instance;
        }

        GameObject go = new GameObject("DraftUpgradeService");
        Instance = go.AddComponent<DraftUpgradeService>();
        Instance.EnsureInitialized();
        return Instance;
    }

    public void EnsureInitialized()
    {
        if (isInitialized) return;
        isInitialized = true;
        ParseCsv();
    }

    private void ParseCsv()
    {
        allDefinitions.Clear();
        byId.Clear();

        string csvText = null;
        if (draftUpgradesCsv != null)
        {
            csvText = draftUpgradesCsv.text;
        }
        else
        {
            string path = Path.Combine(Application.dataPath, "Bladehold/Config/DraftUpgrades.csv");
            if (File.Exists(path))
            {
                csvText = File.ReadAllText(path);
            }
        }

        if (string.IsNullOrEmpty(csvText))
        {
            Debug.LogWarning("[DraftUpgradeService] DraftUpgrades.csv not found or empty!");
            return;
        }

        using (StringReader reader = new StringReader(csvText))
        {
            string headerLine = reader.ReadLine(); // Skip header
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                DraftUpgradeDefinition def = ParseRow(line);
                if (def != null && !string.IsNullOrEmpty(def.id))
                {
                    allDefinitions.Add(def);
                    byId[def.id] = def;
                }
            }
        }

        Debug.Log($"[DraftUpgradeService] Successfully loaded {allDefinitions.Count} draft upgrades from CSV.");
    }

    private DraftUpgradeDefinition ParseRow(string row)
    {
        List<string> cols = ParseCsvRow(row);
        if (cols.Count < 10) return null;

        // Columns: id,displayName,category,weapon,element,isUltimate,maxLevel,description,upgradeText,stat,kind,amount,icon,targetSlot,isDuo,prerequisiteElements
        DraftUpgradeDefinition def = new DraftUpgradeDefinition
        {
            id = cols[0].Trim(),
            displayName = cols.Count > 1 ? cols[1].Trim() : "",
            weapon = cols.Count > 3 ? cols[3].Trim().ToLowerInvariant() : "",
            element = cols.Count > 4 ? cols[4].Trim() : "",
            isUltimate = cols.Count > 5 && (cols[5].Trim() == "1" || cols[5].Trim().Equals("true", StringComparison.OrdinalIgnoreCase)),
            maxLevel = cols.Count > 6 && int.TryParse(cols[6].Trim(), out int ml) ? Mathf.Max(1, ml) : 1,
            description = cols.Count > 7 ? cols[7].Trim() : "",
            upgradeText = cols.Count > 8 ? cols[8].Trim() : "",
            iconName = cols.Count > 12 ? cols[12].Trim() : "",
            targetSlot = cols.Count > 13 ? cols[13].Trim() : "",
            isDuo = cols.Count > 14 && (cols[14].Trim() == "1" || cols[14].Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
        };

        if (cols.Count > 15 && !string.IsNullOrEmpty(cols[15]))
        {
            def.prerequisiteElements.AddRange(cols[15].Split('|', StringSplitOptions.RemoveEmptyEntries));
        }

        if (cols.Count > 2 && Enum.TryParse<DraftCategory>(cols[2].Trim(), true, out DraftCategory parsedCat))
        {
            def.category = parsedCat;
        }

        string statStr = cols.Count > 9 ? cols[9].Trim() : "";
        string kindStr = cols.Count > 10 ? cols[10].Trim() : "";
        string amountStr = cols.Count > 11 ? cols[11].Trim() : "";

        if (!string.IsNullOrEmpty(statStr))
        {
            string[] stats = statStr.Split(';');
            string[] kinds = kindStr.Split(';');
            string[] amounts = amountStr.Split(';');

            for (int i = 0; i < stats.Length; i++)
            {
                if (!Enum.TryParse<StatType>(stats[i].Trim(), true, out StatType statType)) continue;

                ModifierKind kind = ModifierKind.Flat;
                if (i < kinds.Length && kinds[i].Trim().Equals("Percent", StringComparison.OrdinalIgnoreCase))
                {
                    kind = ModifierKind.Percent;
                }

                string amtSpec = i < amounts.Length ? amounts[i].Trim() : "0";
                string[] perLevelStrs = amtSpec.Split('|');
                float[] perLevel = new float[perLevelStrs.Length];
                for (int p = 0; p < perLevelStrs.Length; p++)
                {
                    float.TryParse(perLevelStrs[p].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out perLevel[p]);
                }

                def.effects.Add(new SkillEffect
                {
                    stat = statType,
                    kind = kind,
                    amounts = perLevel
                });
            }
        }

        return def;
    }

    private List<string> ParseCsvRow(string line)
    {
        List<string> result = new List<string>();
        bool inQuotes = false;
        string current = "";

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '\"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                {
                    current += '\"';
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current);
                current = "";
            }
            else
            {
                current += c;
            }
        }
        result.Add(current);
        return result;
    }

    public DraftUpgradeDefinition GetById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        EnsureInitialized();
        return byId.TryGetValue(id, out DraftUpgradeDefinition def) ? def : null;
    }

    /// <summary>
    ///     Generates 3 filtered draft candidates adhering to category, equipped weapons,
    ///     elemental lock, and ultimate exclusivity.
    /// </summary>
    public List<DraftUpgradeDefinition> GetCandidateUpgrades(DraftCategory category, int count = 3, HashSet<string> banishedIds = null)
    {
        EnsureInitialized();

        string equippedMelee = "sword";
        string equippedRanged = "bow";
        if (PlayerWeaponManager.Instance != null)
        {
            equippedMelee = PlayerWeaponManager.Instance.CurrentMeleeId.ToLowerInvariant();
            equippedRanged = PlayerWeaponManager.Instance.CurrentRangedId.ToLowerInvariant();
        }
        else
        {
            SaveData save = SaveSystem.Load();
            if (save != null)
            {
                if (!string.IsNullOrEmpty(save.equippedMeleeWeapon)) equippedMelee = save.equippedMeleeWeapon.ToLowerInvariant();
                if (!string.IsNullOrEmpty(save.equippedRangedWeapon)) equippedRanged = save.equippedRangedWeapon.ToLowerInvariant();
            }
        }

        bool hasUltimate = !string.IsNullOrEmpty(RunSession.ActiveUltimateId);

        List<DraftUpgradeDefinition> candidates = new List<DraftUpgradeDefinition>();

        foreach (DraftUpgradeDefinition def in allDefinitions)
        {
            if (def == null) continue;
            if (def.category != category) continue;
            if (banishedIds != null && banishedIds.Contains(def.id)) continue;

            int currentLevel = RunSession.GetUpgradeLevel(def.id);
            if (currentLevel >= def.maxLevel) continue;

            // Weapon category rule: Targeted Weapon Pool
            if (def.category == DraftCategory.Weapon)
            {
                if (!string.IsNullOrEmpty(def.weapon))
                {
                    bool matchesEquipped = def.weapon.Equals(equippedMelee, StringComparison.OrdinalIgnoreCase) ||
                                          def.weapon.Equals(equippedRanged, StringComparison.OrdinalIgnoreCase);
                    if (!matchesEquipped) continue;
                }
            }

            // Elemental slot rule
            if (def.category == DraftCategory.Elemental)
            {
                if (def.isDuo)
                {
                    bool meetsPrereqs = true;
                    HashSet<string> activeElements = RunSession.GetActiveElements();
                    foreach (var prereq in def.prerequisiteElements)
                    {
                        if (!activeElements.Contains(prereq))
                        {
                            meetsPrereqs = false;
                            break;
                        }
                    }
                    if (!meetsPrereqs) continue;
                }
            }

            // Ultimate exclusivity rule: You can't have more than one ultimate per run!
            if (def.isUltimate)
            {
                if (hasUltimate) continue;
            }

            candidates.Add(def);
        }

        // Shuffle candidates using Fisher-Yates
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int rnd = UnityEngine.Random.Range(0, i + 1);
            DraftUpgradeDefinition temp = candidates[i];
            candidates[i] = candidates[rnd];
            candidates[rnd] = temp;
        }

        if (candidates.Count > count)
        {
            candidates = candidates.GetRange(0, count);
        }

        return candidates;
    }

    /// <summary>
    ///     Applies the selected draft card, records it in RunSession, updates PlayerStats,
    ///     and activates any ultimate or elemental slots.
    /// </summary>
    public bool ApplyUpgrade(DraftUpgradeDefinition def)
    {
        if (def == null) return false;

        // Handle Elemental Slots Overwrite
        if (def.category == DraftCategory.Elemental && !string.IsNullOrEmpty(def.targetSlot))
        {
            if (RunSession.ElementalSlots.TryGetValue(def.targetSlot, out string currentElement))
            {
                // We are overwriting a slot. Is it a different element?
                if (!currentElement.Equals(def.element, StringComparison.OrdinalIgnoreCase))
                {
                    // Find the previous upgrade id for this slot (we can store it in RunSession.ElementalSlotUpgrades if we added it)
                    // Wait, we can iterate all upgrades, find the one with this targetSlot, and remove it.
                    foreach (var kvp in RunSession.InRunUpgradeLevels)
                    {
                        if (kvp.Value > 0)
                        {
                            DraftUpgradeDefinition oldDef = GetById(kvp.Key);
                            if (oldDef != null && oldDef.category == DraftCategory.Elemental && oldDef.targetSlot == def.targetSlot && !oldDef.isDuo)
                            {
                                RemoveUpgrade(oldDef, kvp.Value);
                                RunSession.SetUpgradeLevel(oldDef.id, 0);
                                
                                // Award conversion bonus
                                RunSession.AddInRunGold(25);
                                Debug.Log($"[DraftUpgradeService] Overwrote slot {def.targetSlot}. Removed {oldDef.id}, awarded 25 gold.");
                                break;
                            }
                        }
                    }
                }
            }
        }

        int nextLevel = RunSession.GetUpgradeLevel(def.id) + 1;
        RunSession.SetUpgradeLevel(def.id, nextLevel);

        Player player = Player.Instance;
        if (player != null && player.Stats != null)
        {
            foreach (SkillEffect effect in def.effects)
            {
                player.Stats.AddModifier(effect.stat, effect.kind, effect.AmountForLevel(nextLevel));
            }

            // Handle Ultimate Draft
            if (def.isUltimate)
            {
                RunSession.ActiveUltimateId = def.id;
                player.Stats.SetBase(StatType.UltimateUnlocked, 1f);

                PlayerUltimateController ultCtrl = player.GetComponentInChildren<PlayerUltimateController>();
                if (ultCtrl != null)
                {
                    ConfigureUltimateHandler(player, def.id);
                }
                Debug.Log($"[DraftUpgradeService] Unlocked Weapon Ultimate: '{def.displayName}' (ID: {def.id})!");
            }
        }

        // Handle Elemental Slots
        if (def.category == DraftCategory.Elemental && !string.IsNullOrEmpty(def.targetSlot) && !string.IsNullOrEmpty(def.element))
        {
            RunSession.SetElementalSlot(def.targetSlot, def.element);
            Debug.Log($"[DraftUpgradeService] Equipped Element {def.element} to {def.targetSlot}");
        }

        // Handle Fortress Upgrades
        if (def.category == DraftCategory.Fortress)
        {
            if (FortDefenseManager.Instance != null)
            {
                FortDefenseManager.Instance.HandleSkillNodePurchased(def.id);
            }
        }

        Debug.Log($"[DraftUpgradeService] Applied Upgrade: '{def.displayName}' (Level {nextLevel}/{def.maxLevel}).");
        return true;
    }

    public void RemoveUpgrade(DraftUpgradeDefinition def, int levelToRemove)
    {
        if (def == null || levelToRemove <= 0) return;

        Player player = Player.Instance;
        if (player != null && player.Stats != null)
        {
            foreach (SkillEffect effect in def.effects)
            {
                player.Stats.AddModifier(effect.stat, effect.kind, -effect.AmountForLevel(levelToRemove));
            }
        }
    }

    public static void ConfigureUltimateHandler(Player player, string ultimateId)
    {
        if (player == null || string.IsNullOrEmpty(ultimateId)) return;

        // Disable all handlers first
        foreach (IUltimateHandler h in player.GetComponentsInChildren<IUltimateHandler>(true))
        {
            if (h is MonoBehaviour mb) mb.enabled = false;
        }

        if (ultimateId.StartsWith("sword_mount", StringComparison.OrdinalIgnoreCase))
        {
            SwordMountUltimate mountUlt = player.GetComponent<SwordMountUltimate>();
            if (mountUlt == null) mountUlt = player.gameObject.AddComponent<SwordMountUltimate>();
            mountUlt.enabled = true;
        }
        else if (ultimateId.StartsWith("axe_bladestorm", StringComparison.OrdinalIgnoreCase))
        {
            BerserkerUltimate axeUlt = player.GetComponent<BerserkerUltimate>();
            if (axeUlt == null) axeUlt = player.gameObject.AddComponent<BerserkerUltimate>();
            axeUlt.enabled = true;
        }
        else if (ultimateId.StartsWith("bow_stream", StringComparison.OrdinalIgnoreCase))
        {
            RangerUltimate bowUlt = player.GetComponent<RangerUltimate>();
            if (bowUlt == null) bowUlt = player.gameObject.AddComponent<RangerUltimate>();
            bowUlt.enabled = true;
        }
        else if (ultimateId.StartsWith("taxe_vortex", StringComparison.OrdinalIgnoreCase))
        {
            ThrowingAxeUltimate taxeUlt = player.GetComponent<ThrowingAxeUltimate>();
            if (taxeUlt == null) taxeUlt = player.gameObject.AddComponent<ThrowingAxeUltimate>();
            taxeUlt.enabled = true;
        }
    }

    /// <summary>
    ///     Converts a DraftUpgradeDefinition to a transient SkillNode for UI compatibility.
    /// </summary>
    public SkillNode ConvertToSkillNode(DraftUpgradeDefinition def)
    {
        if (def == null) return null;

        SkillNode node = new SkillNode
        {
            id = def.id,
            displayName = def.displayName,
            description = def.description,
            upgradeText = def.upgradeText,
            maxLevel = def.maxLevel,
            iconName = def.iconName,
            isCard = true,
            isMeta = false,
            isActiveWeapon = def.isUltimate,
            effects = new List<SkillEffect>(def.effects)
        };

        return node;
    }
}
