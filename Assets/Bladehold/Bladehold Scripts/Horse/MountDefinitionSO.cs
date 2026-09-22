using UnityEngine;

/// <summary>
///     Definition ScriptableObject for player mounts.
///     Configures visual appearance (Malbers material and scale variance),
///     locomotion and trample stats (speed, damage, knockback),
///     and lifecycle rules (cast time, active duration, cooldown).
/// </summary>
[CreateAssetMenu(fileName = "NewMountDefinition", menuName = "Scriptable Objects/MountDefinitionSO")]
public class MountDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    public string id = "basic_horse";
    public string displayName = "Warhorse";
    [TextArea(2, 4)]
    public string description = "A reliable armored steed with balanced speed and trample force.";
    public Sprite icon;
    public int orcishMetalUnlockCost = 0;

    [Header("Visual Customization")]
    [Tooltip("Material from Malbers PolyArt applied to all submeshes of the horse.")]
    public Material material;
    [Tooltip("Visual scale multiplier (e.g. 0.95 to 1.10 for subtle 0-10% variance).")]
    [Range(0.85f, 1.25f)]
    public float scaleMultiplier = 1.0f;

    [Header("Locomotion Stats")]
    [Tooltip("Top forward speed in m/s at full movement without charging.")]
    public float maxSpeed = 8f;
    [Tooltip("Top speed in m/s while Shift-charging.")]
    public float chargeSpeed = 12f;

    [Header("Trample Combat Stats")]
    [Tooltip("Base damage dealt per trample hit at full charge speed.")]
    public float chargeDamage = 15f;
    [Tooltip("Knockback force impulse applied to trampled enemies.")]
    public float knockbackForce = 14f;

    [Header("Summon & Lifecycle")]
    [Tooltip("Channeling time in seconds required to summon the mount.")]
    public float castTime = 1.5f;
    [Tooltip("Maximum active riding duration in seconds before auto-dismount.")]
    public float mountDuration = 30f;
    [Tooltip("Cooldown in seconds after dismounting before the mount can be summoned again.")]
    public float mountCooldown = 90f;
}
