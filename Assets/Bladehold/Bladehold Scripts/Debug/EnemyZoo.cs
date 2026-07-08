#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Config/test-only harness (never part of the shipping game — guarded to Editor/dev builds and
///     lives in its own scene that isn't added to Build Profiles). Spawns one of every enemy type in
///     the <see cref="EnemyRosterSO" /> in a labelled gallery grid so they can be inspected side by
///     side, toggled between a frozen "display" state and a live "battle" state (chase/attack the
///     player), and stress-tested by spawning batches of a picked type on demand.
///
///     Roster-faithful: each gallery enemy gets the same CSV overrides <see cref="WaveSpawner" />
///     applies at spawn (via the shared <see cref="WaveSpawner.ApplyDefinition" />), so what you see
///     matches what the waves actually spawn. Controls are an IMGUI panel in the same wiring-free
///     idiom as <see cref="DevConsole" /> (no canvas/prefab setup needed).
///
///     Scene requirements (done in the Editor — see TODO.md): a baked NavMesh, a Player prefab
///     instance (battle mode needs <see cref="Player.Instance" />), a camera, and this component's
///     roster + prefab map wired in the inspector.
/// </summary>
public class EnemyZoo : MonoBehaviour
{
    /// <summary>Inspector mapping from a roster CSV id to its prefab (mirrors the WaveSpawner map).</summary>
    [Serializable]
    private class EnemyPrefabEntry
    {
        public string id;
        public GameObject prefab;
    }

    [Header("Roster")]
    [SerializeField] private EnemyRosterSO roster;
    [Tooltip("Maps each roster CSV id to its prefab. Rows without a mapping are skipped (with a warning).")]
    [SerializeField] private EnemyPrefabEntry[] enemyPrefabs;
    [Tooltip("Apply each row's CSV stat overrides (health/damage/scale/…) so the gallery matches what waves spawn.")]
    [SerializeField] private bool applyRosterOverrides = true;

    [Header("Gallery layout")]
    [Tooltip("World-space anchor for the first (bottom-left) gallery slot.")]
    [SerializeField] private Vector3 galleryOrigin = Vector3.zero;
    [SerializeField] private float columnSpacing = 3f;
    [SerializeField] private float rowSpacing = 3f;
    [SerializeField] private int columns = 5;
    [Tooltip("Max distance a grid slot is snapped onto the baked NavMesh.")]
    [SerializeField] private float navSampleRadius = 4f;
    [Tooltip("Height above each enemy at which its name/health label is drawn.")]
    [SerializeField] private float labelHeight = 2.2f;

