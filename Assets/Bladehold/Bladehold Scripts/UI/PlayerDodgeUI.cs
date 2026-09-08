using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using MoreMountains.Feedbacks;

public class PlayerDodgeUI : MonoBehaviour
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
    public Color cooldownColor = Color.red;
    public Color readyColor = Color.white;

    [Header("Feedbacks")]
    public MMF_Player cooldownFinishedFeedback;
    public MMF_Player activatedFeedback;

    private PlayerInput playerInput;
    [SerializeField]private PlayerDodge playerDodge;
    private bool anyError;

    private void Start()
    {
        if (skillIcon == null || radialFillImage == null || timerText == null || keybindIcon == null)
        {
            Debug.LogError("PlayerDodgeUI: Missing UI Image references.", this);
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
            if (playerDodge == null)
                playerDodge = Player.Instance.transform.root.GetComponentInChildren<PlayerDodge>();

            if (playerDodge != null)
            {
                playerDodge.OnCooldownUpdated += HandleCooldownUpdated;
                playerDodge.OnAbilityReady += HandleAbilityReady;
                playerDodge.OnDodgeStarted += HandleAbilityTriggered;
                playerDodge.OnChargesChanged += HandleChargesChanged;
            }

            playerInput = Player.Instance.GetComponentInChildren<PlayerInput>();
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
        if (playerDodge != null)
        {
            playerDodge.OnCooldownUpdated -= HandleCooldownUpdated;
            playerDodge.OnAbilityReady -= HandleAbilityReady;
            playerDodge.OnDodgeStarted -= HandleAbilityTriggered;
            playerDodge.OnChargesChanged -= HandleChargesChanged;
        }
    }

    [Header("Slot Container")]
    [Tooltip("The root GameObject of the entire dodge slot (including frame, keybind, icon).")]
    [SerializeField] private GameObject rootContainer;

    private void Awake()
    {
        if (rootContainer == null)
        {
            // Traverse up to find Item_00 or Dodge root
            Transform curr = transform;
            while (curr != null)
            {
                if (curr.name.Contains("Dodge") || curr.name.Contains("Item_00"))
                {
                    rootContainer = curr.gameObject;
                    break;
                }
                curr = curr.parent;
            }
        }
    }

    private void Update()
    {
        // UI is no longer dynamically hidden because weapons are unlocked by default.
    }

    private void HandleCooldownUpdated(float current, float max)
    {
        int currentCharges = playerDodge != null ? playerDodge.CurrentCharges : 0;
        int maxCharges = playerDodge != null ? playerDodge.MaxCharges : 1;

        if (currentCharges > 0)
        {
            skillIcon.color = readyColor;
        }
        else
        {
            skillIcon.color = cooldownColor;
        }

        radialFillImage.fillAmount = max > 0 ? (current / max) : 0f;

        if (maxCharges > 1)
        {
            if (currentCharges > 0)
            {
                timerText.text = currentCharges.ToString();
            }
            else
            {
                timerText.text = current.ToString("0.0");
            }
        }
        else
        {
            timerText.text = currentCharges > 0 ? "" : current.ToString("0.0");
        }
    }

    private void HandleAbilityReady()
    {
        int currentCharges = playerDodge != null ? playerDodge.CurrentCharges : 1;
        int maxCharges = playerDodge != null ? playerDodge.MaxCharges : 1;

        skillIcon.color = readyColor;
        radialFillImage.fillAmount = 0f;
        timerText.text = maxCharges > 1 ? currentCharges.ToString() : "";
        
        if (cooldownFinishedFeedback != null)
        {
            cooldownFinishedFeedback.PlayFeedbacks();
        }
    }

    private void HandleAbilityTriggered()
    {
        int currentCharges = playerDodge != null ? playerDodge.CurrentCharges : 0;
        int maxCharges = playerDodge != null ? playerDodge.MaxCharges : 1;

        if (currentCharges <= 0)
        {
            skillIcon.color = cooldownColor;
        }
        else
        {
            skillIcon.color = readyColor;
        }

        if (maxCharges > 1)
        {
            timerText.text = currentCharges > 0 ? currentCharges.ToString() : "";
        }

        if (activatedFeedback != null)
        {
            activatedFeedback.PlayFeedbacks();
        }
    }

    private void HandleChargesChanged(int current, int max)
    {
        if (current > 0)
        {
            skillIcon.color = readyColor;
        }
        else
        {
            skillIcon.color = cooldownColor;
        }

        if (max > 1)
        {
            timerText.text = current > 0 ? current.ToString() : "";
        }
        else if (current > 0)
        {
            timerText.text = "";
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
