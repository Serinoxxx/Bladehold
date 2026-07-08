using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
///     Scene-view input for the skill tree editor (hooked to SceneView.duringSceneGui by
///     <see cref="SkillTreeSceneEditor" /> while a session is active): click to select, drag to move
///     (snapped to the 0.5 grid, connectors following live), drag empty space for a marquee box-select,
///     hover a node for its '+' add-child button, Delete to remove the selection (confirmed), a link-pick
///     mode (entered from the overlay) that links the next node clicked to the source node, and an
///     Alt+click shortcut that does the same in one click. A link is symmetric — purchasing either end
///     unlocks the other — so which node was "source" vs "clicked" doesn't matter once it's made.
///     All state mutations go through <see cref="SkillTreeSceneEditor" />; this class only translates
///     IMGUI events and draws the selection/marquee/link visuals.
/// </summary>
public static class SkillTreeSceneGuiHandler
{
    private enum Mode
    {
        Idle,
        DragNodes,
        Marquee,
    }

    private static Mode mode = Mode.Idle;
    private static string hoveredId;
    private static string grabbedId;
    private static bool didDrag;
    private static Vector2 dragStartGrid;
    private static readonly Dictionary<string, Vector2> dragStartPositions = new Dictionary<string, Vector2>();
    private static Vector2 marqueeStart;
    private static Vector2 marqueeEnd;

    /// <summary>Non-null while waiting for the user to click a link target (the prereq source id).</summary>
    public static string LinkSourceId { get; private set; }
    public static bool IsLinking => LinkSourceId != null;

    private static readonly Color SelectionFill = new Color(0.3f, 0.6f, 1f, 0.12f);
    private static readonly Color SelectionOutline = new Color(0.3f, 0.6f, 1f, 0.9f);
    private static readonly Color HoverOutline = new Color(1f, 1f, 1f, 0.5f);
    private static readonly Color MarqueeFill = new Color(0.3f, 0.6f, 1f, 0.08f);
    private static readonly Color LinkLineColor = new Color(1f, 0.8f, 0.2f, 0.9f);

    private const float PlusButtonSize = 22f;

    /// <summary>Called by the overlay's "Link Skill" toggle: the next node clicked gains sourceId as a prereq.</summary>
    public static void EnterLinkMode(string sourceId)
    {
        LinkSourceId = sourceId;
        SceneView.RepaintAll();
    }

    public static void ExitLinkMode()
    {
        LinkSourceId = null;
        SceneView.RepaintAll();
    }

    public static void OnSceneGUI(SceneView sceneView)
    {
        if (!SkillTreeSceneEditor.IsActive)
        {
            return;
        }

        Event evt = Event.current;
        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        // Claim the default control so Unity's own scene picking never selects canvas objects
        // underneath the tool.
        HandleUtility.AddDefaultControl(controlId);

        switch (evt.type)
        {
            case EventType.MouseMove:
                UpdateHover(evt.mousePosition, sceneView);
                break;

            case EventType.MouseDown:
                if (evt.button == 0)
                {
                    if (evt.alt)
                    {
                        HandleAltClickLink(evt);
                    }
                    else
                    {
                        HandleMouseDown(evt, controlId);
                    }
                }
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl == controlId)
                {
                    HandleMouseDrag(evt, sceneView);
                }
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl == controlId && evt.button == 0)
                {
                    HandleMouseUp(evt, controlId);
                }
                break;

            case EventType.KeyDown:
                HandleKeyDown(evt);
                break;

