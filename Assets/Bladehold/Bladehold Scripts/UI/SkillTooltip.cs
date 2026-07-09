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
    ///     The service supplies purchased state and the ladder-aware cost (family nodes climb a shared
    ///     price ladder, so it isn't just the node's own <see cref="SkillNode.cost" />). For a multi-level
    ///     (family) node the name gains a tier numeral, and — when the node is a single, still-buyable stat
    ///     effect — the description is replaced by a live "before → after (+%)" block read from
    ///     <see cref="Player.Instance" />'s stats. Anything else keeps its authored description.
    /// </summary>
    public void Show(SkillNode node, ISkillTreeService service)
    {
        if (node == null || service == null)
        {
            return;
        }

        bool purchased = service.IsPurchased(node.id);
        int cost = service.GetCost(node);

        if (nameText != null) nameText.text = BuildName(node, service, purchased);
        var (beforeAfter, percentIncrease) = GetImprovementValues(node, purchased);
        if (descriptionText != null) descriptionText.text = node.description;
        if (beforeAfterText != null) beforeAfterText.text = beforeAfter;
        if (percentIncreaseText != null) percentIncreaseText.text = percentIncrease;
        if (costText != null) costText.text = purchased ? "Owned" : cost + costSuffix;

        gameObject.SetActive(true);
        FollowCursor();
    }

    /// <summary>Appends a tier numeral to a multi-level (family) node's name — the tier a purchase reaches.</summary>
    private static string BuildName(SkillNode node, ISkillTreeService service, bool purchased)
    {
        return node.displayName;

        //not doing roman numerals for now, but leaving the code in case we want to add it back in later
        //int familySize = FamilyCount(node, service, out int familyPurchased);
        //if (familySize <= 1)
        //{
        //    return node.displayName;
        //}

        //// Ladder purchases are order-independent, so the numeral reads as "the tier you'd reach".
        //int tier = familyPurchased + (purchased ? 0 : 1);
        //return node.displayName + " " + ToRoman(tier);
    }

    /// <summary>The dynamic value block for a clean single-effect leveled node, else the authored text.</summary>
    private (string beforeAfter, string percentIncrease) GetImprovementValues(SkillNode node, bool purchased)
    {
        PlayerStats stats = Player.Instance != null ? Player.Instance.Stats : null;
        bool multiLevel = !string.IsNullOrEmpty(node.family);
        if (purchased || multiLevel == false || node.effects.Count != 1 || stats == null)
        {
            return (null, null);
        }

        SkillEffect e = node.effects[0];
        float before = stats.GetValue(e.stat);
        float after = stats.PreviewValue(e.stat, e.kind, e.amount);

        string beforeAfter = $"{StatDisplay.Label(e.stat)} {StatDisplay.Value(e.stat, before)} -> {StatDisplay.Value(e.stat, after)}.";
        string percentIncrease = null;
        if (StatDisplay.ShowsPercentDelta(e.stat) && before > 0f && after > before)
        {
            int pct = Mathf.RoundToInt((after - before) / before * 100f);
            percentIncrease = pct + "% increase";
        }

        return (beforeAfter, percentIncrease);
    }

    /// <summary>Counts nodes in the same family (0/1 = not a real family), and how many are purchased.</summary>
    private static int FamilyCount(SkillNode node, ISkillTreeService service, out int purchasedInFamily)
    {
        purchasedInFamily = 0;
        if (string.IsNullOrEmpty(node.family) || service.Tree == null)
        {
            return 0;
        }

        int count = 0;
        foreach (SkillNode other in service.Tree.Nodes)
        {
            if (other.family != node.family)
            {
                continue;
            }
            count++;
            if (service.IsPurchased(other.id))
            {
                purchasedInFamily++;
            }
        }
        return count;
    }

    private static readonly string[] RomanUnits = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" };
    private static readonly string[] RomanTens = { "", "X", "XX", "XXX" };

    /// <summary>Roman numeral for small tier counts (1-39 covers the deepest family); larger falls back to digits.</summary>
    private static string ToRoman(int n)
    {
        if (n <= 0 || n >= 40)
        {
            return n.ToString();
        }
        return RomanTens[n / 10] + RomanUnits[n % 10];
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void Update()
    {
        FollowCursor();
    }

    private void FollowCursor()
    {
        if (Mouse.current == null || canvas == null)
        {
            return;
        }

        Vector2 screenPos = Mouse.current.position.ReadValue();

        // Which corner hugs the cursor depends on the screen half, so the tooltip always opens inward.
        float pivotX = screenPos.x > Screen.width * 0.5f ? 1f : 0f;
        float pivotY = screenPos.y > Screen.height * 0.5f ? 0f : 1f;
        rect.pivot = new Vector2(pivotX, pivotY);

        Vector2 offset = new Vector2(
            pivotX == 0f ? cursorOffset.x : -cursorOffset.x,
            pivotY == 1f ? -cursorOffset.y : cursorOffset.y);

        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)rect.parent, screenPos + offset, cam, out Vector2 localPoint))
        {
            rect.localPosition = localPoint;
        }
    }
}
