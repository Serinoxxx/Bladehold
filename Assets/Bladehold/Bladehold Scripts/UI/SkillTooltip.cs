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

    /// <summary>Fills the tooltip from a node, shows it, and snaps it to the cursor.</summary>
    public void Show(SkillNode node, bool purchased)
    {
        if (node == null)
        {
            return;
        }

        if (nameText != null) nameText.text = node.displayName;
        if (descriptionText != null) descriptionText.text = node.description;
        if (costText != null) costText.text = purchased ? "Owned" : node.cost + costSuffix;

        gameObject.SetActive(true);
        FollowCursor();
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
