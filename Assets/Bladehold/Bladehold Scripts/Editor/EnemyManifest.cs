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

        // Ancient Warrior: standard balanced melee — a pure stat variant (Enemies.csv row only).
        new EnemySpec
        {
            id = "ancient_warrior",
            prefabName = "Ancient Warrior Enemy Variant",
        },

        // Big Ork: heavy melee — high damage, medium speed, big scale; again all stats in the CSV.
        new EnemySpec
        {
            id = "big_ork",
            prefabName = "Big Ork Enemy Variant",
        },

        // Forest Guardian: fast straight projectiles — zero new code, the Storm Witch's
        // LightningBallAttack with its own SO (high projectile speed, short cooldown). Re-tinted
        // projectile prefab is a manual art pass; until then it fires the shared LightningBall.
        new EnemySpec
        {
            id = "forest_guardian",
            soFolder = "Forest Guardian",
            prefabName = "Forest Guardian Enemy Variant",
            disableBaseAIAttack = true,
            navStoppingDistance = 8f,
            removeComponents = new[] { typeof(GoldenGoblin), typeof(ImpulseGoblin) },
            children = new[] { new ChildSpec { name = "Projectile Spawn", localPosition = new Vector3(0f, 1.4f, 0.4f) } },
            assets = new[]
            {
                new SoSpec
                {
                    soType = typeof(LightningBallAttackSO),
                    assetName = "ForestGuardianAttackSO",
                    initDefaults = so =>
                    {
                        var data = (LightningBallAttackSO)so;
                        data.attackRange = 14f;
                        data.damage = 3f;
                        data.ballSpeed = 12f; // Fast and straight — the type's identity vs. the witch's slow ball.
                        data.ballLifetime = 4f;
                        data.attackCooldown = 2f;
                    },
                },
            },
            components = new[]
            {
                new ComponentSpec
                {
                    type = typeof(LightningBallAttack),
                    wire = (so, ctx) =>
                    {
                        EnemyPrefabGenerator.SetReference(so, "attackData", ctx.LoadedAsset("ForestGuardianAttackSO"));
                        EnemyPrefabGenerator.SetReference(so, "animator", ctx.ChildAnimator);
                        EnemyPrefabGenerator.SetReference(so, "health", ctx.Health);
                        EnemyPrefabGenerator.SetReference(so, "firePoint", ctx.FindOrCreateChild("Projectile Spawn", new Vector3(0f, 1.4f, 0.4f)).transform);
                        EnemyPrefabGenerator.SetReference(so, "ballPrefab", LoadProjectile<LightningBall>("Assets/Bladehold/Bladehold Prefabs/LightningBall.prefab"));
                    },
                },
            },
        },

        // Mystic: slow homing orbs (HomingOrbAttack/HomingOrb — the LightningBallAttack skeleton
        // plus turn-rate-capped steering that gives up after homingSeconds). SO defaults are
        // authored on HomingOrbAttackSO itself, so no initDefaults needed.
        new EnemySpec
        {
            id = "mystic",
            soFolder = "Mystic",
            prefabName = "Mystic Enemy Variant",
            disableBaseAIAttack = true,
            navStoppingDistance = 8f,
            removeComponents = new[] { typeof(GoldenGoblin), typeof(ImpulseGoblin) },
            children = new[] { new ChildSpec { name = "Projectile Spawn", localPosition = new Vector3(0f, 1.4f, 0.4f) } },
            assets = new[]
            {
                new SoSpec { soType = typeof(HomingOrbAttackSO), assetName = "MysticAttackSO" },
            },
            components = new[]
            {
                new ComponentSpec
                {
                    type = typeof(HomingOrbAttack),
                    wire = (so, ctx) =>
                    {
                        EnemyPrefabGenerator.SetReference(so, "attackData", ctx.LoadedAsset("MysticAttackSO"));
                        EnemyPrefabGenerator.SetReference(so, "animator", ctx.ChildAnimator);
                        EnemyPrefabGenerator.SetReference(so, "health", ctx.Health);
                        EnemyPrefabGenerator.SetReference(so, "firePoint", ctx.FindOrCreateChild("Projectile Spawn", new Vector3(0f, 1.4f, 0.4f)).transform);
                        EnemyPrefabGenerator.SetReference(so, "orbPrefab", LoadProjectile<HomingOrb>("Assets/Bladehold/Bladehold Prefabs/HomingOrb.prefab"));
                    },
                },
            },
        },

        // Evil God: rare wave-16 mini-boss firing 360° radial bursts (RadialBurstAttack — reuses
        // the LightningBall projectile class; no line of sight needed). SO defaults authored on
        // RadialBurstAttackSO itself.
        new EnemySpec
        {
            id = "evil_god",
            soFolder = "Evil God",
            prefabName = "Evil God Enemy Variant",
            disableBaseAIAttack = true,
            navStoppingDistance = 10f,
            removeComponents = new[] { typeof(GoldenGoblin), typeof(ImpulseGoblin) },
            children = new[] { new ChildSpec { name = "Burst Origin", localPosition = new Vector3(0f, 1.6f, 0f) } },
            assets = new[]
            {
                new SoSpec { soType = typeof(RadialBurstAttackSO), assetName = "EvilGodBurstSO" },
            },
            components = new[]
            {
                new ComponentSpec
                {
                    type = typeof(RadialBurstAttack),
                    wire = (so, ctx) =>
                    {
                        EnemyPrefabGenerator.SetReference(so, "attackData", ctx.LoadedAsset("EvilGodBurstSO"));
                        EnemyPrefabGenerator.SetReference(so, "animator", ctx.ChildAnimator);
                        EnemyPrefabGenerator.SetReference(so, "health", ctx.Health);
                        EnemyPrefabGenerator.SetReference(so, "firePoint", ctx.FindOrCreateChild("Burst Origin", new Vector3(0f, 1.6f, 0f)).transform);
                        EnemyPrefabGenerator.SetReference(so, "ballPrefab", LoadProjectile<LightningBall>("Assets/Bladehold/Bladehold Prefabs/LightningBall.prefab"));
                    },
                },
            },
        },
    };

    /// <summary>Loads a projectile prefab's component for wiring, throwing when the prefab is
    /// missing or lacks the component — a silent null here would only surface as a Start error at
    /// spawn time.</summary>
    private static T LoadProjectile<T>(string path) where T : Component
    {
        var component = AssetDatabase.LoadAssetAtPath<T>(path);
        if (component == null)
        {
            throw new InvalidOperationException($"Projectile prefab '{path}' is missing or has no {typeof(T).Name} component.");
        }
        return component;
    }
}
