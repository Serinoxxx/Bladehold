using System.Collections.Generic;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
///     The Ultimate Wheel (plan 21 phase 5). Holding the Ultimate input (Q / Left Bumper) opens it and slows
///     time to <see cref="UltimateWheelConfigSO.slowTimeScale" />. It offers one slice per ultimate slot: the
///     held melee weapon's and the held ranged weapon's ultimate, each costing one Arcane Core.
///     <list type="bullet">
///         <item>Keyboard and mouse: hold Q and click a slice. Releasing Q closes the wheel.</item>
///         <item>Gamepad: hold LB and point the left stick at a slice; releasing LB fires it, releasing with
///         the stick centred cancels. Movement is suppressed while the wheel is open.</item>
///     </list>
///     A slice that can't fire (no core, no ultimate for that weapon, an ultimate already running, or
///     ultimates off in this scene) is greyed out with the reason in the details box; picking it plays the
///     denied feedback. The wheel owns Time.timeScale while open (re-asserted every frame so a hitstop can't
///     undo it) and closes, restoring <see cref="GameSettingsService.TargetTimeScale" />, before any other
///     modal (pause, draft, shop, build wheel, enemy intro) takes over: they call <see cref="CloseIfOpen" />.
/// </summary>
public class UltimateWheelUI : MonoBehaviour
{
    public static UltimateWheelUI Instance { get; private set; }

    /// <summary>True while the wheel is open (other input readers, like the dash, stand down).</summary>
    public static bool IsOpen => Instance != null && Instance.isOpen;

    [SerializeField] private UltimateWheelConfigSO config;

    [Header("UI References")]
    [Tooltip("The wheel's visuals. Hidden while closed; this component's own GameObject stays active to read the input.")]
    [SerializeField] private GameObject wheelPanel;
    [SerializeField] private TMP_Text headerText;
    [Tooltip("\"Arcane Cores: N\" under the header.")]
    [SerializeField] private TMP_Text coreLabel;
    [SerializeField] private BuildWheelDetailsPanel detailsPanel;
    [Tooltip("Authored slices (at least two, so the ring can be measured). Extra ones are cloned from the first.")]
    [SerializeField] private List<BuildWheelButton> sliceButtons = new List<BuildWheelButton>();
    [Tooltip("Optional \"MELEE\" / \"RANGED\" caption per slice, same order as the slices.")]
    [SerializeField] private List<TMP_Text> slotLabels = new List<TMP_Text>();

    [Header("Feedback (unscaled time)")]
    [SerializeField] private MMF_Player openFeedback;
    [SerializeField] private MMF_Player deniedFeedback;

    private static readonly UltimateSlot[] Slots = { UltimateSlot.Melee, UltimateSlot.Ranged };

    // Gameplay actions switched off while the wheel is open, so the stick doesn't walk the player and a
    // click on a slice doesn't swing the sword. Only the ones this wheel disabled are re-enabled.
    private static readonly string[] SuppressedActions = { "Move", "Look", "Attack", "Aim", "Jump", "Crouch", "Interact", "SummonMount", "Dismount", "StartWave", "LockOn" };
    private readonly List<InputAction> disabledActions = new List<InputAction>();

