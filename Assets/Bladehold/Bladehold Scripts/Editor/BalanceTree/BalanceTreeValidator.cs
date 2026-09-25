using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

public enum BalanceIssueSeverity
{
    None,
    Info,
    Warning,
    Error
}

public class BalanceIssue
{
    public BalanceIssueSeverity Severity;
    public string NodeKey;
    public string Message;
}

/// <summary>
///     Balance Tree checks. Row-level rules come from <see cref="DraftUpgradeService.ParseRow" /> itself
///     (unknown category/slot/StatType, bad duos), so the panel can never disagree with the game. On top:
///     duplicate ids, malformed rows, unknown weapons, numbers the runtime would silently read as 0,
///     per-level amounts that don't match maxLevel, missing icons, and stats no gameplay script reads.
/// </summary>
public static class BalanceTreeValidator
{
    public static List<BalanceIssue> Run(BalanceTreeModel model, SkillTreeIconsSO icons)
    {
        var issues = new List<BalanceIssue>();
        void Add(BalanceIssueSeverity s, BalanceNode n, string msg) => issues.Add(new BalanceIssue { Severity = s, NodeKey = n?.Key, Message = msg });

        var weaponIds = new HashSet<string>(model.Weapons.Select(w => w.id), StringComparer.OrdinalIgnoreCase);
        var seenIds = new Dictionary<string, BalanceNode>(StringComparer.OrdinalIgnoreCase);

        foreach (BalanceNode card in model.Cards)
        {
            DraftCsvRow row = card.Row;
            string id = row.Id;
            string where = $"'{(id.Length > 0 ? id : "(no id)")}' (line {row.LineNumber})";

            if (row.IsMalformed)
                Add(BalanceIssueSeverity.Error, card, $"{where} has a different column count to the header.");
            if (id.Length == 0)
                Add(BalanceIssueSeverity.Error, card, $"{where} has no id.");
            else if (seenIds.TryGetValue(id, out BalanceNode first))
                Add(BalanceIssueSeverity.Error, card, $"{where} duplicates the id on line {first.Row.LineNumber}. Both enter the draft pool, and lookups by id return the last one.");
            else
                seenIds[id] = card;

            foreach (string err in card.ParseErrors)
                Add(err.Contains("Row skipped") ? BalanceIssueSeverity.Error : BalanceIssueSeverity.Warning, card, err);

            if (card.Weapon.Length > 0 && !weaponIds.Contains(card.Weapon))
                Add(BalanceIssueSeverity.Error, card, $"{where} targets weapon '{card.Weapon}', which no WeaponDefinitionSO has. It can never be drafted.");
            if (card.IsUltimate && card.Weapon.Length == 0)
                Add(BalanceIssueSeverity.Warning, card, $"{where} is an ultimate with no weapon, so it's offered whatever you hold.");

            bool isElemental = card.Category.Equals("Elemental", StringComparison.OrdinalIgnoreCase);
            if (isElemental && !BalanceTreeModel.IsTrue(row.Get("isDuo")) && row.Get("targetSlot").Trim().Length == 0)
                Add(BalanceIssueSeverity.Info, card, $"{where} has no targetSlot: a passive card (imbues nothing, counts as an active element for duos).");

            CheckNumbers(card, where, Add);
            CheckIcon(card, where, icons, Add);
        }

        foreach (BalanceNode stat in model.Nodes.Where(n => n.Kind == BalanceNodeKind.Stat))
        {
            string name = (string)stat.Data;
            if (!Enum.TryParse(name, out StatType _)) continue; // already reported by ParseRow
            if (!model.StatConsumers.ContainsKey(name))
                Add(BalanceIssueSeverity.Warning, stat, $"StatType.{name} is written by a card or armour but no gameplay script references it, so it does nothing.");
        }

        foreach (WeaponDefinitionSO w in model.Weapons.Where(w => !DemoConfigSO.IsWeaponLocked(w)))
        {
            if (!model.Cards.Any(c => c.Weapon.Equals(w.id, StringComparison.OrdinalIgnoreCase)))
                Add(BalanceIssueSeverity.Warning, model.Find($"weapon:{w.id}"), $"Weapon '{w.id}' is playable but has no draft cards.");
            else if (!model.Cards.Any(c => c.IsUltimate && c.Weapon.Equals(w.id, StringComparison.OrdinalIgnoreCase)))
                Add(BalanceIssueSeverity.Info, model.Find($"weapon:{w.id}"), $"Weapon '{w.id}' has no ultimate card.");
        }

        Add(BalanceIssueSeverity.Info, null, "Draft cards have no localization keys yet (Loc/Strings.csv has no draft.* rows). That's Phase 3 localization work.");

        return issues.OrderByDescending(i => i.Severity).ToList();
    }

