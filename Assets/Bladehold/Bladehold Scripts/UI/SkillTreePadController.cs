using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
///     Gamepad control for one <see cref="SkillTreeView" /> — sits beside it and drives its public
///     pad API while the tree is on screen: <b>right stick</b> pans, <b>LT/RT</b> zoom around the
///     viewport center, <b>left stick / d-pad</b> flicks move a node selection spatially over the
///     authored grid, <b>A</b> buys the selected node, and <b>LB/RB</b> fire the serialized tab
///     events (wired to the death screen's tab buttons). Selection shows the pinned tooltip
///     (<see cref="SkillTooltip.ShowAtRect" />) and pans the view to follow when it leaves the
///     viewport. Owns its own <see cref="MenuInputActions" /> instance, enabling only the UiNav map,
///     and only while this component is active AND a gamepad is the active device — mouse users see
///     no change. All timing is unscaled: these screens show while Time.timeScale is 0.
/// </summary>
public class SkillTreePadController : MonoBehaviour
{
    [Tooltip("The tree this controller drives; auto-wired from this GameObject.")]
    [SerializeField] private SkillTreeView treeView;
    [Tooltip("Pan speed at full stick deflection, in content pixels per second.")]
    [SerializeField] private float panSpeed = 900f;
    [Tooltip("Zoom change per second at full trigger pull (same units as the wheel's zoomStep accumulation).")]
    [SerializeField] private float zoomSpeed = 1.2f;
    [Tooltip("Stick deflection that registers a selection flick; the stick must fall below half of this to re-arm.")]
    [SerializeField] [Range(0.2f, 0.95f)] private float flickThreshold = 0.6f;
    [Tooltip("Nodes outside a cone this wide (degrees) around the flick direction are not selection candidates.")]
    [SerializeField] private float selectionConeDegrees = 75f;
    [Tooltip("Invoked on LB — wire the previous tab button's onClick.")]
    [SerializeField] private UnityEvent onTabPrev;
    [Tooltip("Invoked on RB — wire the next tab button's onClick.")]
    [SerializeField] private UnityEvent onTabNext;

    private MenuInputActions actions;
    private SkillNodeView selected;
    private bool flickArmed = true;

    private void OnValidate()
    {
        if (treeView == null)
        {
            treeView = GetComponent<SkillTreeView>();
        }
    }

    private void Start()
    {
        if (treeView == null)
        {
            Debug.LogError("SkillTreePadController has no SkillTreeView assigned or on its GameObject.");
            enabled = false;
        }
    }

    private void OnEnable()
    {
        actions ??= new MenuInputActions();
        actions.EnableUiNav();
        InputDeviceWatcher.SchemeChanged += HandleSchemeChanged;
    }

    private void OnDisable()
    {
        actions?.DisableUiNav();
        InputDeviceWatcher.SchemeChanged -= HandleSchemeChanged;
        Deselect();
    }

    private void OnDestroy()
    {
        actions?.Dispose();
        actions = null;
    }

    private void HandleSchemeChanged(ControlScheme scheme)
    {
        if (scheme != ControlScheme.Gamepad)
        {
            // Mouse took over: drop the pad selection so hover behaviour owns the tooltip again.
            Deselect();
        }
    }

    private void Update()
    {
        if (treeView == null || !treeView.IsBuilt || !InputDeviceWatcher.GamepadActive)
        {
            return;
        }

        float dt = Time.unscaledDeltaTime;

        Vector2 pan = actions.TreePan.ReadValue<Vector2>();
        if (pan.sqrMagnitude > 0.001f)
        {
            // Stick right pulls the view right = content moves left (and Unity UI y is inverted vs stick y).
            treeView.PanBy(new Vector2(-pan.x, pan.y) * (panSpeed * dt));
        }

        float zoom = actions.TreeZoom.ReadValue<float>();
        if (Mathf.Abs(zoom) > 0.01f)
        {
            treeView.ZoomBy(zoom * zoomSpeed * dt);
        }

        HandleSelectionFlick();

        if (actions.Buy.WasPressedThisFrame() && selected != null && selected.Node != null)
        {
            treeView.PurchaseNode(selected.Node.id);
            RefreshTooltip();
        }
        if (actions.TabPrev.WasPressedThisFrame())
        {
            onTabPrev?.Invoke();
        }
        if (actions.TabNext.WasPressedThisFrame())
        {
            onTabNext?.Invoke();
        }
    }

