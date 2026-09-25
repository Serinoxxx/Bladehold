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
    [SerializeField] private SkillTreeIconsSO iconsConfig;

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
        if (iconsConfig == null)
        {
            iconsConfig = Resources.Load<SkillTreeIconsSO>("SkillTreeIcons");
        }
        ParseCsv();
    }

    private void ParseCsv()
    {
        allDefinitions.Clear();
        byId.Clear();

        // Resources are bundled in players, unlike loose files under Application.dataPath.
        TextAsset csv = draftUpgradesCsv != null
            ? draftUpgradesCsv
            : Resources.Load<TextAsset>("DraftUpgrades");
        string csvText = csv != null ? csv.text : null;

        if (string.IsNullOrEmpty(csvText))
        {
            Debug.LogError("[DraftUpgradeService] Draft catalog is missing or empty. Assign draftUpgradesCsv or include Resources/DraftUpgrades.csv.");
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

    /// <summary>
    ///     Parses one catalog row with the runtime's rules. Problems go to <paramref name="errors" /> when given
    ///     (the Balance Tree Editor's validation panel), otherwise to the console. Returns null for a skipped row.
    /// </summary>
    public static DraftUpgradeDefinition ParseRow(string row, List<string> errors = null)
    {
        void Report(string message)
        {
            if (errors != null) errors.Add(message);
            else Debug.LogError($"[DraftUpgradeService] {message}");
        }

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

        string categoryStr = cols.Count > 2 ? cols[2].Trim() : "";
        if (!Enum.TryParse<DraftCategory>(categoryStr, true, out DraftCategory parsedCat) || !Enum.IsDefined(typeof(DraftCategory), parsedCat))
        {
            Report($"Draft '{def.id}' has unknown category '{categoryStr}'. Expected one of: {string.Join(", ", Enum.GetNames(typeof(DraftCategory)))}. Row skipped.");
            return null;
        }
        def.category = parsedCat;

        if (def.category == DraftCategory.Elemental)
        {
            if (!string.IsNullOrEmpty(def.targetSlot) && !RunSession.KnownElementalSlots.Contains(def.targetSlot))
            {
                Report($"Elemental draft '{def.id}' has unknown targetSlot '{def.targetSlot}'. Row skipped.");
                return null;
            }
            if (!def.isDuo && string.IsNullOrEmpty(def.element))
            {
                Report($"Elemental draft '{def.id}' has no element. Row skipped.");
                return null;
            }
            if (def.isDuo && (def.prerequisiteElements.Count < 2 || !string.IsNullOrEmpty(def.targetSlot)))
            {
                Report($"Duo draft '{def.id}' needs 2+ prerequisiteElements and no targetSlot. Row skipped.");
                return null;
            }
        }
        else if (def.isDuo || !string.IsNullOrEmpty(def.targetSlot))
        {
            Report($"Draft '{def.id}' sets isDuo/targetSlot but its category is {def.category}, not Elemental.");
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
                if (!Enum.TryParse<StatType>(stats[i].Trim(), true, out StatType statType))
                {
                    Report($"Draft '{def.id}' references unknown StatType '{stats[i].Trim()}'. Effect skipped.");
                    continue;
                }

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

    public static List<string> ParseCsvRow(string line)
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
    ///     Resolves a sprite icon for the given icon asset name.
    /// </summary>
    public Sprite GetIcon(string iconName)
    {
        if (string.IsNullOrEmpty(iconName)) return null;
        EnsureInitialized();

        if (iconsConfig != null)
        {
            Sprite s = iconsConfig.GetIcon(iconName);
            if (s != null) return s;
        }

        if (SkillTreeService.Instance != null && SkillTreeService.Instance.Tree != null)
        {
            Sprite s = SkillTreeService.Instance.Tree.GetIcon(iconName);
            if (s != null) return s;
        }

        return null;
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
        HashSet<string> activeElements = GetActiveElements();

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
                    if (DemoConfigSO.IsWeaponIdLocked(def.weapon)) continue;
                }
            }

            // Elemental rule: no run-wide element lock (docs/ElementSystemSpec.md). Slots mix freely,
            // and duos only enter the pool once all their prerequisite elements are active.
            if (def.category == DraftCategory.Elemental && def.isDuo && !MeetsDuoPrerequisites(def, activeElements))
            {
                continue;
            }

            // Ultimate exclusivity rule: You can't have more than one ultimate per run!
            if (def.isUltimate)
            {
                if (hasUltimate) continue;
            }

            // Fortress upgrades are built via battlefield Tower Plots, not random draft cards
            if (def.category == DraftCategory.Fortress)
            {
                continue;
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

        // Imbue first: overwriting a slot strips the previous element's cards before we level this one.
        if (HasSlot(def))
        {
            ImbueSlot(def.targetSlot, def.element, awardConversionGold: true);
        }

        int previousLevel = RunSession.GetUpgradeLevel(def.id);
        int nextLevel = previousLevel + 1;
        RunSession.SetUpgradeLevel(def.id, nextLevel);

        Player player = Player.Instance;
        if (player != null && player.Stats != null)
        {
            // Per-level amounts are absolute (matches RunSession's scene-load reapply): swap the old level's value for the new one.
            foreach (SkillEffect effect in def.effects)
            {
                if (previousLevel > 0) player.Stats.AddModifier(effect.stat, effect.kind, -effect.AmountForLevel(previousLevel));
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

    private static bool HasSlot(DraftUpgradeDefinition def) =>
        def != null && def.category == DraftCategory.Elemental && !def.isDuo
        && !string.IsNullOrEmpty(def.targetSlot) && !string.IsNullOrEmpty(def.element);

    /// <summary>
    ///     The one way to put an element on a slot (draft cards and the DevConsole both use it).
    ///     A different element already on the slot is overwritten: its slot cards are removed,
    ///     paying the 25 gold conversion bonus per card when <paramref name="awardConversionGold"/>.
    /// </summary>
    public void ImbueSlot(string slotName, string element, bool awardConversionGold = false)
    {
        EnsureInitialized();
        if (!RunSession.KnownElementalSlots.Contains(slotName ?? "") || string.IsNullOrEmpty(element))
        {
            Debug.LogError($"[DraftUpgradeService] Cannot imbue slot '{slotName}' with element '{element}'.");
            return;
        }

        string current = RunSession.GetElementInSlot(slotName);
        if (!string.IsNullOrEmpty(current) && !current.Equals(element, StringComparison.OrdinalIgnoreCase))
        {
            int removed = RemoveSlotCards(slotName);
            if (awardConversionGold && removed > 0)
            {
                RunSession.AddInRunGold(ElementOverwriteGold * removed);
            }
            Debug.Log($"[DraftUpgradeService] Overwrote {slotName} ({current} -> {element}), removed {removed} card(s).");
        }

        RunSession.SetElementalSlot(slotName, element);
    }

    /// <summary>Clears a slot's element and removes the drafted cards that were on it.</summary>
    public void ClearSlot(string slotName)
    {
        EnsureInitialized();
        RemoveSlotCards(slotName);
        RunSession.ClearElementalSlot(slotName);
    }

    public const int ElementOverwriteGold = 25;

    private bool SlotHasCards(string slotName)
    {
        foreach (var kvp in RunSession.InRunUpgradeLevels)
        {
            DraftUpgradeDefinition def = kvp.Value > 0 ? GetById(kvp.Key) : null;
            if (HasSlot(def) && def.targetSlot.Equals(slotName, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private int RemoveSlotCards(string slotName)
    {
        int removed = 0;
        foreach (var kvp in new List<KeyValuePair<string, int>>(RunSession.InRunUpgradeLevels))
        {
            if (kvp.Value <= 0) continue;
            DraftUpgradeDefinition oldDef = GetById(kvp.Key);
            if (!HasSlot(oldDef) || !oldDef.targetSlot.Equals(slotName, StringComparison.OrdinalIgnoreCase)) continue;
            RemoveUpgrade(oldDef, kvp.Value);
            RunSession.SetUpgradeLevel(oldDef.id, 0);
            removed++;
        }
        return removed;
    }

    /// <summary>
    ///     True when drafting <paramref name="def"/> would replace a different element on its slot.
    /// </summary>
    public bool WouldOverwrite(DraftUpgradeDefinition def, out string currentElement)
    {
        currentElement = HasSlot(def) ? RunSession.GetElementInSlot(def.targetSlot) : "";
        return !string.IsNullOrEmpty(currentElement) && !currentElement.Equals(def.element, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Elements the run is invested in: every imbued slot plus owned slotless elemental cards
    ///     (Kindling, Deep Freeze, Shatter). Duo prerequisites check against this.
    /// </summary>
    public HashSet<string> GetActiveElements()
    {
        EnsureInitialized();
        HashSet<string> active = RunSession.GetActiveElements();
        foreach (DraftUpgradeDefinition def in allDefinitions)
        {
            if (def.category == DraftCategory.Elemental && !def.isDuo && !string.IsNullOrEmpty(def.element)
                && RunSession.GetUpgradeLevel(def.id) > 0)
            {
                active.Add(def.element);
            }
        }
        return active;
    }

    public static bool MeetsDuoPrerequisites(DraftUpgradeDefinition def, HashSet<string> activeElements)
    {
        foreach (string prereq in def.prerequisiteElements)
        {
            if (!activeElements.Contains(prereq)) return false;
        }
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

    /// <summary>
    ///     Debug/Cheat method: directly sets a draft upgrade's level, accurately applying
    ///     or reverting stat modifiers and updating RunSession / Ultimate / Fortress state.
    /// </summary>
    public void DebugSetDraftLevel(DraftUpgradeDefinition def, int targetLevel)
    {
        if (def == null) return;
        EnsureInitialized();

        int currentLevel = RunSession.GetUpgradeLevel(def.id);
        targetLevel = Mathf.Clamp(targetLevel, 0, def.maxLevel);
        if (currentLevel == targetLevel) return;

        // Same slot path as a real draft (no conversion gold for cheats).
        if (HasSlot(def) && targetLevel > 0)
        {
            ImbueSlot(def.targetSlot, def.element);
        }

        Player player = Player.Instance;

        // Revert old level stat modifiers
        if (currentLevel > 0 && player != null && player.Stats != null && def.effects != null)
        {
            foreach (SkillEffect effect in def.effects)
            {
                player.Stats.AddModifier(effect.stat, effect.kind, -effect.AmountForLevel(currentLevel));
            }
        }

        // Apply new level stat modifiers
        if (targetLevel > 0 && player != null && player.Stats != null && def.effects != null)
        {
            foreach (SkillEffect effect in def.effects)
            {
                player.Stats.AddModifier(effect.stat, effect.kind, effect.AmountForLevel(targetLevel));
            }
        }

        RunSession.SetUpgradeLevel(def.id, targetLevel);

        if (HasSlot(def) && targetLevel == 0 && !SlotHasCards(def.targetSlot))
        {
            RunSession.ClearElementalSlot(def.targetSlot);
        }

        if (def.isUltimate && player != null && player.Stats != null)
        {
            if (targetLevel > 0)
            {
                RunSession.ActiveUltimateId = def.id;
                player.Stats.SetBase(StatType.UltimateUnlocked, 1f);
                ConfigureUltimateHandler(player, def.id);
            }
            else if (string.Equals(RunSession.ActiveUltimateId, def.id, StringComparison.OrdinalIgnoreCase))
            {
                RunSession.ActiveUltimateId = null;
                player.Stats.SetBase(StatType.UltimateUnlocked, 0f);
            }
        }

        if (def.category == DraftCategory.Fortress && targetLevel > 0)
        {
            if (FortDefenseManager.Instance != null)
            {
                FortDefenseManager.Instance.HandleSkillNodePurchased(def.id);
            }
        }
    }

    /// <summary>
    ///     Debug/Cheat method: sets all regular draft upgrades to their maximum level,
    ///     optionally unlocking and configuring the equipped weapon's ultimate.
    /// </summary>
    public void DebugMaxAllDrafts(bool includeCurrentWeaponUltimate = true)
    {
        EnsureInitialized();
        foreach (var def in allDefinitions)
        {
            if (def == null) continue;
            if (def.isUltimate) continue;
            DebugSetDraftLevel(def, def.maxLevel);
        }

        if (includeCurrentWeaponUltimate)
        {
            UnlockDefaultWeaponUltimate();
        }
    }

    /// <summary>
    ///     Debug/Cheat method: unlocks the default ultimate for the currently equipped weapons.
    /// </summary>
    public void UnlockDefaultWeaponUltimate()
    {
        string ultId = RunSession.ActiveUltimateId;
        if (string.IsNullOrEmpty(ultId))
        {
            string meleeId = PlayerWeaponManager.Instance != null ? PlayerWeaponManager.Instance.CurrentMeleeId : "sword";
            if (meleeId.Contains("mace")) ultId = "mace_earthshaker_ult";
            else if (meleeId.Contains("axe")) ultId = "axe_bladestorm_ult";
            else ultId = "sword_blade_tempest";
        }

        DraftUpgradeDefinition ultDef = GetById(ultId);
        if (ultDef != null)
        {
            DebugSetDraftLevel(ultDef, 1);
        }
    }

    /// <summary>
    ///     Debug/Cheat method: resets all draft upgrades to 0 and clears ultimate state.
    /// </summary>
    public void DebugResetAllDrafts()
    {
        EnsureInitialized();
        foreach (var def in allDefinitions)
        {
            if (def != null)
            {
                DebugSetDraftLevel(def, 0);
            }
        }
        foreach (string slot in new List<string>(RunSession.ElementalSlots.Keys))
        {
            RunSession.ClearElementalSlot(slot);
        }
        RunSession.ActiveUltimateId = null;
        if (Player.Instance != null && Player.Instance.Stats != null)
        {
            Player.Instance.Stats.SetBase(StatType.UltimateUnlocked, 0f);
        }
    }

    public static void ConfigureUltimateHandler(Player player, string ultimateId)
    {
        if (player == null || string.IsNullOrEmpty(ultimateId)) return;

        // Ensure we search the entire player hierarchy (both root Player and child SidekickSyntyCharacter)
        Transform rootTr = player.transform.root;
        foreach (IUltimateHandler h in rootTr.GetComponentsInChildren<IUltimateHandler>(true))
        {
            if (h is MonoBehaviour mb) mb.enabled = false;
        }

        // Attach or enable handler on the GameObject holding PlayerUltimateController if present, otherwise player.gameObject
        PlayerUltimateController controller = rootTr.GetComponentInChildren<PlayerUltimateController>();
        GameObject targetGo = controller != null ? controller.gameObject : player.gameObject;

        if (ultimateId.StartsWith("sword_blade", StringComparison.OrdinalIgnoreCase))
        {
            SwordBladeTempestUltimate swordUlt = rootTr.GetComponentInChildren<SwordBladeTempestUltimate>(true);
            if (swordUlt == null) swordUlt = targetGo.AddComponent<SwordBladeTempestUltimate>();
            swordUlt.enabled = true;
        }
        else if (ultimateId.StartsWith("sword_mount", StringComparison.OrdinalIgnoreCase))
        {
            SwordMountUltimate mountUlt = rootTr.GetComponentInChildren<SwordMountUltimate>(true);
            if (mountUlt == null) mountUlt = targetGo.AddComponent<SwordMountUltimate>();
            mountUlt.enabled = true;
        }
        else if (ultimateId.StartsWith("axe_bladestorm", StringComparison.OrdinalIgnoreCase))
        {
            BerserkerUltimate axeUlt = rootTr.GetComponentInChildren<BerserkerUltimate>(true);
            if (axeUlt == null) axeUlt = targetGo.AddComponent<BerserkerUltimate>();
            axeUlt.enabled = true;
        }
        else if (ultimateId.StartsWith("bow_stream", StringComparison.OrdinalIgnoreCase))
        {
            RangerUltimate bowUlt = rootTr.GetComponentInChildren<RangerUltimate>(true);
            if (bowUlt == null) bowUlt = targetGo.AddComponent<RangerUltimate>();
            bowUlt.enabled = true;
        }
        else if (ultimateId.StartsWith("taxe_vortex", StringComparison.OrdinalIgnoreCase))
        {
            ThrowingAxeUltimate taxeUlt = rootTr.GetComponentInChildren<ThrowingAxeUltimate>(true);
            if (taxeUlt == null) taxeUlt = targetGo.AddComponent<ThrowingAxeUltimate>();
            taxeUlt.enabled = true;
        }
        else if (ultimateId.StartsWith("mace_earthshaker", StringComparison.OrdinalIgnoreCase))
        {
            MaceUltimate maceUlt = rootTr.GetComponentInChildren<MaceUltimate>(true);
            if (maceUlt == null) maceUlt = targetGo.AddComponent<MaceUltimate>();
            maceUlt.enabled = true;
        }
        else if (ultimateId.StartsWith("mage", StringComparison.OrdinalIgnoreCase) || ultimateId.StartsWith("staff", StringComparison.OrdinalIgnoreCase) || ultimateId.StartsWith("wand", StringComparison.OrdinalIgnoreCase))
        {
            MageUltimate mageUlt = rootTr.GetComponentInChildren<MageUltimate>(true);
            if (mageUlt == null) mageUlt = targetGo.AddComponent<MageUltimate>();
            mageUlt.enabled = true;
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

        if (WouldOverwrite(def, out string currentElement))
        {
            node.description += $"\n<color=#FFA500>[Overwrite] Replaces {currentElement} on your {SlotDisplayName(def.targetSlot)} (+{ElementOverwriteGold} gold per card)</color>";
        }

        return node;
    }

    private static string SlotDisplayName(string slotName)
    {
        switch (slotName?.ToUpperInvariant())
        {
            case RunSession.SlotMelee: return "melee weapon";
            case RunSession.SlotRanged: return "ranged weapon";
            case RunSession.SlotMobility: return "dash";
            case RunSession.SlotUltimate: return "ultimate";
            case RunSession.SlotFortress: return "towers";
            default: return slotName;
        }
    }
}
