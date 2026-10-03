using System;
using System.Globalization;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
///     One between-wave choice card (plan 15), the view for <see cref="WaveCard" />. Instantiated from
///     <c>WaveCard.prefab</c> by <see cref="SurvivorsCardSelectUI.OpenWaveChoice" />; it only displays the
///     card and forwards the click. Glance order top to bottom: stance band (colour + icon + title),
///     timer, skulls, clan modifier, captain, reward bundle, variety bonus.
/// </summary>
public class WaveCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [Header("Card")]
    [SerializeField] private Button selectButton;

    [Header("1. Stance")]
    [SerializeField] private Image stanceBand;
    [SerializeField] private Image stanceIcon;
    [Tooltip("Offence only: the 'leaves tower cover' chip.")]
    [SerializeField] private GameObject leavesCoverChip;
    [SerializeField] private TMP_Text leavesCoverText;
    [SerializeField] private Sprite defenceSprite;
    [SerializeField] private Sprite offenceSprite;
    [SerializeField] private Color defenceColor = new Color(0.22f, 0.42f, 0.78f, 1f);
    [SerializeField] private Color offenceColor = new Color(0.72f, 0.16f, 0.12f, 1f);

    [Header("2. Objective")]
    [SerializeField] private TMP_Text titleText;

    [Header("4. Skulls")]
    [Tooltip("Three skull images, left to right.")]
    [SerializeField] private Image[] skullImages = new Image[3];
    [SerializeField] private Color skullEmptyColor = new Color(0f, 0f, 0f, 0.25f);
    [Tooltip("Tint per skull count (element 0 = 1 skull) for the filled skulls. Unfilled skulls stay visible in skullEmptyColor.")]
    [SerializeField] private Color[] skullTierColors =
    {
        new Color(0.30f, 0.26f, 0.20f, 1f),
        new Color(0.85f, 0.50f, 0.08f, 1f),
        new Color(0.80f, 0.12f, 0.10f, 1f)
    };

    [Header("5. Clan modifier + captain")]
    [SerializeField] private GameObject clanRow;
    [SerializeField] private Image clanIcon;
    [SerializeField] private TMP_Text clanText;
    [SerializeField] private GameObject captainRow;
    [SerializeField] private TMP_Text captainText;

    [Header("6. Timer")]
    [Tooltip("Hidden on objectives with no time limit.")]
    [SerializeField] private GameObject timerRow;
    [SerializeField] private TMP_Text timerText;

    [Header("8-9. Reward")]
    [SerializeField] private TMP_Text rewardHeaderText;
    [SerializeField] private TMP_Text multiplierText;
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text supplyText;
    [SerializeField] private GameObject bonusRow;
    [SerializeField] private Image bonusIcon;
    [SerializeField] private TMP_Text bonusText;
    [SerializeField] private Sprite draftPickSprite;
    [SerializeField] private Sprite goblinBloodSprite;
    [SerializeField] private Sprite orcishMetalSprite;
    [SerializeField] private Sprite trollHeartSprite;
    [Tooltip("The weapon draft's rerolls (2+ skulls): dice icon + '+1 draft reroll'.")]
    [SerializeField] private GameObject rerollRow;
    [SerializeField] private TMP_Text rerollText;
    [SerializeField] private GameObject freshRow;
    [SerializeField] private TMP_Text freshText;

    [Header("Layout")]
    [Tooltip("On: a hidden optional row (timer, clan, captain, bonus, fresh) keeps its space, so the three cards line up row for row. " +
             "Needs a CanvasGroup and a LayoutElement minHeight on each optional row. Off: hidden rows collapse.")]
    [SerializeField] private bool reserveHiddenRows = true;

    [Header("Feedbacks")]
    [SerializeField] private MMF_Player hoverEnterFeedback;
    [SerializeField] private MMF_Player hoverExitFeedback;
    [SerializeField] private MMF_Player selectFeedback;

    private Action onClicked;
    private bool anyError;
    private bool validated;

    public Button SelectButton => selectButton;

    private void Awake()
    {
        Validate();
        if (anyError) return;
        selectButton.onClick.AddListener(HandleClick);
        ForceUnscaledTime(hoverEnterFeedback);
        ForceUnscaledTime(hoverExitFeedback);
        ForceUnscaledTime(selectFeedback);
    }

    private void OnValidate()
    {
        if (selectButton == null) selectButton = GetComponent<Button>();
    }

    private void Validate()
    {
        if (validated) return;
        validated = true;
        if (selectButton == null || titleText == null || timerText == null || goldText == null || supplyText == null ||
            skullImages == null || skullImages.Length < WaveChoiceConfigSO.MaxSkulls)
        {
            Debug.LogError($"[WaveCardUI] {name} is missing required text/button references on WaveCard.prefab.", this);
            anyError = true;
        }
        if (hoverEnterFeedback == null || hoverExitFeedback == null || selectFeedback == null)
        {
            Debug.LogError($"[WaveCardUI] {name} is missing hover/select MMF_Player feedbacks.", this);
        }
        if (rerollRow == null || rerollText == null)
        {
            Debug.LogError($"[WaveCardUI] {name} is missing rerollRow/rerollText; 2+ skull cards won't show their draft reroll.", this);
        }
    }

    private static void ForceUnscaledTime(MMF_Player player)
    {
        if (player == null) return;
        player.ForceTimescaleMode = true;
        player.ForcedTimescaleMode = TimescaleModes.Unscaled;
        player.PlayerTimescaleMode = TimescaleModes.Unscaled;
    }

    public void SetData(WaveCard card, Action onPicked)
    {
        Validate();
        onClicked = onPicked;
        if (anyError || card == null || card.objective == null) return;

        bool offence = card.stance == WaveStance.Offence;
        Color stanceColor = offence ? offenceColor : defenceColor;
        if (stanceBand != null) stanceBand.color = stanceColor;
        if (stanceIcon != null)
        {
            stanceIcon.sprite = offence ? offenceSprite : defenceSprite;
            stanceIcon.enabled = stanceIcon.sprite != null;
        }
        if (leavesCoverChip != null) leavesCoverChip.SetActive(offence);
        if (leavesCoverText != null) leavesCoverText.text = Loc.Get("wave.card.leaves_cover", "Leaves tower cover");

        titleText.text = card.objective.TitleText;

        Color tier = skullTierColors != null && skullTierColors.Length > 0
            ? skullTierColors[Mathf.Clamp(card.skulls, 1, skullTierColors.Length) - 1]
            : Color.white;
        for (int i = 0; i < skullImages.Length; i++)
        {
            if (skullImages[i] != null) skullImages[i].color = i < card.skulls ? tier : skullEmptyColor;
        }

        SetRowShown(clanRow, card.HasClan);
        if (card.HasClan)
        {
            if (clanIcon != null)
            {
                clanIcon.sprite = card.clan.clanIcon;
                clanIcon.enabled = card.clan.clanIcon != null;
            }
            if (clanText != null) clanText.text = $"<b>{card.ClanName}</b>\n{card.ModifierEffectText}";
        }

        SetRowShown(captainRow, card.hasCaptain);
        if (card.hasCaptain && captainText != null)
        {
            string line = string.IsNullOrEmpty(card.captainName)
                ? Loc.Get("wave.card.captain_generic", "A clan captain joins the fight")
                : Loc.Get("wave.card.captain_named", "{0} joins the fight").Replace("{0}", card.captainName);
            captainText.text = $"<b>{Loc.Get("wave.card.captain_header", "Enemy Clan Captain")}</b>\n{line}";
        }

        SetRowShown(timerRow, card.objective.HasTimer);
        timerText.text = card.objective.HasTimer ? WaveCard.FormatSeconds(card.objective.timerSeconds) : "";

        if (rewardHeaderText != null) rewardHeaderText.text = Loc.Get("wave.card.reward", "REWARD");
        if (multiplierText != null) multiplierText.text = "×" + card.rewardMultiplier.ToString("0.##", CultureInfo.InvariantCulture);
        goldText.text = Loc.Get("wave.reward.gold", "{0} gold").Replace("{0}", card.gold.ToString(CultureInfo.InvariantCulture));
        supplyText.text = Loc.Get("wave.reward.supply", "{0} supply").Replace("{0}", card.supply.ToString(CultureInfo.InvariantCulture));

        bool hasBonus = card.bonusType != WaveBonusType.None && card.bonusAmount > 0;
        SetRowShown(bonusRow, hasBonus);
        if (hasBonus)
        {
            if (bonusText != null) bonusText.text = card.BonusText;
            if (bonusIcon != null)
            {
                bonusIcon.sprite = BonusSprite(card.bonusType);
                bonusIcon.enabled = bonusIcon.sprite != null;
            }
        }

        // Every wave pays a weapon draft; skulls add rerolls to it.
        SetRowShown(rerollRow, card.draftRerolls > 0);
        if (rerollText != null) rerollText.text = card.RerollText;

        SetRowShown(freshRow, card.isFresh);
        if (card.isFresh && freshText != null)
        {
            WaveChoiceConfigSO config = WaveChoiceConfigSO.Load();
            float pct = config != null ? config.varietyBonusPercent : 0f;
            freshText.text = Loc.Get("wave.card.fresh", "Fresh: +{0}%").Replace("{0}", pct.ToString("0", CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    ///     Shows or hides an optional row. With <see cref="reserveHiddenRows" /> the row stays active and fades
    ///     out through its CanvasGroup, so it keeps its space and its authored children are left alone.
    /// </summary>
    private void SetRowShown(GameObject row, bool shown)
    {
        if (row == null) return;
        CanvasGroup group = reserveHiddenRows ? row.GetComponent<CanvasGroup>() : null;
        if (group == null)
        {
            if (reserveHiddenRows) Debug.LogError($"[WaveCardUI] Optional row '{row.name}' has no CanvasGroup; it collapses instead of keeping its space.", row);
            row.SetActive(shown);
            return;
        }
        row.SetActive(true);
        group.alpha = shown ? 1f : 0f;
    }

    private Sprite BonusSprite(WaveBonusType type) => type switch
    {
        WaveBonusType.DraftPick => draftPickSprite,
        WaveBonusType.GoblinBlood => goblinBloodSprite,
        WaveBonusType.OrcishMetal => orcishMetalSprite,
        WaveBonusType.TrollHeart => trollHeartSprite,
        _ => null
    };

    public void SetInteractable(bool interactable)
    {
        if (selectButton != null) selectButton.interactable = interactable;
    }

    private void HandleClick()
    {
        if (selectButton != null && !selectButton.interactable) return;
        if (selectFeedback != null) selectFeedback.PlayFeedbacks();
        onClicked?.Invoke();
    }

    public void OnPointerEnter(PointerEventData eventData) => PlayHover(true);
    public void OnPointerExit(PointerEventData eventData) => PlayHover(false);
    public void OnSelect(BaseEventData eventData) => PlayHover(true);
    public void OnDeselect(BaseEventData eventData) => PlayHover(false);

    private void PlayHover(bool enter)
    {
        if (selectButton != null && !selectButton.interactable) return;
        MMF_Player player = enter ? hoverEnterFeedback : hoverExitFeedback;
        if (player != null) player.PlayFeedbacks();
    }
}
