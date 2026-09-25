using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public enum BalanceNodeKind
{
    Weapon,
    Element,
    Tower,
    Card,
    Stat,
    Armour,
    Mount,
    MetaPerk
}

/// <summary>One box in the Balance Tree graph. <see cref="Data" /> is the asset, CSV row or stat name it stands for.</summary>
public class BalanceNode
{
    public string Key;
    public BalanceNodeKind Kind;
    public string Title;
    public string Subtitle;
    public object Data;

    // Filter facets (cards only, except DemoLocked).
    public string Weapon = "";
    public readonly HashSet<string> Elements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public string Category = "";
    public bool IsUltimate;
    public bool DemoLocked;
    public string SearchText = "";

    public DraftCsvRow Row => Data as DraftCsvRow;

    /// <summary>The card as the runtime parses it (null when the runtime would skip the row).</summary>
    public DraftUpgradeDefinition Definition;
    public readonly List<string> ParseErrors = new List<string>();
}

public class BalanceEdge
{
    public BalanceNode From;
    public BalanceNode To;
}

/// <summary>
///     Everything the Balance Tree shows: draft cards from the CSV, the weapons/elements/towers they hang off,
///     the StatTypes they write, and the armour sets, mounts and meta perks. Edges are real dependencies:
///     weapon → card, element → card (and duo prerequisites), tower → card (a script on the tower prefab reads
///     the card's stat), card/armour → stat, and meta perk prerequisite → perk.
/// </summary>
public class BalanceTreeModel
{
    private const string ScriptsRoot = "Assets/Bladehold/Bladehold Scripts";
    private const string TowerPrefabFolder = "Assets/Bladehold/Bladehold Prefabs/Defenses";

    // Files that name every StatType without reading them (labels, the enum, dev tools).
    private static readonly string[] NonConsumerFiles = { "Stats/StatType.cs", "Stats/StatDisplay.cs", "Player/ArmourSetSO.cs" };
    private static readonly string[] NonConsumerFolders = { "Editor/", "Debug/" };

    public DraftCsvDocument Csv { get; private set; }
    public List<BalanceNode> Nodes { get; } = new List<BalanceNode>();
    public List<BalanceEdge> Edges { get; } = new List<BalanceEdge>();
    public WeaponDefinitionSO[] Weapons { get; private set; }

    /// <summary>StatType name → gameplay scripts that reference it (asset paths).</summary>
    public Dictionary<string, List<string>> StatConsumers { get; } = new Dictionary<string, List<string>>();

    private readonly Dictionary<string, BalanceNode> byKey = new Dictionary<string, BalanceNode>(StringComparer.OrdinalIgnoreCase);

    public static BalanceTreeModel Load(DraftCsvDocument csv)
    {
        var model = new BalanceTreeModel { Csv = csv };
        model.Build();
        return model;
    }

    public BalanceNode Find(string key) => key != null && byKey.TryGetValue(key, out BalanceNode n) ? n : null;

    public IEnumerable<BalanceNode> Neighbours(BalanceNode node) =>
        Edges.Where(e => e.From == node).Select(e => e.To).Concat(Edges.Where(e => e.To == node).Select(e => e.From));

    public IEnumerable<BalanceNode> Cards => Nodes.Where(n => n.Kind == BalanceNodeKind.Card);

    /// <summary>Re-parses one card after an inline edit and refreshes its facets and edges.</summary>
    public void RefreshCard(BalanceNode card)
    {
        Edges.RemoveAll(e => e.From == card || e.To == card);
        FillCard(card);
        ConnectCard(card);
    }

