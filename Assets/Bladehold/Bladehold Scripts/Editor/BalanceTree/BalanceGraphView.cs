using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using Edge = UnityEditor.Experimental.GraphView.Edge;

/// <summary>
///     Read-only GraphView of a <see cref="BalanceTreeModel" />. Nodes are auto-laid out in lanes
///     (hubs | weapon cards | elemental cards | fortress cards | stats | armour, mounts, perks), and nodes the
///     user drags keep their position in <see cref="BalanceTreeLayoutStore" />. Nodes and edges can't be
///     created or deleted here: the graph is a view of the CSV and assets.
/// </summary>
public class BalanceGraphView : GraphView
{
    private const float NodeWidth = 230f;
    private const float ColumnStep = 300f;
    private const float RowStep = 78f;
    private const float GroupGap = 40f;

    public Action<BalanceNode> OnNodeSelected;

    private readonly Dictionary<string, BalanceGraphNode> nodesByKey = new Dictionary<string, BalanceGraphNode>();
    private BalanceTreeLayoutStore positionStore;
    private bool rebuilding;
    private bool silentSelection;

    public BalanceGraphView()
    {
        SetupZoom(0.1f, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());

        var grid = new GridBackground();
        grid.StretchToParentSize();
        Insert(0, grid);

        graphViewChanged = OnGraphViewChanged;
    }

    public void Build(BalanceTreeModel model, HashSet<BalanceNode> visible, BalanceTreeLayoutStore layoutStore, Dictionary<string, BalanceIssueSeverity> worstIssue)
    {
        positionStore = layoutStore;
        rebuilding = true;
        DeleteElements(graphElements.ToList());
        nodesByKey.Clear();
        rebuilding = false;

        Dictionary<BalanceNode, Vector2> positions = AutoLayout(model, visible);
        foreach (BalanceNode n in model.Nodes.Where(visible.Contains))
        {
            Vector2 pos = positionStore.TryGet(n.Key, out Vector2 saved) ? saved : positions[n];
            worstIssue.TryGetValue(n.Key, out BalanceIssueSeverity severity);
            var gn = new BalanceGraphNode(n, severity, NodeWidth, NotifySelected);
            gn.SetPosition(new Rect(pos, Vector2.zero));
            AddElement(gn);
            nodesByKey[n.Key] = gn;
        }

        foreach (BalanceEdge e in model.Edges)
        {
            if (!nodesByKey.TryGetValue(e.From.Key, out BalanceGraphNode from) || !nodesByKey.TryGetValue(e.To.Key, out BalanceGraphNode to)) continue;
            Edge edge = from.Output.ConnectTo(to.Input);
            edge.capabilities &= ~(Capabilities.Deletable | Capabilities.Selectable);
            AddElement(edge);
        }
    }

    /// <summary>Selects (and optionally frames) a node if it's visible, without raising <see cref="OnNodeSelected" />.</summary>
    public bool Reveal(string key, bool frame)
    {
        if (!nodesByKey.TryGetValue(key, out BalanceGraphNode gn)) return false;
        silentSelection = true;
        ClearSelection();
        AddToSelection(gn);
        silentSelection = false;
        if (frame) FrameSelection();
        return true;
    }

    private void NotifySelected(BalanceNode node)
    {
        if (!silentSelection) OnNodeSelected?.Invoke(node);
    }

    public void UpdateNode(BalanceNode node, BalanceIssueSeverity severity)
    {
        if (nodesByKey.TryGetValue(node.Key, out BalanceGraphNode gn)) gn.Refresh(severity);
    }

    private GraphViewChange OnGraphViewChanged(GraphViewChange change)
    {
        if (rebuilding) return change;

        // The graph mirrors data; structure edits happen in the inspector.
        change.elementsToRemove?.Clear();
        change.edgesToCreate?.Clear();

        if (change.movedElements != null && positionStore != null)
        {
            foreach (BalanceGraphNode gn in change.movedElements.OfType<BalanceGraphNode>())
            {
                positionStore.Set(gn.Model.Key, gn.GetPosition().position);
            }
            positionStore.Save();
        }
        return change;
    }

    // --- Auto layout -------------------------------------------------------------------------------------

