using System;
using UnityEditor;
using UnityEngine;

/// <summary>
///     The declarative manifest the enemy prefab generator (Bladehold > Generate Enemy Prefabs) builds
///     from: one <see cref="EnemySpec" /> per generator-owned enemy type, describing the prefab's
///     *structure* — components to add/remove, per-enemy ScriptableObject assets, fire-point children,
///     reference wiring. Balance numbers stay in <c>Config/Enemies.csv</c> (the roster is the balance
///     sheet; the manifest never duplicates it), and visuals (materials/models) are a manual art pass —
///     <see cref="EnemySpec.materialPath" /> can only point at a material that already exists.
///
///     Hand-built variants (Goblin Brute, Storm Witch, Troll) deliberately have no entry here: the
///     generator only owns enemies authored through this manifest and must never clobber hand wiring.
/// </summary>
internal static class EnemyManifest
{
    /// <summary>A child GameObject to ensure under the variant root (e.g. a projectile fire point).
    /// Found by name on re-runs, created (with the root's layer) when missing.</summary>
    internal class ChildSpec
    {
        public string name;
        public Vector3 localPosition;
    }

    /// <summary>A per-enemy ScriptableObject asset, created at
    /// <c>Enemies/&lt;soFolder&gt;/&lt;assetName&gt;.asset</c> when missing. An existing asset is
    /// never overwritten — designer tuning survives re-runs — so <see cref="initDefaults" /> only
    /// runs on first creation.</summary>
    internal class SoSpec
    {
        public Type soType;
        public string assetName;
        public Action<ScriptableObject> initDefaults;
    }

    /// <summary>A component to ensure on the variant root. <see cref="wire" /> runs on every generator
    /// pass (rewiring is idempotent) with the component's <see cref="SerializedObject" /> and the
    /// generation context; use <see cref="EnemyPrefabGenerator.SetReference" /> so a renamed serialized
    /// field fails loudly instead of silently leaving a null reference.</summary>
    internal class ComponentSpec
    {
        public Type type;
        public Action<SerializedObject, EnemyPrefabGenerator.GenContext> wire;
    }

    /// <summary>Everything the generator needs to build (or re-sync) one enemy prefab variant.</summary>
    internal class EnemySpec
    {
        /// <summary>Roster CSV id this prefab is registered under in the <see cref="EnemyPrefabMapSO" />.</summary>
        public string id;

        /// <summary>Per-enemy SO folder name under <c>Bladehold Scripts/Enemies/</c> (the Storm Witch /
        /// Troll folder convention). Only needed when <see cref="assets" /> is non-empty.</summary>
        public string soFolder;

        /// <summary>Prefab asset name, e.g. "Dwarf Enemy Variant" (the existing variant naming).</summary>
        public string prefabName;

        /// <summary>Authored on the variant root; the CSV <c>scale</c> column multiplies on top at spawn.</summary>
        public float rootScale = 1f;

        /// <summary>Optional path to an *existing* material to swap onto the body renderer (the Storm
        /// Witch pattern). The generator never creates materials — art is a manual pass.</summary>
        public string materialPath;

        /// <summary>Disable the base goblin's melee <see cref="AIAttack" /> (disabled, never removed —
        /// matches the Storm Witch/Troll variants). Set when the enemy has its own attack component.</summary>
        public bool disableBaseAIAttack;

        /// <summary>Base components to remove from the variant (e.g. the Storm Witch removes
        /// <see cref="GoldenGoblin" />/<see cref="ImpulseGoblin" />).</summary>
        public Type[] removeComponents;

        public ChildSpec[] children;
        public SoSpec[] assets;
        public ComponentSpec[] components;

        /// <summary>Optional <c>NavMeshAgent.stoppingDistance</c> override (ranged enemies stand off;
        /// Storm Witch uses 6). Negative = leave the base value. Agent *avoidance* is never touched —
        /// <see cref="AIMovement" /> owns that in code.</summary>
        public float navStoppingDistance = -1f;
    }

    internal static readonly EnemySpec[] Entries =
    {
        // Dwarf: swarm unit — a pure stat variant of the goblin (extreme speed, low HP, small scale
        // all live in Enemies.csv). Structurally identical to the base, so this entry doubles as the
        // generator's smoke test.
        new EnemySpec
        {
            id = "dwarf",
            prefabName = "Dwarf Enemy Variant",
        },
    };
}