    [Header("On-demand spawns")]
    [Tooltip("Where picker-spawned enemies appear. Defaults to this object's position if unset.")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float spawnScatter = 2f;

    // One spawnable roster type paired with its prefab.
    private struct Spawnable
    {
        public EnemyDefinition def;
        public GameObject prefab;
    }

    // A live gallery member and the AI drivers battle mode toggles.
    private class ZooEntry
    {
        public EnemyDefinition def;
        public GameObject instance;
        public Health health;
        // Movement is paused via SetMovementPaused (which stops the agent) rather than by disabling
        // the component, since a disabled AIMovement leaves its NavMeshAgent following its last path.
        public AIMovement movement;
        // Attack behaviours are frozen by disabling them (their Start defers until re-enabled, so a
        // frozen gallery enemy never even resolves Player.Instance).
        public Behaviour[] attacks;
    }

    private readonly List<Spawnable> spawnables = new List<Spawnable>();
    private readonly List<ZooEntry> gallery = new List<ZooEntry>();
    private readonly List<GameObject> extraSpawns = new List<GameObject>();

    private bool battleMode;
    private bool anyError;
    private bool guiVisible = true;
    private int pickerIndex;
    private int batchSize = 1;
    private string batchText = "1";
    private Camera cam;

    private const float PanelWidth = 240f;
    private const float Padding = 10f;
    private const float ButtonHeight = 30f;

    private void Start()
    {
        if (roster == null)
        {
            Debug.LogError("EnemyZoo: no EnemyRosterSO assigned.");
            anyError = true;
            return;
        }

        BuildSpawnables();
        if (spawnables.Count == 0)
        {
            Debug.LogError("EnemyZoo: no roster types have a valid prefab mapping; nothing to show.");
            anyError = true;
            return;
        }

        cam = Camera.main;
        BuildGallery();
    }

    private void OnDestroy()
    {
        ClearGallery();
        ClearExtraSpawns();
    }

    /// <summary>Pairs each roster row with its inspector-mapped prefab, mirroring WaveSpawner's wiring.</summary>
    private void BuildSpawnables()
    {
        foreach (EnemyDefinition def in roster.Enemies)
        {
            GameObject prefab = FindPrefab(def.id);
            if (prefab == null)
            {
                Debug.LogWarning($"EnemyZoo: roster row '{def.id}' has no prefab mapping; skipping.");
                continue;
            }
            if (prefab.GetComponent<Health>() == null)
            {
                Debug.LogError($"EnemyZoo: prefab for '{def.id}' has no Health component; skipping.");
                continue;
            }
            spawnables.Add(new Spawnable { def = def, prefab = prefab });
        }
    }

    private GameObject FindPrefab(string id)
    {
        if (enemyPrefabs == null)
        {
            return null;
        }
        foreach (EnemyPrefabEntry entry in enemyPrefabs)
        {
            if (entry != null && entry.id == id)
            {
                return entry.prefab;
            }
        }
        return null;
    }

    private void BuildGallery()
    {
        for (int i = 0; i < spawnables.Count; i++)
        {
            int col = i % Mathf.Max(1, columns);
            int row = i / Mathf.Max(1, columns);
            Vector3 slot = galleryOrigin + new Vector3(col * columnSpacing, 0f, row * rowSpacing);

            GameObject instance = SpawnInstance(spawnables[i], slot, FaceTowardViewer(slot));
            if (instance == null)
            {
                continue;
            }

            var entry = new ZooEntry
            {
                def = spawnables[i].def,
                instance = instance,
                health = instance.GetComponent<Health>(),
                movement = instance.GetComponent<AIMovement>(),
                attacks = CollectAttacks(instance),
            };
            // Gallery enemies start frozen (AI disabled before its Start runs → no chase, no
            // Player.Instance dependency) until battle mode is switched on.
            SetEntryBattle(entry, battleMode);
            gallery.Add(entry);
        }
    }

    /// <summary>Instantiates a roster-faithful enemy, snapped onto the NavMesh.</summary>
    private GameObject SpawnInstance(Spawnable spawnable, Vector3 position, Quaternion rotation)
    {
        Vector3 snapped = SnapToNavMesh(position);
        GameObject instance = Instantiate(spawnable.prefab, snapped, rotation);
        if (applyRosterOverrides)
        {
            // Same single source of truth the waves use, applied before the instance's Start runs.
            WaveSpawner.ApplyDefinition(instance, spawnable.def);
        }
        return instance;
    }

    /// <summary>Every "*Attack" behaviour on the instance (AIAttack, LightningBallAttack, …).</summary>
    private static Behaviour[] CollectAttacks(GameObject instance)
    {
        var list = new List<Behaviour>();
        foreach (MonoBehaviour mb in instance.GetComponents<MonoBehaviour>())
        {
            if (mb != null && mb.GetType().Name.EndsWith("Attack", StringComparison.Ordinal))
            {
                list.Add(mb);
            }
        }
        return list.ToArray();
    }

    private static void SetEntryBattle(ZooEntry entry, bool on)
    {
        if (entry.movement != null)
        {
            entry.movement.SetMovementPaused(!on);
        }
        if (entry.attacks != null)
        {
            foreach (Behaviour attack in entry.attacks)
            {
                if (attack != null)
                {
                    attack.enabled = on;
                }
            }
        }
    }

    private Vector3 SnapToNavMesh(Vector3 p)
    {
        return NavMesh.SamplePosition(p, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas) ? hit.position : p;
    }

    private Quaternion FaceTowardViewer(Vector3 fromPosition)
    {
        Vector3 target = spawnPoint != null ? spawnPoint.position : transform.position;
        Vector3 dir = target - fromPosition;
        dir.y = 0f;
        return dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir) : Quaternion.identity;
    }

    // ---- Controls --------------------------------------------------------

    private void ToggleBattle()
    {
        battleMode = !battleMode;
        foreach (ZooEntry entry in gallery)
        {
            SetEntryBattle(entry, battleMode);
        }
    }

    private void RespawnGallery()
    {
        ClearGallery();
        BuildGallery();
    }

    private void SpawnBatch()
    {
        if (spawnables.Count == 0)
        {
            return;
        }
        pickerIndex = Mathf.Clamp(pickerIndex, 0, spawnables.Count - 1);
        Spawnable pick = spawnables[pickerIndex];
        Vector3 origin = spawnPoint != null ? spawnPoint.position : transform.position;

        for (int i = 0; i < Mathf.Max(1, batchSize); i++)
        {
            // Ring scatter so a batch doesn't stack on one point.
            float angle = (i / (float)Mathf.Max(1, batchSize)) * Mathf.PI * 2f;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spawnScatter;
            // On-demand spawns are always live (that's the point of spawning them), so no battle toggle.
            GameObject instance = SpawnInstance(pick, origin + offset, Quaternion.identity);
            if (instance != null)
            {
                extraSpawns.Add(instance);
            }
        }
    }

