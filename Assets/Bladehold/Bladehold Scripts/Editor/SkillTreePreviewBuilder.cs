using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
///     Owns the edit-mode preview hierarchy for the Scene-view skill tree editor: instantiates the
///     *actual* runtime node/connector prefabs (read off the scene's <see cref="SkillTreeView" /> via
///     SerializedObject) under the view's content rect, positioned with SkillTreeView's own static
///     layout math — so what you see is exactly what Play mode builds. Every preview object is
///     HideFlags.DontSave and parented under one "~SkillTreePreview (Edit Mode)" root, torn down on
///     session end / play mode / domain reload; the saved scene is never touched (content's size and
///     pivot are only read, never written).
///
///     Also provides the GUI-space transforms the Scene-view input handler needs: node GUI rects for
///     hit-testing and a camera-independent mouse→grid mapping (ray/plane intersection against the
///     content plane).
/// </summary>
public class SkillTreePreviewBuilder
{
    private class NodePreview
    {
        public SkillTreeRow row;
        public RectTransform rect;
        public SkillNodeView view;
    }

    private class ConnectorPreview
    {
        public string fromId;
        public string toId;
        public RectTransform rect;
    }

    private SkillTreeSO tree;
    private RectTransform content;
    private SkillNodeView nodePrefab;
    private RectTransform connectorPrefab;
    private float spacing = 160f;
    private float contentPadding = 200f;
    private Vector2 nodeSize = new Vector2(100f, 100f);
    private Vector2 treeOffset;

    private RectTransform previewRoot;
    private RectTransform connectorLayer;
    private RectTransform nodeLayer;

    private readonly Dictionary<string, NodePreview> nodesById = new Dictionary<string, NodePreview>();
    private readonly List<ConnectorPreview> connectors = new List<ConnectorPreview>();

    public bool IsValid => previewRoot != null;
    public float Spacing => spacing;

    /// <summary>Destroys any previous preview and rebuilds everything from the session's rows.</summary>
    public void BuildAll(SkillTreeEditSession session, SkillTreeView view)
    {
        TearDown();
        nodesById.Clear();
        connectors.Clear();

        tree = session.Tree;

        var so = new SerializedObject(view);
        content = so.FindProperty("content").objectReferenceValue as RectTransform;
        nodePrefab = so.FindProperty("nodePrefab").objectReferenceValue as SkillNodeView;
        connectorPrefab = so.FindProperty("connectorPrefab").objectReferenceValue as RectTransform;
        spacing = so.FindProperty("spacing").floatValue;
        contentPadding = so.FindProperty("contentPadding").floatValue;

        if (content == null || nodePrefab == null)
        {
            Debug.LogError("Skill Tree Scene Editor: the SkillTreeView has no content/nodePrefab assigned.");
            return;
        }
        nodeSize = nodePrefab.GetComponent<RectTransform>().sizeDelta;

        RefitTreeOffset(session.rows);

        previewRoot = CreateLayer("~SkillTreePreview (Edit Mode)", content);
        connectorLayer = CreateLayer("Connectors", previewRoot);
        nodeLayer = CreateLayer("Nodes", previewRoot);

        foreach (SkillTreeRow row in session.rows)
        {
            CreateNode(row);
        }
        foreach (SkillTreeRow row in session.rows)
        {
            SyncConnectors(row);
        }
    }

    public void TearDown()
    {
        if (previewRoot != null)
        {
            Object.DestroyImmediate(previewRoot.gameObject);
        }
        previewRoot = null;
        connectorLayer = null;
        nodeLayer = null;
        nodesById.Clear();
        connectors.Clear();
    }

    /// <summary>
    ///     Recomputes the session-fixed treeOffset from the current rows (the runtime FitContentToTree
    ///     math). Called once per full build so nodes never shift under the cursor mid-drag.
    /// </summary>
    private void RefitTreeOffset(List<SkillTreeRow> rows)
    {
        var coords = new List<Vector2>(rows.Count);
        foreach (SkillTreeRow row in rows)
        {
            coords.Add(new Vector2(row.x, row.y));
        }
        (Vector2 offset, Vector2 _) = SkillTreeView.ComputeContentFit(coords, spacing, nodeSize, contentPadding);
        treeOffset = offset;
    }

    /// <summary>
    ///     A zero-size DontSave rect whose pivot sits exactly on the parent's top-left corner, so
    ///     children anchored at (0, 1) get the same anchoredPositions they would as direct children of
    ///     content — the runtime layout, unchanged.
    /// </summary>
    private static RectTransform CreateLayer(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        SkillTreeView.SetTopLeftAnchor(rect);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
        SetDontSaveRecursive(go);
        return rect;
    }

