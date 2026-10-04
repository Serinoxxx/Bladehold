using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
///     One perk tile in the Spirit's meta upgrade window: icon, name, rank pips and the next rank's price.
///     Hovering or selecting it (mouse or pad) shows the perk in the details panel; clicking buys the next rank.
/// </summary>
public class MetaPerkCardUI : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    public enum CardState { Buyable, TooExpensive, Maxed, Locked }

    [SerializeField] private Button button;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text costText;
    [Tooltip("Rank pips are copies of this inactive template, one per rank.")]
    [SerializeField] private Image pipTemplate;
    [Tooltip("Shown while this card is the one in the details panel.")]
    [SerializeField] private GameObject selectedHighlight;

    [Header("Colours (roles of the MetaPerks theme)")]
    [SerializeField] private UIColorRole pipFilledColor = UIColorRole.Accent;
    [SerializeField] private UIColorRole pipEmptyColor = UIColorRole.Ghost;
    [SerializeField] private UIColorRole costBuyableColor = UIColorRole.Cost;
    [SerializeField] private UIColorRole costTooExpensiveColor = UIColorRole.Danger;
    [SerializeField] private UIColorRole costMaxedColor = UIColorRole.Success;
    [SerializeField] private UIColorRole costLockedColor = UIColorRole.TextDim;

    private readonly List<Image> pips = new List<Image>();
    private Action onSelected;
    private Action onClicked;

    public Button Button => button;
    public MetaPerkDefinitionSO Perk { get; private set; }

    private void OnValidate()
    {
        if (button == null) button = GetComponent<Button>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Awake()
    {
        if (button == null || icon == null || nameText == null || costText == null || pipTemplate == null)
        {
            Debug.LogError($"[MetaPerkCardUI] '{name}' is missing a reference (button/icon/name/cost/pipTemplate).", this);
            return;
        }
        pipTemplate.gameObject.SetActive(false);
        button.onClick.AddListener(() => onClicked?.Invoke());
    }

    public void Bind(MetaPerkDefinitionSO perk, int rank, CardState state, string costLabel, Action selected, Action clicked)
    {
        Perk = perk;
        onSelected = selected;
        onClicked = clicked;

        if (nameText != null) nameText.text = perk.displayName;
        if (icon != null)
        {
            icon.sprite = perk.icon;
            icon.enabled = perk.icon != null;
        }

        if (costText != null)
        {
            costText.text = costLabel;
            costText.color = UITheme.For(this).Get(state switch
            {
                CardState.Buyable => costBuyableColor,
                CardState.TooExpensive => costTooExpensiveColor,
                CardState.Maxed => costMaxedColor,
                _ => costLockedColor
            });
        }

        BuildPips(perk.MaxRank, rank);

        if (canvasGroup != null) canvasGroup.alpha = state == CardState.Locked ? 0.45f : 1f;
        SetHighlighted(false);
    }

    public void SetHighlighted(bool highlighted)
    {
        if (selectedHighlight != null) selectedHighlight.SetActive(highlighted);
    }

    private void BuildPips(int maxRank, int rank)
    {
        if (pipTemplate == null) return;
        Transform container = pipTemplate.transform.parent;
        UIThemeSO theme = UITheme.For(this);
        while (pips.Count < maxRank)
        {
            Image pip = Instantiate(pipTemplate, container);
            pips.Add(pip);
        }
        for (int i = 0; i < pips.Count; i++)
        {
            bool used = i < maxRank;
            // A one-rank perk shows no pips: its cost label already says OWNED.
            pips[i].gameObject.SetActive(used && maxRank > 1);
            pips[i].color = theme.Get(i < rank ? pipFilledColor : pipEmptyColor);
        }
    }

    public void OnPointerEnter(PointerEventData eventData) => onSelected?.Invoke();
    public void OnSelect(BaseEventData eventData) => onSelected?.Invoke();
}
