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
        // ---- Phase ③: auras & on-death ----

        // Ancient Queen: armored melee elite — light hits glance off (ArmorPlating on
        // Health.ScaleDamageTaken); the counter is charged swings. Melee stays the stock AIAttack.
        new EnemySpec
        {
            id = "ancient_queen",
            soFolder = "Ancient Queen",
            prefabName = "Ancient Queen Enemy Variant",
            assets = new[]
            {
                new SoSpec { soType = typeof(ArmorPlatingSO), assetName = "AncientQueenArmorSO" },
            },
            components = new[]
            {
                new ComponentSpec
                {
                    type = typeof(ArmorPlating),
                    wire = (so, ctx) =>
                    {
                        EnemyPrefabGenerator.SetReference(so, "data", ctx.LoadedAsset("AncientQueenArmorSO"));
                        EnemyPrefabGenerator.SetReference(so, "health", ctx.Health);
                    },
                },
            },
        },

        // Forest Witch: support healer — AllyAura heals nearby enemies (heal-only v1), never
        // herself. Golden/impulse rolls removed (the Storm Witch caster precedent).
        new EnemySpec
        {
            id = "forest_witch",
            soFolder = "Forest Witch",
            prefabName = "Forest Witch Enemy Variant",
            removeComponents = new[] { typeof(GoldenGoblin), typeof(ImpulseGoblin) },
            assets = new[]
            {
                new SoSpec { soType = typeof(AllyAuraSO), assetName = "ForestWitchAuraSO" },
            },
            components = new[]
            {
                new ComponentSpec
                {
                    type = typeof(AllyAura),
                    wire = (so, ctx) =>
                    {
                        EnemyPrefabGenerator.SetReference(so, "data", ctx.LoadedAsset("ForestWitchAuraSO"));
                        EnemyPrefabGenerator.SetReference(so, "health", ctx.Health);
                    },
                },
            },
        },

        // Mutant Guy: melee chaser that leaves a ToxicPoolZone where it dies (hand-authored
        // ToxicPool.prefab; a green light is the pre-art visual — see TODO.md).
        new EnemySpec
        {
            id = "mutant_guy",
            soFolder = "Mutant Guy",
            prefabName = "Mutant Guy Enemy Variant",
            assets = new[]
            {
                new SoSpec { soType = typeof(ToxicPoolOnDeathSO), assetName = "MutantToxicPoolSO" },
            },
            components = new[]
            {
                new ComponentSpec
                {
                    type = typeof(ToxicPoolOnDeath),
                    wire = (so, ctx) =>
                    {
                        EnemyPrefabGenerator.SetReference(so, "data", ctx.LoadedAsset("MutantToxicPoolSO"));
                        EnemyPrefabGenerator.SetReference(so, "health", ctx.Health);
                        EnemyPrefabGenerator.SetReference(so, "poolPrefab", LoadProjectile<ToxicPoolZone>("Assets/Bladehold/Bladehold Prefabs/ToxicPool.prefab"));
                    },
                },
            },
        },

        // Medusa: melee body with a cone slow gaze (MedusaGazeAura — static refcount guards the
        // MoveSpeed modifier). Golden/impulse rolls removed (caster precedent).
        new EnemySpec
        {
            id = "medusa",
            soFolder = "Medusa",
            prefabName = "Medusa Enemy Variant",
            removeComponents = new[] { typeof(GoldenGoblin), typeof(ImpulseGoblin) },
            assets = new[]
            {
                new SoSpec { soType = typeof(MedusaGazeAuraSO), assetName = "MedusaGazeSO" },
            },
            components = new[]
            {
                new ComponentSpec
                {
                    type = typeof(MedusaGazeAura),
                    wire = (so, ctx) =>
                    {
                        EnemyPrefabGenerator.SetReference(so, "data", ctx.LoadedAsset("MedusaGazeSO"));
                        EnemyPrefabGenerator.SetReference(so, "health", ctx.Health);
                    },
                },
            },
        },

        // ---- Phase ④: movement specials ----

        // Spirit Demon: no body-blocking — its own AIMovementSO with both avoidance tiers off, and
        // the body capsule excludes the enemy layer (7) so other enemies pass through it. Ghost
        // material is the manual art pass; the CSV gives it 50 impulse resistance (a ragdolling
        // ghost reads wrong).
        new EnemySpec
        {
            id = "spirit_demon",
            soFolder = "Spirit Demon",
            prefabName = "Spirit Demon Enemy Variant",
            assets = new[]
            {
                new SoSpec
                {
                    soType = typeof(AIMovementSO),
                    assetName = "SpiritDemonMovementSO",
                    initDefaults = so =>
                    {
                        var data = (AIMovementSO)so;
                        data.nearAvoidance = UnityEngine.AI.ObstacleAvoidanceType.NoObstacleAvoidance;
                        data.farAvoidance = UnityEngine.AI.ObstacleAvoidanceType.NoObstacleAvoidance;
                    },
                },
            },
            components = new[]
            {
                new ComponentSpec
                {
                    type = typeof(AIMovement),
                    wire = (so, ctx) =>
                    {
                        EnemyPrefabGenerator.SetReference(so, "movementSO", ctx.LoadedAsset("SpiritDemonMovementSO"));
                    },
                },
                new ComponentSpec
                {
                    type = typeof(CapsuleCollider),
                    wire = (so, ctx) => SetExcludeLayers(so, 1 << 7), // the enemy layer
                },
            },
        },

        // Dark Elf: melee skirmisher that burst-strafes when the player lines it up (DodgeDash —
        // timer v1 of the plan's open "when targeted" question).
        new EnemySpec
        {
            id = "dark_elf",
            soFolder = "Dark Elf",
            prefabName = "Dark Elf Enemy Variant",
            assets = new[]
            {
                new SoSpec { soType = typeof(DodgeDashSO), assetName = "DarkElfDodgeSO" },
            },
            components = new[]
            {
                new ComponentSpec
                {
                    type = typeof(DodgeDash),
                    wire = (so, ctx) =>
                    {
                        EnemyPrefabGenerator.SetReference(so, "data", ctx.LoadedAsset("DarkElfDodgeSO"));
                        EnemyPrefabGenerator.SetReference(so, "health", ctx.Health);
                        EnemyPrefabGenerator.SetReference(so, "movement", ctx.Movement);
                        EnemyPrefabGenerator.SetReference(so, "agent", ctx.Root.GetComponent<UnityEngine.AI.NavMeshAgent>());
                    },
                },
            },
        },

        // Slayer: telegraphed line dash (SlayerDashAttack — red lane, then near-instant sweep +
        // Warp). The dash IS its melee: base AIAttack disabled (the Troll precedent).
        new EnemySpec
        {
            id = "slayer",
            soFolder = "Slayer",
            prefabName = "Slayer Enemy Variant",
            disableBaseAIAttack = true,
            assets = new[]
            {
                new SoSpec { soType = typeof(SlayerDashAttackSO), assetName = "SlayerDashSO" },
            },
            components = new[]
            {
                new ComponentSpec
                {
                    type = typeof(SlayerDashAttack),
                    wire = (so, ctx) =>
                    {
                        EnemyPrefabGenerator.SetReference(so, "attackData", ctx.LoadedAsset("SlayerDashSO"));
                        EnemyPrefabGenerator.SetReference(so, "animator", ctx.ChildAnimator);
                        EnemyPrefabGenerator.SetReference(so, "health", ctx.Health);
                        EnemyPrefabGenerator.SetReference(so, "movement", ctx.Movement);
                        EnemyPrefabGenerator.SetReference(so, "agent", ctx.Root.GetComponent<UnityEngine.AI.NavMeshAgent>());
                        EnemyPrefabGenerator.SetReference(so, "telegraphPrefab", LoadPrefab("Assets/Bladehold/Bladehold Prefabs/SlamTelegraph.prefab"));
                    },
                },
            },
        },

        // Red Demon: leap & slam (LeapSlamAttack — TrollSlam telegraph at the player, parabolic
        // flight, impulse-stamped landing). Base AIAttack disabled (the Troll precedent).
        new EnemySpec
        {
            id = "red_demon",
            soFolder = "Red Demon",
            prefabName = "Red Demon Enemy Variant",
            disableBaseAIAttack = true,
            assets = new[]
            {
                new SoSpec { soType = typeof(LeapSlamAttackSO), assetName = "RedDemonLeapSO" },
            },
            components = new[]
            {
                new ComponentSpec
                {
                    type = typeof(LeapSlamAttack),
                    wire = (so, ctx) =>
                    {
                        EnemyPrefabGenerator.SetReference(so, "attackData", ctx.LoadedAsset("RedDemonLeapSO"));
                        EnemyPrefabGenerator.SetReference(so, "animator", ctx.ChildAnimator);
                        EnemyPrefabGenerator.SetReference(so, "health", ctx.Health);
                        EnemyPrefabGenerator.SetReference(so, "movement", ctx.Movement);
                        EnemyPrefabGenerator.SetReference(so, "agent", ctx.Root.GetComponent<UnityEngine.AI.NavMeshAgent>());
                        EnemyPrefabGenerator.SetReference(so, "telegraphPrefab", LoadPrefab("Assets/Bladehold/Bladehold Prefabs/SlamTelegraph.prefab"));
                    },
                },
            },
        },

        // ---- Phase ⑤: the hard four ----

        // Pig Butcher: parryable hook projectile that drags the player into chopping range
        // (HookProjectileAttack + hand-authored HookProjectile.prefab + PlayerPullReceiver on the
        // Player prefab — the receiver wiring is manual, see TODO.md). Base melee stays enabled:
        // the hook exists to feed it.
        new EnemySpec
        {
            id = "pig_butcher",
            soFolder = "Pig Butcher",
            prefabName = "Pig Butcher Enemy Variant",
            children = new[] { new ChildSpec { name = "Hook Spawn", localPosition = new Vector3(0f, 1.4f, 0.4f) } },
            assets = new[]
            {
                new SoSpec { soType = typeof(HookProjectileAttackSO), assetName = "PigButcherHookSO" },
            },
            components = new[]
            {
                new ComponentSpec
                {
                    type = typeof(HookProjectileAttack),
                    wire = (so, ctx) =>
                    {
                        EnemyPrefabGenerator.SetReference(so, "attackData", ctx.LoadedAsset("PigButcherHookSO"));
                        EnemyPrefabGenerator.SetReference(so, "animator", ctx.ChildAnimator);
                        EnemyPrefabGenerator.SetReference(so, "health", ctx.Health);
                        EnemyPrefabGenerator.SetReference(so, "firePoint", ctx.FindOrCreateChild("Hook Spawn", new Vector3(0f, 1.4f, 0.4f)).transform);
                        EnemyPrefabGenerator.SetReference(so, "hookPrefab", LoadProjectile<HookProjectile>("Assets/Bladehold/Bladehold Prefabs/HookProjectile.prefab"));
                    },
                },
            },
        },

        // Barbarian Giant: permanent whirlwind — periodic unparryable pulse + eats thrown
        // axes/magic missiles mid-flight (IPlayerProjectile registry). No collider is added, so
        // the hitscan bow is never eaten. The whirlwind IS its melee: base AIAttack disabled.
        new EnemySpec
        {
            id = "barbarian_giant",
            soFolder = "Barbarian Giant",
            prefabName = "Barbarian Giant Enemy Variant",
            disableBaseAIAttack = true,
            assets = new[]
            {
                new SoSpec { soType = typeof(WhirlwindAttackSO), assetName = "BarbarianWhirlwindSO" },
            },
            components = new[]
            {
                new ComponentSpec
                {
                    type = typeof(WhirlwindAttack),
                    wire = (so, ctx) =>
                    {
                        EnemyPrefabGenerator.SetReference(so, "attackData", ctx.LoadedAsset("BarbarianWhirlwindSO"));
                        EnemyPrefabGenerator.SetReference(so, "health", ctx.Health);
                    },
                },
            },
        },

        // Fort Golem: slow walking dwarf factory (MinionSpawner → WaveSpawner.RegisterExternalEnemy;
        // capped so a camped golem can't flood the wave). Weak base melee stays enabled.
        new EnemySpec
        {
            id = "fort_golem",
            soFolder = "Fort Golem",
            prefabName = "Fort Golem Enemy Variant",
            assets = new[]
            {
                new SoSpec { soType = typeof(MinionSpawnerSO), assetName = "FortGolemSpawnerSO" },
            },
            components = new[]
            {
                new ComponentSpec
                {
                    type = typeof(MinionSpawner),
                    wire = (so, ctx) =>
                    {
                        EnemyPrefabGenerator.SetReference(so, "data", ctx.LoadedAsset("FortGolemSpawnerSO"));
                        EnemyPrefabGenerator.SetReference(so, "health", ctx.Health);
                        EnemyPrefabGenerator.SetReference(so, "minionPrefab", LoadPrefab("Assets/Bladehold/Bladehold Prefabs/Dwarf Enemy Variant.prefab"));
                        EnemyPrefabGenerator.SetReference(so, "roster", LoadAsset<EnemyRosterSO>("Assets/Bladehold/Bladehold Scripts/Enemies/EnemyRosterSO.asset"));
                    },
                },
            },
        },

        // Mechanical Golem: pinball charge — rev-up telegraph, then wall-bouncing contact-damage
        // careen (NavMesh.Raycast reflection), Warp re-seat. The charge IS its melee: base
        // AIAttack disabled. High CSV impulse resistance stands in for a non-goblin silhouette.
        new EnemySpec
        {
            id = "mechanical_golem",
            soFolder = "Mechanical Golem",
            prefabName = "Mechanical Golem Enemy Variant",
            disableBaseAIAttack = true,
            assets = new[]
            {
                new SoSpec { soType = typeof(PinballChargeSO), assetName = "MechGolemChargeSO" },
            },
            components = new[]
            {
                new ComponentSpec
                {
                    type = typeof(PinballCharge),
                    wire = (so, ctx) =>
                    {
                        EnemyPrefabGenerator.SetReference(so, "attackData", ctx.LoadedAsset("MechGolemChargeSO"));
                        EnemyPrefabGenerator.SetReference(so, "animator", ctx.ChildAnimator);
                        EnemyPrefabGenerator.SetReference(so, "health", ctx.Health);
                        EnemyPrefabGenerator.SetReference(so, "movement", ctx.Movement);
                        EnemyPrefabGenerator.SetReference(so, "agent", ctx.Root.GetComponent<UnityEngine.AI.NavMeshAgent>());
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

    /// <summary>Loads a plain prefab for wiring (telegraphs, minion variants), throwing when missing.</summary>
    private static GameObject LoadPrefab(string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            throw new InvalidOperationException($"Prefab '{path}' doesn't exist.");
        }
        return prefab;
    }

    /// <summary>Loads a non-prefab asset for wiring (the roster), throwing when missing.</summary>
    private static T LoadAsset<T>(string path) where T : UnityEngine.Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            throw new InvalidOperationException($"Asset '{path}' doesn't exist.");
        }
        return asset;
    }

    /// <summary>Sets a collider's Exclude Layers mask (LayerMask fields serialize as a nested
    /// m_Bits on some Unity versions — handle both), failing loudly when the field is missing.</summary>
    private static void SetExcludeLayers(SerializedObject serialized, int mask)
    {
        SerializedProperty property = serialized.FindProperty("m_ExcludeLayers");
        if (property == null)
        {
            throw new InvalidOperationException($"{serialized.targetObject.GetType().Name} has no serialized 'm_ExcludeLayers' — renamed?");
        }
        if (property.propertyType == SerializedPropertyType.LayerMask || property.propertyType == SerializedPropertyType.Integer)
        {
            property.intValue = mask;
            return;
        }
        SerializedProperty bits = property.FindPropertyRelative("m_Bits");
        if (bits == null)
        {
            throw new InvalidOperationException("m_ExcludeLayers has no usable value field (neither LayerMask nor m_Bits).");
        }
        bits.longValue = (uint)mask;
    }
}
