using System;
using MoreMountains.Feedbacks;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
///     "Your horse has fallen. Buy a new horse for 500g?" — the modal the Summon Mount button opens while
///     the run's warhorse is dead (<see cref="PlayerSummonMount" />). Buy does exactly what the gate's
///     <see cref="HorseStall" /> does (<see cref="HorseReplacement.TryBuy" />, same shared price); short on
///     gold, Buy is disabled and red with "not enough gold". Mid-wave without the Field Stables meta perk
///     (<see cref="HorseReplacement.CanBuyNow" />) it still opens, but Buy is disabled with "only between waves".
///     Self-contained: the whole hierarchy is built from code on first use (its own overlay canvas just
///     under the pause menu's), coloured and fonted from the global <see cref="UITheme" />, so no HUD prefab
///     wiring is needed. Like the shop and pause menu it pauses the game (<see cref="Time.timeScale" /> 0 plus
///     the Feel time-scale freeze), unlocks the cursor, suspends the player's <see cref="InputReader" /> and
///     the Esc pause toggle, and runs on unscaled time. Pad: A on the focused button, left/right between
///     them, B (or Esc) cancels.
/// </summary>
public class HorseReplacementDialog : MonoBehaviour
{
    private const string CursorOwner = "HorseReplacementDialog";
    private const int SortingOrder = 90; // under PauseMenuCanvas (100), over the HUD (0)
    private const float SelectedScale = 1.08f;

    private static HorseReplacementDialog instance;

    /// <summary>True while the prompt is showing.</summary>
    public static bool IsOpen => instance != null && instance.isOpen;

    private TMP_Text titleText;
    private TMP_Text bodyText;
    private TMP_Text statusText;
    private Button buyButton;
    private Button cancelButton;
    private Image buyImage;
    private TMP_Text buyLabel;
    private RectTransform window;

    private bool isOpen;
    private InputReader suspendedReader;
    private bool readerWasEnabled;
    private bool pauseToggleSuspended;
    private int openedFrame;
    private Action onPurchased;
    private UIThemeSO theme;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    /// <summary>
    ///     Opens the prompt (no-op if already open). <paramref name="readerToSuspend" /> is the player's input,
    ///     disabled while the prompt is up so its button presses don't also move/attack/jump;
    ///     <paramref name="purchased" /> runs after a successful purchase (purchase feedback).
    /// </summary>
    public static void Open(InputReader readerToSuspend, Action purchased = null)
    {
        if (IsOpen) return;
        if (instance == null)
        {
            GameObject go = new GameObject("HorseReplacementDialog", typeof(RectTransform));
            instance = go.AddComponent<HorseReplacementDialog>();
            instance.Build();
        }
        instance.Show(readerToSuspend, purchased);
    }

    /// <summary>Closes the prompt without buying.</summary>
    public static void CloseIfOpen()
    {
        if (IsOpen) instance.Close();
    }

    private void OnDestroy()
    {
        // Scene unloaded under an open prompt: give back everything it took.
        if (isOpen) RestoreState();
        RunSession.OnInRunGoldChanged -= HandleGoldChanged;
        if (instance == this) instance = null;
    }

    // ---------------------------------------------------------------- open / close

