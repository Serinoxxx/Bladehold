using UnityEngine;

/// <summary>
///     The PlayerBarrier layer: colliders that only keep the player out (the Fishing Pond's water dome,
///     tower plot pads). The physics matrix limits the layer to colliding with Player, and every
///     projectile/aim cast strips it from its mask via <see cref="Exclude" /> so shots fly through.
/// </summary>
public static class PlayerBarrier
{
    public const string LayerName = "PlayerBarrier";

    /// <summary><paramref name="mask" /> without the PlayerBarrier layer (unchanged if the layer doesn't exist).</summary>
    public static LayerMask Exclude(LayerMask mask)
    {
        int layer = LayerMask.NameToLayer(LayerName);
        return layer >= 0 ? mask & ~(1 << layer) : mask;
    }
}
