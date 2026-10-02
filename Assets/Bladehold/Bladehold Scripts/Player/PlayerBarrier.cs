using UnityEngine;

/// <summary>
///     The PlayerBarrier layer: colliders that only keep the player out (the Fishing Pond's water dome,
///     tower plot pads). The physics matrix limits the layer to colliding with Player, and every
///     projectile/aim cast strips it from its mask via <see cref="Exclude" /> so shots fly through.
///     The Fortification layer (plan 17 walls) is stripped too: walls stop enemies and the player's
///     body, but the player's and the towers' shots fly over them.
/// </summary>
public static class PlayerBarrier
{
    public const string LayerName = "PlayerBarrier";
    public const string FortificationLayerName = "Fortification";

    /// <summary><paramref name="mask" /> without the PlayerBarrier and Fortification layers (each unchanged if the layer doesn't exist).</summary>
    public static LayerMask Exclude(LayerMask mask)
    {
        int layer = LayerMask.NameToLayer(LayerName);
        if (layer >= 0) mask &= ~(1 << layer);
        int fort = LayerMask.NameToLayer(FortificationLayerName);
        if (fort >= 0) mask &= ~(1 << fort);
        return mask;
    }
}
