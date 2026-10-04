using UnityEngine;

/// <summary>
///     Goblin Caravan tunables (any biome). Read by <see cref="GoblinCaravanEvent" />. The event's
///     <see cref="WorldEventSO.duration" /> is the escape timer: the wagon crosses the field leg after leg until
///     it runs out, then leaves with the gold, so "Escapes in 45s" on the banner is always true.
/// </summary>
[CreateAssetMenu(fileName = "GoblinCaravanEvent", menuName = "Scriptable Objects/World Events/Goblin Caravan")]
public class GoblinCaravanEventSO : WorldEventSO
{
    [Header("Route")]
    [Tooltip("Each leg runs from this far on one side of the player to this far on the other (random between x and y), so it passes close by.")]
    public Vector2 routeRadius = new Vector2(28f, 40f);
    [Tooltip("Wagon cruising speed (m/s). Player walk speed is ~5, so it can be caught on foot and run down on horseback.")]
    public float wagonSpeed = 3.5f;

    [Header("Wagon")]
    public float wagonHealth = 400f;
    [Tooltip("Gold dropped as a pickup every 10% of the wagon's health lost.")]
    public int goldPerChunk = 8;
    [Tooltip("Gold burst when the wagon is wrecked, split across burstBags pickups.")]
    public int wreckGold = 150;
    [Min(1)] public int burstBags = 10;
    [Tooltip("Scatter radius of dropped gold bags.")]
    public float bagScatter = 2.5f;

    [Header("Escort")]
    [Tooltip("Roster ids spawned around the wagon when it arrives. Must be in the scene's roster.")]
    public string[] escortIds = { "goblin_brute", "goblin_brute", "goblin", "goblin" };
}
