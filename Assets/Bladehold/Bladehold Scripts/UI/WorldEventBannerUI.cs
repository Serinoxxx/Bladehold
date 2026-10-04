using System.Collections.Generic;
using System.Globalization;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     The world-event announcement (<c>WorldEventBanner.prefab</c>, nested in the WorldEvents prefab). When
///     <see cref="WorldEventDirector" /> starts an event, a big banner drops in: emblem, title, tagline, then
///     BOONS and PERILS columns built from the event's <see cref="WorldEventEffectLine" />s, with a thin bar
///     draining over <see cref="lingerSeconds" />. It then folds into a small timer chip (emblem, name, remaining
///     time) that stays until the event ends, and shows the event's outcome line ("Caravan looted!") if it has one.
///     Content comes from the event's <see cref="WorldEventSO" />; animation is the prefab's MMF players. Runs on
///     scaled time, so pausing freezes it with the event.
/// </summary>
public class WorldEventBannerUI : MonoBehaviour
{
    [Header("Banner")]
    [SerializeField] private GameObject bannerRoot;
    [SerializeField] private Image emblemIcon;
    [Tooltip("Tinted with the event's accent colour: the disc behind the emblem.")]
    [SerializeField] private Image emblemDisc;
    [Tooltip("Tinted with the event's accent colour: the glow band behind the whole banner.")]
    [SerializeField] private Image accentGlow;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text taglineText;
    [SerializeField] private TMP_Text boonsHeader;
    [SerializeField] private TMP_Text perilsHeader;
    [SerializeField] private Transform boonsContainer;
    [SerializeField] private Transform perilsContainer;
    [SerializeField] private WorldEventEffectRowView rowPrefab;
    [Tooltip("Thin bar under the banner that drains while it lingers (Image type Filled).")]
    [SerializeField] private Image lingerFill;
    [Tooltip("'45s' next to the title: how long the event lasts.")]
    [SerializeField] private TMP_Text durationText;
    [Tooltip("Seconds the full banner stays up before folding into the chip.")]
    [SerializeField] private float lingerSeconds = 5f;
    [Tooltip("Seconds the hide animation takes (matches bannerHideFeedback).")]
    [SerializeField] private float hideSeconds = 0.35f;

    [Header("Timer chip")]
    [SerializeField] private GameObject chipRoot;
    [SerializeField] private Image chipIcon;
    [Tooltip("Tinted with the event's accent colour: the diamond behind the chip's icon.")]
    [SerializeField] private Image chipDisc;
    [SerializeField] private TMP_Text chipTitle;
    [Tooltip("Remaining-time bar (Image type Filled), tinted with the accent colour.")]
    [SerializeField] private Image chipFill;
    [SerializeField] private TMP_Text chipTime;
    [Tooltip("The outcome line shown when the event ends (hidden otherwise).")]
    [SerializeField] private TMP_Text chipOutcome;
    [SerializeField] private Color outcomeGoodColor = new Color(1f, 0.82f, 0.25f, 1f);
    [SerializeField] private Color outcomeBadColor = new Color(0.9f, 0.3f, 0.25f, 1f);
    [Tooltip("Seconds the outcome line stays before the chip goes.")]
    [SerializeField] private float outcomeSeconds = 2.5f;

    [Header("Feedback")]
    [SerializeField] private MMF_Player bannerShowFeedback;
    [SerializeField] private MMF_Player bannerHideFeedback;
    [SerializeField] private MMF_Player chipShowFeedback;
    [Tooltip("Optional: a chime/pop when the outcome line appears.")]
    [SerializeField] private MMF_Player outcomeFeedback;

    private enum Phase { Hidden, Banner, Folding, Chip, Outcome }

    private readonly List<WorldEventEffectRowView> rows = new List<WorldEventEffectRowView>();
    private WorldEventDirector director;
    private WorldEvent current;
    private Phase phase = Phase.Hidden;
    private float phaseTimer;
    private bool anyError;

    private void Awake()
    {
        if (bannerRoot != null) bannerRoot.SetActive(false);
        if (chipRoot != null) chipRoot.SetActive(false);
    }

    private void Start()
    {
        if (bannerRoot == null || chipRoot == null || titleText == null || taglineText == null || rowPrefab == null ||
            boonsContainer == null || perilsContainer == null || emblemIcon == null || chipIcon == null || chipTitle == null ||
            chipFill == null || chipTime == null)
        {
            Debug.LogError("[WorldEventBannerUI] missing required references on WorldEventBanner.prefab.", this);
            anyError = true;
        }
        if (bannerShowFeedback == null || bannerHideFeedback == null || chipShowFeedback == null)
        {
            Debug.LogError("[WorldEventBannerUI] missing show/hide MMF players; the banner will pop without animation.", this);
        }

        director = WorldEventDirector.Instance;
        if (director == null)
        {
            Debug.LogError("[WorldEventBannerUI] no WorldEventDirector in the scene.", this);
            anyError = true;
            return;
        }
        director.OnEventStarted += HandleStarted;
        director.OnEventEnded += HandleEnded;
    }

    private void OnDestroy()
    {
        if (director != null)
        {
            director.OnEventStarted -= HandleStarted;
            director.OnEventEnded -= HandleEnded;
        }
    }

