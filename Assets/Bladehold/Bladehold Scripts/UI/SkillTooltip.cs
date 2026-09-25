using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
///     Cursor-following tooltip for draft cards (e.g. the acquired-skills sidebar): shows a card's name,
///     description and an optional cost line. While visible it follows the mouse every frame, flipping
///     which corner hugs the cursor per screen half so it never runs off screen. A CanvasGroup with
///     raycasts blocked is forced on so the tooltip can sit next to the cursor without stealing the very
///     hover it is reporting on.
/// </summary>
public class SkillTooltip : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text beforeAfterText;
    [SerializeField] private TMP_Text percentIncreaseText;
    [SerializeField] private TMP_Text costText;

    [Tooltip("Appended after the cost number (a common.* loc key, e.g. \"gold\"). Leave blank for a bare number.")]
    [SerializeField] private string costSuffix = "";

    [Tooltip("Distance from the cursor to the tooltip's near corner, in screen pixels.")]
    [SerializeField] private Vector2 cursorOffset = new Vector2(18f, 18f);

    [Header("Sorting")]
    [Tooltip("Sorting order for the tooltip canvas to render on top of all UI layers.")]
    [SerializeField] private int sortingOrder = 30000;

    private RectTransform rect;
    private Canvas canvas;
    private Canvas tooltipCanvas;

    private void Awake()
    {
        rect = (RectTransform)transform;
        canvas = transform.parent != null ? transform.parent.GetComponentInParent<Canvas>(true) : null;

        tooltipCanvas = GetComponent<Canvas>();
        if (tooltipCanvas == null)
        {
            tooltipCanvas = gameObject.AddComponent<Canvas>();
        }
        tooltipCanvas.overrideSorting = true;
        tooltipCanvas.sortingOrder = sortingOrder;

        if (canvas != null)
        {
            tooltipCanvas.additionalShaderChannels = canvas.additionalShaderChannels;
            tooltipCanvas.sortingLayerID = canvas.sortingLayerID;
        }

        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = gameObject.AddComponent<CanvasGroup>();
        }
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    private void OnEnable()
    {
        transform.SetAsLastSibling();
    }

    private void Start()
    {
        if (nameText == null || descriptionText == null || costText == null)
        {
            Debug.LogError("SkillTooltip is missing one of its TMP_Text references (name/description/cost).");
        }
        if (canvas == null && transform.parent != null)
        {
            canvas = transform.parent.GetComponentInParent<Canvas>(true);
            if (canvas != null && tooltipCanvas != null)
            {
                tooltipCanvas.additionalShaderChannels = canvas.additionalShaderChannels;
                tooltipCanvas.sortingLayerID = canvas.sortingLayerID;
            }
        }
        if (canvas == null && tooltipCanvas == null)
        {
            Debug.LogError("SkillTooltip is not under a Canvas.");
        }
    }

    /// <summary>
    ///     Shows a draft card at <paramref name="level" /> with an optional cost line.
    /// </summary>
    public void ShowDirect(SkillNode node, int level, int cost, bool isMaxed)
    {
        if (node == null) return;
        if (nameText != null) nameText.text = node.LocalizedDisplayName ?? "";
        if (descriptionText != null) descriptionText.text = DescriptionFor(node, level) ?? "";
        if (beforeAfterText != null) beforeAfterText.text = "";
        if (percentIncreaseText != null) percentIncreaseText.text = "";
        if (costText != null)
        {
            costText.text = isMaxed ? Loc.Get("common.maxed", "MAXED") : FormatCost(cost);
            if (costText.transform.parent != null && costText.transform.parent != transform)
            {
                costText.transform.parent.gameObject.SetActive(true);
            }
        }

        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        FollowCursor();
    }

    /// <summary>
    ///     Direct text-based overload for generic tooltips.
    /// </summary>
    public void ShowDirect(string title, string description, string costInfo = "")
    {
        if (nameText != null) nameText.text = title ?? "";
        if (descriptionText != null) descriptionText.text = description ?? "";
        if (beforeAfterText != null) beforeAfterText.text = "";
        if (percentIncreaseText != null) percentIncreaseText.text = "";
        if (costText != null)
        {
            costText.text = costInfo ?? "";
            if (costText.transform.parent != null && costText.transform.parent != transform)
            {
                costText.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(costInfo));
            }
        }

        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        FollowCursor();
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

    /// <summary>Cost with its currency word: the suffix doubles as a common.* loc key.</summary>
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
        gameObject.SetActive(false);
    }

    private void Update()
    {
        FollowCursor();
    }

    private Camera GetCanvasCamera()
    {
        Canvas c = canvas != null ? canvas.rootCanvas : tooltipCanvas?.rootCanvas;
        if (c == null || c.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }
        return c.worldCamera;
    }

    private void FollowCursor()
    {
        if (Mouse.current == null)
        {
            return;
        }

        Vector2 screenPos = Mouse.current.position.ReadValue();
        PlaceAt(screenPos, GetCanvasCamera());
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
            rect.localPosition = new Vector3(localPoint.x, localPoint.y, 0f);
        }
    }
}