            case EventType.Repaint:
                Draw(evt.mousePosition);
                break;
        }
    }

    // ---------------------------------------------------------------- event handlers

    private static void UpdateHover(Vector2 mouse, SceneView sceneView)
    {
        string hit = HitTestNode(mouse);
        if (hit != hoveredId)
        {
            hoveredId = hit;
            sceneView.Repaint();
        }
    }

    private static void HandleMouseDown(Event evt, int controlId)
    {
        string hit = HitTestNode(evt.mousePosition);

        if (IsLinking)
        {
            if (hit != null && hit != LinkSourceId)
            {
                SkillTreeSceneEditor.LinkSkill(LinkSourceId, hit);
                ExitLinkMode();
            }
            else if (hit == null)
            {
                ExitLinkMode();
            }
            evt.Use();
            return;
        }

        // The '+' add-child button beside the hovered/selected node has priority over node picking.
        string plusOwner = PlusButtonOwner();
        if (plusOwner != null && PlusButtonRect(plusOwner).Contains(evt.mousePosition))
        {
            SkillTreeSceneEditor.AddChildNode(plusOwner);
            evt.Use();
            return;
        }

        var session = SkillTreeSceneEditor.Session;
        if (hit != null)
        {
            bool additive = evt.shift || evt.control || evt.command;
            if (additive)
            {
                SkillTreeSceneEditor.ToggleSelected(hit);
                evt.Use();
                return;
            }

            if (!session.selectedIds.Contains(hit))
            {
                SkillTreeSceneEditor.SetSelection(new[] { hit });
            }

            // Arm a group drag anchored on the grabbed node.
            mode = Mode.DragNodes;
            grabbedId = hit;
            didDrag = false;
            dragStartGrid = SkillTreeSceneEditor.Builder.GuiToGrid(evt.mousePosition);
            dragStartPositions.Clear();
            foreach (SkillTreeRow row in session.SelectedRows())
            {
                dragStartPositions[row.id] = new Vector2(row.x, row.y);
            }
            SkillTreeSceneEditor.BeginDrag();
            GUIUtility.hotControl = controlId;
            evt.Use();
        }
        else
        {
            if (!evt.shift)
            {
                SkillTreeSceneEditor.ClearSelection();
            }
            mode = Mode.Marquee;
            marqueeStart = marqueeEnd = evt.mousePosition;
            GUIUtility.hotControl = controlId;
            evt.Use();
        }
    }

    /// <summary>
    ///     Alt+click shortcut: links the clicked node with the single selected node (a link is symmetric —
    ///     purchasing either one unlocks the other), without entering the overlay's Link Skill mode first.
    ///     Only consumes the event on an actual node hit, so alt-click/drag elsewhere still reaches the
    ///     Scene view's own camera controls.
    /// </summary>
    private static void HandleAltClickLink(Event evt)
    {
        if (IsLinking)
        {
            return;
        }

        string hit = HitTestNode(evt.mousePosition);
        if (hit == null)
        {
            return;
        }

        List<string> selected = SkillTreeSceneEditor.Session.selectedIds;
        if (selected.Count == 1 && selected[0] != hit)
        {
            SkillTreeSceneEditor.LinkSkill(hit, selected[0]);
        }
        evt.Use();
    }

    private static void HandleMouseDrag(Event evt, SceneView sceneView)
    {
        if (mode == Mode.DragNodes)
        {
            didDrag = true;
            Vector2 grid = SkillTreeSceneEditor.Builder.GuiToGrid(evt.mousePosition);
            Vector2 rawDelta = grid - dragStartGrid;

            // Snap the grabbed node's target, then move everything by that same snapped delta so the
            // selection stays rigid (relative offsets never distort).
            if (dragStartPositions.TryGetValue(grabbedId, out Vector2 grabbedStart))
            {
                Vector2 target = grabbedStart + rawDelta;
                Vector2 snappedDelta = new Vector2(
                    SkillTreeSceneEditor.Snap(target.x) - grabbedStart.x,
                    SkillTreeSceneEditor.Snap(target.y) - grabbedStart.y);
                foreach (KeyValuePair<string, Vector2> pair in dragStartPositions)
                {
                    SkillTreeSceneEditor.SetNodeGrid(pair.Key, pair.Value.x + snappedDelta.x, pair.Value.y + snappedDelta.y);
                }
            }
            evt.Use();
        }
        else if (mode == Mode.Marquee)
        {
            marqueeEnd = evt.mousePosition;
            sceneView.Repaint();
            evt.Use();
        }
    }

    private static void HandleMouseUp(Event evt, int controlId)
    {
        if (mode == Mode.Marquee)
        {
            Rect marquee = RectFromPoints(marqueeStart, marqueeEnd);
            var picked = new List<string>();
            if (evt.shift)
            {
                picked.AddRange(SkillTreeSceneEditor.Session.selectedIds);
            }
            foreach ((string id, Rect guiRect) in SkillTreeSceneEditor.Builder.AllNodeGuiRects())
            {
                if (marquee.Overlaps(guiRect) && !picked.Contains(id))
                {
                    picked.Add(id);
                }
            }
            SkillTreeSceneEditor.SetSelection(picked);
        }
        else if (mode == Mode.DragNodes && !didDrag && grabbedId != null && !evt.shift)
        {
            // A click (no movement) on an already-selected node reduces the selection to it.
            SkillTreeSceneEditor.SetSelection(new[] { grabbedId });
        }

        mode = Mode.Idle;
        grabbedId = null;
        GUIUtility.hotControl = 0;
        evt.Use();
    }

    private static void HandleKeyDown(Event evt)
    {
        if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace)
        {
            if (SkillTreeSceneEditor.Session.selectedIds.Count > 0)
            {
                SkillTreeSceneEditor.DeleteSelection();
                evt.Use();
            }
        }
        else if (evt.keyCode == KeyCode.Escape)
        {
            if (IsLinking)
            {
                ExitLinkMode();
                evt.Use();
            }
            else if (SkillTreeSceneEditor.Session.selectedIds.Count > 0)
            {
                SkillTreeSceneEditor.ClearSelection();
                evt.Use();
            }
        }
    }

    // ---------------------------------------------------------------- drawing

    private static void Draw(Vector2 mouse)
    {
        Handles.BeginGUI();

        var session = SkillTreeSceneEditor.Session;
        foreach (string id in session.selectedIds)
        {
            Rect rect = SkillTreeSceneEditor.Builder.NodeGuiRect(id);
            if (rect.width <= 0f)
            {
                continue;
            }
            EditorGUI.DrawRect(rect, SelectionFill);
            DrawOutline(rect, SelectionOutline, 2f);
        }

        if (hoveredId != null && !session.selectedIds.Contains(hoveredId))
        {
            Rect rect = SkillTreeSceneEditor.Builder.NodeGuiRect(hoveredId);
            if (rect.width > 0f)
            {
                DrawOutline(rect, HoverOutline, 1f);
            }
        }

        string plusOwner = PlusButtonOwner();
        if (plusOwner != null && !IsLinking)
        {
            Rect plus = PlusButtonRect(plusOwner);
            GUI.Box(plus, GUIContent.none);
            EditorGUI.DrawRect(plus, plus.Contains(mouse) ? new Color(0.35f, 0.7f, 0.35f, 0.95f) : new Color(0.25f, 0.5f, 0.25f, 0.9f));
            GUI.Label(plus, "+", PlusLabelStyle());
        }

        if (mode == Mode.Marquee)
        {
            Rect marquee = RectFromPoints(marqueeStart, marqueeEnd);
            EditorGUI.DrawRect(marquee, MarqueeFill);
            DrawOutline(marquee, SelectionOutline, 1f);
        }

        if (IsLinking)
        {
            Vector2 from = SkillTreeSceneEditor.Builder.NodeGuiCenter(LinkSourceId);
            Handles.color = LinkLineColor;
            Handles.DrawAAPolyLine(3f, new Vector3(from.x, from.y, 0f), new Vector3(mouse.x, mouse.y, 0f));

            var label = new GUIContent($"Click a node to link with '{LinkSourceId}' (either unlocks the other) — Esc to cancel");
            Vector2 size = EditorStyles.helpBox.CalcSize(label);
            GUI.Label(new Rect(mouse.x + 16f, mouse.y + 8f, size.x + 8f, size.y + 4f), label, EditorStyles.helpBox);
        }

        Handles.EndGUI();
    }

    private static GUIStyle plusLabelStyle;

    private static GUIStyle PlusLabelStyle()
    {
        if (plusLabelStyle == null)
        {
            plusLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                normal = { textColor = Color.white },
            };
        }
        return plusLabelStyle;
    }

    private static void DrawOutline(Rect rect, Color color, float thickness)
    {
        EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
        EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
    }

    // ---------------------------------------------------------------- hit-testing

    private static string HitTestNode(Vector2 guiPos)
    {
        foreach ((string id, Rect guiRect) in SkillTreeSceneEditor.Builder.AllNodeGuiRects())
        {
            if (guiRect.Contains(guiPos))
            {
                return id;
            }
        }
        return null;
    }

    /// <summary>The node currently showing a '+' button: the hovered node, else a single selection.</summary>
    private static string PlusButtonOwner()
    {
        if (hoveredId != null)
        {
            return hoveredId;
        }
        var selected = SkillTreeSceneEditor.Session.selectedIds;
        return selected.Count == 1 ? selected[0] : null;
    }

    private static Rect PlusButtonRect(string id)
    {
        Rect node = SkillTreeSceneEditor.Builder.NodeGuiRect(id);
        return new Rect(node.xMax + 4f, node.center.y - PlusButtonSize * 0.5f, PlusButtonSize, PlusButtonSize);
    }

    private static Rect RectFromPoints(Vector2 a, Vector2 b)
    {
        return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }
}
