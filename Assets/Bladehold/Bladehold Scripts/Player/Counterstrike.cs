using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     The "Counterstrike" skill line: a successful <see cref="Parry" /> instantly deals
///     <see cref="StatType.CounterstrikePercent" /> of the player's effective sword damage back to
///     the attacker (<see cref="Damage.source" />, when the blocked hit carried one — e.g. a
///     goblin's own <see cref="Health" />). Listens to <see cref="Parry.OnParried" /> the same way
///     <see cref="VampiricBlade" /> listens to the sword's <c>OnHit</c>: a reactive add-on rather
///     than a change to Parry itself. Base 0 = locked (Parry alone still blocks, just without
///     payback).
/// </summary>
public class Counterstrike : MonoBehaviour
{
    [SerializeField] private Parry parry;
    [Tooltip("Optional; defaults to Player.Instance.Stats.")]
    [SerializeField] private PlayerStats stats;
    [Tooltip("Optional: played when a counterstrike lands on the attacker.")]
    [SerializeField] private MMF_Player counterFeedback;

    private bool anyError = false;

    private void OnValidate()
    {
        if (parry == null)
        {
            parry = GetComponent<Parry>();
        }
    }

    private void Start()
    {
        if (parry == null)
        {
            Debug.LogError("Parry component is not assigned or found on the GameObject.");
            anyError = true;
        }

        if (stats == null)
        {
            stats = Player.Instance != null ? Player.Instance.Stats : null;
        }
        if (stats == null)
        {
            Debug.LogError("Counterstrike could not find PlayerStats (set it or ensure Player.Instance.Stats exists).");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        stats.SetBase(StatType.CounterstrikePercent, 0f);
        parry.OnParried += HandleParried;
    }

    private void OnDestroy()
    {
        if (parry != null)
        {
            parry.OnParried -= HandleParried;
        }
    }

    private void HandleParried(Damage blocked)
    {
        float percent = stats.GetValue(StatType.CounterstrikePercent);
        if (percent <= 0f || blocked.source == null)
        {
            return;
        }

        float allDamageMultiplier = stats.GetValue(StatType.AllDamageMultiplier);
        if (allDamageMultiplier <= 0f)
        {
            allDamageMultiplier = 1f;
        }

        blocked.source.ReceiveDamage(new Damage
        {
            value = stats.GetValue(StatType.SwordDamage) * allDamageMultiplier * percent,
            type = DamageType.sharp,
            sourcePosition = transform.position,
            source = Player.Instance != null ? Player.Instance.Damageable : null,
            isPlayerDamage = true,
        });

        if (counterFeedback != null)
        {
            counterFeedback.PlayFeedbacks();
        }
    }
}