    private void Build()
    {
        IndexStatConsumers();

        Weapons = FindAssets<WeaponDefinitionSO>().OrderBy(w => w.category).ThenBy(w => w.id).ToArray();
        foreach (WeaponDefinitionSO w in Weapons)
        {
            BalanceNode n = Add($"weapon:{w.id}", BalanceNodeKind.Weapon, w.displayName, w);
            n.Weapon = w.id;
            n.DemoLocked = w.isLockedForDemo;
            n.Subtitle = $"{w.category}{(w.isLockedForDemo ? " · demo-locked" : "")}";
        }

        foreach (DraftCsvRow row in Csv.Rows)
        {
            string key = $"card:{row.Id}";
            // Duplicate ids still get a node each so the validator can point at both.
            if (byKey.ContainsKey(key)) key += $"@{row.LineNumber}";
            BalanceNode card = Add(key, BalanceNodeKind.Card, "", row);
            FillCard(card);
        }

        foreach (string element in Cards.SelectMany(c => c.Elements).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(e => e))
        {
            Add($"element:{element}", BalanceNodeKind.Element, element, element);
        }

        foreach (GameObject tower in LoadTowers())
        {
            BalanceNode n = Add($"tower:{tower.name}", BalanceNodeKind.Tower, tower.name.Replace("Defense_", ""), tower);
            n.Subtitle = "Tower";
        }

        foreach (ArmourSetSO a in FindAssets<ArmourSetSO>().OrderBy(a => a.id))
        {
            BalanceNode n = Add($"armour:{a.id}", BalanceNodeKind.Armour, a.displayName, a);
            n.Subtitle = $"Armour · {a.orcishMetalUnlockCost} metal";
            foreach (ArmourSetSO.ArmourStatModifier mod in a.statModifiers)
            {
                Connect(n, StatNode(mod.stat.ToString()));
            }
        }

        foreach (MountDefinitionSO m in FindAssets<MountDefinitionSO>().OrderBy(m => m.id))
        {
            BalanceNode n = Add($"mount:{m.id}", BalanceNodeKind.Mount, m.displayName, m);
            n.Subtitle = $"Mount · {m.orcishMetalUnlockCost} metal";
        }

        MetaPerkDefinitionSO[] perks = FindAssets<MetaPerkDefinitionSO>().OrderBy(p => p.tier).ThenBy(p => p.id).ToArray();
        foreach (MetaPerkDefinitionSO p in perks)
        {
            BalanceNode n = Add($"perk:{p.id}", BalanceNodeKind.MetaPerk, p.displayName, p);
            n.Subtitle = $"Meta perk · tier {p.tier} · {p.goblinBloodCost} blood";
            // Demo scope is tier-1 perks only (CLAUDE.md). Plan 07 owns the real demo gating data.
            n.DemoLocked = p.tier > 1;
        }
        foreach (MetaPerkDefinitionSO p in perks)
        {
            if (p.prerequisites == null) continue;
            foreach (MetaPerkDefinitionSO pre in p.prerequisites)
            {
                if (pre != null) Connect(Find($"perk:{pre.id}"), Find($"perk:{p.id}"));
            }
        }

        foreach (BalanceNode card in Cards.ToList()) ConnectCard(card);
    }

