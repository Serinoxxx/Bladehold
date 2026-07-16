using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     A player-fired projectile currently in flight, registered while alive so enemy effects (the
///     Barbarian Giant's whirlwind) can find and destroy it mid-air. Implemented by
///     <see cref="AxeProjectile" />, <see cref="MagicMissileProjectile" />, and
///     <see cref="ArrowProjectile" /> via <c>OnEnable</c>/<c>OnDisable</c> — so whirlwinds swat
///     arrows out of the air like everything else.
/// </summary>
public interface IPlayerProjectile
{
    /// <summary>Current world position, for radius checks.</summary>
    Vector3 Position { get; }

    /// <summary>Destroys the projectile mid-flight without dealing its damage (VFX optional).</summary>
    void Shatter();
}

/// <summary>
///     Static registry of live player projectiles (the <see cref="EnemyRagdoll" />.<c>ActiveCount</c>
///     flavor — a cheap static set instead of scene scans). Consumers must iterate a copy:
///     <see cref="IPlayerProjectile.Shatter" /> unregisters mid-iteration.
/// </summary>
public static class PlayerProjectileRegistry
{
    private static readonly HashSet<IPlayerProjectile> live = new HashSet<IPlayerProjectile>();

    /// <summary>Every player projectile currently in flight. Iterate a copy if you might Shatter.</summary>
    public static IReadOnlyCollection<IPlayerProjectile> Live => live;

    public static void Register(IPlayerProjectile projectile)
    {
        live.Add(projectile);
    }

    public static void Unregister(IPlayerProjectile projectile)
    {
        live.Remove(projectile);
    }
}