    private static void SetDontSaveRecursive(GameObject go)
    {
        go.hideFlags = HideFlags.DontSave;
        foreach (Transform child in go.transform)
        {
            SetDontSaveRecursive(child.gameObject);
        }
    }

    // ---------------------------------------------------------------- incremental updates

    public void AddNode(SkillTreeRow row)
    {
        if (!IsValid || nodesById.ContainsKey(row.id))
        {
            return;
        }
        CreateNode(row);
        SyncConnectors(row);
    }

    public void RemoveNode(string id)
    {
        if (nodesById.TryGetValue(id, out NodePreview node))
        {
            if (node.rect != null)
            {
                Object.DestroyImmediate(node.rect.gameObject);
            }
            nodesById.Remove(id);
        }

        for (int i = connectors.Count - 1; i >= 0; i--)
        {
            if (connectors[i].fromId == id || connectors[i].toId == id)
            {
                if (connectors[i].rect != null)
                {
                    Object.DestroyImmediate(connectors[i].rect.gameObject);
                }
                connectors.RemoveAt(i);
            }
        }
    }

    /// <summary>Refreshes a node's name/cost/icon from its row (after an overlay edit).</summary>
    public void UpdateNodeVisual(SkillTreeRow row)
    {
        if (nodesById.TryGetValue(row.id, out NodePreview node) && node.view != null)
        {
            node.view.BindPreview(row.displayName, row.cost, row.maxLevel, tree != null ? tree.GetIcon(row.icon) : null);
        }
    }

    /// <summary>Moves a node to its row's grid position and re-lays every connector touching it.</summary>
    public void UpdateNodePosition(SkillTreeRow row)
    {
        if (!nodesById.TryGetValue(row.id, out NodePreview node) || node.rect == null)
        {
            return;
        }
        node.rect.anchoredPosition = SkillTreeView.GridToLocal(row.x, row.y, spacing, treeOffset);

        foreach (ConnectorPreview connector in connectors)
        {
            if (connector.fromId == row.id || connector.toId == row.id)
            {
                PlaceConnector(connector);
            }
        }
    }

    /// <summary>
    ///     Renames a preview node's key after an id edit (the connectors' endpoint ids are rewritten by
    ///     the controller editing the rows; this just re-keys the lookup and endpoint records).
    /// </summary>
    public void RenameNode(string oldId, string newId)
    {
        if (nodesById.TryGetValue(oldId, out NodePreview node))
        {
            nodesById.Remove(oldId);
            nodesById[newId] = node;
        }
        foreach (ConnectorPreview connector in connectors)
        {
            if (connector.fromId == oldId)
            {
                connector.fromId = newId;
            }
            if (connector.toId == oldId)
            {
                connector.toId = newId;
            }
        }
    }

    /// <summary>
    ///     Diffs the row's prereq list against the existing preview connectors into this node, creating
    ///     missing lines and destroying stale ones. Prereq ids without a preview node are skipped
    ///     (matches runtime, which ignores unknown ids).
    /// </summary>
    public void SyncConnectors(SkillTreeRow row)
    {
        if (!IsValid || connectorPrefab == null)
        {
            return;
        }

        var wanted = new HashSet<string>(row.PrereqList());

        // Links are undirected and stored on both ends, so a connector is one line per unordered pair.
        // Remove any connector touching this row whose other endpoint is no longer linked.
        for (int i = connectors.Count - 1; i >= 0; i--)
        {
            string other = OtherEnd(connectors[i], row.id);
            if (other != null && !wanted.Contains(other))
            {
                if (connectors[i].rect != null)
                {
                    Object.DestroyImmediate(connectors[i].rect.gameObject);
                }
                connectors.RemoveAt(i);
            }
        }

        foreach (string other in wanted)
        {
            if (!nodesById.ContainsKey(other) || HasConnectorEither(row.id, other))
            {
                continue;
            }
            RectTransform line = Object.Instantiate(connectorPrefab, connectorLayer);
            SetDontSaveRecursive(line.gameObject);
            SkillTreeView.SetTopLeftAnchor(line);
            var connector = new ConnectorPreview { fromId = other, toId = row.id, rect = line };
            connectors.Add(connector);
            PlaceConnector(connector);
        }
    }

    /// <summary>The endpoint of <paramref name="connector" /> that isn't <paramref name="id" />, or null if it doesn't touch it.</summary>
    private static string OtherEnd(ConnectorPreview connector, string id)
    {
        if (connector.fromId == id) return connector.toId;
        if (connector.toId == id) return connector.fromId;
        return null;
    }

    private bool HasConnectorEither(string a, string b)
    {
        foreach (ConnectorPreview connector in connectors)
        {
            if ((connector.fromId == a && connector.toId == b) ||
                (connector.fromId == b && connector.toId == a))
            {
                return true;
            }
        }
        return false;
    }