    private void FillCard(BalanceNode card)
    {
        DraftCsvRow row = card.Row;
        card.ParseErrors.Clear();
        card.Definition = DraftUpgradeService.ParseRow(row.CurrentLine, card.ParseErrors);

        card.Title = string.IsNullOrWhiteSpace(row.Get("displayName")) ? row.Id : row.Get("displayName").Trim();
        card.Weapon = row.Get("weapon").Trim().ToLowerInvariant();
        card.Category = row.Get("category").Trim();
        card.IsUltimate = IsTrue(row.Get("isUltimate"));

        card.Elements.Clear();
        string element = row.Get("element").Trim();
        if (element.Length > 0) card.Elements.Add(element);
        foreach (string pre in row.Get("prerequisiteElements").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
        {
            card.Elements.Add(pre.Trim());
        }

        WeaponDefinitionSO weapon = Weapons.FirstOrDefault(w => string.Equals(w.id, card.Weapon, StringComparison.OrdinalIgnoreCase));
        card.DemoLocked = weapon != null && weapon.isLockedForDemo;

        var tags = new List<string> { card.Category };
        if (card.Weapon.Length > 0) tags.Add(card.Weapon);
        if (card.Elements.Count > 0) tags.Add(string.Join("+", card.Elements));
        string slot = row.Get("targetSlot").Trim();
        if (slot.Length > 0) tags.Add(slot.Replace("SLOT_", "").ToLowerInvariant());
        tags.Add($"L{row.Get("maxLevel").Trim()}");
        if (card.IsUltimate) tags.Add("ULTIMATE");
        if (IsTrue(row.Get("isDuo"))) tags.Add("duo");
        card.Subtitle = string.Join(" · ", tags);

        card.SearchText = string.Join(" ", row.Id, card.Title, row.Get("description"), row.Get("upgradeText"), row.Get("stat"), card.Subtitle).ToLowerInvariant();
    }

    private void ConnectCard(BalanceNode card)
    {
        if (card.Weapon.Length > 0) Connect(Find($"weapon:{card.Weapon}"), card);
        foreach (string element in card.Elements) Connect(Find($"element:{element}") ?? Add($"element:{element}", BalanceNodeKind.Element, element, element), card);

        foreach (string stat in CardStats(card))
        {
            BalanceNode statNode = StatNode(stat);
            Connect(card, statNode);

            if (!StatConsumers.TryGetValue(stat, out List<string> files)) continue;
            foreach (BalanceNode tower in Nodes.Where(n => n.Kind == BalanceNodeKind.Tower))
            {
                if (TowerScripts((GameObject)tower.Data).Any(files.Contains)) Connect(tower, card);
            }
        }
    }

    /// <summary>Stat names as written in the CSV (unknown names included, so a typo still shows up).</summary>
    public static IEnumerable<string> CardStats(BalanceNode card) =>
        card.Row.Get("stat").Split(';').Select(s => s.Trim()).Where(s => s.Length > 0);

    private BalanceNode StatNode(string stat)
    {
        BalanceNode n = Find($"stat:{stat}");
        if (n != null) return n;
        n = Add($"stat:{stat}", BalanceNodeKind.Stat, stat, stat);
        int consumers = StatConsumers.TryGetValue(stat, out List<string> files) ? files.Count : 0;
        n.Subtitle = consumers == 0 ? "StatType · no consumer" : $"StatType · {consumers} script{(consumers == 1 ? "" : "s")}";
        return n;
    }

    private BalanceNode Add(string key, BalanceNodeKind kind, string title, object data)
    {
        var n = new BalanceNode { Key = key, Kind = kind, Title = title, Data = data };
        n.SearchText = $"{key} {title}".ToLowerInvariant();
        Nodes.Add(n);
        byKey[key] = n;
        return n;
    }

    private void Connect(BalanceNode from, BalanceNode to)
    {
        if (from == null || to == null || from == to) return;
        if (Edges.Any(e => e.From == from && e.To == to)) return;
        Edges.Add(new BalanceEdge { From = from, To = to });
    }

    private void IndexStatConsumers()
    {
        var pattern = new Regex(@"\bStatType\.([A-Za-z_]\w*)");
        foreach (string file in Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string path = file.Replace('\\', '/');
            string relative = path.Substring(ScriptsRoot.Length + 1);
            if (NonConsumerFiles.Contains(relative) || NonConsumerFolders.Any(f => relative.StartsWith(f) || relative.Contains("/" + f))) continue;

            foreach (string stat in pattern.Matches(File.ReadAllText(path)).Cast<Match>().Select(m => m.Groups[1].Value).Distinct())
            {
                if (!StatConsumers.TryGetValue(stat, out List<string> list)) StatConsumers[stat] = list = new List<string>();
                list.Add(path);
            }
        }
    }

    private readonly Dictionary<GameObject, HashSet<string>> towerScriptCache = new Dictionary<GameObject, HashSet<string>>();

    /// <summary>Asset paths of the Fort/ scripts on a tower prefab (shared scripts like Health would link every tower to everything).</summary>
    public HashSet<string> TowerScripts(GameObject tower)
    {
        if (towerScriptCache.TryGetValue(tower, out HashSet<string> cached)) return cached;
        var paths = new HashSet<string>();
        foreach (MonoBehaviour mb in tower.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            string path = AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(mb));
            if (path.StartsWith(ScriptsRoot + "/Fort/")) paths.Add(path);
        }
        towerScriptCache[tower] = paths;
        return paths;
    }

    public static bool IsTrue(string cell)
    {
        string v = cell.Trim();
        return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static T[] FindAssets<T>() where T : ScriptableObject =>
        AssetDatabase.FindAssets($"t:{typeof(T).Name}")
            .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(a => a != null)
            .ToArray();

    private static IEnumerable<GameObject> LoadTowers() =>
        AssetDatabase.FindAssets("t:Prefab Defense_", new[] { TowerPrefabFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(go => go != null && go.name.StartsWith("Defense_"))
            .OrderBy(go => go.name);
}