    private void Show(InputReader readerToSuspend, Action purchased)
    {
        isOpen = true;
        openedFrame = Time.frameCount;
        onPurchased = purchased;
        gameObject.SetActive(true);

        suspendedReader = readerToSuspend;
        readerWasEnabled = suspendedReader != null && suspendedReader.enabled;
        if (readerWasEnabled) suspendedReader.enabled = false;

        if (PauseMenuController.Instance != null && PauseMenuController.Instance.Actions != null)
        {
            PauseMenuController.Instance.SetToggleEnabled(false);
            pauseToggleSuspended = true;
        }

        Time.timeScale = 0f;
        MMTimeScaleEvent.Trigger(MMTimeScaleMethods.For, 0f, 0f, false, 0f, true);
        CursorLockManager.SetUnlock(CursorOwner, true);

        RunSession.OnInRunGoldChanged -= HandleGoldChanged;
        RunSession.OnInRunGoldChanged += HandleGoldChanged;
        Refresh();
        if (InputDeviceWatcher.GamepadActive) SelectDefault();
        else if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private void Close()
    {
        if (!isOpen) return;
        RestoreState();
        gameObject.SetActive(false);
    }

    private void RestoreState()
    {
        isOpen = false;
        RunSession.OnInRunGoldChanged -= HandleGoldChanged;

        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
            && window != null && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(window))
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        MMTimeScaleEvent.Reset();
        // Pause menu opened over us can't happen (its toggle is suspended), so the game was running.
        Time.timeScale = GameSettingsService.TargetTimeScale;
        CursorLockManager.SetUnlock(CursorOwner, false);

        if (pauseToggleSuspended && PauseMenuController.Instance != null && PauseMenuController.Instance.Actions != null)
        {
            PauseMenuController.Instance.SetToggleEnabled(true);
        }
        pauseToggleSuspended = false;

        if (readerWasEnabled && suspendedReader != null) suspendedReader.enabled = true;
        suspendedReader = null;
        readerWasEnabled = false;
    }

    private void Update()
    {
        if (!isOpen) return;

        // The press that opened us must not also cancel us.
        if (Time.frameCount == openedFrame) return;

        bool cancelPressed = (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
                             || (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame);
        if (cancelPressed)
        {
            Close();
            return;
        }

        // Keep pad focus inside the dialog (a disabled Buy, a mouse click on the dimmer, a device switch).
        if (InputDeviceWatcher.GamepadActive && EventSystem.current != null)
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            Selectable control = selected != null ? selected.GetComponent<Selectable>() : null;
            if (selected == null || !selected.transform.IsChildOf(window) || (control != null && !control.IsInteractable()))
            {
                SelectDefault();
            }
        }

        // Pad focus cue: the selected button stands slightly proud (a tint alone barely reads on parchment).
        GameObject current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        buyButton.transform.localScale = current == buyButton.gameObject ? Vector3.one * SelectedScale : Vector3.one;
        cancelButton.transform.localScale = current == cancelButton.gameObject ? Vector3.one * SelectedScale : Vector3.one;
    }

    private void SelectDefault()
    {
        if (EventSystem.current == null) return;
        Button target = buyButton.interactable ? buyButton : cancelButton;
        EventSystem.current.SetSelectedGameObject(target.gameObject);
    }

    private void HandleGoldChanged(int gold) => Refresh();

    private void Refresh()
    {
        int cost = HorseReplacement.GoldCost;
        // Mid-wave without the Field Stables perk the prompt still opens (so the player learns the rule),
        // but Buy is disabled with the reason; that takes precedence over "not enough gold".
        bool windowOpen = HorseReplacement.CanBuyNow(out string blockedReason);
        bool canAfford = HorseReplacement.CanAfford;
        bool canBuy = windowOpen && canAfford;

        titleText.text = Loc.Get("horse_replace.title", "Warhorse Fallen");
        bodyText.text = string.Format(Loc.Get("horse_replace.body", "Your horse has fallen. Buy a new horse for {0}g?"), cost);
        buyLabel.text = string.Format(Loc.Get("horse_replace.buy", "Buy ({0}g)"), cost);
        cancelButton.GetComponentInChildren<TMP_Text>().text = Loc.Get("common.cancel", "Cancel");

        buyButton.interactable = canBuy;
        buyImage.color = canBuy ? theme.Get(UIColorRole.Parchment) : theme.Get(UIColorRole.Danger, 0.55f);
        buyLabel.color = canBuy ? theme.Get(UIColorRole.TextOnParchment) : theme.Get(UIColorRole.Text, 0.7f);

        statusText.gameObject.SetActive(!canBuy);
        statusText.text = !windowOpen ? blockedReason : Loc.Get("horse_replace.no_gold", "Not enough gold");
    }

