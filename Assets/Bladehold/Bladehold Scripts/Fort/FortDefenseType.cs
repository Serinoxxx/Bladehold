/// <summary>
///     Tower kinds a <see cref="TowerPlot" /> can build. Values are explicit and serialized as ints
///     (build-wheel options on the HUD prefab), so never renumber one. 1 and 2 were the removed Oil Vat
///     and Spike Trap towers (plan 17 moved those jobs onto wall upgrades); don't reuse them.
/// </summary>
public enum FortDefenseType
{
    ArrowSlits = 0,
    Catapult = 3,
    Ballista = 4,
    NetThrower = 5
}

public enum FortSocketType
{
    WallSlit,
    GateOverhead,
    GroundBarricade,
    Courtyard
}
