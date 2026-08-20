using System.Collections;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
///     HUD notification prompt positioned directly below the crosshair.
///     Appears when the player has pending skill drafts from level-ups, displaying
///     "New skills available" with an input sprite icon (T on KBM / D-pad Down on Gamepad).
///     Pressing the input triggers card selection.
/// </summary>
public class SurvivorsLevelUpPromptUI : MonoBehaviour
{
    public static SurvivorsLevelUpPromptUI Instance { get; private set; }

    [Header("UI Containers")]
    [Tooltip("CanvasGroup or child container used to show/hide the prompt without disabling this MonoBehaviour.")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private GameObject contentContainer;

    [Tooltip("Label displaying 'New skills available' or count.")]
    [SerializeField] private TMP_Text promptLabel;

    [Header("Keybind Icon Reference")]
    [Tooltip("UI Image component displaying the input key/button sprite.")]
    [SerializeField] private Image keybindIcon;

    [Header("Input Sprites")]
    [Tooltip("Sprite shown when using Keyboard & Mouse (e.g. T key sprite).")]
    [SerializeField] private Sprite keyboardSprite;

    [Tooltip("Sprite shown when using Gamepad (e.g. D-pad Down sprite).")]
    [SerializeField] private Sprite gamepadSprite;

    [Header("Input References")]
    [SerializeField] private InputReader inputReader;

    private PlayerInput playerInput;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }

    private void Start()
    {
        StartCoroutine(InitRoutine());
    }

    private IEnumerator InitRoutine()
    {
        yield return null;

        if (Player.Instance != null)
        {
            if (inputReader == null)
            {
                inputReader = Player.Instance.GetComponentInChildren<InputReader>();
            }

            playerInput = Player.Instance.GetComponent<PlayerInput>();
            if (playerInput != null)
            {
                playerInput.onControlsChanged += OnControlsChanged;
                UpdateKeybindIcons(playerInput.currentControlScheme);
            }
        }

        if (inputReader != null)
        {
            inputReader.onDraftSkillsPerformed -= HandleDraftInput;
            inputReader.onDraftSkillsPerformed += HandleDraftInput;
        }

        if (SurvivorsLevelSystem.Instance != null)
        {
            SurvivorsLevelSystem.Instance.OnPendingDraftsChanged -= HandlePendingDraftsChanged;
            SurvivorsLevelSystem.Instance.OnPendingDraftsChanged += HandlePendingDraftsChanged;
            HandlePendingDraftsChanged(SurvivorsLevelSystem.Instance.PendingDrafts);
        }
        else
        {
            SetPromptVisible(false);
        }
    }

    private void OnEnable()
    {
        if (SurvivorsLevelSystem.Instance != null)
        {
            HandlePendingDraftsChanged(SurvivorsLevelSystem.Instance.PendingDrafts);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (playerInput != null)
        {
            playerInput.onControlsChanged -= OnControlsChanged;
        }

        if (inputReader != null)
        {
            inputReader.onDraftSkillsPerformed -= HandleDraftInput;
        }

        if (SurvivorsLevelSystem.Instance != null)
        {
            SurvivorsLevelSystem.Instance.OnPendingDraftsChanged -= HandlePendingDraftsChanged;
        }
    }

    private void Update()
    {
        if (SurvivorsLevelSystem.Instance == null) return;

        int pending = SurvivorsLevelSystem.Instance.PendingDrafts;
        bool shouldBeVisible = pending > 0;

        // Ensure visibility state stays synced
        if (canvasGroup != null)
        {
            bool isVis = canvasGroup.alpha > 0.01f;
            if (isVis != shouldBeVisible)
            {
                SetPromptVisible(shouldBeVisible);
                if (shouldBeVisible && promptLabel != null)
                {
                    promptLabel.text = pending > 1 ? $"New skills available (x{pending})" : "New skills available";
                }
            }
        }
        else if (contentContainer != null && contentContainer.activeSelf != shouldBeVisible)
        {
            SetPromptVisible(shouldBeVisible);
        }

        // Direct input polling fallback
        if (shouldBeVisible && !CursorLockManager.IsCursorUnlocked)
        {
            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            {
                HandleDraftInput();
            }
            else if (Gamepad.current != null && Gamepad.current.dpad.down.wasPressedThisFrame)
            {
                HandleDraftInput();
            }
        }
    }

    private void OnControlsChanged(PlayerInput input)
    {
        UpdateKeybindIcons(input.currentControlScheme);
    }

    private void UpdateKeybindIcons(string controlScheme)
    {
        if (keybindIcon == null) return;

        bool isGamepad = controlScheme == "Gamepad";
        if (isGamepad && gamepadSprite != null)
        {
            keybindIcon.sprite = gamepadSprite;
            keybindIcon.gameObject.SetActive(true);
        }
        else if (!isGamepad && keyboardSprite != null)
        {
            keybindIcon.sprite = keyboardSprite;
            keybindIcon.gameObject.SetActive(true);
        }
    }

    private void HandlePendingDraftsChanged(int pendingCount)
    {
        bool hasPending = pendingCount > 0;
        SetPromptVisible(hasPending);

        if (hasPending && promptLabel != null)
        {
            promptLabel.text = pendingCount > 1 
                ? $"New skills available (x{pendingCount})" 
                : "New skills available";
        }
    }

    private void SetPromptVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        if (contentContainer != null)
        {
            contentContainer.SetActive(visible);
        }
    }

    private void HandleDraftInput()
    {
        if (SurvivorsLevelSystem.Instance == null || SurvivorsLevelSystem.Instance.PendingDrafts <= 0)
        {
            return;
        }

        if (SurvivorsCardSelectUI.Instance != null)
        {
            SurvivorsCardSelectUI.Instance.OpenDraft();
        }
        else
        {
            Debug.LogWarning("[SurvivorsLevelUpPromptUI] SurvivorsCardSelectUI.Instance is not available.");
        }
    }
}
