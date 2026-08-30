/// <summary>
///     A hold-aim, charge-up, fire-on-attack weapon — the Swordsman's <see cref="PlayerBow" /> and the
///     Berserker's <see cref="PlayerThrownAxe" />. Lets the shared aim presentation
///     (<see cref="BowAimCamera" />, <see cref="BowCrosshairUI" />, <see cref="BowReloadUI" />) poll
///     whichever weapon the active class carries without knowing its concrete type; they resolve it
///     from a serialized <see cref="PlayerBow" /> (legacy wiring) or
///     <see cref="PlayerClassController.ActiveAimWeapon" />. The aim-camera framing values live on
///     each weapon's own SO and are surfaced here so the camera needs no per-weapon config reference.
/// </summary>
public interface IChargedAimWeapon
{
    /// <summary>True while the aim button is held and the weapon is drawn/wound up.</summary>
    bool IsAiming { get; }

    /// <summary>True if this aim weapon is unlocked and available to draw/aim.</summary>
    bool IsUnlocked => true;

    /// <summary>Ranged weapon type integer passed to the player Animator during aim (0 = Bow, 1 = Thrown Axe, 2 = Wand).</summary>
    int RangedWeaponType { get; }

    /// <summary>Alias for RangedWeaponType for backwards compatibility with generic weapon type queries.</summary>
    int WeaponType => RangedWeaponType;

    /// <summary>Charge level of the draw in progress, 0..MaxChargeLevels.</summary>
    int ChargeLevel { get; }

    /// <summary>Levels the current draw can reach.</summary>
    int MaxChargeLevels { get; }

    /// <summary>True while aiming and charging up a shot.</summary>
    bool IsCharging => IsAiming && MaxChargeLevels > 0;

    /// <summary>Elapsed seconds of the current ranged charge, clamped to [0, MaxChargeTime].</summary>
    float CurrentChargeTime => 0f;

    /// <summary>Total time in seconds required to reach maximum charge levels.</summary>
    float MaxChargeTime => 0f;

    /// <summary>Normalized charge progress [0..1] of the current hold.</summary>
    float ChargeProgress => MaxChargeTime > 0f ? UnityEngine.Mathf.Clamp01(CurrentChargeTime / MaxChargeTime) : 0f;

    /// <summary>Time in seconds required per charge level.</summary>
    float ChargeTimePerLevel => 0f;

    /// <summary>Fraction of the post-shot cooldown elapsed: 0 the instant a shot fires, 1 when ready.</summary>
    float CooldownFraction { get; }

    /// <summary>True while the weapon is between shots and can't fire yet.</summary>
    bool IsCoolingDown { get; }

    /// <summary>Camera boom distance while aiming, in metres.</summary>
    float AimCameraDistance { get; }

    /// <summary>Horizontal camera offset while aiming, in metres (positive = over the right shoulder).</summary>
    float AimCameraHorizontalOffset { get; }

    /// <summary>Field of view while aiming, as a fraction of the resting FOV (1 = unchanged).</summary>
    float AimFieldOfViewPercent { get; }

    /// <summary>Seconds the camera blends into (and out of) the aim framing.</summary>
    float AimBlendSeconds { get; }
}
