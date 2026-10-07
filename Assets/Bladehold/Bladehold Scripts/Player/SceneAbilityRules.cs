using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Per-scene switches for player abilities. Place one in a scene that restricts the player's kit
///     (the Fishing Pond: just you and your bow, no mount, ultimate or sword swings); a scene without
///     one allows everything. Scene singleton, read through the static <c>*Allowed</c> properties by
///     <see cref="PlayerSummonMount" />, <see cref="PlayerUltimateController" />, <see cref="PlayerAttack" />
///     and the HUD elements for those abilities. Blocking never touches run state, so the ultimate
///     charge and mount cooldown carry on into the next scene.
///     A tutorial step can lift the ultimate block for itself (<see cref="AllowUltimateFor" />): T2's
///     Ultimate Trial, in a scene that otherwise keeps ultimates off.
/// </summary>
public class SceneAbilityRules : MonoBehaviour
{
    [Tooltip("Summoning the mount (X / D-pad Up) works in this scene.")]
    [SerializeField] private bool allowMount = true;
    [Tooltip("Firing the ultimate works in this scene, and its HUD bar shows.")]
    [SerializeField] private bool allowUltimate = true;
    [Tooltip("Un-aimed attack presses swing the melee weapon in this scene.")]
    [SerializeField] private bool allowMelee = true;

    public static SceneAbilityRules Instance { get; private set; }

    public static bool MountAllowed => Instance == null || Instance.allowMount;
    public static bool UltimateAllowed => Instance == null || Instance.allowUltimate || UltimateOverrides.Count > 0;
    public static bool MeleeAllowed => Instance == null || Instance.allowMelee;

    private static readonly HashSet<Object> UltimateOverrides = new HashSet<Object>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => UltimateOverrides.Clear();

    /// <summary>Allows the ultimate here while <paramref name="owner" /> holds it, whatever the scene says.</summary>
    public static void AllowUltimateFor(Object owner)
    {
        if (owner != null) UltimateOverrides.Add(owner);
    }

    public static void ReleaseUltimateFor(Object owner)
    {
        UltimateOverrides.Remove(owner);
        UltimateOverrides.RemoveWhere(o => o == null);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("[SceneAbilityRules] More than one in the scene; keeping the first.", this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
