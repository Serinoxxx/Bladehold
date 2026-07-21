using UnityEngine;

/// <summary>
///     The Berserker's "Pain into Power" skill line: damage taken while charging a melee swing
///     (<see cref="PlayerAttack.IsCharging" />) or winding up a throw
///     (<see cref="PlayerThrownAxe.IsAiming" />) is banked at
///     <see cref="StatType.PainIntoPowerPercent" /> (base 0 = locked) and added <b>flat</b> to that
///     attack's damage — "adds to the damage of that attack". The pool resets when a fresh
///     charge/wind-up begins (edge-detected in Update rather than via input events, so it can never
///     race the throw that consumes it) and is drained by <see cref="ConsumeBonus" /> — called once
///     per melee activation by <see cref="DamageTrigger" /> and once per throw by
///     <see cref="PlayerThrownAxe" />, so every target of that one attack shares the same bonus.
///
///     Lives on the player root, enabled only in the Berserker's class slot (a disabled component
///     banks nothing and returns 0).
/// </summary>
public class PainIntoPower : MonoBehaviour
{
    [Tooltip("Optional; defaults to the PlayerStats on this GameObject.")]
    [SerializeField] private PlayerStats stats;
    [Tooltip("Optional; defaults to the Health on this GameObject.")]
    [SerializeField] private Health health;
    [Tooltip("Optional; defaults to the PlayerAttack on this GameObject.")]
    [SerializeField] private PlayerAttack playerAttack;
    [Tooltip("Optional; defaults to the PlayerThrownAxe on this GameObject.")]
    [SerializeField] private PlayerThrownAxe thrownAxe;

    private float stored;
    private bool wasCharging;
    private bool wasAiming;
    private bool anyError = false;

    /// <summary>Bonus damage currently banked for the attack being charged — for the DevConsole readout.</summary>
    public float StoredBonus => stored;

    private void OnValidate()
    {
        if (stats == null)
        {
            stats = GetComponentInParent<PlayerStats>();
        }
        if (health == null)
        {
            health = GetComponentInParent<Health>();
        }
        if (playerAttack == null)
        {
            playerAttack = GetComponentInParent<PlayerAttack>();
        }
        if (thrownAxe == null)
        {
            thrownAxe = GetComponent<PlayerThrownAxe>();
        }
    }

    private void Start()
    {
        if (stats == null)
        {
            Debug.LogError("PainIntoPower could not find PlayerStats on the GameObject.");
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError("PainIntoPower could not find Health on the GameObject.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        // Base 0 = locked until the "Pain into Power" node is bought (the standard convention).
        stats.SetBase(StatType.PainIntoPowerPercent, 0f);

        health.OnDamaged += HandleDamaged;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
        }
    }

    private void Update()
    {
        if (anyError)
        {
            return;
        }

        // A fresh charge/wind-up starts a fresh pool. Edge-detected here so the reset can never
        // fire between an attack press and the throw/swing that should consume the old pool.
        bool charging = playerAttack != null && playerAttack.IsCharging;
        if (charging && !wasCharging)
        {
            stored = 0f;
        }
        wasCharging = charging;

        bool aiming = thrownAxe != null && thrownAxe.IsAiming;
        if (aiming && !wasAiming)
        {
            stored = 0f;
        }
        wasAiming = aiming;
    }

    private void HandleDamaged(Damage damage)
    {
        if (anyError || !(wasCharging || wasAiming))
        {
            return;
        }

        stored += damage.value * stats.GetValue(StatType.PainIntoPowerPercent);
    }

    /// <summary>
    ///     Drains the banked bonus for the attack that's landing now. The caller applies it flat to
    ///     every target of that one activation/throw.
    /// </summary>
    public float ConsumeBonus()
    {
        float bonus = stored;
        stored = 0f;
        return bonus;
    }
}