    private static Dictionary<BalanceNode, Vector2> AutoLayout(BalanceTreeModel model, HashSet<BalanceNode> visible)
    {
        var pos = new Dictionary<BalanceNode, Vector2>();
        List<BalanceNode> Shown(BalanceNodeKind kind) => model.Nodes.Where(n => n.Kind == kind && visible.Contains(n)).ToList();

        List<BalanceNode> cards = Shown(BalanceNodeKind.Card);
        List<string> weaponOrder = model.Weapons.Select(w => w.id).ToList();

        var weaponCards = cards.Where(c => c.Category.Equals("Weapon", StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => weaponOrder.IndexOf(c.Weapon) < 0 ? int.MaxValue : weaponOrder.IndexOf(c.Weapon)).ThenBy(c => c.IsUltimate)
            .ToList();
        var elementCards = cards.Where(c => c.Category.Equals("Elemental", StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Elements.Count > 1 ? 1 : 0).ThenBy(c => c.Elements.FirstOrDefault() ?? "").ThenBy(c => c.Row.Get("targetSlot"))
            .ToList();
        var otherCards = cards.Except(weaponCards).Except(elementCards).ToList();

        StackGrouped(pos, weaponCards, 1, c => c.Weapon);
        StackGrouped(pos, elementCards, 2, c => c.Elements.Count > 1 ? "duo" : c.Elements.FirstOrDefault() ?? "");
        StackGrouped(pos, otherCards, 3, c => c.Category);

        // Hubs sit level with the cards they feed, in their own sub-lanes of column 0.
        float y = PlaceByBarycenter(pos, model, Shown(BalanceNodeKind.Weapon), 0, 0f);
        y = PlaceByBarycenter(pos, model, Shown(BalanceNodeKind.Element), 0, y + GroupGap);
        PlaceByBarycenter(pos, model, Shown(BalanceNodeKind.Tower), 0, y + GroupGap);

        PlaceByBarycenter(pos, model, Shown(BalanceNodeKind.Stat), 4, 0f);

        y = PlaceByBarycenter(pos, model, Shown(BalanceNodeKind.Armour), 5, 0f);
        y = Stack(pos, Shown(BalanceNodeKind.Mount), 5, y + GroupGap);
        StackGrouped(pos, Shown(BalanceNodeKind.MetaPerk), 5, p => p.Subtitle.Split('·')[1], y + GroupGap);

        return pos;
    }

    private static float StackGrouped(Dictionary<BalanceNode, Vector2> pos, List<BalanceNode> nodes, int column, Func<BalanceNode, string> group, float y = 0f)
    {
        string last = null;
        foreach (BalanceNode n in nodes)
        {
            string g = group(n);
            if (last != null && g != last) y += GroupGap;
            last = g;
            pos[n] = new Vector2(column * ColumnStep, y);
            y += RowStep;
        }
        return y;
    }

    private static float Stack(Dictionary<BalanceNode, Vector2> pos, List<BalanceNode> nodes, int column, float y) =>
        StackGrouped(pos, nodes, column, _ => "", y);

    /// <summary>Places each node at the mean height of its already-placed neighbours, pushing down to avoid overlap.</summary>
    private static float PlaceByBarycenter(Dictionary<BalanceNode, Vector2> pos, BalanceTreeModel model, List<BalanceNode> nodes, int column, float minY)
    {
        var desired = new List<(BalanceNode node, float y)>();
        for (int i = 0; i < nodes.Count; i++)
        {
            var placed = model.Neighbours(nodes[i]).Where(pos.ContainsKey).Select(n => pos[n].y).ToList();
            desired.Add((nodes[i], placed.Count > 0 ? placed.Average() : float.MaxValue));
        }

        float y = minY;
        foreach (var (node, want) in desired.OrderBy(d => d.y))
        {
            y = want == float.MaxValue ? y : Mathf.Max(y, want);
            pos[node] = new Vector2(column * ColumnStep, y);
            y += RowStep;
        }
        return y;
    }
}

/// <summary>One graph box: coloured by kind (cards by element), with a subtitle line and an issue marker.</summary>
public class BalanceGraphNode : Node
{
    public BalanceNode Model { get; }
    public Port Input { get; }
    public Port Output { get; }

    private readonly Action<BalanceNode> onSelected;
    private readonly Label subtitle;

    public BalanceGraphNode(BalanceNode model, BalanceIssueSeverity severity, float width, Action<BalanceNode> onSelected)
    {
        Model = model;
        this.onSelected = onSelected;
        capabilities &= ~Capabilities.Deletable;
        style.width = width;

        Input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
        Input.portName = "";
        inputContainer.Add(Input);
        Output = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
        Output.portName = "";
        outputContainer.Add(Output);

        subtitle = new Label { style = { fontSize = 10, color = new Color(0.8f, 0.8f, 0.8f), paddingLeft = 6, paddingRight = 6, paddingBottom = 3, whiteSpace = WhiteSpace.Normal } };
        extensionContainer.Add(subtitle);
        extensionContainer.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.95f);

        Refresh(severity);
        RefreshPorts();
        expanded = true;
        RefreshExpandedState();
    }

    public void Refresh(BalanceIssueSeverity severity)
    {
        title = Model.Title;
        subtitle.text = Model.Subtitle ?? "";
        titleContainer.style.backgroundColor = KindColor(Model);

        Color border = severity switch
        {
            BalanceIssueSeverity.Error => new Color(0.9f, 0.2f, 0.2f),
            BalanceIssueSeverity.Warning => new Color(0.95f, 0.75f, 0.1f),
            _ => Model.IsUltimate ? new Color(1f, 0.85f, 0.3f) : Color.clear
        };
        float w = border == Color.clear ? 0f : 3f;
        style.borderLeftWidth = w;
        style.borderRightWidth = w;
        style.borderTopWidth = w;
        style.borderBottomWidth = w;
        style.borderLeftColor = border;
        style.borderRightColor = border;
        style.borderTopColor = border;
        style.borderBottomColor = border;
    }

    public override void OnSelected()
    {
        base.OnSelected();
        onSelected?.Invoke(Model);
    }

    private static Color KindColor(BalanceNode n)
    {
        switch (n.Kind)
        {
            case BalanceNodeKind.Weapon: return new Color(0.35f, 0.35f, 0.45f);
            case BalanceNodeKind.Tower: return new Color(0.4f, 0.32f, 0.22f);
            case BalanceNodeKind.Stat: return new Color(0.25f, 0.3f, 0.3f);
            case BalanceNodeKind.Armour: return new Color(0.3f, 0.35f, 0.25f);
            case BalanceNodeKind.Mount: return new Color(0.4f, 0.3f, 0.35f);
            case BalanceNodeKind.MetaPerk: return new Color(0.45f, 0.2f, 0.2f);
            case BalanceNodeKind.Element: return ElementColor((string)n.Data);
            case BalanceNodeKind.Card:
                if (n.Elements.Count > 1) return new Color(0.45f, 0.25f, 0.5f);
                if (n.Elements.Count == 1) return ElementColor(n.Elements.First());
                return n.Category.Equals("Fortress", StringComparison.OrdinalIgnoreCase) ? new Color(0.35f, 0.28f, 0.2f) : new Color(0.28f, 0.28f, 0.34f);
            default: return Color.gray;
        }
    }

    private static Color ElementColor(string element)
    {
        switch (element?.ToLowerInvariant())
        {
            case "fire": return new Color(0.6f, 0.25f, 0.1f);
            case "ice": return new Color(0.2f, 0.4f, 0.6f);
            case "lightning": return new Color(0.5f, 0.45f, 0.1f);
            case "poison": return new Color(0.25f, 0.45f, 0.2f);
            default: return new Color(0.35f, 0.35f, 0.35f);
        }
    }
}

/// <summary>Manually dragged node positions, per machine, in UserSettings/ (never in the CSV).</summary>
public class BalanceTreeLayoutStore
{
    private const string FilePath = "UserSettings/BalanceTreeLayout.json";

    [Serializable]
    private class Entry
    {
        public string key;
        public Vector2 position;
    }

    [Serializable]
    private class Data
    {
        public List<Entry> entries = new List<Entry>();
    }

    private readonly Dictionary<string, Vector2> positions = new Dictionary<string, Vector2>();

    public int Count => positions.Count;

    public static BalanceTreeLayoutStore Load()
    {
        var store = new BalanceTreeLayoutStore();
        if (!File.Exists(FilePath)) return store;
        var data = JsonUtility.FromJson<Data>(File.ReadAllText(FilePath));
        if (data?.entries == null) return store;
        foreach (Entry e in data.entries) store.positions[e.key] = e.position;
        return store;
    }

    public bool TryGet(string key, out Vector2 position) => positions.TryGetValue(key, out position);

    public void Set(string key, Vector2 position) => positions[key] = position;

    public void Clear()
    {
        positions.Clear();
        Save();
    }

    public void Save()
    {
        var data = new Data { entries = positions.Select(kv => new Entry { key = kv.Key, position = kv.Value }).ToList() };
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
        File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
    }
}
