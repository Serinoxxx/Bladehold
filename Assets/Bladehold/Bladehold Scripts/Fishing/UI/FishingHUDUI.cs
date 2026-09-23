using System.Collections;
using TMPro;
using UnityEngine;
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
    [SerializeField] private TMP_Text promptText;
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

    private Coroutine punchRoutine;

    private void Start()
    {
        if (promptText != null) promptText.text = "Press [T] to begin Fishing Frenzy";

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
        if (countdownPanel != null) countdownPanel.SetActive(state == FishingState.Countdown);
        if (frenzyHudPanel != null) frenzyHudPanel.SetActive(state == FishingState.FrenzyActive);
    }

    private void HandleCountdownTick(int tick)
    {
        if (countdownText == null) return;

        if (tick > 0)
        {
            countdownText.text = tick.ToString();
            countdownText.color = new Color(1f, 0.85f, 0.2f, 1f);
        }
        else
        {
            countdownText.text = "FISHING FRENZY!";
            countdownText.color = new Color(0.2f, 1f, 0.8f, 1f);
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