    private void HandleBuy()
    {
        if (!isOpen) return;
        if (!HorseReplacement.TryBuy())
        {
            Refresh();
            return;
        }
        Action callback = onPurchased;
        Close();
        callback?.Invoke();
    }

    private void HandleCancel()
    {
        if (isOpen) Close();
    }

    // ---------------------------------------------------------------- hierarchy

    private void Build()
    {
        theme = UITheme.For(UIMenuId.Global);
        TMP_FontAsset headerFont = theme.GetFont(UIFontRole.Header);
        TMP_FontAsset bodyFont = theme.GetFont(UIFontRole.Body);

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = SortingOrder;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        // Full-screen dimmer: also swallows clicks meant for the world/HUD behind.
        RectTransform dimmer = CreateRect("Dimmer", transform);
        Stretch(dimmer);
        dimmer.gameObject.AddComponent<Image>().color = theme.Get(UIColorRole.Dimmer);

        window = CreateRect("Window", transform);
        window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
        window.sizeDelta = new Vector2(680f, 330f);
        Image windowImage = window.gameObject.AddComponent<Image>();
        windowImage.color = theme.Get(UIColorRole.Window);
        Outline frame = window.gameObject.AddComponent<Outline>();
        frame.effectColor = theme.Get(UIColorRole.Frame);
        frame.effectDistance = new Vector2(3f, -3f);

        titleText = CreateText("Title", window, headerFont, 42f, theme.Get(UIColorRole.Accent));
        Place(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -28f), 56f);
        titleText.fontStyle = FontStyles.UpperCase;

        bodyText = CreateText("Body", window, bodyFont, 30f, theme.Get(UIColorRole.Text));
        Place(bodyText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -96f), 90f);
        bodyText.rectTransform.offsetMin = new Vector2(40f, bodyText.rectTransform.offsetMin.y);
        bodyText.rectTransform.offsetMax = new Vector2(-40f, bodyText.rectTransform.offsetMax.y);

        statusText = CreateText("NotEnoughGold", window, bodyFont, 24f, theme.Get(UIColorRole.Danger));
        Place(statusText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 112f), 32f);

        buyButton = CreateButton("BuyButton", window, bodyFont, theme.Get(UIColorRole.Parchment),
            theme.Get(UIColorRole.TextOnParchment), new Vector2(-130f, 56f), out buyImage, out buyLabel);
        cancelButton = CreateButton("CancelButton", window, bodyFont, theme.Get(UIColorRole.Ghost),
            theme.Get(UIColorRole.Text), new Vector2(130f, 56f), out Image _, out TMP_Text _);

        Navigation buyNav = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = cancelButton };
        Navigation cancelNav = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = buyButton };
        buyButton.navigation = buyNav;
        cancelButton.navigation = cancelNav;

        buyButton.onClick.AddListener(HandleBuy);
        cancelButton.onClick.AddListener(HandleCancel);

        gameObject.SetActive(false);
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>A full-width row of <paramref name="height" /> whose centre sits at <paramref name="position" /> from the anchor edge.</summary>
    private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, float height)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(0f, height);
    }

    private static TMP_Text CreateText(string name, Transform parent, TMP_FontAsset font, float size, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, TMP_FontAsset font, Color background, Color labelColor,
        Vector2 position, out Image image, out TMP_Text label)
    {
        RectTransform rect = CreateRect(name, parent);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(230f, 64f);

        image = rect.gameObject.AddComponent<Image>();
        image.color = background;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.selectedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.disabledColor = Color.white; // the Danger tint is the disabled look
        colors.colorMultiplier = 1.2f;
        button.colors = colors;

        // Pad focus needs to be visible on both buttons: a frame outline that the selected state brightens.
        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
        outline.effectDistance = new Vector2(2f, -2f);

        label = CreateText("Label", rect, font, 30f, labelColor);
        Stretch(label.rectTransform);
        return button;
    }
}
