using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
///     HUD overlay for the Fishing Minigame: start prompt, 3-2-1 countdown punch animation,
///     and 60s frenzy timer/progress indicators.
/// </summary>
public class FishingHUDUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject promptPanel;
    [SerializeField] private GameObject countdownPanel;
    [SerializeField] private GameObject frenzyHudPanel;

    [Header("Labels")]
    [Tooltip("Glyph + label row bound to the StartWave action, so it follows rebinds and the active device.")]
    [SerializeField] private HintEntryView startHint;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text fishCountText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Slider xpSlider;

    [Header("Resource Counters")]
    [SerializeField] private TMP_Text goldCounterText;
    [SerializeField] private TMP_Text bloodCounterText;
    [SerializeField] private TMP_Text metalCounterText;
    [SerializeField] private TMP_Text diamondBonesText;

    private const string StartWaveActionName = "StartWave";

    private Coroutine punchRoutine;
    private bool hintBound;

    private void Start()
    {
        if (startHint == null) Debug.LogError("[FishingHUDUI] startHint is not assigned: the start prompt shows no button.", this);

        if (FishingManager.Instance != null)
        {
            FishingManager.Instance.OnStateChanged += HandleStateChanged;
            FishingManager.Instance.OnCountdownTick += HandleCountdownTick;
        }

        UpdateVisuals();
    }

    private void OnDestroy()
    {
        if (FishingManager.Instance != null)
        {
            FishingManager.Instance.OnStateChanged -= HandleStateChanged;
            FishingManager.Instance.OnCountdownTick -= HandleCountdownTick;
        }
    }

    private void Update()
    {
        if (FishingManager.Instance == null) return;

        if (FishingManager.Instance.IsFrenzyActive)
        {
            // Timer mm:ss
            float remaining = Mathf.Max(0f, FishingManager.Instance.FrenzyTimeRemaining);
            int minutes = Mathf.FloorToInt(remaining / 60f);
            int seconds = Mathf.FloorToInt(remaining % 60f);
            if (timerText != null) timerText.text = $"{minutes}:{seconds:D2}";

            // Fish count
            if (fishCountText != null)
            {
                fishCountText.text = $"Fish: {FishingManager.Instance.ActiveFishCount}/{FishingManager.Instance.MaxFishCount}";
            }

            // Level & XP
            if (levelText != null)
            {
                levelText.text = $"Fishing Lv. {FishingManager.Instance.CurrentFishingLevel}";
            }
            if (xpSlider != null)
            {
                xpSlider.maxValue = FishingManager.Instance.XpToNextLevel;
                xpSlider.value = FishingManager.Instance.CurrentFishingXp;
            }

            // Resources
            if (goldCounterText != null) goldCounterText.text = $"{FishingManager.Instance.SessionGold}g";
            if (bloodCounterText != null) bloodCounterText.text = $"{FishingManager.Instance.SessionBlood}";
            if (metalCounterText != null) metalCounterText.text = $"{FishingManager.Instance.SessionMetal}";
            if (diamondBonesText != null) diamondBonesText.text = $"{FishingManager.Instance.SessionDiamondBones}";
        }
    }

    private void HandleStateChanged()
    {
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (FishingManager.Instance == null) return;

        FishingState state = FishingManager.Instance.CurrentState;
        if (promptPanel != null) promptPanel.SetActive(state == FishingState.WaitingToStart);
        if (state == FishingState.WaitingToStart && !hintBound) BindHint();
        if (countdownPanel != null) countdownPanel.SetActive(state == FishingState.Countdown);
        if (frenzyHudPanel != null) frenzyHudPanel.SetActive(state == FishingState.FrenzyActive);
    }

    /// <summary>Points the start prompt at Start Wave (T / D-pad Down). Deferred until the prompt shows, since Player.Instance may not exist at Start.</summary>
    private void BindHint()
    {
        if (startHint == null) return;
        InputActionMap map = Player.Instance != null && Player.Instance.InputSettings != null
            ? Player.Instance.InputSettings.GetRebindableActionMap()
            : null;
        InputAction action = map != null ? map.FindAction(StartWaveActionName) : null;
        if (action != null)
        {
            startHint.Bind(action, "fishing.prompt.start", "Begin Fishing Frenzy");
        }
        else
        {
            startHint.Bind("<Keyboard>/t", "<Gamepad>/dpad/down", "fishing.prompt.start", "Begin Fishing Frenzy");
        }
        hintBound = true;
    }

    private void HandleCountdownTick(int tick)
    {
        if (countdownText == null) return;

        if (tick > 0)
        {
            countdownText.text = tick.ToString();
            countdownText.color = UITheme.For(this).Get(UIColorRole.Accent);
        }
        else
        {
            countdownText.text = "FISHING FRENZY!";
            countdownText.color = UITheme.For(this).Get(UIColorRole.Success);
        }

        if (punchRoutine != null) StopCoroutine(punchRoutine);
        punchRoutine = StartCoroutine(PunchScaleRoutine());
    }

    private IEnumerator PunchScaleRoutine()
    {
        Transform target = countdownText.transform;
        target.localScale = Vector3.one * 1.8f;
        float elapsed = 0f;
        float duration = 0.35f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            target.localScale = Vector3.Lerp(Vector3.one * 1.8f, Vector3.one, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        target.localScale = Vector3.one;
    }
}
