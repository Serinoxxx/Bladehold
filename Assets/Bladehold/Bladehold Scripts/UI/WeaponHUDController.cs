using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;

public class WeaponHUDController : MonoBehaviour
{
    [Header("Weapon Icons")]
    public Image meleeWeaponIcon;
    public Image rangedWeaponIcon;

    [Header("Keybind Icons")]
    public Image meleeKeybindIcon;
    public Image rangedKeybindIcon;

    [Header("Synty Input Icons (Keyboard/Mouse)")]
    public Sprite mouseLeftSprite;
    public Sprite mouseRightSprite;

    [Header("Synty Input Icons (Gamepad)")]
    public Sprite gamepadMeleeSprite;
    public Sprite gamepadRangedSprite;

    [Header("Slot Containers")]
    [Tooltip("The root container for the ranged weapon HUD slot.")]
    public GameObject rangedRootContainer;

    private PlayerInput playerInput;
    private bool anyError;

    private void Awake()
    {
        if (rangedRootContainer == null && rangedWeaponIcon != null)
        {
            Transform curr = rangedWeaponIcon.transform;
            while (curr != null)
            {
                if (curr.name.Contains("Ranged") || curr.name.Contains("Item_01"))
                {
                    rangedRootContainer = curr.gameObject;
                    break;
                }
                curr = curr.parent;
            }
        }
    }

    private void Start()
    {
        if (meleeWeaponIcon == null || rangedWeaponIcon == null || meleeKeybindIcon == null || rangedKeybindIcon == null)
        {
            Debug.LogError("WeaponHUDController: Missing UI Image references.", this);
            anyError = true;
        }

        if (anyError) return;

        // Wait a frame to ensure PlayerClassController has initialized ActiveClass in its Awake
        StartCoroutine(InitIconsRoutine());
    }

    private void Update()
    {
        if (anyError || Player.Instance == null) return;

        bool rangedUnlocked = false;
        var classController = Player.Instance.GetComponent<PlayerClassController>();
        if (classController != null)
        {
            rangedUnlocked = classController.IsAimWeaponUnlocked;
        }
        else
        {
            rangedUnlocked = Player.Instance.Stats.GetValue(StatType.BowUnlocked) > 0f;
        }

        if (rangedRootContainer != null)
        {
            if (rangedRootContainer.activeSelf != rangedUnlocked)
            {
                rangedRootContainer.SetActive(rangedUnlocked);
            }
        }
        else if (rangedWeaponIcon != null)
        {
            if (rangedWeaponIcon.gameObject.activeSelf != rangedUnlocked)
            {
                rangedWeaponIcon.gameObject.SetActive(rangedUnlocked);
                if (rangedKeybindIcon != null) rangedKeybindIcon.gameObject.SetActive(rangedUnlocked);
            }
        }
    }

    private IEnumerator InitIconsRoutine()
    {
        yield return null;

        if (Player.Instance != null)
        {
            var classController = Player.Instance.GetComponent<PlayerClassController>();
            if (classController != null && classController.ActiveClass != null)
            {
                if (classController.ActiveClass.meleeIcon != null)
                    meleeWeaponIcon.sprite = classController.ActiveClass.meleeIcon;
                
                if (classController.ActiveClass.rangedIcon != null)
                    rangedWeaponIcon.sprite = classController.ActiveClass.rangedIcon;
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
    }

    private void OnControlsChanged(PlayerInput input)
    {
        UpdateKeybindIcons(input.currentControlScheme);
    }

    private void UpdateKeybindIcons(string controlScheme)
    {
        bool isGamepad = controlScheme == "Gamepad";

        if (meleeKeybindIcon != null)
            meleeKeybindIcon.sprite = isGamepad ? gamepadMeleeSprite : mouseLeftSprite;

        if (rangedKeybindIcon != null)
            rangedKeybindIcon.sprite = isGamepad ? gamepadRangedSprite : mouseRightSprite;
    }
}