    private static void CheckNumbers(BalanceNode card, string where, Action<BalanceIssueSeverity, BalanceNode, string> add)
    {
        DraftCsvRow row = card.Row;
        string maxLevelText = row.Get("maxLevel").Trim();
        if (!int.TryParse(maxLevelText, out int maxLevel) || maxLevel < 1)
        {
            add(BalanceIssueSeverity.Warning, card, $"{where} maxLevel '{maxLevelText}' isn't a positive number; the runtime uses 1.");
            maxLevel = 1;
        }

        string[] stats = row.Get("stat").Split(';');
        string[] kinds = row.Get("kind").Split(';');
        string[] amounts = row.Get("amount").Split(';');
        if (row.Get("stat").Trim().Length == 0) return;

        if (kinds.Length != stats.Length || amounts.Length != stats.Length)
            add(BalanceIssueSeverity.Warning, card, $"{where} has {stats.Length} stat(s), {kinds.Length} kind(s) and {amounts.Length} amount(s); missing kinds default to Flat, missing amounts to 0.");

        for (int i = 0; i < kinds.Length; i++)
        {
            string k = kinds[i].Trim();
            if (k.Length > 0 && !k.Equals("Flat", StringComparison.OrdinalIgnoreCase) && !k.Equals("Percent", StringComparison.OrdinalIgnoreCase))
                add(BalanceIssueSeverity.Warning, card, $"{where} kind '{k}' isn't Flat or Percent; the runtime treats it as Flat.");
        }

        for (int i = 0; i < amounts.Length; i++)
        {
            string[] perLevel = amounts[i].Split('|');
            foreach (string v in perLevel)
            {
                if (!float.TryParse(v.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    add(BalanceIssueSeverity.Error, card, $"{where} amount '{v.Trim()}' isn't a number; the runtime reads it as 0.");
            }

            string stat = i < stats.Length ? stats[i].Trim() : "?";
            if (perLevel.Length > 1 && perLevel.Length != maxLevel)
                add(BalanceIssueSeverity.Warning, card, $"{where} {stat} has {perLevel.Length} per-level values but maxLevel is {maxLevel}.");
            else if (perLevel.Length == 1 && maxLevel > 1)
                add(BalanceIssueSeverity.Warning, card, $"{where} {stat} has one value but maxLevel {maxLevel}. Amounts are absolute per level, so levels 2+ add nothing.");
        }
    }

    private static void CheckIcon(BalanceNode card, string where, SkillTreeIconsSO icons, Action<BalanceIssueSeverity, BalanceNode, string> add)
    {
        string icon = card.Row.Get("icon").Trim();
        if (icon.Length == 0)
            add(BalanceIssueSeverity.Warning, card, $"{where} has no icon.");
        else if (icons != null && icons.GetIcon(icon) == null)
            add(BalanceIssueSeverity.Warning, card, $"{where} icon '{icon}' isn't in Resources/SkillTreeIcons.");
    }

    public static Dictionary<string, BalanceIssueSeverity> WorstByNode(List<BalanceIssue> issues)
    {
        var worst = new Dictionary<string, BalanceIssueSeverity>();
        foreach (BalanceIssue i in issues)
        {
            if (i.NodeKey == null || i.Severity < BalanceIssueSeverity.Warning) continue;
            if (!worst.TryGetValue(i.NodeKey, out BalanceIssueSeverity s) || i.Severity > s) worst[i.NodeKey] = i.Severity;
        }
        return worst;
    }
}

/// <summary>Card-pool depth for spotting thin pools: per weapon, per element × slot, per category.</summary>
public static class BalanceTreePacing
{
    public static readonly string[] SlotColumns = { "SLOT_MELEE", "SLOT_RANGED", "SLOT_MOBILITY", "SLOT_ULTIMATE", "SLOT_FORTRESS", "" };

    public struct WeaponRow
    {
        public string Weapon;
        public bool DemoLocked;
        public int Cards;
        public int Ultimates;
        public int Picks; // sum of maxLevel: how many drafts the pool can absorb
    }

    public static List<WeaponRow> ByWeapon(BalanceTreeModel model) =>
        model.Weapons.Select(w =>
        {
            var cards = model.Cards.Where(c => c.Weapon.Equals(w.id, StringComparison.OrdinalIgnoreCase)).ToList();
            return new WeaponRow
            {
                Weapon = w.id,
                DemoLocked = DemoConfigSO.IsWeaponLocked(w),
                Cards = cards.Count,
                Ultimates = cards.Count(c => c.IsUltimate),
                Picks = cards.Sum(MaxLevel)
            };
        }).ToList();

    /// <summary>element → slot → card count (slot "" = passive). Duos are counted under "Duo".</summary>
    public static SortedDictionary<string, int[]> ByElementAndSlot(BalanceTreeModel model, bool elementalOnly)
    {
        var table = new SortedDictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
        foreach (BalanceNode c in model.Cards)
        {
            if (elementalOnly && !c.Category.Equals("Elemental", StringComparison.OrdinalIgnoreCase)) continue;
            if (c.Elements.Count == 0) continue;
            string key = BalanceTreeModel.IsTrue(c.Row.Get("isDuo")) ? $"Duo ({string.Join("+", c.Elements)})" : c.Elements.First();
            if (!table.TryGetValue(key, out int[] counts)) table[key] = counts = new int[SlotColumns.Length];
            int slot = Array.IndexOf(SlotColumns, c.Row.Get("targetSlot").Trim().ToUpperInvariant());
            counts[slot < 0 ? SlotColumns.Length - 1 : slot]++;
        }
        return table;
    }

    public static IEnumerable<(string category, int cards, int picks)> ByCategory(BalanceTreeModel model) =>
        model.Cards.GroupBy(c => c.Category).OrderBy(g => g.Key).Select(g => (g.Key, g.Count(), g.Sum(MaxLevel)));

    private static int MaxLevel(BalanceNode c) => int.TryParse(c.Row.Get("maxLevel").Trim(), out int m) ? Mathf.Max(1, m) : 1;
}
