using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>
///     Editor-time generator for enemy prefab variants (Bladehold > Generate Enemy Prefabs). For each
///     <see cref="EnemyManifest.EnemySpec" /> it builds a prefab *variant* of the goblin base — the
///     same structure the hand-built Goblin Brute / Storm Witch / Troll variants use — creates any
///     missing per-enemy ScriptableObject assets, wires component references, and registers the
///     id → prefab mapping in the shared <see cref="EnemyPrefabMapSO" /> asset (no scene edits).
///
///     Idempotent: re-running updates existing variants in place (preserving their variant link to the
///     base), re-applies structure and wiring, and never overwrites an existing SO asset — designer
///     tuning survives. Hand-built variants are untouched; the generator only owns manifest entries.
///
///     Headless entry point: <c>Unity.exe -batchmode -quit -projectPath . -executeMethod
///     EnemyPrefabGenerator.GenerateAll</c> (the Editor must be closed). Hard failures throw, so a
///     batchmode run exits non-zero.
///
///     What this can NOT do (stays manual, tracked via TODO.md): animator controller states/triggers,
///     MMF feedback tuning, new visual prefabs/VFX, materials/models (it only *references* existing
///     assets), NavMesh, and the balance numbers in <c>Config/Enemies.csv</c>.
/// </summary>
public static class EnemyPrefabGenerator
{
    private const string BasePrefabPath = "Assets/Bladehold/Bladehold Prefabs/Goblin Enemy (Base).prefab";
    private const string PrefabFolder = "Assets/Bladehold/Bladehold Prefabs";
    private const string EnemiesFolder = "Assets/Bladehold/Bladehold Scripts/Enemies";
    private const string MapAssetPath = EnemiesFolder + "/EnemyPrefabMap.asset";
    private const string RosterAssetPath = EnemiesFolder + "/EnemyRosterSO.asset";

    /// <summary>Per-spec context handed to the manifest's wiring lambdas.</summary>
    public class GenContext
    {
        public GameObject Root;
        /// <summary>The rig Animator — Synty rigs keep it on a child, so components that need it
        /// can't rely on GetComponent.</summary>
        public Animator ChildAnimator;
        public Health Health;
        public AIMovement Movement;

        internal Dictionary<string, ScriptableObject> CreatedAssets;

        /// <summary>Direct child of the root by name, created (inheriting the root's layer) when missing.</summary>
        public GameObject FindOrCreateChild(string name, Vector3 localPosition)
        {
            Transform existing = Root.transform.Find(name);
            if (existing != null)
            {
                existing.localPosition = localPosition;
                return existing.gameObject;
            }
            var child = new GameObject(name) { layer = Root.layer };
            child.transform.SetParent(Root.transform, false);
            child.transform.localPosition = localPosition;
            return child;
        }

        /// <summary>An SO asset declared in this spec's <c>assets</c> list, by asset name.</summary>
        public ScriptableObject LoadedAsset(string assetName)
        {
            if (!CreatedAssets.TryGetValue(assetName, out ScriptableObject asset))
            {
                throw new InvalidOperationException(
                    $"Manifest wiring asked for asset '{assetName}' but the spec declares no SoSpec with that name.");
            }
            return asset;
        }
    }

    [MenuItem("Bladehold/Generate Enemy Prefabs")]
    private static void GenerateFromMenu()
    {
        GenerateAll();
    }

    /// <summary>Builds/updates every manifest enemy. Public + parameterless for -executeMethod.</summary>
    public static void GenerateAll()
    {
        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath);
        if (basePrefab == null)
        {
            throw new InvalidOperationException($"Goblin base prefab not found at '{BasePrefabPath}'.");
        }

        int created = 0, updated = 0;
        foreach (EnemyManifest.EnemySpec spec in EnemyManifest.Entries)
        {
            if (Generate(spec, basePrefab))
            {
                created++;
            }
            else
            {
                updated++;
            }
        }