    private void HandleStarted(WorldEvent worldEvent)
    {
        if (anyError || worldEvent == null || worldEvent.Config == null) return;
        current = worldEvent;
        WorldEventSO config = worldEvent.Config;

        emblemIcon.sprite = config.icon;
        emblemIcon.enabled = config.icon != null;
        if (emblemDisc != null) emblemDisc.color = config.accentColor;
        if (accentGlow != null) accentGlow.color = WithAlpha(config.accentColor, accentGlow.color.a);
        titleText.text = config.Title.ToUpper(CultureInfo.CurrentCulture);
        taglineText.text = config.Tagline;
        if (durationText != null) durationText.text = Seconds(config.duration);
        if (boonsHeader != null) boonsHeader.text = Loc.Get("world_event.banner.boons", "BOONS");
        if (perilsHeader != null) perilsHeader.text = Loc.Get("world_event.banner.perils", "PERILS");
        BuildRows(config);

        chipIcon.sprite = config.icon;
        chipIcon.enabled = config.icon != null;
        chipTitle.text = config.Title;
        chipFill.color = config.accentColor;
        if (chipDisc != null) chipDisc.color = config.accentColor;
        chipTitle.gameObject.SetActive(true);
        chipTime.gameObject.SetActive(true);
        if (chipOutcome != null) chipOutcome.gameObject.SetActive(false);
        chipRoot.SetActive(false);

        bannerRoot.SetActive(true);
        if (lingerFill != null)
        {
            lingerFill.fillAmount = 1f;
            lingerFill.color = config.accentColor;
        }
        if (bannerShowFeedback != null) bannerShowFeedback.PlayFeedbacks();
        phase = Phase.Banner;
        phaseTimer = lingerSeconds;
    }

    private void BuildRows(WorldEventSO config)
    {
        foreach (WorldEventEffectRowView row in rows)
        {
            if (row != null) Destroy(row.gameObject);
        }
        rows.Clear();

        bool anyBoon = false, anyPeril = false;
        foreach (WorldEventEffectLine line in config.effects)
        {
            Transform parent = line.positive ? boonsContainer : perilsContainer;
            WorldEventEffectRowView row = Instantiate(rowPrefab, parent);
            row.Bind(line.icon, config.FormatLine(line), line.positive);
            rows.Add(row);
            anyBoon |= line.positive;
            anyPeril |= !line.positive;
        }
        // A column with nothing in it shouldn't show an empty header.
        if (boonsHeader != null) boonsHeader.gameObject.SetActive(anyBoon);
        if (perilsHeader != null) perilsHeader.gameObject.SetActive(anyPeril);
        boonsContainer.gameObject.SetActive(anyBoon);
        perilsContainer.gameObject.SetActive(anyPeril);
    }

    private void HandleEnded(WorldEvent worldEvent)
    {
        if (anyError || worldEvent != current) return;
        string outcome = worldEvent.OutcomeText;

        if (bannerRoot.activeSelf) bannerRoot.SetActive(false);
        if (string.IsNullOrEmpty(outcome) || chipOutcome == null)
        {
            chipRoot.SetActive(false);
            phase = Phase.Hidden;
            current = null;
            return;
        }

        chipRoot.SetActive(true);
        chipFill.fillAmount = 0f;
        // The outcome line takes the title's place: one message, read at a glance.
        chipTitle.gameObject.SetActive(false);
        chipTime.gameObject.SetActive(false);
        chipOutcome.text = outcome;
        chipOutcome.color = worldEvent.OutcomeIsGood ? outcomeGoodColor : outcomeBadColor;
        chipOutcome.gameObject.SetActive(true);
        if (outcomeFeedback != null) outcomeFeedback.PlayFeedbacks();
        phase = Phase.Outcome;
        phaseTimer = outcomeSeconds;
    }

    private void Update()
    {
        if (anyError || phase == Phase.Hidden) return;
        phaseTimer -= Time.deltaTime;

        switch (phase)
        {
            case Phase.Banner:
                if (lingerFill != null) lingerFill.fillAmount = Mathf.Clamp01(phaseTimer / Mathf.Max(0.01f, lingerSeconds));
                if (phaseTimer <= 0f)
                {
                    if (bannerHideFeedback != null) bannerHideFeedback.PlayFeedbacks();
                    phase = Phase.Folding;
                    phaseTimer = hideSeconds;
                }
                break;
            case Phase.Folding:
                if (phaseTimer <= 0f)
                {
                    bannerRoot.SetActive(false);
                    chipRoot.SetActive(true);
                    if (chipShowFeedback != null) chipShowFeedback.PlayFeedbacks();
                    phase = Phase.Chip;
                }
                break;
            case Phase.Chip:
                break;
            case Phase.Outcome:
                if (phaseTimer <= 0f)
                {
                    chipRoot.SetActive(false);
                    phase = Phase.Hidden;
                    current = null;
                }
                return;
        }

        if (current != null && current.IsRunning)
        {
            chipFill.fillAmount = current.RemainingNormalized;
            chipTime.text = Seconds(current.Remaining);
        }
    }

    private static string Seconds(float s) =>
        Loc.Get("world_event.banner.seconds", "{0}s").Replace("{0}", Mathf.CeilToInt(s).ToString(CultureInfo.InvariantCulture));

    private static Color WithAlpha(Color c, float a)
    {
        c.a = a;
        return c;
    }
}
