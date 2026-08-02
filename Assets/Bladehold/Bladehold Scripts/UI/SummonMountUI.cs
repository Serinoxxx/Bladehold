using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using MoreMountains.Feedbacks;

public class SummonMountUI : MonoBehaviour
{
    [Header("UI Elements")]
    public Image skillIcon;
    public Image radialFillImage;
    public TextMeshProUGUI timerText;
    public Image keybindIcon;

    [Header("Synty Input Icons (Keyboard/Mouse)")]
    public Sprite keyboardSprite;

    [Header("Synty Input Icons (Gamepad)")]
    public Sprite gamepadSprite;

    [Header("Colors")]
    public Color activeDurationColor = Color.cyan;
    public Color cooldownColor = Color.red;
    public Color readyColor = Color.white;

    [Header("Feedbacks")]
    public MMF_Player cooldownFinishedFeedback;
    public MMF_Player activatedFeedback;

    private PlayerInput playerInput;
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

        if (Player.Instance != null)
        {
            playerSummonMount = Player.Instance.GetComponent<PlayerSummonMount>();
            if (playerSummonMount != null)
            {
                playerSummonMount.OnDurationUpdated += HandleDurationUpdated;
                playerSummonMount.OnCooldownUpdated += HandleCooldownUpdated;
                playerSummonMount.OnAbilityReady += HandleAbilityReady;
                playerSummonMount.OnAbilityTriggered += HandleAbilityTriggered;
            }

            playerInput = Player.Instance.GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                playerInput.onControlsChanged += OnControlsChanged;
                UpdateKeybindIcons(playerInput.currentControlScheme);
            }
        }
    }

    private void OnDestroy()
    {
        if (playerInput != null)
        {
            playerInput.onControlsChanged -= OnControlsChanged;
        }
        if (playerSummonMount != null)
        {
            playerSummonMount.OnDurationUpdated -= HandleDurationUpdated;
            playerSummonMount.OnCooldownUpdated -= HandleCooldownUpdated;
            playerSummonMount.OnAbilityReady -= HandleAbilityReady;
            playerSummonMount.OnAbilityTriggered -= HandleAbilityTriggered;
        }
    }

    private void Update()
    {
        if (anyError || playerSummonMount == null) return;

        // Ensure visibility is correct based on whether the ability is unlocked
        bool isUnlocked = playerSummonMount.IsAbilityUnlocked;
        if (skillIcon.gameObject.activeSelf != isUnlocked)
        {
            skillIcon.gameObject.SetActive(isUnlocked);
        }

        if (isUnlocked && !playerSummonMount.IsHorseActive && !playerSummonMount.IsCooldownActive)
        {
            radialFillImage.fillAmount = 0f;
            timerText.text = "";
            skillIcon.color = readyColor;
        }
    }

    private void HandleDurationUpdated(float current, float max)
    {
        skillIcon.color = activeDurationColor;
        radialFillImage.fillAmount = max > 0 ? current / max : 0;
        timerText.text = current.ToString("0.0");
    }

    private void HandleCooldownUpdated(float current, float max)
    {
        skillIcon.color = cooldownColor;
        // Fill drains over time (or grows, up to preference. Buffs drain)
        radialFillImage.fillAmount = max > 0 ? current / max : 0;
        timerText.text = current.ToString("0.0");
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

    private void OnControlsChanged(PlayerInput input)
    {
        UpdateKeybindIcons(input.currentControlScheme);
    }

    private void UpdateKeybindIcons(string controlScheme)
    {
        bool isGamepad = controlScheme == "Gamepad";
        if (keybindIcon != null)
        {
            keybindIcon.sprite = isGamepad ? gamepadSprite : keyboardSprite;
        }
    }
}
