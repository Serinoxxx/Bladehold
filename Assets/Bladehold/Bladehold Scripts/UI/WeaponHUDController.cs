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

    private PlayerInput playerInput;
    private bool anyError;

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
