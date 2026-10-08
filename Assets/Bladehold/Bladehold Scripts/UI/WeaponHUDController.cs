using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
///     The bottom HUD's melee/ranged weapon slots: the equipped weapons' icons, and the Attack/Aim
///     button glyphs beside them. The glyphs are live <see cref="InputGlyph" />s (attached at runtime to
///     the keybind images), so they follow the last-used device and rebinds.
/// </summary>
public class WeaponHUDController : MonoBehaviour
{
    [Header("Weapon Icons")]
    public Image meleeWeaponIcon;
    public Image rangedWeaponIcon;

    [Header("Keybind Icons")]
    public Image meleeKeybindIcon;
    public Image rangedKeybindIcon;

    [Header("Slot Containers")]
    [Tooltip("The root container for the ranged weapon HUD slot.")]
    public GameObject rangedRootContainer;

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

        // Wait a frame to ensure PlayerWeaponManager has equipped the loadout in its Awake
        StartCoroutine(InitIconsRoutine());
    }

    private void OnEnable()
    {
        PlayerWeaponManager.OnWeaponLoadoutChanged += RefreshWeaponIcons;
    }

    private void OnDisable()
    {
        PlayerWeaponManager.OnWeaponLoadoutChanged -= RefreshWeaponIcons;
    }

    public void RefreshWeaponIcons()
    {
        var weaponManager = PlayerWeaponManager.Instance;
        if (weaponManager != null)
        {
            if (weaponManager.ActiveMeleeDefinition != null && weaponManager.ActiveMeleeDefinition.icon != null && meleeWeaponIcon != null)
                meleeWeaponIcon.sprite = weaponManager.ActiveMeleeDefinition.icon;

            if (weaponManager.ActiveRangedDefinition != null && weaponManager.ActiveRangedDefinition.icon != null && rangedWeaponIcon != null)
                rangedWeaponIcon.sprite = weaponManager.ActiveRangedDefinition.icon;
        }
    }

    private IEnumerator InitIconsRoutine()
    {
        yield return null;

        if (Player.Instance == null) yield break;

        RefreshWeaponIcons();

        InputActionMap map = Player.Instance.InputSettings != null ? Player.Instance.InputSettings.GetRebindableActionMap() : null;
        BindGlyph(meleeKeybindIcon, map, "Attack");
        BindGlyph(rangedKeybindIcon, map, "Aim");
    }

    private void BindGlyph(Image target, InputActionMap map, string actionName)
    {
        InputAction action = map != null ? map.FindAction(actionName) : null;
        if (action == null)
        {
            Debug.LogError($"WeaponHUDController: no '{actionName}' action on the player's Controls map.", this);
            return;
        }
        InputGlyph.AttachTo(target, action);
    }
}