    private void CreateNode(SkillTreeRow row)
    {
        SkillNodeView view = Object.Instantiate(nodePrefab, nodeLayer);
        view.gameObject.name = $"Node {row.id}";
        SetDontSaveRecursive(view.gameObject);
        var rect = (RectTransform)view.transform;
        SkillTreeView.SetTopLeftAnchor(rect);
        rect.anchoredPosition = SkillTreeView.GridToLocal(row.x, row.y, spacing, treeOffset);
        view.BindPreview(row.displayName, row.cost, row.maxLevel, tree != null ? tree.GetIcon(row.icon) : null);
        nodesById[row.id] = new NodePreview { row = row, rect = rect, view = view };
    }

    private void PlaceConnector(ConnectorPreview connector)
    {
        SkillTreeRow from = nodesById.TryGetValue(connector.fromId, out NodePreview a) ? a.row : null;
        SkillTreeRow to = nodesById.TryGetValue(connector.toId, out NodePreview b) ? b.row : null;
        if (from == null || to == null || connector.rect == null)
        {
            return;
        }
        SkillTreeView.PlaceConnector(
            connector.rect,
            SkillTreeView.GridToLocal(from.x, from.y, spacing, treeOffset),
            SkillTreeView.GridToLocal(to.x, to.y, spacing, treeOffset));
    }

    // ---------------------------------------------------------------- GUI-space transforms

    private static readonly Vector3[] CornerBuffer = new Vector3[4];

    /// <summary>The node's screen-space GUI rect in the current Scene view (for hit-testing/outlines).</summary>
    public Rect NodeGuiRect(string id)
    {
        if (!nodesById.TryGetValue(id, out NodePreview node) || node.rect == null)
        {
            return Rect.zero;
        }
        return WorldRectToGui(node.rect);
    }

    public IEnumerable<(string id, Rect guiRect)> AllNodeGuiRects()
    {
        foreach (KeyValuePair<string, NodePreview> pair in nodesById)
        {
            if (pair.Value.rect != null)
            {
                yield return (pair.Key, WorldRectToGui(pair.Value.rect));
            }
        }
    }

    private static Rect WorldRectToGui(RectTransform rect)
    {
        rect.GetWorldCorners(CornerBuffer);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < 4; i++)
        {
            Vector2 gui = HandleUtility.WorldToGUIPoint(CornerBuffer[i]);
            min = Vector2.Min(min, gui);
            max = Vector2.Max(max, gui);
        }
        return new Rect(min, max - min);
    }

    /// <summary>The world position of a node's center (for link-line drawing).</summary>
    public Vector2 NodeGuiCenter(string id)
    {
        Rect rect = NodeGuiRect(id);
        return rect.center;
    }

    /// <summary>
    ///     Maps a Scene-view GUI point to unsnapped grid coordinates by intersecting the mouse ray with
    ///     the content plane — camera-angle and canvas-scale independent.
    /// </summary>
    public Vector2 GuiToGrid(Vector2 guiPos)
    {
        if (nodeLayer == null)
        {
            return Vector2.zero;
        }

        Ray ray = HandleUtility.GUIPointToWorldRay(guiPos);
        var plane = new Plane(nodeLayer.rotation * Vector3.forward, nodeLayer.position);
        if (!plane.Raycast(ray, out float distance))
        {
            return Vector2.zero;
        }

        // nodeLayer's pivot sits on content's top-left corner, so its local space is exactly the
        // anchoredPosition space the nodes are placed in.
        Vector3 local = nodeLayer.InverseTransformPoint(ray.GetPoint(distance));
        float gridX = (local.x - treeOffset.x) / spacing;
        float gridY = -(local.y - treeOffset.y) / spacing;
        return new Vector2(gridX, gridY);
    }

    /// <summary>World bounds of every preview node, for SceneView.Frame at session start.</summary>
    public Bounds ContentWorldBounds()
    {
        var bounds = new Bounds();
        bool any = false;
        foreach (NodePreview node in nodesById.Values)
        {
            if (node.rect == null)
            {
                continue;
            }
            node.rect.GetWorldCorners(CornerBuffer);
            for (int i = 0; i < 4; i++)
            {
                if (!any)
                {
                    bounds = new Bounds(CornerBuffer[i], Vector3.zero);
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(CornerBuffer[i]);
                }
            }
        }
        if (!any && content != null)
        {
            content.GetWorldCorners(CornerBuffer);
            bounds = new Bounds(CornerBuffer[0], Vector3.zero);
            for (int i = 1; i < 4; i++)
            {
                bounds.Encapsulate(CornerBuffer[i]);
            }
        }
        return bounds;
    }
}