    private RadialWheel wheel;
    private InputAction ultimateAction;
    private PlayerUltimateController controller;
    private bool isOpen;
    private bool openedThisPress;
    private int stickSlot = -1;
    private int hoveredSlot = -1;
    private bool anyError;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        wheel = new RadialWheel(sliceButtons, "UltimateWheel");
        if (wheelPanel != null) wheelPanel.SetActive(false);
    }

    private void Start()
    {
        if (config == null) { Debug.LogError("[UltimateWheelUI] No UltimateWheelConfigSO assigned.", this); anyError = true; }
        if (wheelPanel == null || wheelPanel == gameObject) { Debug.LogError("[UltimateWheelUI] wheelPanel must be a child panel (this GameObject reads the input and stays active).", this); anyError = true; }
        if (sliceButtons.Count < 2 || sliceButtons[0] == null) { Debug.LogError("[UltimateWheelUI] Needs at least two authored slices.", this); anyError = true; }
        if (detailsPanel == null) Debug.LogError("[UltimateWheelUI] detailsPanel is not assigned.", this);
        if (openFeedback == null) Debug.LogError("[UltimateWheelUI] openFeedback is not assigned.", this);
        if (deniedFeedback == null) Debug.LogError("[UltimateWheelUI] deniedFeedback is not assigned.", this);
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        if (isOpen) Close(restoreTime: true);
        Instance = null;
    }

    /// <summary>Closes the wheel (restoring normal time) if it's open. Called by every modal before it opens.</summary>
    public static void CloseIfOpen()
    {
        if (IsOpen) Instance.Close(restoreTime: true);
    }

    private void Update()
    {
        if (anyError) return;
        if (ultimateAction == null && !TryBindInput()) return;

        bool held = ultimateAction.IsPressed();

        if (!isOpen)
        {
            // A press made while something else was up (or a held key from before) never opens the wheel late.
            if (!held) openedThisPress = false;
            else if (!openedThisPress && CanOpen())
            {
                Open();
                UpdateStickSelection(); // a stick already held when LB goes down counts
            }
            return;
        }

        // Something else froze time (a modal that didn't close us first): stand down without touching time.
        if (Time.timeScale == 0f || (PauseMenuController.Instance != null && PauseMenuController.Instance.IsPaused))
        {
            Close(restoreTime: false);
            return;
        }

        Time.timeScale = config.slowTimeScale;

        if (CancelPressed())
        {
            Close(restoreTime: true);
            return;
        }

        if (!held)
        {
            // Releasing the hold confirms the stick's slice (gamepad); otherwise it just closes. The selection
            // is last frame's, since the stick often springs back to centre in the same frame as the release.
            int pick = stickSlot;
            if (pick >= 0) Select(pick);
            else Close(restoreTime: true);
            return;
        }

        UpdateStickSelection();
    }

    private bool TryBindInput()
    {
        if (Player.Instance == null || Player.Instance.InputSettings == null) return false;
        InputActionMap map = Player.Instance.InputSettings.GetRebindableActionMap();
        ultimateAction = map?.FindAction("Ultimate");
        if (ultimateAction == null) return false;
        if (!ultimateAction.enabled) ultimateAction.Enable();
        controller = Player.Instance.transform.root.GetComponentInChildren<PlayerUltimateController>(true);
        if (controller == null)
        {
            Debug.LogError("[UltimateWheelUI] No PlayerUltimateController on the player.", this);
            anyError = true;
            return false;
        }
        return true;
    }

    private bool CanOpen()
    {
        if (Time.timeScale == 0f) return false;
        if (PauseMenuController.Instance != null && PauseMenuController.Instance.IsPaused) return false;
        if (BuildWheelUI.Instance != null && BuildWheelUI.Instance.IsOpen) return false;
        if (CursorLockManager.IsCursorUnlocked) return false; // another menu owns the cursor
        if (Player.Instance == null || Player.Instance.Health == null || Player.Instance.Health.IsDead) return false;
        return controller != null;
    }

    private void Open()
    {
        isOpen = true;
        openedThisPress = true;
        stickSlot = -1;
        hoveredSlot = -1;

        wheelPanel.SetActive(true);
        wheel.SetModal(true);
        SuppressGameplayInput(true);
        Time.timeScale = config.slowTimeScale;

        Refresh();
        if (openFeedback != null) openFeedback.PlayFeedbacks();
    }

    private void Close(bool restoreTime)
    {
        isOpen = false;
        stickSlot = -1;
        hoveredSlot = -1;

        if (wheelPanel != null) wheelPanel.SetActive(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        wheel.SetModal(false);
        SuppressGameplayInput(false);
        if (restoreTime) Time.timeScale = GameSettingsService.TargetTimeScale;
    }

    private void SuppressGameplayInput(bool suppress)
    {
        if (suppress)
        {
            InputActionMap map = Player.Instance != null && Player.Instance.InputSettings != null
                ? Player.Instance.InputSettings.GetRebindableActionMap()
                : null;
            if (map == null) return;
            foreach (string name in SuppressedActions)
            {
                InputAction action = map.FindAction(name);
                if (action == null || !action.enabled) continue;
                action.Disable();
                disabledActions.Add(action);
            }
            return;
        }

        foreach (InputAction action in disabledActions)
        {
            action?.Enable();
        }
        disabledActions.Clear();
    }

    private void Refresh()
    {
        if (headerText != null) headerText.text = Loc.Get("ultwheel.header", "UNLEASH AN ULTIMATE");
        if (coreLabel != null) coreLabel.text = string.Format(Loc.Get("ultwheel.cores", "Arcane Cores: {0}"), RunSession.ArcaneCores);

        for (int i = 0; i < slotLabels.Count && i < Slots.Length; i++)
        {
            if (slotLabels[i] == null) continue;
            slotLabels[i].text = Slots[i] == UltimateSlot.Melee
                ? Loc.Get("ultwheel.slot_melee", "MELEE")
                : Loc.Get("ultwheel.slot_ranged", "RANGED");
        }

        wheel.EnsureButtonCount(Slots.Length);
        for (int i = 0; i < sliceButtons.Count; i++)
        {
            BuildWheelButton button = sliceButtons[i];
            if (button == null) continue;
            bool used = i < Slots.Length;
            button.gameObject.SetActive(used);
            if (!used) continue;

            int index = i;
            DraftUpgradeDefinition ult = DraftUpgradeService.GetUltimate(Slots[i]);
            UltimateBlockReason reason = controller.GetBlockReason(Slots[i]);
            string title = ult != null ? ult.displayName : Loc.Get("ultwheel.none", "No ultimate");
            Sprite icon = ult != null ? DraftUpgradeService.GetOrCreateInstance().GetIcon(ult.iconName) : null;

            wheel.PlaceOnRing(button, i, Slots.Length);
            button.RebindClick();
            // Always clickable, so a greyed slice can still answer with the denied feedback.
            button.Setup(title, CostLine(reason), icon, true,
                () => Select(index),
                () => Hover(index),
                () => Unhover(index));
            button.SetAffordable(reason == UltimateBlockReason.None);
            if (button.Button != null) button.Button.interactable = true;
        }

        if (hoveredSlot >= 0) Hover(hoveredSlot);
        else ShowIdleDetails();
    }

    private static string CostLine(UltimateBlockReason reason)
    {
        string cost = Loc.Get("ultwheel.cost", "1 Arcane Core");
        return reason == UltimateBlockReason.NoArcaneCore || reason == UltimateBlockReason.NotAllowedHere || reason == UltimateBlockReason.NoUltimate
            ? $"<color=#FF5555>{cost}</color>"
            : $"<color=#B98CFF>{cost}</color>";
    }

    private static string BlockedText(UltimateBlockReason reason)
    {
        switch (reason)
        {
            case UltimateBlockReason.NotAllowedHere: return Loc.Get("ultwheel.blocked_scene", "Ultimates can't be used here.");
            case UltimateBlockReason.NoUltimate: return Loc.Get("ultwheel.blocked_weapon", "This weapon has no ultimate.");
            case UltimateBlockReason.NoArcaneCore: return Loc.Get("ultwheel.blocked_core", "No Arcane Core. Captains and Arcane Fish drop them, and the shop sells them.");
            case UltimateBlockReason.AlreadyActive: return Loc.Get("ultwheel.blocked_active", "An ultimate is already running.");
            default: return null;
        }
    }

    private void ShowIdleDetails()
    {
        if (detailsPanel == null) return;
        detailsPanel.ShowPrompt(Loc.Get("ultwheel.idle_title", "Ultimates"),
            Loc.Get("ultwheel.idle_body", "Pick an ultimate. Each one costs an Arcane Core."));
    }

    private void Hover(int index)
    {
        hoveredSlot = index;
        if (detailsPanel == null || index < 0 || index >= Slots.Length) return;
        DraftUpgradeDefinition ult = DraftUpgradeService.GetUltimate(Slots[index]);
        UltimateBlockReason reason = controller.GetBlockReason(Slots[index]);
        if (ult == null)
        {
            detailsPanel.ShowPrompt(Loc.Get("ultwheel.none", "No ultimate"), BlockedText(UltimateBlockReason.NoUltimate));
            return;
        }
        string blocked = reason != UltimateBlockReason.None ? $"<color=#B8AE9C>{BlockedText(reason)}</color>" : null;
        detailsPanel.Show(ult.displayName, DraftUpgradeService.GetOrCreateInstance().GetIcon(ult.iconName),
            CostLine(reason), ult.description, blocked);
    }

    private void Unhover(int index)
    {
        if (hoveredSlot != index) return;
        hoveredSlot = -1;
        ShowIdleDetails();
    }

    private void Select(int index)
    {
        if (!isOpen || index < 0 || index >= Slots.Length) return;
        UltimateSlot slot = Slots[index];
        if (controller.GetBlockReason(slot) != UltimateBlockReason.None)
        {
            if (deniedFeedback != null) deniedFeedback.PlayFeedbacks();
            // Releasing LB on a greyed slice still closes; a click keeps the wheel up to pick again.
            if (!ultimateAction.IsPressed()) Close(restoreTime: true);
            else Refresh();
            return;
        }

        // Back to normal speed first, so the ultimate starts at full time.
        Close(restoreTime: true);
        controller.TryActivate(slot);
    }

    private void UpdateStickSelection()
    {
        Gamepad pad = Gamepad.current;
        if (pad == null) return;
        Vector2 stick = pad.leftStick.ReadValue();
        int slot = wheel.SlotForDirection(stick, Slots.Length, config.stickDeadZone);
        if (slot == stickSlot) return;
        stickSlot = slot;
        if (EventSystem.current == null) return;
        // Selecting the slice drives its glow and the details box (BuildWheelButton.OnSelect).
        EventSystem.current.SetSelectedGameObject(slot >= 0 && sliceButtons[slot] != null ? sliceButtons[slot].gameObject : null);
        if (slot < 0) ShowIdleDetails();
    }

    private static bool CancelPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) return true;
        Gamepad pad = Gamepad.current;
        return pad != null && pad.buttonEast.wasPressedThisFrame;
    }
}
