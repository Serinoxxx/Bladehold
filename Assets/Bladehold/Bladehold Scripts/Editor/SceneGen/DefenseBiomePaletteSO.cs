using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Everything a <see cref="DefenseSceneSpecSO" /> is dressed with: terrain layers, Synty prefabs by
///     role, and lighting. Swapping the palette re-skins a layout (Alpine, Arid, Enchanted…) without
///     touching its geometry. Prefab roles are measured at generation time by <see cref="PrefabMeasure" />,
///     so any Synty prefab dropped into a list is sized and grounded correctly.
/// </summary>
[CreateAssetMenu(fileName = "DefenseBiomePalette", menuName = "Scriptable Objects/Scene Gen/Defense Biome Palette")]
public class DefenseBiomePaletteSO : ScriptableObject
{
    [Header("Terrain layers")]
    public TerrainLayer ground;
    [Tooltip("Blended into the ground by noise to break up tiling.")]
    public TerrainLayer groundVariant;
    [Tooltip("Mid slopes (roughly 25-40 degrees).")]
    public TerrainLayer slope;
    [Tooltip("Steep faces (over ~40 degrees).")]
    public TerrainLayer cliff;
    public TerrainLayer road;
    public TerrainLayer ravineFloor;
    public TerrainLayer courtyard;
    public Material terrainMaterial;

    [Header("Nature scatter")]
    [Tooltip("Big faces set into steep slopes along the valley edge.")]
    public List<GameObject> cliffs = new List<GameObject>();
    [Tooltip("Optional material forced onto every cliff/outcrop renderer, e.g. to put a snow-capped rock material on another biome's cliff meshes.")]
    public Material cliffMaterial;
    [Tooltip("Scale range for the big rock outcrops dotted over the mountain slopes (uses the cliff meshes).")]
    public Vector2 outcropScale = new Vector2(2f, 3.4f);
    public List<GameObject> largeRocks = new List<GameObject>();
    public List<GameObject> mediumRocks = new List<GameObject>();
    public List<GameObject> smallRocks = new List<GameObject>();
    public List<GameObject> snowMounds = new List<GameObject>();
    [Tooltip("Placed as terrain trees (GPU instanced, no GameObjects).")]
    public List<GameObject> trees = new List<GameObject>();
    [Tooltip("Dead/bare trees and stumps sprinkled near the field edge.")]
    public List<GameObject> deadTrees = new List<GameObject>();

    [Header("Ravine walls")]
    [Tooltip("Optional: painted on the ravine walls below the rim, so they read as rock rather than stretched ground. Empty keeps the plain slope/cliff paint.")]
    public TerrainLayer ravineWall;
    [Tooltip("Optional: rocks set into both ravine walls below the rim (colliders stripped, so the floor NavMesh is untouched). Tall, narrow rocks read best.")]
    public List<GameObject> ravineWallRocks = new List<GameObject>();

    [Header("Prop clusters")]
    [Tooltip("Hand-composed groups (bones, spiky rocks, succulents...) stamped between the roads. Harvested from a Synty demo scene by DefenseClusterHarvester.")]
    public List<PropCluster> propClusters = new List<PropCluster>();

    [Header("Structures")]
    public GameObject gate;
    [Tooltip("Yaw applied to the gate model so its outer face points +Z (towards the attackers).")]
    public float gateModelYaw = 180f;
    public GameObject wall;
    [Tooltip("Yaw applied to wall modules so their outer face points +Z.")]
    public float wallYaw = 180f;
    public GameObject wallTower;
    public GameObject bridgeTile;
    public GameObject bridgePillar;
    [Tooltip("Optional: one piece that spans the whole crossing (an arched stone bridge, length along Z), used instead of bridgeTile/bridgePillar. Its ends are seated on the rims; it must be longer than the ravine is wide.")]
    public GameObject bridgeSpan;
    [Tooltip("Width scale on bridgeSpan (X only), so a narrow arch carries the horde. One 'tile' of BridgeSpec.tilesWide is one scaled span.")]
    public float bridgeSpanWidthScale = 1f;
    public List<GameObject> pitSpikes = new List<GameObject>();
    [Tooltip("Wooden stake fortifications set in short lines on the gate-side flanks (keep their colliders).")]
    public List<GameObject> defenseStakes = new List<GameObject>();
    [Tooltip("Battle banners flanking the gate apron.")]
    public List<GameObject> banners = new List<GameObject>();
    [Tooltip("Sparse battlefield litter (logs, stones). Colliders are stripped so it never affects pathing or combat.")]
    public List<GameObject> fieldLitter = new List<GameObject>();
    [Tooltip("Props sprinkled inside the courtyard between buildings.")]
    public List<GameObject> courtyardProps = new List<GameObject>();

    [Header("Lanterns (optional)")]
    [Tooltip("Lamps lining both sides of every road, the bridge heads and the gate apron, each given a point light. Colliders are stripped. Instances of these prefabs inside prop clusters get a light too. Empty = none (the stock palettes).")]
    public List<GameObject> lanterns = new List<GameObject>();
    public Vector2 lanternScale = new Vector2(1f, 1f);
    [Tooltip("Metres between lanterns along each road edge.")]
    public float lanternSpacing = 14f;
    public Color lanternLightColor = new Color(1f, 0.72f, 0.38f);
    public float lanternLightIntensity = 3f;
    public float lanternLightRange = 9f;
    [Tooltip("Height of the light as a fraction of the lantern's own height (where the lamp is).")]
    [Range(0f, 1.2f)] public float lanternLightHeight = 0.8f;

    [Header("Ground fog (optional)")]
    [Tooltip("Particle fog patches laid over the field on a grid and along every ravine floor. Empty = none.")]
    public List<GameObject> groundFog = new List<GameObject>();
    [Tooltip("Multiplied into each fog system's start colour.")]
    public Color groundFogTint = Color.white;
    public float groundFogSpacing = 32f;
    [Tooltip("Optional glow rising out of the spike pits: point lights every ~12 m along each ravine floor. Intensity 0 = none.")]
    public float ravineGlowIntensity = 0f;
    public Color ravineGlowColor = new Color(0.35f, 1f, 0.45f);
    public float ravineGlowRange = 11f;

    [Header("Lighting")]
    public Material skybox;
    public UnityEngine.Rendering.VolumeProfile volumeProfile;
    public Color sunColor = new Color(1f, 0.95f, 0.88f);
    public float sunIntensity = 1.3f;
    public Vector3 sunEuler = new Vector3(38f, -35f, 0f);
    public Color fogColor = new Color(0.62f, 0.7f, 0.78f);
    public float fogDensity = 0.006f;
    public Color ambientSky = new Color(0.55f, 0.63f, 0.75f);
    public Color ambientEquator = new Color(0.42f, 0.5f, 0.58f);
    public Color ambientGround = new Color(0.3f, 0.3f, 0.32f);
}

/// <summary>A group of props laid out around a centre, re-grounded piece by piece wherever it is stamped.</summary>
[System.Serializable]
public class PropCluster
{
    public string name;
    [Tooltip("Furthest piece from the centre, in metres.")]
    public float radius;
    public List<ClusterPiece> pieces = new List<ClusterPiece>();
}

[System.Serializable]
public class ClusterPiece
{
    public GameObject prefab;
    [Tooltip("XZ offset from the cluster centre; Y is the height above the ground under the piece (negative = sunk).")]
    public Vector3 offset;
    public Quaternion rotation = Quaternion.identity;
    public Vector3 scale = Vector3.one;
}
