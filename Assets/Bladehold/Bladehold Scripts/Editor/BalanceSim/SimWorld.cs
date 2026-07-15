using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Bladehold.BalanceSim
{
    /// <summary>Behaviour hint the tick engine uses for enemies that aren't plain chase-and-swing melee.</summary>
    public enum EnemyArchetype
    {
        Melee,
        /// <summary>Detonates once shortly after engaging, then dies (the Bomber's one-shot threat).</summary>
        Suicide,
        /// <summary>Attacks from range: elemental (unparryable) hits, doesn't occupy a melee-attacker slot.</summary>
        Ranged,
    }

    /// <summary>Sim-owned copy of one roster row with SO fallbacks already resolved. Mutable (overrides edit this, never the assets).</summary>
    public class SimEnemyType
    {
        public string id;
        public float health;
        public float damage;
        public int minGold;
        public int maxGold;
        public float speed;
        public int unlockWave;
        /// <summary>0..1 (already divided by 100 by <see cref="EnemyRosterSO" />).</summary>
        public float spawnChance;
        public int minSpawn;
        public int maxConcurrent;
        public EnemyArchetype archetype;
        /// <summary>Shared across the roster from the one AIAttackSO asset (no CSV column).</summary>
        public float attackCooldown;
        public float windupToApex;
    }

    /// <summary>
    ///     The in-memory snapshot the sim runs against: the real assets' values copied into mutable
    ///     structs so <see cref="SimOverrides" /> can apply what-ifs without ever touching an asset.
    ///     Loading calls <c>Reload()</c> on the CSV-backed SOs first so a just-edited CSV is honoured.
    /// </summary>
    public class SimWorld
    {
        // Shipped asset locations; each has a FindAssets fallback in Load() should files move.
        private const string RosterPath = "Assets/Bladehold/Bladehold Scripts/Enemies/EnemyRosterSO.asset";
        private const string GoldTreePath = "Assets/Bladehold/Bladehold Scripts/Upgrades/SkillTreeSO.asset";
        private const string WaveConfigPath = "Assets/Bladehold/Bladehold Scripts/Waves/WaveConfigSO.asset";
        private const string PlayerHealthPath = "Assets/Bladehold/Bladehold Scripts/DamageSystem/PlayerHealthSO.asset";
        private const string SwordDamagePath = "Assets/Bladehold/Bladehold Scripts/DamageSystem/DamageSO.asset";
        private const string SwordTriggerPath = "Assets/Bladehold/Bladehold Scripts/DamageSystem/DamageTriggerSO.asset";
        private const string EnemyAttackPath = "Assets/Bladehold/Bladehold Scripts/Enemies/AIAttackSO.asset";
        private const string GoblinHealthPath = "Assets/Bladehold/Bladehold Scripts/DamageSystem/GoblinHealthSO.asset";
        private const string EnemyGoldPath = "Assets/Bladehold/Bladehold Scripts/Enemies/EnemySO.asset";
        private const string EnemyMovementPath = "Assets/Bladehold/Bladehold Scripts/Enemies/AIMovementSO.asset";
        private const string HealthPackDropPath = "Assets/Bladehold/Bladehold Scripts/Enemies/HealthpackPowerupDropSO.asset";

        // Player
        public float playerMaxHealth;
        public float swordBaseDamage;
        public int swordMaxHitsBase;
        /// <summary>Serialized default on the sword's DamageTrigger — mirrors DamageTrigger.cs:47.</summary>
        public float baseCritMultiplier = 1.5f;
        /// <summary>Mirrors PlayerAttack.cs:32 (Swordsman default; classes override via ClassDefinitionSO).</summary>
        public float chargeTimePerLevel = 1f;

        // Economy — mirrors CoinDropper.cs:27-29 serialized defaults (per-prefab fields, no SO).
        public float goldBagChance = 0.05f;
        public float goldBagMultiplier = 5f;
        public float healthPackDropChance;

        // Waves
        public int baseGoblinCount;
        public int goblinsAddedPerWave;
        public int maxConcurrent;
        public int timeBetweenWaves;
        public float spawnInterval;

        // Sim-only abstractions
        public float spawnDistanceMeters = 20f;
        /// <summary>Seconds between a bomber engaging and detonating — its intercept window.</summary>
        public float bomberFuseSeconds = 1.5f;

        public List<SimEnemyType> enemies = new List<SimEnemyType>();
        public SkillTreeSO goldTree;
        /// <summary>Node levels granted free at run start (the <c>node.&lt;id&gt;</c> override).</summary>
        public Dictionary<string, int> prePurchasedNodes = new Dictionary<string, int>();
        public Dictionary<string, PlayerProfile> profiles;

        /// <summary>Wave N kill total — mirrors WaveConfigSO.GoblinsForWave.</summary>
        public int GoblinsForWave(int wave) => baseGoblinCount + Math.Max(0, wave - 1) * goblinsAddedPerWave;

        public static SimWorld Load()
        {
            var w = new SimWorld();

            EnemyRosterSO roster = LoadAsset<EnemyRosterSO>(RosterPath);
            w.goldTree = LoadAsset<SkillTreeSO>(GoldTreePath);
            WaveConfigSO wave = LoadAsset<WaveConfigSO>(WaveConfigPath);
            HealthSO playerHealth = LoadAsset<HealthSO>(PlayerHealthPath);
            DamageSO swordDamage = LoadAsset<DamageSO>(SwordDamagePath);
            DamageTriggerSO swordTrigger = LoadAsset<DamageTriggerSO>(SwordTriggerPath);
            AIAttackSO enemyAttack = LoadAsset<AIAttackSO>(EnemyAttackPath);
            HealthSO goblinHealth = LoadAsset<HealthSO>(GoblinHealthPath);
            EnemySO enemyGold = LoadAsset<EnemySO>(EnemyGoldPath);
            AIMovementSO enemyMove = LoadAsset<AIMovementSO>(EnemyMovementPath);
            PowerupDropSO packDrop = LoadAsset<PowerupDropSO>(HealthPackDropPath, optional: true);

            // Re-parse CSV-backed SOs so a freshly edited CSV is picked up within the same editor session.
            roster.Reload();
            w.goldTree.Reload();

            w.playerMaxHealth = playerHealth.maxHealth;
            w.swordBaseDamage = swordDamage.baseDamage;
            w.swordMaxHitsBase = swordTrigger.maxHits;

            w.baseGoblinCount = wave.baseGoblinCount;
            w.goblinsAddedPerWave = wave.goblinsAddedPerWave;
            w.maxConcurrent = wave.maxConcurrent;
            w.timeBetweenWaves = wave.timeBetweenWaves;
            w.spawnInterval = wave.spawnInterval;

            w.healthPackDropChance = 0f;
            if (packDrop != null)
            {
                foreach (PowerupDropSO.Entry entry in packDrop.entries)
                {
                    w.healthPackDropChance += entry.chance; // entries roll independently; sum ≈ per-kill pack chance
                }
            }

            foreach (EnemyDefinition def in roster.Enemies)
            {
                w.enemies.Add(new SimEnemyType
                {
                    id = def.id,
                    // CSV cell present → CSV wins; blank → the shared prefab SO value stands
                    // (the WaveSpawner.ApplyDefinition precedence).
                    health = def.health ?? goblinHealth.maxHealth,
                    damage = def.damage ?? enemyAttack.damage,
                    minGold = def.minGold ?? enemyGold.minCoinDrop,
                    maxGold = def.maxGold ?? enemyGold.maxCoinDrop,
                    speed = def.speed ?? enemyMove.speed,
                    unlockWave = def.unlockWave,
                    spawnChance = def.spawnChance,
                    minSpawn = def.minSpawn,
                    maxConcurrent = def.maxConcurrent,
                    archetype = ArchetypeFor(def.id),
                    attackCooldown = enemyAttack.attackCooldown,
                    windupToApex = enemyAttack.windupToApex,
                });
            }
            if (w.enemies.Count == 0)
            {
                throw new InvalidOperationException("Enemy roster parsed to zero rows.");
            }

            w.profiles = PlayerProfile.LoadAll();
            return w;
        }

        /// <summary>
        ///     v1 behaviour hints for enemies that aren't plain melee. Promote to an Enemies.csv column if
        ///     this list grows — for now only the two archetypes that materially change the survival math.
        /// </summary>
        private static EnemyArchetype ArchetypeFor(string id)
        {
            switch (id)
            {
                case "bomber": return EnemyArchetype.Suicide;
                case "storm_witch":
                case "forest_witch":
                case "mystic": return EnemyArchetype.Ranged;
                default: return EnemyArchetype.Melee;
            }
        }

        private static T LoadAsset<T>(string path, bool optional = false) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }
            // Fallback: the asset moved — find the first of its type.
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                {
                    Debug.LogWarning($"BalanceSim: {typeof(T).Name} not at '{path}'; using '{AssetDatabase.GetAssetPath(asset)}'.");
                    return asset;
                }
            }
            if (optional)
            {
                return null;
            }
            throw new InvalidOperationException($"BalanceSim: no {typeof(T).Name} asset found (expected at {path}).");
        }

        public SimEnemyType FindEnemy(string id)
        {
            foreach (SimEnemyType e in enemies)
            {
                if (string.Equals(e.id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return e;
                }
            }
            return null;
        }
    }
}
