using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     The view for a single skill-tree node: a button showing the node's name and cost, tinted by state
///     (hidden / locked / available / purchased). Instantiated and positioned by <see cref="SkillTreeView" />,
///     which also feeds it the <see cref="SkillTreeService" /> and a click callback.
/// </summary>
public class SkillNodeView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private Image background;

    [Header("State colors")]
    [SerializeField] private Color availableColor = new Color(0.9f, 0.8f, 0.3f);
    [SerializeField] private Color lockedColor = new Color(0.4f, 0.4f, 0.4f);
    [SerializeField] private Color purchasedColor = new Color(0.3f, 0.8f, 0.4f);

    private SkillNode node;
    private SkillTreeService service;
    private Action<string> onClicked;

    public SkillNode Node => node;

    public void Bind(SkillNode node, SkillTreeService service, Action<string> onClicked)
    {
        this.node = node;
        this.service = service;
        this.onClicked = onClicked;

        if (nameText != null) nameText.text = node.displayName;
        if (costText != null) costText.text = node.cost.ToString();

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
        }

        Refresh();
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

    /// <summary>Updates the node's visuals (and whether it's shown at all) from current tree state.</summary>
    public void Refresh()
    {
        if (node == null || service == null)
        {
            return;
        }

        bool purchased = service.IsPurchased(node.id);
        bool revealed = service.IsRevealed(node);

        // Hidden nodes (no prereq bought yet) are not shown at all.
        gameObject.SetActive(revealed);
        if (!revealed)
        {
            return;
        }

        bool canBuy = service.CanPurchase(node);

        if (background != null)
        {
            background.color = purchased ? purchasedColor : (canBuy ? availableColor : lockedColor);
        }
        if (button != null)
        {
            button.interactable = canBuy;
        }
        if (costText != null)
        {
            costText.text = purchased ? "Owned" : node.cost.ToString();
        }
    }
}
