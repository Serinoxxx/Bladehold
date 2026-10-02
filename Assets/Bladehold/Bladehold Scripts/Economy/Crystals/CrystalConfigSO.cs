using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Where elemental crystals come from (plan 17): elite/special enemy drops, siege units and captains,
///     a wave-clear reward and an objective bonus. Which element a roll gives is biased by the scene's
///     <see cref="SceneCrystalBias" />. Elemental buff fish in the Fishing Pond pay crystals too
///     (<see cref="FishingManager" />). Read by <see cref="CrystalRewards" />.
/// </summary>
[CreateAssetMenu(fileName = "CrystalConfig", menuName = "Scriptable Objects/Economy/Crystal Config")]
public class CrystalConfigSO : ScriptableObject
{
    [Header("Enemy drops")]
    [Tooltip("Roster ids that never drop crystals (fodder). Every other roster enemy counts as elite/special.")]
    public List<string> fodderIds = new List<string> { "goblin", "skeleton_soldier", "skeleton_soldier_shield", "skeleton_knight", "skeleton_knight_shield" };
    [Tooltip("Chance an elite/special enemy drops a crystal.")]
    [Range(0f, 1f)] public float eliteDropChance = 0.2f;
    [Min(0)] public int eliteDropAmount = 1;
    [Tooltip("Siege units (troll, sapper, ram) always drop this many.")]
    [Min(0)] public int siegeDropAmount = 2;
    [Tooltip("Captains always drop this many.")]
    [Min(0)] public int captainDropAmount = 3;
    [Tooltip("Dropped where the enemy died; collected by walking over it (or the horse).")]
    public CrystalPickup pickupPrefab;

    [Header("Wave rewards")]
    [Tooltip("Crystals for every wave survived.")]
    [Min(0)] public int waveClearCrystals = 1;
    [Tooltip("Extra crystals when the wave's objective succeeded.")]
    [Min(0)] public int objectiveCrystals = 1;

    [Header("Elemental fish")]
    [Min(0)] public int elementalFishCrystals = 1;
}
