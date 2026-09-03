using UnityEngine;

public enum WeaponCategory
{
    Melee,
    Ranged
}

/// <summary>
///     Configurable definition of a player weapon (Sword, Axe, Bow, Throwing Axe, Staff, Wand).
/// </summary>
[CreateAssetMenu(fileName = "WeaponDefinitionSO", menuName = "Scriptable Objects/WeaponDefinitionSO")]
public class WeaponDefinitionSO : ScriptableObject
{
    [Tooltip("Unique weapon identifier matching SaveData and roster logic.")]
    public string id = "sword";

    [Tooltip("Display name shown in UI and pedestals.")]
    public string displayName = "Sword";

    [Tooltip("Weapon slot category (Melee or Ranged).")]
    public WeaponCategory category = WeaponCategory.Melee;

    [Tooltip("Whether this weapon is unlocked by default on fresh saves.")]
    public bool isUnlockedByDefault = false;

    [Tooltip("Permanent cost in Orcish Metal to unlock this weapon.")]
    public int orcishMetalUnlockCost = 10;

    [Tooltip("Locked for demo flag.")]
    public bool isLockedForDemo = false;

    [Tooltip("General weapon description.")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("Hold/charge attack mechanic description.")]
    [TextArea(2, 4)]
    public string chargeDescription;

    [Tooltip("UI icon sprite for this weapon.")]
    public Sprite icon;

    [Tooltip("3D model prefab displayed on pedestals in the meta area.")]
    public GameObject modelPrefab;

    [Header("Combat Tuning")]
    [Tooltip("Hold attack charge time per level in seconds.")]
    public float chargeTimePerLevel = 0.33f;

    [Tooltip("Attack cooldown in seconds.")]
    public float attackCooldown = 0.5f;

    [Tooltip("Animator melee weapon type integer parameter (0 = Sword, 1 = Greataxe).")]
    public int animatorWeaponType = 0;
}
