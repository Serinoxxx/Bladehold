/// <summary>
///     Shared by the pieces of one of Captain Mogra's casts (one rune pattern step, one bolt volley) so
///     overlapping pieces hit the player once, not once each. A dense pattern would otherwise stack
///     triple damage where its runes overlap.
/// </summary>
public sealed class HexHitGroup
{
    private bool spent;

    /// <summary>True the first time it's called; false after, so only one piece of the cast deals damage.</summary>
    public bool TryConsume()
    {
        if (spent) return false;
        spent = true;
        return true;
    }
}