    private void HandleSelectionFlick()
    {
        Vector2 nav = actions.NodeNav.ReadValue<Vector2>();
        float magnitude = nav.magnitude;

        if (!flickArmed)
        {
            if (magnitude < flickThreshold * 0.5f)
            {
                flickArmed = true;
            }
            return;
        }
        if (magnitude < flickThreshold)
        {
            return;
        }
        flickArmed = false;

        // Stick up should read as "toward the tree's visual up"; authored grid y grows downward.
        Vector2 direction = new Vector2(nav.x, -nav.y).normalized;

        if (selected == null || !selected.isActiveAndEnabled)
        {
            SelectInitialNode();
            return;
        }
        SkillNodeView next = FindInDirection(selected, direction);
        if (next != null)
        {
            Select(next);
        }
    }

    /// <summary>First selection: the cheapest sensible anchor is the visible node nearest the tree origin — in practice the root the view opens on.</summary>
    private void SelectInitialNode()
    {
        SkillNodeView best = null;
        float bestScore = float.MaxValue;
        foreach (SkillNodeView view in treeView.VisibleNodeViews)
        {
            // Prefer on-screen nodes; among those, the top-left-most (matches the default scroll position).
            float score = view.Node.x + view.Node.y + (treeView.IsNodeInViewport(view) ? 0f : 10000f);
            if (score < bestScore)
            {
                bestScore = score;
                best = view;
            }
        }
        if (best != null)
        {
            Select(best);
        }
    }

    /// <summary>
    ///     Directional pick over the authored grid: among visible nodes inside the flick cone, the one
    ///     with the best distance-weighted alignment (near and straight ahead beats far or off-axis).
    /// </summary>
    private SkillNodeView FindInDirection(SkillNodeView from, Vector2 direction)
    {
        Vector2 origin = new Vector2(from.Node.x, from.Node.y);
        float minDot = Mathf.Cos(selectionConeDegrees * 0.5f * Mathf.Deg2Rad);

        SkillNodeView best = null;
        float bestScore = float.MaxValue;
        foreach (SkillNodeView view in treeView.VisibleNodeViews)
        {
            if (view == from)
            {
                continue;
            }
            Vector2 offset = new Vector2(view.Node.x, view.Node.y) - origin;
            float distance = offset.magnitude;
            if (distance < 0.001f)
            {
                continue;
            }
            float dot = Vector2.Dot(direction, offset / distance);
            if (dot < minDot)
            {
                continue;
            }
            float score = distance / Mathf.Max(dot, 0.1f);
            if (score < bestScore)
            {
                bestScore = score;
                best = view;
            }
        }
        return best;
    }

    private void Select(SkillNodeView view)
    {
        if (selected != null)
        {
            selected.SetSelected(false);
        }
        selected = view;
        selected.SetSelected(true);

        if (!treeView.IsNodeInViewport(selected))
        {
            treeView.CenterOn(selected.Node);
        }
        RefreshTooltip();
    }

    private void RefreshTooltip()
    {
        treeView.ShowTooltipFor(selected != null && selected.isActiveAndEnabled ? selected : null);
    }

    private void Deselect()
    {
        if (selected != null)
        {
            selected.SetSelected(false);
            if (treeView != null)
            {
                treeView.ShowTooltipFor(null);
            }
            selected = null;
        }
    }
}
