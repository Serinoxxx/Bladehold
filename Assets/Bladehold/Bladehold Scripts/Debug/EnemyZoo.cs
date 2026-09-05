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

    private class DummyEntry
    {
        public EnemyDefinition def;
        public GameObject instance;
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
    private readonly List<DummyEntry> gallery = new List<DummyEntry>();
    private readonly List<ExtraSpawn> extraSpawns = new List<ExtraSpawn>();

    private bool anyError;
    private bool guiVisible = true;
    private int pickerIndex;
    private int batchSize = 1;
    private string batchText = "1";

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

        BuildGallery();

        // Handle player death cleanly in the test scene
        if (Player.Instance != null && Player.Instance.Health != null)
        {
            Player.Instance.Health.TryPreventDeath += HandlePlayerDeath;
        }
    }

    private void OnDestroy()
    {
        if (Player.Instance != null && Player.Instance.Health != null)
        {
            Player.Instance.Health.TryPreventDeath -= HandlePlayerDeath;
        }
        ClearGallery();
        ClearExtraSpawns();
    }

    private bool HandlePlayerDeath()
    {
        // Cleanly revive the player to max health
        Player.Instance.Health.Revive(Player.Instance.Health.MaxHealth);
        Debug.Log("[EnemyZoo] Player died. Reviving to full health and clearing live enemies.");

        // Clear all spawned enemies so they don't immediately kill the player again
        ClearExtraSpawns();

        // Prevent actual death sequence
        return true;
    }

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

            GameObject instance = SpawnGalleryDummy(spawnables[i], slot, facing);
            if (instance == null)
            {
                continue;
            }

            GameObject nameplateObj = CreateNameplate(spawnables[i].def, slot);

            var entry = new DummyEntry
            {
                def = spawnables[i].def,
                instance = instance,
                nameplate = nameplateObj,
                nameplateTmp = nameplateObj != null ? nameplateObj.GetComponent<TextMeshPro>() : null,
            };
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

    private GameObject SpawnGalleryDummy(Spawnable spawnable, Vector3 position, Quaternion rotation)
    {
        Vector3 snapped = SnapToNavMesh(position);
        GameObject instance = Instantiate(spawnable.prefab, snapped, rotation);
        
        if (applyRosterOverrides)
        {
            WaveSpawner.ApplyDefinition(instance, spawnable.def);
        }

        // Strip everything that makes it alive
        var agent = instance.GetComponent<NavMeshAgent>();
        if (agent != null) Destroy(agent);

        var rb = instance.GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);

        foreach (var mb in instance.GetComponentsInChildren<MonoBehaviour>(true))
        {
            mb.enabled = false;
        }

        // Make it interactable to spawn the real enemy
        var interactable = instance.AddComponent<Interactable>();
        interactable.PromptText = $"Spawn {spawnable.def.displayName}";
        interactable.OnInteractedEvent += (p) => 
        {
            // Spawn a live instance slightly in front of the dummy
            Vector3 spawnPos = instance.transform.position + instance.transform.forward * 2f;
            SpawnLiveEnemy(spawnable, spawnPos, Quaternion.identity);
        };

        return instance;
    }

    private void SpawnLiveEnemy(Spawnable spawnable, Vector3 position, Quaternion rotation)
    {
        Vector3 snapped = SnapToNavMesh(position);
        GameObject instance = Instantiate(spawnable.prefab, snapped, rotation);
        if (applyRosterOverrides)
        {
            WaveSpawner.ApplyDefinition(instance, spawnable.def);
        }
        extraSpawns.Add(new ExtraSpawn { id = spawnable.def.id, instance = instance });
    }

    private Vector3 SnapToNavMesh(Vector3 p)
    {
        return NavMesh.SamplePosition(p, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas) ? hit.position : p;
    }

    // ---- Controls --------------------------------------------------------

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
            SpawnLiveEnemy(pick, origin + offset, Quaternion.identity);
        }
    }

    private void ClearGallery()
    {
        foreach (DummyEntry entry in gallery)
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
        // Must clean up any dead items that might have been destroyed by the player naturally
        extraSpawns.RemoveAll(s => s.instance == null);

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

    public bool IsReady => !anyError && spawnables.Count > 0;

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
        foreach (DummyEntry entry in gallery)
        {
            if (entry.def.id == def.id && entry.instance != null)
            {
                entry.def = def;
                // Don't apply live to dummy because the dummy might reactivate scripts?
                // ApplyDefinitionLive doesn't enable scripts, it just sets stats.
                WaveSpawner.ApplyDefinitionLive(entry.instance, def);
                if (entry.nameplateTmp != null)
                {
                    entry.nameplateTmp.text = string.IsNullOrEmpty(def.displayName) ? def.id : def.displayName;
                }
                updated++;
            }
        }

        extraSpawns.RemoveAll(s => s.instance == null);
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

    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f3Key.wasPressedThisFrame)
        {
            guiVisible = !guiVisible;
        }
    }

    private void OnGUI()
    {
        if (anyError || !guiVisible)
        {
            return;
        }
        DrawPanel();
    }

    private void DrawPanel()
    {
        float x = Screen.width - PanelWidth - Padding;
        GUILayout.BeginArea(new Rect(x, Padding, PanelWidth, Screen.height - 2f * Padding), GUI.skin.box);
        GUILayout.Label("Enemy Zoo (Press F3 to hide)", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });

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
        if (GUILayout.Button($"Spawn {batchSize}x {pickName}", GUILayout.Height(ButtonHeight)))
        {
            SpawnBatch();
        }
        
        extraSpawns.RemoveAll(s => s.instance == null);
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
}
#endif
