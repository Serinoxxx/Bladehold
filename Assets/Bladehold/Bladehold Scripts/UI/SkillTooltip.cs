using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
///     Cursor-following tooltip for the skill tree: shows a hovered node's name, description, and cost.
///     One instance lives (inactive by default) on each tree's canvas; <see cref="SkillTreeView" /> shows and
///     hides it as <see cref="SkillNodeView" /> hover events come in. While visible it follows the mouse every
///     frame, flipping which corner hugs the cursor per screen half so it never runs off screen. A
///     CanvasGroup with raycasts blocked is forced on so the tooltip can sit next to the cursor without
///     stealing the very hover it is reporting on.
/// </summary>
public class SkillTooltip : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text beforeAfterText;
    [SerializeField] private TMP_Text percentIncreaseText;
    [SerializeField] private TMP_Text costText;

    [Tooltip("Appended after the cost number, e.g. \" pts\" for a Reincarnate Points tree. Leave blank for gold.")]
    [SerializeField] private string costSuffix = "";

    [Tooltip("Distance from the cursor to the tooltip's near corner, in screen pixels.")]
    [SerializeField] private Vector2 cursorOffset = new Vector2(18f, 18f);

    private RectTransform rect;
    private Canvas canvas;

    private void Awake()
    {
        rect = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>(true);

        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = gameObject.AddComponent<CanvasGroup>();
        }
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    private void Start()
    {
        if (nameText == null || descriptionText == null || costText == null)
        {
            Debug.LogError("SkillTooltip is missing one of its TMP_Text references (name/description/cost).");
        }
        if (canvas == null)
        {
            Debug.LogError("SkillTooltip is not under a Canvas.");
        }
    }

    /// <summary>
    ///     Fills the tooltip from a node and its owning service, shows it, and snaps it to the cursor.
    ///     The service supplies the node's current level and the next-level cost. The description is the
    ///     node's unlock text before it's owned, its upgrade text once owned and still upgradeable; a
    ///     multi-level name gains a current/max level suffix; and — when the node is a single,
    ///     still-buyable stat effect — a live "before → after (+%)" block for the <em>next</em> level is
    ///     read from <see cref="Player.Instance" />'s stats.
    /// </summary>
    public void Show(SkillNode node, ISkillTreeService service) => Show(node, service, true);

    /// <summary>
    ///     <paramref name="showLiveImprovement" /> = false skips the before → after stat block: it reads
    ///     <see cref="Player.Instance" />'s current stats, which is wrong when previewing a class that
    ///     isn't the active one (the class-select screen's Key Skills row).
    /// </summary>
    public void Show(SkillNode node, ISkillTreeService service, bool showLiveImprovement)
    {
        if (node == null || service == null)
        {
            return;
        }

        int level = service.GetLevel(node);
        bool maxed = service.IsMaxed(node);
        int cost = service.GetCost(node);

        if (nameText != null) nameText.text = BuildName(node) ?? "";
        var (beforeAfter, percentIncrease) = showLiveImprovement
            ? GetImprovementValues(node, level, maxed)
            : ("", "");
        if (descriptionText != null) descriptionText.text = DescriptionFor(node, level) ?? "";
        if (beforeAfterText != null) beforeAfterText.text = beforeAfter ?? "";
        if (percentIncreaseText != null) percentIncreaseText.text = percentIncrease ?? "";
        if (costText != null) costText.text = maxed ? Loc.Get("common.maxed") : FormatCost(cost);

        anchoredTo = null;
        gameObject.SetActive(true);
        FollowCursor();
    }

    /// <summary>
    ///     Gamepad flavor of <see cref="Show" />: same content, but instead of following the cursor the
    ///     tooltip pins itself beside <paramref name="anchor" /> (the selected node's rect), re-anchoring
    ///     as the tree pans/zooms. Cleared by <see cref="Hide" /> or the next pointer-driven Show.
    /// </summary>
    public void ShowAtRect(SkillNode node, ISkillTreeService service, RectTransform anchor)
    {
        Show(node, service);
        anchoredTo = anchor;
        FollowAnchor();
    }

    /// <summary>Appends the current/max level to a multi-level node's name (e.g. "Sharpened Edge 3/10").</summary>
    private static string BuildName(SkillNode node)
    {
        if (node.maxLevel <= 1)
        {
            return node.LocalizedDisplayName;
        }
        // level shown by the owning view's badge; here the name just carries the max so the tooltip reads
        // as a leveled skill.
        return node.LocalizedDisplayName;
    }

    /// <summary>Unlock text before the node is owned; upgrade text (when authored) once it has a level.</summary>
    private static string DescriptionFor(SkillNode node, int level)
    {
        if (level >= 1 && !string.IsNullOrEmpty(node.upgradeText))
        {
            return node.LocalizedUpgradeText;
        }
        return node.LocalizedDescription;
    }

    /// <summary>The dynamic value block for a clean single-effect node's next level, else the authored text.</summary>
    private (string beforeAfter, string percentIncrease) GetImprovementValues(SkillNode node, int level, bool maxed)
    {
        PlayerStats stats = Player.Instance != null ? Player.Instance.Stats : null;
        if (maxed || node.effects.Count != 1 || stats == null)
        {
            return ("", "");
        }

        SkillEffect e = node.effects[0];
        float amount = e.AmountForLevel(level + 1);
        float before = stats.GetValue(e.stat);
        float after = stats.PreviewValue(e.stat, e.kind, amount);

        string beforeAfter = $"{StatDisplay.Label(e.stat)} {StatDisplay.Value(e.stat, before)} -> {StatDisplay.Value(e.stat, after)}.";
        string percentIncrease = "";
        if (StatDisplay.ShowsPercentDelta(e.stat) && before > 0f && after > before)
        {
            int pct = Mathf.RoundToInt((after - before) / before * 100f);
            percentIncrease = Loc.Format("skill.percent_increase", pct);
        }

        return (beforeAfter, percentIncrease);
    }

    /// <summary>Same currency-word rendering as <see cref="SkillNodeView" />: the suffix doubles as a common.* loc key.</summary>
    private string FormatCost(int cost)
    {
        string suffix = costSuffix != null ? costSuffix.Trim() : "";
        if (suffix.Length == 0)
        {
            return cost.ToString();
        }
        return Loc.Format("skill.cost", cost, Loc.Get("common." + suffix, suffix));
    }

    public void Hide()
    {
        anchoredTo = null;
        gameObject.SetActive(false);
    }

    private RectTransform anchoredTo;

    private void Update()
    {
        if (anchoredTo != null)
        {
            FollowAnchor();
        }
        else
        {
            FollowCursor();
        }
    }

    /// <summary>Pins the tooltip beside the anchored node, using the same inward pivot flip as the cursor path.</summary>
    private void FollowAnchor()
    {
        if (anchoredTo == null || canvas == null)
        {
            return;
        }

        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, anchoredTo.position);
        PlaceAt(screenPos, cam);
    }

    private void FollowCursor()
    {
        if (Mouse.current == null || canvas == null)
        {
            return;
        }

        Vector2 screenPos = Mouse.current.position.ReadValue();
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        PlaceAt(screenPos, cam);
    }

    /// <summary>Positions the tooltip near a screen point, flipping which corner hugs it per screen half so it always opens inward.</summary>
    private void PlaceAt(Vector2 screenPos, Camera cam)
    {
        float pivotX = screenPos.x > Screen.width * 0.5f ? 1f : 0f;
        float pivotY = screenPos.y > Screen.height * 0.5f ? 0f : 1f;
        rect.pivot = new Vector2(pivotX, pivotY);

        Vector2 offset = new Vector2(
            pivotX == 0f ? cursorOffset.x : -cursorOffset.x,
            pivotY == 1f ? -cursorOffset.y : cursorOffset.y);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)rect.parent, screenPos + offset, cam, out Vector2 localPoint))
        {
            rect.localPosition = localPoint;
        }
    }
}
