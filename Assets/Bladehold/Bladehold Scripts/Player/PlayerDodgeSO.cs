using UnityEngine;

/// <summary>
///     Base tunables for the player's dodge / dash ability.
///     Registered as <see cref="PlayerStats" /> bases by <see cref="PlayerDodge" /> in <c>Start</c>.
///     Allows tuning cooldown, charge counts, distance, and duration directly from an asset.
/// </summary>
[CreateAssetMenu(fileName = "PlayerDodgeSO", menuName = "Scriptable Objects/PlayerDodgeSO")]
public class PlayerDodgeSO : ScriptableObject
{
    [Header("Charges & Cooldown")]
    [Tooltip("Base cooldown in seconds for each dash charge to recharge.")]
    public float baseCooldown = 1.2f;

    [Tooltip("Base maximum number of dash charges available before upgrades or perks (e.g. 2 for double dash).")]
    public int baseMaxCharges = 2;

    [Header("Movement")]
    [Tooltip("Base distance in metres the dodge covers.")]
    public float baseDistance = 2.5f;

    [Tooltip("Duration in seconds of the dash movement.")]
    public float dashDuration = 0.2f;

    [Header("Input Buffering")]
    [Tooltip("Window in seconds during which a dash input pressed while dashing will buffer and fire immediately upon completion.")]
    public float inputBufferDuration = 0.15f;
}