        AssetDatabase.SaveAssets();
        WarnOnRosterMismatches();
        Debug.Log($"EnemyPrefabGenerator: {created} variant(s) created, {updated} updated, map at '{MapAssetPath}'.");
    }

    /// <summary>Builds or re-syncs one variant. Returns true when the prefab was newly created.</summary>
    private static bool Generate(EnemyManifest.EnemySpec spec, GameObject basePrefab)
    {
        if (string.IsNullOrEmpty(spec.id) || string.IsNullOrEmpty(spec.prefabName))
        {
            throw new InvalidOperationException("Manifest entry is missing its id or prefabName.");
        }

        Dictionary<string, ScriptableObject> assets = EnsureSoAssets(spec);

        string variantPath = $"{PrefabFolder}/{spec.prefabName}.prefab";
        bool isNew = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath) == null;

        // A fresh variant comes from instantiating the base as a prefab *instance* (that link is what
        // makes SaveAsPrefabAsset produce a variant). An existing variant is edited in isolation via
        // LoadPrefabContents, which preserves its parent linkage on save.
        GameObject root = isNew
            ? (GameObject)PrefabUtility.InstantiatePrefab(basePrefab)
            : PrefabUtility.LoadPrefabContents(variantPath);
        try
        {
            Apply(root, spec, assets);
            PrefabUtility.SaveAsPrefabAsset(root, variantPath, out bool success);
            if (!success)
            {
                throw new InvalidOperationException($"Failed to save prefab variant '{variantPath}'.");
            }
        }
        finally
        {
            if (isNew)
            {
                Object.DestroyImmediate(root);
            }
            else
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        RegisterInMap(spec.id, variantPath);
        return isNew;
    }

    /// <summary>Creates the spec's missing SO assets under <c>Enemies/&lt;soFolder&gt;/</c>; existing
    /// assets are loaded untouched (designer tuning survives re-runs).</summary>
    private static Dictionary<string, ScriptableObject> EnsureSoAssets(EnemyManifest.EnemySpec spec)
    {
        var result = new Dictionary<string, ScriptableObject>();
        if (spec.assets == null || spec.assets.Length == 0)
        {
            return result;
        }
        if (string.IsNullOrEmpty(spec.soFolder))
        {
            throw new InvalidOperationException($"Manifest entry '{spec.id}' declares SO assets but no soFolder.");
        }

        string folder = $"{EnemiesFolder}/{spec.soFolder}";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder(EnemiesFolder, spec.soFolder);
        }

        foreach (EnemyManifest.SoSpec soSpec in spec.assets)
        {
            string path = $"{folder}/{soSpec.assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (existing != null)
            {
                if (existing.GetType() != soSpec.soType)
                {
                    throw new InvalidOperationException(
                        $"Asset '{path}' already exists but is a {existing.GetType().Name}, not the manifest's {soSpec.soType.Name}.");
                }
                result[soSpec.assetName] = existing;
                continue;
            }

            var asset = ScriptableObject.CreateInstance(soSpec.soType);
            soSpec.initDefaults?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            result[soSpec.assetName] = asset;
        }
        return result;
    }

    /// <summary>Applies a spec to the open variant root. Every operation is find-or-create /
    /// set-always, so re-runs converge instead of duplicating.</summary>
    private static void Apply(GameObject root, EnemyManifest.EnemySpec spec, Dictionary<string, ScriptableObject> assets)
    {
        root.name = spec.prefabName;
        root.transform.localScale = Vector3.one * spec.rootScale;

        // A model swapped in by the Enemy Manager wins over the manifest material: the manifest's
        // materialPath targets the goblin base mesh, which the swap disabled, and re-applying it to
        // whatever GetComponentInChildren finds first would paint the wrong renderer.
        if (!string.IsNullOrEmpty(spec.materialPath) && root.GetComponentInChildren<ModelSwapRecord>(true) != null)
        {
            Debug.Log($"EnemyPrefabGenerator: '{spec.id}' has a swapped model (ModelSwapRecord); skipping the manifest material apply.");
        }
        else if (!string.IsNullOrEmpty(spec.materialPath))
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(spec.materialPath);
            if (material == null)
            {
                throw new InvalidOperationException(
                    $"Manifest entry '{spec.id}' references material '{spec.materialPath}', which doesn't exist. The generator never creates materials.");
            }
            var body = root.GetComponentInChildren<SkinnedMeshRenderer>();
            if (body == null)
            {
                throw new InvalidOperationException($"No SkinnedMeshRenderer found under '{spec.prefabName}' to apply the material to.");
            }
            Material[] materials = body.sharedMaterials;
            materials[0] = material;
            body.sharedMaterials = materials;
        }

        if (!string.IsNullOrEmpty(spec.animatorOverridePath))
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(spec.animatorOverridePath);
            if (controller == null)
            {
                throw new InvalidOperationException($"Manifest entry '{spec.id}' references animator override '{spec.animatorOverridePath}', which doesn't exist.");
            }
            var animator = root.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                throw new InvalidOperationException($"No Animator found under '{spec.prefabName}' to apply the override to.");
            }
            animator.runtimeAnimatorController = controller;
        }

        if (spec.disableBaseAIAttack)
        {
            var baseAttack = root.GetComponent<AIAttack>();
            if (baseAttack != null)
            {
                baseAttack.enabled = false;
            }
        }

        if (spec.removeComponents != null)
        {
            foreach (Type type in spec.removeComponents)
            {
                Component component = root.GetComponent(type);
                if (component != null)
                {
                    Object.DestroyImmediate(component, true);
                }
            }
        }

        var context = new GenContext
        {
            Root = root,
            ChildAnimator = root.GetComponentInChildren<Animator>(),
            Health = root.GetComponent<Health>(),
            Movement = root.GetComponent<AIMovement>(),
            CreatedAssets = assets,
        };

        if (spec.children != null)
        {
            foreach (EnemyManifest.ChildSpec child in spec.children)
            {
                context.FindOrCreateChild(child.name, child.localPosition);
            }
        }

        if (spec.components != null)
        {
            foreach (EnemyManifest.ComponentSpec componentSpec in spec.components)
            {
                Component component = root.GetComponent(componentSpec.type) ?? root.AddComponent(componentSpec.type);
                if (componentSpec.wire != null)
                {
                    var serialized = new SerializedObject(component);
                    componentSpec.wire(serialized, context);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        if (spec.navStoppingDistance >= 0f)
        {
            var agent = root.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.stoppingDistance = spec.navStoppingDistance;
            }
        }
    }

    /// <summary>Sets a serialized reference field, failing loudly when the field doesn't exist — a
    /// silent miss (e.g. after a field rename) would leave a null reference the component only
    /// reports at runtime.</summary>
    public static void SetReference(SerializedObject serialized, string fieldName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(fieldName);
        if (property == null)
        {
            throw new InvalidOperationException(
                $"{serialized.targetObject.GetType().Name} has no serialized field '{fieldName}' — renamed?");
        }
        property.objectReferenceValue = value;
    }

    /// <summary>Adds or refreshes the id → prefab entry in the shared map asset, creating the asset
    /// on first use. Writes through SerializedObject since the entry list is private.</summary>
    private static void RegisterInMap(string id, string variantPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
        var map = AssetDatabase.LoadAssetAtPath<EnemyPrefabMapSO>(MapAssetPath);
        if (map == null)
        {
            map = ScriptableObject.CreateInstance<EnemyPrefabMapSO>();
            AssetDatabase.CreateAsset(map, MapAssetPath);
            Debug.LogWarning($"EnemyPrefabGenerator: created '{MapAssetPath}' — assign it on the WaveSpawner and EnemyZoo (see TODO.md) and add the existing hand-built mappings.");
        }

        var serialized = new SerializedObject(map);
        SerializedProperty entries = serialized.FindProperty("entries");
        if (entries == null)
        {
            throw new InvalidOperationException("EnemyPrefabMapSO has no serialized 'entries' list — renamed?");
        }

        int index = -1;
        for (int i = 0; i < entries.arraySize; i++)
        {
            if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == id)
            {
                index = i;
                break;
            }
        }
        if (index < 0)
        {
            index = entries.arraySize;
            entries.InsertArrayElementAtIndex(index);
            entries.GetArrayElementAtIndex(index).FindPropertyRelative("id").stringValue = id;
        }
        entries.GetArrayElementAtIndex(index).FindPropertyRelative("prefab").objectReferenceValue = prefab;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(map);
    }

    /// <summary>Warns on manifest ids with no roster CSV row (they can never spawn in waves) and on
    /// roster rows the map has no prefab for (authored ahead of their prefab — expected, but worth a
    /// reminder). Warnings only: neither blocks generation.</summary>
    private static void WarnOnRosterMismatches()
    {
        var roster = AssetDatabase.LoadAssetAtPath<EnemyRosterSO>(RosterAssetPath);
        if (roster == null)
        {
            Debug.LogWarning($"EnemyPrefabGenerator: no roster asset at '{RosterAssetPath}'; skipping CSV cross-check.");
            return;
        }

        var rosterIds = new HashSet<string>();
        foreach (EnemyDefinition def in roster.Enemies)
        {
            rosterIds.Add(def.id);
        }

        foreach (EnemyManifest.EnemySpec spec in EnemyManifest.Entries)
        {
            if (!rosterIds.Contains(spec.id))
            {
                Debug.LogWarning($"EnemyPrefabGenerator: manifest id '{spec.id}' has no row in Enemies.csv — the prefab exists but waves will never spawn it.");
            }
        }

        var map = AssetDatabase.LoadAssetAtPath<EnemyPrefabMapSO>(MapAssetPath);
        if (map == null)
        {
            return;
        }
        foreach (string id in rosterIds)
        {
            if (map.FindPrefab(id) == null)
            {
                Debug.LogWarning($"EnemyPrefabGenerator: roster row '{id}' has no prefab in the enemy prefab map yet; that type won't spawn.");
            }
        }
    }
}
