using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using MoreMountains.Feedbacks;

/// <summary>
///     The HUD's Summon Mount slot: icon, the SummonMount button glyph (follows device switches and
///     rebinds via <see cref="InputGlyph" />), a radial fill + seconds for the ride time left and then the
///     cooldown. Driven by <see cref="PlayerSummonMount" />'s events, which carry <see cref="PlayerMount" />'s
///     real timers. The slot hides with a CanvasGroup on <see cref="rootContainer" /> (never by deactivating
///     it, which would stop this component) when the scene's <see cref="SceneAbilityRules" /> block the mount.
/// </summary>
public class SummonMountUI : MonoBehaviour
{
    [Header("UI Elements")]
    public Image skillIcon;
    public Image radialFillImage;
    public TextMeshProUGUI timerText;
    public Image keybindIcon;
    [Tooltip("Glyph for the SummonMount action (on the keybind image). Added to the keybind image at runtime when unassigned.")]
    [SerializeField] private InputGlyph keybindGlyph;

    [Header("Colors")]
    public Color activeDurationColor = Color.cyan;
    public Color cooldownColor = Color.red;
    public Color readyColor = Color.white;
    [Tooltip("Icon tint while the run's warhorse is dead (replace it at the shop).")]
    public Color lostColor = new Color(0.62f, 0.24f, 0.2f, 0.8f);

    [Tooltip("Ride durations at or above this many seconds count as unlimited: no countdown is shown.")]
    [SerializeField] private float unlimitedDurationSeconds = 600f;

    [Tooltip("Optional \"500g\" tag shown while the warhorse is dead (pressing the button then offers a new one). Built above the slot at runtime when unassigned.")]
    [SerializeField] private MountReplacementCostLabel replacementCostLabel;

    [Header("Feedbacks")]
    public MMF_Player cooldownFinishedFeedback;
    public MMF_Player activatedFeedback;

    private PlayerSummonMount playerSummonMount;
    private bool anyError;

    private void Start()
    {
        if (skillIcon == null || radialFillImage == null || timerText == null || keybindIcon == null)
        {
            Debug.LogError("SummonMountUI: Missing UI Image references.", this);
            anyError = true;
        }

        if (anyError) return;

        StartCoroutine(InitRoutine());
    }

    private IEnumerator InitRoutine()
    {
        yield return null;

        if (replacementCostLabel == null)
        {
            RectTransform slot = rootContainer != null ? rootContainer.transform as RectTransform : null;
            if (slot == null) slot = skillIcon.rectTransform;
            replacementCostLabel = MountReplacementCostLabel.Create(slot, timerText);
        }

        if (Player.Instance != null)
        {
            // The ability sits on the player root; Player.Instance is on the Synty character child.
            playerSummonMount = Player.Instance.transform.root.GetComponentInChildren<PlayerSummonMount>(true);
            if (playerSummonMount != null)
            {
                playerSummonMount.OnDurationUpdated += HandleDurationUpdated;
                playerSummonMount.OnCooldownUpdated += HandleCooldownUpdated;
                playerSummonMount.OnAbilityReady += HandleAbilityReady;
                playerSummonMount.OnAbilityTriggered += HandleAbilityTriggered;
            }

            InputActionMap map = Player.Instance.InputSettings != null ? Player.Instance.InputSettings.GetRebindableActionMap() : null;
            InputAction action = map != null ? map.FindAction("SummonMount") : null;
            if (action == null) Debug.LogError("SummonMountUI: no SummonMount action on the player's Controls map.", this);
            else if (keybindGlyph != null) keybindGlyph.SetAction(action);
            else keybindGlyph = InputGlyph.AttachTo(keybindIcon, action);
        }
    }

    private void OnDestroy()
    {
        if (playerSummonMount != null)
        {
            playerSummonMount.OnDurationUpdated -= HandleDurationUpdated;
            playerSummonMount.OnCooldownUpdated -= HandleCooldownUpdated;
            playerSummonMount.OnAbilityReady -= HandleAbilityReady;
            playerSummonMount.OnAbilityTriggered -= HandleAbilityTriggered;
        }
    }

    [Header("Slot Container")]
    [Tooltip("The root GameObject of the entire mount slot (including frame, keybind, icon). Hidden through its CanvasGroup.")]
    [SerializeField] private GameObject rootContainer;

    private CanvasGroup rootGroup;

    private void Awake()
    {
        if (rootContainer == null)
        {
            // Traverse up to find Item_01 or Mount root
            Transform curr = transform;
            while (curr != null)
            {
                if (curr.name.Contains("Mount") || curr.name.Contains("Item_01"))
                {
                    rootContainer = curr.gameObject;
                    break;
                }
                curr = curr.parent;
            }
        }
        if (rootContainer != null)
        {
            rootGroup = rootContainer.GetComponent<CanvasGroup>();
            if (rootGroup == null) Debug.LogError("SummonMountUI: rootContainer needs a CanvasGroup to hide the slot.", this);
        }
    }

    private void Update()
    {
        if (anyError) return;

        bool isUnlocked = playerSummonMount != null && playerSummonMount.IsAbilityUnlocked;
        if (rootGroup != null)
        {
            rootGroup.alpha = isUnlocked ? 1f : 0f;
        }
        else if (skillIcon != null && skillIcon.gameObject.activeSelf != isUnlocked)
        {
            skillIcon.gameObject.SetActive(isUnlocked);
            if (keybindIcon != null) keybindIcon.gameObject.SetActive(isUnlocked);
        }

        if (isUnlocked && playerSummonMount.IsMountLost && !playerSummonMount.IsHorseActive)
        {
            if (radialFillImage != null) radialFillImage.fillAmount = 0f;
            if (timerText != null) timerText.text = "";
            if (skillIcon != null) skillIcon.color = lostColor;
            return;
        }

        if (isUnlocked && playerSummonMount != null && !playerSummonMount.IsHorseActive && !playerSummonMount.IsCooldownActive)
        {
            if (radialFillImage != null) radialFillImage.fillAmount = 0f;
            if (timerText != null) timerText.text = "";
            if (skillIcon != null) skillIcon.color = readyColor;
        }
    }

    private void HandleDurationUpdated(float current, float max)
    {
        skillIcon.color = activeDurationColor;
        if (max >= unlimitedDurationSeconds)
        {
            // An unlimited ride (the basic warhorse): no countdown, the slot just reads "riding".
            radialFillImage.fillAmount = 0f;
            timerText.text = "";
            return;
        }
        radialFillImage.fillAmount = max > 0 ? current / max : 0;
        timerText.text = Mathf.CeilToInt(current).ToString();
    }

    private void HandleCooldownUpdated(float current, float max)
    {
        // A dead horse reads as "lost" (Update), not as a countdown.
        if (playerSummonMount != null && playerSummonMount.IsMountLost) return;

        skillIcon.color = cooldownColor;
        // Fill drains over time (or grows, up to preference. Buffs drain)
        radialFillImage.fillAmount = max > 0 ? current / max : 0;
        timerText.text = current > 0f ? Mathf.CeilToInt(current).ToString() : "";
    }

    private void HandleAbilityReady()
    {
        skillIcon.color = readyColor;
        radialFillImage.fillAmount = 0f;
        timerText.text = "";
        
        if (cooldownFinishedFeedback != null)
        {
            cooldownFinishedFeedback.PlayFeedbacks();
        }
    }

    private void HandleAbilityTriggered()
    {
        if (activatedFeedback != null)
        {
            activatedFeedback.PlayFeedbacks();
        }
    }
}
