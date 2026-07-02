using System;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
///     The view for a single skill-tree node: a button showing the node's name and cost, with a border
///     tinted by state (hidden / locked / available / purchased), a tick on purchased nodes, and a lock on
///     teased ones. Instantiated and positioned by <see cref="SkillTreeView" />,
///     which also feeds it the <see cref="SkillTreeService" /> and a click callback. Raises
///     <see cref="HoverEntered" />/<see cref="HoverExited" /> for the tree's <see cref="SkillTooltip" /> and
///     plays optional <see cref="MMF_Player" /> feedbacks on spawn (enable), hover, and successful purchase
///     (the purchase one is triggered by <see cref="SkillTreeView" />, which owns the buy result).
/// </summary>
public class SkillNodeView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text costText;
    [Tooltip("Border ring tinted by node state; the background image keeps its authored color.")]
    [SerializeField] private Image border;
    [Tooltip("Optional: shows the node's CSV-authored icon (resolved through the tree's SkillTreeSO). Hidden when the node has none.")]
    [SerializeField] private Image icon;
    [Tooltip("Shown on purchased nodes.")]
    [SerializeField] private GameObject purchasedTick;
    [Tooltip("Shown on teased nodes — the one-step-ahead preview that can't be bought yet.")]
    [SerializeField] private GameObject teasedLock;

    [Header("Feedbacks (optional)")]
    [Tooltip("Played when a prereq purchase reveals this node (not on initial tree build — the death screen is alpha-hidden but active then, and every node would fire at scene load).")]
    [SerializeField] private MMF_Player spawnFeedback;
    [Tooltip("Played when the pointer enters the node.")]
    [SerializeField] private MMF_Player hoverFeedback;
    [Tooltip("Played when this node is successfully purchased.")]
    [SerializeField] private MMF_Player purchaseFeedback;

    [Tooltip("Appended after the cost number, e.g. \" pts\" for a Reincarnate Points tree. Leave blank for gold.")]
    [SerializeField] private string costSuffix = "";

    [Header("State colors")]
    [SerializeField] private Color availableColor = new Color(0.9f, 0.8f, 0.3f);
    [SerializeField] private Color lockedColor = new Color(0.4f, 0.4f, 0.4f);
    [SerializeField] private Color purchasedColor = new Color(0.3f, 0.8f, 0.4f);
    [Tooltip("Tint for teased nodes — visible one step ahead of the frontier, not purchasable yet.")]
    [SerializeField] private Color teasedColor = new Color(0.2f, 0.2f, 0.25f);
    [Tooltip("Icon alpha on teased nodes, so the lookahead reads as locked.")]
    [SerializeField] [Range(0f, 1f)] private float teasedIconAlpha = 0.4f;

    private SkillNode node;
    private ISkillTreeService service;
    private Action<string> onClicked;

    public SkillNode Node => node;

    /// <summary>Raised when the pointer enters/leaves this node while it is shown.</summary>
    public event Action<SkillNodeView> HoverEntered;
    public event Action<SkillNodeView> HoverExited;

    public void Bind(SkillNode node, ISkillTreeService service, Action<string> onClicked)
    {
        this.node = node;
        this.service = service;
        this.onClicked = onClicked;

        if (nameText != null) nameText.text = node.displayName;
        if (costText != null) costText.text = node.cost + costSuffix;

        if (icon != null)
        {
            Sprite sprite = service.Tree != null ? service.Tree.GetIcon(node.iconName) : null;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
        }

        Refresh();
    }

    private void OnEnable()
    {
        // Instantiate fires OnEnable before Bind, so node == null distinguishes the initial build
        // (where already-revealed nodes must stay silent — the death screen is alpha-hidden but active
        // at scene load) from Refresh re-activating this node when a prereq purchase reveals it.
        if (node != null && spawnFeedback != null)
        {
            spawnFeedback.PlayFeedbacks();
        }
    }

    private void OnDisable()
    {
        // Pointer-exit never fires on an object that got deactivated under the cursor, so make sure
        // the tooltip doesn't stick around.
        HoverExited?.Invoke(this);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }
    }

    private void HandleClick()
    {
        if (node != null)
        {
            onClicked?.Invoke(node.id);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverFeedback != null)
        {
            hoverFeedback.PlayFeedbacks();
        }
        HoverEntered?.Invoke(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HoverExited?.Invoke(this);
    }

    /// <summary>Called by <see cref="SkillTreeView" /> when this node's purchase went through.</summary>
    public void PlayPurchaseFeedback()
    {
        if (purchaseFeedback != null)
        {
            purchaseFeedback.PlayFeedbacks();
        }
    }

    /// <summary>Updates the node's visuals (and whether it's shown at all) from current tree state.</summary>
    public void Refresh()
    {
        if (node == null || service == null)
        {
            return;
        }

        bool purchased = service.IsPurchased(node.id);
        bool revealed = service.IsRevealed(node);
        bool teased = service.IsTeased(node);

        // Fully hidden nodes (more than one step past the frontier) are not shown at all; teased ones
        // show dimmed as a preview of what buying their prereq unlocks.
        gameObject.SetActive(revealed || teased);
        if (!revealed && !teased)
        {
            return;
        }

        bool canBuy = service.CanPurchase(node);

        if (border != null)
        {
            border.color = purchased ? purchasedColor : teased ? teasedColor : (canBuy ? availableColor : lockedColor);
        }
        if (purchasedTick != null && purchasedTick.activeSelf != purchased)
        {
            purchasedTick.SetActive(purchased);
        }
        if (teasedLock != null && teasedLock.activeSelf != teased)
        {
            teasedLock.SetActive(teased);
        }
        if (icon != null && icon.enabled)
        {
            Color iconColor = icon.color;
            iconColor.a = teased ? teasedIconAlpha : 1f;
            icon.color = iconColor;
        }
        if (button != null)
        {
            button.interactable = canBuy;
        }
        if (costText != null)
        {
            costText.text = purchased ? "Owned" : node.cost + costSuffix;
        }
    }
}
