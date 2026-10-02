using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Layout of one gate-defense battle scene, in metres, for <see cref="DefenseSceneGenerator" />.
///     Frame: the gate sits at the origin facing +Z; enemies come from +Z, the castle is behind at -Z,
///     the battlefield is at world y = 0. Everything visual (prefabs, terrain layers, lighting) comes
///     from the <see cref="palette" />, so one spec can be re-skinned per biome.
/// </summary>
[CreateAssetMenu(fileName = "DefenseSceneSpec", menuName = "Scriptable Objects/Scene Gen/Defense Scene Spec")]
public class DefenseSceneSpecSO : ScriptableObject
{
    [Header("Output")]
    public string scenePath = "Assets/Bladehold/Bladehold Scenes/Bladehold Outer Gate.unity";
    public int seed = 1337;
    public DefenseBiomePaletteSO palette;

    [Header("Terrain")]
    [Tooltip("World XZ of the terrain's min corner.")]
    public Vector2 terrainMin = new Vector2(-200f, -120f);
    public float terrainSize = 400f;
    [Tooltip("Height range of the terrain; the tallest peak can't exceed terrainHeight - playfieldElevation above the field.")]
    public float terrainHeight = 130f;
    [Tooltip("How far the battlefield sits above the terrain's lowest possible point. Must exceed the deepest ravine.")]
    public float playfieldElevation = 30f;
    [Tooltip("Power of two + 1. 1025 over 400 m is ~0.39 m per sample, sharp enough for ravine walls.")]
    public int heightmapResolution = 1025;
    public int alphamapResolution = 1024;

    [Header("Valley (the walkable play space)")]
    [Tooltip("Half width of the notch the wall and gate span.")]
    public float gateNotchHalfWidth = 20f;
    [Tooltip("Half width of the open battlefield.")]
    public float fieldHalfWidth = 75f;
    [Tooltip("Z over which the notch flares out to the full field width.")]
    public float flareLength = 40f;
    [Tooltip("Z where the battlefield ends and the far mountains start.")]
    public float fieldEndZ = 175f;
    public float cornerRadius = 30f;
    [Tooltip("Amplitude of the organic wobble on the valley edge (not applied at the notch).")]
    public float edgeNoise = 9f;
    [Tooltip("Gentle roll on the battlefield floor. Suppressed around plots, the gate, bridges and ravines.")]
    public float fieldUndulation = 0.35f;

    [Header("Mountains")]
    public float foothillWidth = 16f;
    public float foothillHeight = 5f;
    public float mountainHeight = 85f;
    public float mountainWidth = 80f;
    [Tooltip("Foothill width right beside the notch, so the gate is framed by steep rock, not a slope you can walk round.")]
    public float notchFoothillWidth = 3f;

    [Header("Castle behind the gate")]
    public float courtyardDepth = 60f;
    public float courtyardHalfWidth = 38f;
    [Tooltip("Castle wall modules extend this far past the notch edge into the rock on each side.")]
    public float wallEmbed = 8f;
    public List<StructurePlacement> castleBuildings = new List<StructurePlacement>();

    [Header("Ravines (spike pits crossed by bridges)")]
    public List<RavineSpec> ravines = new List<RavineSpec>();

    [Header("Gameplay layout")]
    public Vector2 playerSpawn = new Vector2(0f, 9f);
    [Tooltip("Tower plot centres. Six is the battlefield standard.")]
    public List<Vector2> towerPlots = new List<Vector2>();
    [Tooltip("Enemy spawn points. Keep at least 9: DefeatSlayerObjective uses Spawnpoints child 8.")]
    public List<Vector2> enemySpawns = new List<Vector2>();
    [Tooltip("Extra clearance beyond the longest tower range that objective markers must keep from every plot.")]
    public float objectiveRangeMargin = 8f;
    [Tooltip("Objective markers are chosen from this Z band (and must still clear tower range).")]
    public Vector2 objectiveZRange = new Vector2(90f, 150f);

    [Header("Enemies")]
    [Tooltip("Roster ids this scene's sector waves spawn, replacing the threat curve (a SceneEnemyRoster is placed). Empty = the normal curve.")]
    public List<string> enemyRosterIds = new List<string>();
    [Tooltip("Fodder-floor enemy for this scene. Empty = the pacing asset's (goblin).")]
    public string fodderEnemyId = "";

    [Header("Walls and fort rules (plan 17)")]
    [Tooltip("Put a wall plot on every bridge (up to 8), across the deck just in from the gate-side rim, with its crafting bench on the rim beside the bridge.")]
    public bool wallPlotsOnBridges = true;
    [Tooltip("How far in from the gate-side rim the wall sits on the bridge deck, metres.")]
    public float wallPlotInset = 1.5f;
    [Tooltip("Place the fort-rules prefab (upgrade wheel, crystal rewards, crystal biome bias).")]
    public bool placeFortRules = true;
    [Tooltip("Place an ammo chest by the main gate.")]
    public bool placeAmmoChest = true;
    [Tooltip("Ammo chest position (outside the gate, beside the apron).")]
    public Vector2 ammoChestPosition = new Vector2(8f, 5f);

    [Header("Scatter")]
    [Tooltip("Scales every scatter density. 0 = structures only.")]
    public float scatterDensity = 1f;
    [Tooltip("Clear radius kept around plots, spawns, objective markers and the gate apron.")]
    public float gameplayClearRadius = 7f;
}

[Serializable]
public class StructurePlacement
{
    public GameObject prefab;
    public Vector2 position;
    public float yaw;
}

[Serializable]
public class RavineSpec
{
    [Tooltip("Z of the ravine centreline at x = 0.")]
    public float z = 70f;
    public float topWidth = 11f;
    public float floorWidth = 6f;
    public float depth = 6f;
    [Tooltip("Sideways meander of the centreline.")]
    public float meanderAmplitude = 4f;
    public float meanderWavelength = 90f;
    public List<BridgeSpec> bridges = new List<BridgeSpec>();
    public List<RampSpec> exitRamps = new List<RampSpec>();
}

[Serializable]
public class BridgeSpec
{
    public float x;
    [Tooltip("Bridge tiles side by side. 1 tile = 5 m deck, which fits the Large Enemy agent (radius 1).")]
    [Range(1, 3)] public int tilesWide = 1;
}

[Serializable]
public class RampSpec
{
    [Tooltip("X where the ramp leaves the ravine floor.")]
    public float x;
    [Tooltip("+1 climbs toward +X, -1 toward -X.")]
    public int direction = 1;
    public float width = 4.5f;
    [Tooltip("Max slope in degrees; must stay under the Humanoid agent's 45 so goblins can walk out.")]
    public float slopeDegrees = 24f;
}
