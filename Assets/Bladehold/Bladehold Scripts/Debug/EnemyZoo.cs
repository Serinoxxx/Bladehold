#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Config/test-only harness (never part of the shipping game — guarded to Editor/dev builds and
///     lives in its own scene that isn't added to Build Profiles). Spawns one of every enemy type in
///     the <see cref="EnemyRosterSO" /> in a labelled gallery lineup so they can be inspected side by
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
    [Header("Roster")]
    [SerializeField] private EnemyRosterSO roster;
    [Tooltip("The shared id → prefab map asset (the same one the WaveSpawner uses). Rows without a mapping are skipped (with a warning).")]
    [SerializeField] private EnemyPrefabMapSO prefabMap;
    [Tooltip("Apply each row's CSV stat overrides (health/damage/scale/…) so the gallery matches what waves spawn.")]
    [SerializeField] private bool applyRosterOverrides = true;

    [Header("Gallery layout")]
    [Tooltip("World-space anchor for the first (leftmost) gallery slot.")]
    [SerializeField] private Vector3 galleryOrigin = Vector3.zero;
    [Tooltip("Distance along the lineup between adjacent enemies.")]
    [SerializeField] private float lineSpacing = 3.5f;
    [Tooltip("Direction the lineup extends along in world space.")]
    [SerializeField] private Vector3 lineDirection = Vector3.right;
    [Tooltip("Direction the enemies face while in the lineup.")]
    [SerializeField] private Vector3 facingDirection = Vector3.back;
    [Tooltip("Offset in front of each enemy where the World Space TextMeshPro nameplate is placed.")]
    [SerializeField] private Vector3 nameplateOffset = new Vector3(0f, 0.15f, -1.8f);
    [Tooltip("Font size for the 3D TextMeshPro nameplate.")]
    [SerializeField] private float nameplateFontSize = 3.5f;
    [Tooltip("Tilt angle (degrees) for the nameplate so it angles up towards the camera/player.")]
    [SerializeField] private float nameplateTiltAngle = 40f;
    [Tooltip("Max distance a slot is snapped onto the baked NavMesh.")]
    [SerializeField] private float navSampleRadius = 4f;
    [Tooltip("Height above each enemy at which its IMGUI health label is drawn.")]
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
        public GameObject nameplate;
        public TextMeshPro nameplateTmp;
    }

    // An on-demand spawn tagged with its roster id, so live stat re-applies can find it.
    private struct ExtraSpawn
    {
        public string id;
        public GameObject instance;
    }

    private readonly List<Spawnable> spawnables = new List<Spawnable>();
    private readonly List<ZooEntry> gallery = new List<ZooEntry>();
    private readonly List<ExtraSpawn> extraSpawns = new List<ExtraSpawn>();

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
        if (prefabMap == null)
        {
            Debug.LogError("EnemyZoo: no EnemyPrefabMapSO assigned.");
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

    /// <summary>Pairs each roster row with its prefab from the shared map asset — the same wiring WaveSpawner uses.</summary>
    private void BuildSpawnables()
    {
        foreach (EnemyDefinition def in roster.Enemies)
        {
            GameObject prefab = prefabMap.FindPrefab(def.id);
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

    private void BuildGallery()
    {
        Vector3 dir = lineDirection.sqrMagnitude > 0.001f ? lineDirection.normalized : Vector3.right;
        Quaternion facing = facingDirection.sqrMagnitude > 0.001f ? Quaternion.LookRotation(facingDirection.normalized) : Quaternion.Euler(0f, 180f, 0f);

        for (int i = 0; i < spawnables.Count; i++)
        {
            Vector3 slot = galleryOrigin + dir * (i * lineSpacing);

            GameObject instance = SpawnInstance(spawnables[i], slot, facing);
            if (instance == null)
            {
                continue;
            }

            GameObject nameplateObj = CreateNameplate(spawnables[i].def, slot);

            var entry = new ZooEntry
            {
                def = spawnables[i].def,
                instance = instance,
                health = instance.GetComponent<Health>(),
                movement = instance.GetComponent<AIMovement>(),
                attacks = CollectAttacks(instance),
                nameplate = nameplateObj,
                nameplateTmp = nameplateObj != null ? nameplateObj.GetComponent<TextMeshPro>() : null,
            };
            // Gallery enemies start frozen (AI disabled before its Start runs → no chase, no
            // Player.Instance dependency) until battle mode is switched on.
            SetEntryBattle(entry, battleMode);
            gallery.Add(entry);
        }
    }

    private GameObject CreateNameplate(EnemyDefinition def, Vector3 enemyPosition)
    {
        GameObject labelObj = new GameObject($"{def.id}_Nameplate");
        labelObj.transform.position = enemyPosition + nameplateOffset;
        labelObj.transform.rotation = Quaternion.Euler(nameplateTiltAngle, 0f, 0f);

        TextMeshPro tmp = labelObj.AddComponent<TextMeshPro>();
        tmp.text = string.IsNullOrEmpty(def.displayName) ? def.id : def.displayName;
        tmp.fontSize = nameplateFontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.rectTransform.sizeDelta = new Vector2(8f, 2f);

        return labelObj;
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

    // ---- Controls --------------------------------------------------------

    private void ToggleBattle()
    {
        battleMode = !battleMode;
        foreach (ZooEntry entry in gallery)
        {
            SetEntryBattle(entry, battleMode);
        }
    }

    public void RespawnGallery()
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
                extraSpawns.Add(new ExtraSpawn { id = pick.def.id, instance = instance });
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
            if (entry.nameplate != null)
            {
                Destroy(entry.nameplate);
            }
        }
        gallery.Clear();
    }

    private void ClearExtraSpawns()
    {
        foreach (ExtraSpawn spawn in extraSpawns)
        {
            if (spawn.instance != null)
            {
                Destroy(spawn.instance);
            }
        }
        extraSpawns.Clear();
    }

    // ---- Editor-tool API (Enemy Manager window) ----------------------------

    /// <summary>Whether the zoo booted with a valid roster + prefab map and can take commands.</summary>
    public bool IsReady => !anyError && spawnables.Count > 0;

    public bool BattleMode => battleMode;

    public void SetBattleMode(bool on)
    {
        if (battleMode != on)
        {
            ToggleBattle();
        }
    }

    /// <summary>Points the spawn picker at a roster id. False when the id has no spawnable (no prefab mapping).</summary>
    public bool TrySelect(string id)
    {
        for (int i = 0; i < spawnables.Count; i++)
        {
            if (spawnables[i].def.id == id)
            {
                pickerIndex = i;
                return true;
            }
        }
        return false;
    }

    /// <summary>Spawns a batch of the given type at the spawn point (always live, like the panel's Spawn button).</summary>
    public bool SpawnBatchOf(string id, int count)
    {
        if (!TrySelect(id))
        {
            return false;
        }
        batchSize = Mathf.Clamp(count, 1, 500);
        batchText = batchSize.ToString();
        SpawnBatch();
        return true;
    }

    /// <summary>
    ///     Replaces a roster row's definition for this zoo session (future spawns use it) and re-applies
    ///     it to every live instance of that type via <see cref="WaveSpawner.ApplyDefinitionLive" /> —
    ///     current damage fractions survive the tweak. Returns how many live instances were updated.
    ///     The roster asset itself is untouched; saving is the Enemy Manager's explicit CSV save.
    /// </summary>
    public int ApplyLiveDefinition(EnemyDefinition def)
    {
        if (def == null || string.IsNullOrEmpty(def.id))
        {
            return 0;
        }

        for (int i = 0; i < spawnables.Count; i++)
        {
            if (spawnables[i].def.id == def.id)
            {
                spawnables[i] = new Spawnable { def = def, prefab = spawnables[i].prefab };
            }
        }

        int updated = 0;
        foreach (ZooEntry entry in gallery)
        {
            if (entry.def.id == def.id && entry.instance != null)
            {
                entry.def = def;
                WaveSpawner.ApplyDefinitionLive(entry.instance, def);
                if (entry.nameplateTmp != null)
                {
                    entry.nameplateTmp.text = string.IsNullOrEmpty(def.displayName) ? def.id : def.displayName;
                }
                updated++;
            }
        }
        foreach (ExtraSpawn spawn in extraSpawns)
        {
            if (spawn.id == def.id && spawn.instance != null)
            {
                WaveSpawner.ApplyDefinitionLive(spawn.instance, def);
                updated++;
            }
        }
        return updated;
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
