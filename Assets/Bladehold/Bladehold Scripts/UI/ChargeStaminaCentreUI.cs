using System.Collections.Generic;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
///     Centre-screen copy of the warhorse's charge-stamina bar (plan 22 slices 2.2/2.3). Hidden while
///     riding normally; fades in while the horse is charging so the player sees the carrot energy burning
///     down, with ember sparks (<see cref="ChargeEmberUI" />) thrown off the burning edge. When the rider asks
///     for a charge the horse can't give (<see cref="HorseMotor.OnChargeDenied" />) the bar flashes
///     (<see cref="deniedFeedback" />) and a "Your mount is tired" line shows for <see cref="tiredSeconds" />,
///     with the Dismount glyph and a carrot icon.
/// </summary>
public class ChargeStaminaCentreUI : MonoBehaviour
{
    [Tooltip("The player's mount. Resolved from Player.Instance's root if left empty.")]
    [SerializeField] private PlayerMount mount;

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private MMProgressBar progressBar;

    [Tooltip("The bar's fill area; embers are emitted from its right edge at the current fill fraction.")]
    [SerializeField] private RectTransform fillArea;

    [Header("Embers")]
    [Tooltip("Ember spark element, instantiated into Ember Container and reused.")]
    [SerializeField] private ChargeEmberUI emberPrefab;
    [Tooltip("Rect the embers live in (same parent space as Fill Area).")]
    [SerializeField] private RectTransform emberContainer;
    [SerializeField] private float embersPerSecond = 40f;

    [Header("Tired message")]
    [SerializeField] private GameObject tiredGroup;
    [SerializeField] private TMP_Text tiredTitle;
    [SerializeField] private TMP_Text tiredRestLabel;
    [Tooltip("Glyph showing the action that gets the player off the horse to rest.")]
    [SerializeField] private InputGlyph restGlyph;
    [SerializeField] private string restActionName = "Dismount";
    [SerializeField] private float tiredSeconds = 2.5f;

    [Header("Feedback")]
    [Tooltip("Flash/shake on the bar when a charge is denied.")]
    [SerializeField] private MMF_Player deniedFeedback;

    [SerializeField] private float fadeSpeed = 6f;

    private readonly List<ChargeEmberUI> embers = new List<ChargeEmberUI>();
    private HorseMotor subscribedHorse;
    private float tiredUntil;
    private float emberDebt;
    private bool glyphBound;
    private bool anyError;

    private void Start()
    {
        if (mount == null && Player.Instance != null)
        {
            mount = Player.Instance.transform.root.GetComponentInChildren<PlayerMount>(true);
        }
        if (mount == null) { Debug.LogError("[ChargeStaminaCentreUI] PlayerMount is not assigned and could not be found on the Player.", this); anyError = true; }
        if (canvasGroup == null) { Debug.LogError("[ChargeStaminaCentreUI] canvasGroup is not assigned.", this); anyError = true; }
        if (progressBar == null) { Debug.LogError("[ChargeStaminaCentreUI] progressBar is not assigned.", this); anyError = true; }
        if (fillArea == null || emberContainer == null || emberPrefab == null) Debug.LogError("[ChargeStaminaCentreUI] ember references (fillArea / emberContainer / emberPrefab) are not assigned.", this);
        if (tiredGroup == null) Debug.LogError("[ChargeStaminaCentreUI] tiredGroup is not assigned.", this);
        if (deniedFeedback == null) Debug.LogError("[ChargeStaminaCentreUI] deniedFeedback is not assigned.", this);

        if (tiredTitle != null) tiredTitle.text = Loc.Get("hud.mount.tired_title", "Your mount is tired");
        if (tiredRestLabel != null) tiredRestLabel.text = Loc.Get("hud.mount.tired_rest", "to rest, or eat some");
        if (tiredGroup != null) tiredGroup.SetActive(false);
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    private void OnDestroy()
    {
        SubscribeTo(null);
    }

    private void Update()
    {
        if (anyError) return;

        SubscribeTo(mount.CurrentHorse);
        TryBindGlyph();

        bool tired = Time.unscaledTime < tiredUntil && mount.IsMounted;
        bool charging = mount.IsCharging;
        if (tiredGroup != null && tiredGroup.activeSelf != tired) tiredGroup.SetActive(tired);

        float targetAlpha = charging || tired ? 1f : 0f;
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.unscaledDeltaTime);
        if (canvasGroup.alpha <= 0f) return;

        float fraction = mount.MountStaminaFraction;
        progressBar.SetBar01(fraction);

        if (charging) EmitEmbers(fraction);
    }

    private void SubscribeTo(HorseMotor horse)
    {
        if (horse == subscribedHorse) return;
        if (subscribedHorse != null) subscribedHorse.OnChargeDenied -= HandleChargeDenied;
        subscribedHorse = horse;
        if (subscribedHorse != null) subscribedHorse.OnChargeDenied += HandleChargeDenied;
    }

    private void HandleChargeDenied()
    {
        tiredUntil = Time.unscaledTime + tiredSeconds;
        if (deniedFeedback != null) deniedFeedback.PlayFeedbacks();
    }

    private void EmitEmbers(float fraction)
    {
        if (emberPrefab == null || emberContainer == null || fillArea == null) return;
        emberDebt += embersPerSecond * Time.unscaledDeltaTime;
        if (emberDebt < 1f) return;

        Rect r = fillArea.rect;
        Vector3 edgeWorld = fillArea.TransformPoint(new Vector3(Mathf.Lerp(r.xMin, r.xMax, fraction), r.center.y, 0f));
        Vector2 edgeLocal = emberContainer.InverseTransformPoint(edgeWorld);
        while (emberDebt >= 1f)
        {
            emberDebt -= 1f;
            GetEmber().Emit(edgeLocal + new Vector2(0f, Random.Range(-r.height * 0.4f, r.height * 0.4f)));
        }
    }

    private ChargeEmberUI GetEmber()
    {
        foreach (ChargeEmberUI e in embers)
        {
            if (!e.gameObject.activeSelf) return e;
        }
        ChargeEmberUI ember = Instantiate(emberPrefab, emberContainer);
        embers.Add(ember);
        return ember;
    }

    /// <summary>The player (and its Controls map) can spawn after the HUD, so bind once it exists.</summary>
    private void TryBindGlyph()
    {
        if (glyphBound || restGlyph == null || Player.Instance == null || Player.Instance.InputSettings == null) return;
        InputActionMap map = Player.Instance.InputSettings.GetRebindableActionMap();
        InputAction action = map != null ? map.FindAction(restActionName) : null;
        if (action == null) return;
        restGlyph.SetAction(action);
        glyphBound = true;
    }
}