    private void ClearGallery()
    {
        foreach (ZooEntry entry in gallery)
        {
            if (entry.instance != null)
            {
                Destroy(entry.instance);
            }
        }
        gallery.Clear();
    }

    private void ClearExtraSpawns()
    {
        foreach (GameObject go in extraSpawns)
        {
            if (go != null)
            {
                Destroy(go);
            }
        }
        extraSpawns.Clear();
    }

    // ---- IMGUI -----------------------------------------------------------

    private void OnGUI()
    {
        if (anyError || !guiVisible)
        {
            return;
        }
        DrawLabels();
        DrawPanel();
    }

    private void DrawLabels()
    {
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null)
            {
                return;
            }
        }

        foreach (ZooEntry entry in gallery)
        {
            if (entry.instance == null)
            {
                continue;
            }
            Vector3 world = entry.instance.transform.position + Vector3.up * labelHeight;
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z <= 0f)
            {
                continue; // behind the camera
            }

            string name = string.IsNullOrEmpty(entry.def.displayName) ? entry.def.id : entry.def.displayName;
            string hp = entry.health == null ? ""
                : entry.health.IsDead ? "  (dead)"
                : $"  {Mathf.CeilToInt(entry.health.CurrentHealth)}/{Mathf.CeilToInt(entry.health.MaxHealth)}";

            var rect = new Rect(screen.x - 90f, Screen.height - screen.y - 12f, 180f, 24f);
            GUI.Label(rect, name + hp, LabelStyle);
        }
    }

    private void DrawPanel()
    {
        float x = Screen.width - PanelWidth - Padding;
        GUILayout.BeginArea(new Rect(x, Padding, PanelWidth, Screen.height - 2f * Padding), GUI.skin.box);
        GUILayout.Label("Enemy Zoo");

        if (GUILayout.Button(battleMode ? "Battle Mode: ON (freeze)" : "Battle Mode: OFF (fight)", GUILayout.Height(ButtonHeight)))
        {
            ToggleBattle();
        }
        if (GUILayout.Button("Respawn Gallery", GUILayout.Height(ButtonHeight)))
        {
            RespawnGallery();
        }

        GUILayout.Space(8f);
        GUILayout.Label("Spawn a type");
        DrawTypePicker();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Count", GUILayout.Width(44f));
        string edited = GUILayout.TextField(batchText, GUILayout.Height(ButtonHeight));
        if (edited != batchText)
        {
            batchText = edited;
            if (int.TryParse(edited, out int typed))
            {
                batchSize = Mathf.Clamp(typed, 1, 500);
            }
        }
        GUILayout.EndHorizontal();

        Spawnable pick = spawnables[Mathf.Clamp(pickerIndex, 0, spawnables.Count - 1)];
        string pickName = string.IsNullOrEmpty(pick.def.displayName) ? pick.def.id : pick.def.displayName;
        if (GUILayout.Button($"Spawn {batchSize}× {pickName}", GUILayout.Height(ButtonHeight)))
        {
            SpawnBatch();
        }
        if (GUILayout.Button($"Clear Spawns ({extraSpawns.Count})", GUILayout.Height(ButtonHeight)))
        {
            ClearExtraSpawns();
        }

        GUILayout.EndArea();
    }

    private void DrawTypePicker()
    {
        pickerIndex = Mathf.Clamp(pickerIndex, 0, spawnables.Count - 1);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<", GUILayout.Width(32f), GUILayout.Height(ButtonHeight)))
        {
            pickerIndex = (pickerIndex - 1 + spawnables.Count) % spawnables.Count;
        }
        EnemyDefinition def = spawnables[pickerIndex].def;
        string label = string.IsNullOrEmpty(def.displayName) ? def.id : def.displayName;
        GUILayout.Label(label, GUILayout.ExpandWidth(true));
        if (GUILayout.Button(">", GUILayout.Width(32f), GUILayout.Height(ButtonHeight)))
        {
            pickerIndex = (pickerIndex + 1) % spawnables.Count;
        }
        GUILayout.EndHorizontal();
    }

    private GUIStyle labelStyle;
    private GUIStyle LabelStyle
    {
        get
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                };
            }
            return labelStyle;
        }
    }
}
#endif
