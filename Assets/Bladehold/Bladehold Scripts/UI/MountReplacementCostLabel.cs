using TMPro;
using UnityEngine;

/// <summary>
///     The small "500g" tag on the HUD's Summon Mount slot: visible only while the run's warhorse is dead
///     (pressing the button then opens <see cref="HorseReplacementDialog" />), in the theme's Cost colour,
///     Danger when the player can't afford it, or dimmed Danger mid-wave without the Field Stables perk
///     (<see cref="HorseReplacement.IsBuyWindowOpen" />). Price is the shared <see cref="HorseReplacement.GoldCost" />.
///     <see cref="SummonMountUI" /> builds one at runtime via <see cref="Create" /> when none is wired, so
///     the HUD prefab needs no edits; to hand-place it instead, put this on a TMP text and assign it to
///     SummonMountUI's <c>replacementCostLabel</c>.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class MountReplacementCostLabel : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    private int shownCost = -1;
    private bool shownAffordable;
    private bool shownWindowOpen;
    private bool anyError;

    private void OnValidate()
    {
        if (label == null) label = GetComponent<TMP_Text>();
    }

    private void Awake()
    {
        if (label == null) label = GetComponent<TMP_Text>();
        if (label == null)
        {
            Debug.LogError("[MountReplacementCostLabel] No TMP_Text to write to.", this);
            anyError = true;
        }
    }

    private void OnEnable()
    {
        RunSession.OnInRunGoldChanged -= HandleGoldChanged;
        RunSession.OnInRunGoldChanged += HandleGoldChanged;
        RunSession.OnMountLostChanged -= HandleMountLostChanged;
        RunSession.OnMountLostChanged += HandleMountLostChanged;
        Loc.OnLanguageChanged -= ForceRefresh;
        Loc.OnLanguageChanged += ForceRefresh;
        ForceRefresh();
    }

    private void OnDisable()
    {
        RunSession.OnInRunGoldChanged -= HandleGoldChanged;
        RunSession.OnMountLostChanged -= HandleMountLostChanged;
        Loc.OnLanguageChanged -= ForceRefresh;
    }

    private void Update()
    {
        // The buy window opens/closes with the wave phase, which raises no event: poll the cheap bool.
        if (anyError || !label.enabled) return;
        if (HorseReplacement.IsBuyWindowOpen != shownWindowOpen) Refresh();
    }

    private void HandleGoldChanged(int gold) => Refresh();
    private void HandleMountLostChanged(bool lost) => Refresh();

    private void ForceRefresh()
    {
        shownCost = -1;
        Refresh();
    }

    private void Refresh()
    {
        if (anyError) return;

        // Hidden via the text's own enabled flag, never by deactivating this object (that would stop the listeners).
        bool show = RunSession.MountLost && SceneAbilityRules.MountAllowed;
        label.enabled = show;
        if (!show) return;

        int cost = HorseReplacement.GoldCost;
        bool affordable = HorseReplacement.CanAfford;
        bool windowOpen = HorseReplacement.IsBuyWindowOpen;
        if (cost == shownCost && affordable == shownAffordable && windowOpen == shownWindowOpen) return;
        shownCost = cost;
        shownAffordable = affordable;
        shownWindowOpen = windowOpen;

        label.text = string.Format(Loc.Get("horse_replace.cost_tag", "{0}g"), cost);
        UIThemeSO theme = UITheme.For(this);
        // Mid-wave without Field Stables: dimmed red (can't buy right now, whatever the gold).
        if (!windowOpen) label.color = theme.Get(UIColorRole.Danger, 0.5f);
        else label.color = theme.Get(affordable ? UIColorRole.Cost : UIColorRole.Danger);
    }

    /// <summary>
    ///     Builds a label under <paramref name="slot" />, centred just above its top edge, in
    ///     <paramref name="fontSource" />'s font (the HUD's house font). Never blocks raycasts.
    /// </summary>
    public static MountReplacementCostLabel Create(RectTransform slot, TMP_Text fontSource)
    {
        GameObject go = new GameObject("ReplacementCostLabel", typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(slot, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0f);
        // Wider than the slot so "500g" never wraps; a quarter of the slot's height tall.
        float height = Mathf.Max(18f, slot.rect.height * 0.28f);
        rect.offsetMin = new Vector2(-slot.rect.width * 0.25f, 0f);
        rect.offsetMax = new Vector2(slot.rect.width * 0.25f, 0f);
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        rect.anchoredPosition = new Vector2(0f, height * 0.1f);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (fontSource != null) text.font = fontSource.font;
        text.alignment = TextAlignmentOptions.Bottom;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.enableAutoSizing = true;
        text.fontSizeMin = 8f;
        text.fontSizeMax = height;
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;
        text.outlineWidth = 0.25f;
        text.outlineColor = new Color32(0, 0, 0, 220);
        text.enabled = false;

        return go.AddComponent<MountReplacementCostLabel>();
    }
}
