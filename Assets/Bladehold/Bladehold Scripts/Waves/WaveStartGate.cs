using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Lets something outside the wave loop hold the Ready hold shut (plan 21: the tutorial blocks the
///     next wave until the current lesson is done). <see cref="GameLoopManager" /> only reads
///     <see cref="IsBlocked" />, so it never needs to know who is blocking. Owners are Unity objects:
///     destroyed ones are pruned, so a block can't leak into the next scene.
/// </summary>
public static class WaveStartGate
{
    private static readonly HashSet<Object> owners = new HashSet<Object>();

    public static bool IsBlocked
    {
        get
        {
            if (owners.Count == 0) return false;
            owners.RemoveWhere(o => o == null);
            return owners.Count > 0;
        }
    }

    public static void Block(Object owner)
    {
        if (owner != null) owners.Add(owner);
    }

    public static void Release(Object owner)
    {
        owners.Remove(owner);
    }
}
