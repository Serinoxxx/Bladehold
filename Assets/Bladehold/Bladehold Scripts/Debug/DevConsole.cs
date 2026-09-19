using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
///     In-game developer cheat console, toggled with the backquote/tilde key. Draws an IMGUI panel of
///     cheat buttons (add gold, advance wave, …); extend it by adding buttons in <see cref="DrawButtons" />.
///     It bootstraps itself when play starts, so it needs no scene
///     object and survives the death screen's scene reloads.
/// </summary>
public class DevConsole : MonoBehaviour
{
    public static DevConsole Instance { get; private set; }
    public static bool IsVisible => Instance != null && Instance.visible;

    private const float PanelWidth = 400f;
    private const float SkillsPanelWidth = 360f;
    private const float Padding = 10f;
    private const float ButtonHeight = 30f;
    private const string NextWaveFieldName = "DevConsoleNextWave";

    private bool visible;
    private string nextWaveText = "";
    private int spawnTypeIndex;
    private int objectiveIndex;
    private bool isGodMode;
    private Vector2 mainScrollPos;
    private Vector2 draftScrollPos;
    private DraftCategory draftFilter = DraftCategory.Weapon;
    private bool filterAllDrafts = true;
    private int selectedUltimateIndex = 0;
    private Health subscribedHealth;

    private struct UltimateOption
    {
        public string id;
        public string displayName;
    }

    private static readonly UltimateOption[] AvailableUltimates = new[]
    {
        new UltimateOption { id = "sword_mount_ult", displayName = "Warhorse Mount" },
        new UltimateOption { id = "axe_bladestorm_ult", displayName = "Bladestorm" },
        new UltimateOption { id = "bow_stream_ult", displayName = "Arrow Stream" },
        new UltimateOption { id = "taxe_vortex_ult", displayName = "Axe Vortex" },
        new UltimateOption { id = "mace_earthshaker_ult", displayName = "Seismic Quake" },
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        GameObject consoleObject = new GameObject("DevConsole");
        consoleObject.AddComponent<DevConsole>();
        DontDestroyOnLoad(consoleObject);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    GameObject hud;
    private void Start()
    {
        hud = GameObject.FindGameObjectWithTag("HUD");
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        // The project is new-Input-System-only, so read the key via Keyboard rather than legacy Input.
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[Key.Backquote].wasPressedThisFrame)
        {
            SetVisible(!visible);
        }

        UpdateGodModeSubscription();
    }

    public void SetVisible(bool value)
    {
        if (visible == value)
        {
            return;
        }

        visible = value;
        CursorLockManager.SetUnlock("DevConsole", visible);
    }

    private void OnDisable()
    {
        if (subscribedHealth != null)
        {
            subscribedHealth.TryBlockDamage -= OnTryBlockDamage;
            subscribedHealth = null;
        }
    }

    private void UpdateGodModeSubscription()
    {
        Health currentHealth = Player.Instance != null ? Player.Instance.Health : null;
        if (subscribedHealth != currentHealth)
        {
            if (subscribedHealth != null)
            {
                subscribedHealth.TryBlockDamage -= OnTryBlockDamage;
            }
            subscribedHealth = currentHealth;
            if (subscribedHealth != null)
            {
                subscribedHealth.TryBlockDamage += OnTryBlockDamage;
            }
        }
    }

    private bool OnTryBlockDamage(Damage damage)
    {
        return isGodMode;
    }

    private void OnGUI()
    {
        if (!visible)
        {
            return;
        }

        GUILayout.BeginArea(new Rect(Padding, Padding, PanelWidth, Screen.height - 2f * Padding), GUI.skin.box);
        GUILayout.Label("Dev Console", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
        mainScrollPos = GUILayout.BeginScrollView(mainScrollPos, false, false);
        DrawButtons();
        GUILayout.EndScrollView();
        GUILayout.EndArea();

        DrawRightPanel();
    }

    private void DrawRightPanel()
    {
        float x = Padding + PanelWidth + Padding;
        float height = Screen.height - 2f * Padding;

        GUILayout.BeginArea(new Rect(x, Padding, SkillsPanelWidth, height), GUI.skin.box);
        DrawDraftAbilitiesTab();
        GUILayout.EndArea();
    }

    private void DrawDraftAbilitiesTab()
    {
        DraftUpgradeService service = DraftUpgradeService.GetOrCreateInstance();
        if (service == null || service.AllDefinitions == null)
        {
            GUILayout.Label("DraftUpgradeService not ready.");
            return;
        }

        var allDrafts = service.AllDefinitions;
        GUILayout.Label($"Draft Abilities ({allDrafts.Count})");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Max All", GUILayout.Height(ButtonHeight)))
        {
            service.DebugMaxAllDrafts(true);
        }
        if (GUILayout.Button("Reset All", GUILayout.Height(ButtonHeight)))
        {
            service.DebugResetAllDrafts();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(4f);
        GUILayout.Label("Filter Category:");
        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(filterAllDrafts, "All", GUI.skin.button, GUILayout.Height(24f)))
        {
            filterAllDrafts = true;
        }
        if (GUILayout.Toggle(!filterAllDrafts && draftFilter == DraftCategory.Weapon, "Wep", GUI.skin.button, GUILayout.Height(24f)))
        {
            filterAllDrafts = false;
            draftFilter = DraftCategory.Weapon;
        }
        if (GUILayout.Toggle(!filterAllDrafts && draftFilter == DraftCategory.Elemental, "Elem", GUI.skin.button, GUILayout.Height(24f)))
        {
            filterAllDrafts = false;
            draftFilter = DraftCategory.Elemental;
        }
        if (GUILayout.Toggle(!filterAllDrafts && draftFilter == DraftCategory.Fortress, "Fort", GUI.skin.button, GUILayout.Height(24f)))
        {
            filterAllDrafts = false;
            draftFilter = DraftCategory.Fortress;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(4f);
        draftScrollPos = GUILayout.BeginScrollView(draftScrollPos, false, true);

        foreach (DraftUpgradeDefinition def in allDrafts)
        {
            if (def == null) continue;
            if (!filterAllDrafts && def.category != draftFilter) continue;

            int curLevel = RunSession.GetUpgradeLevel(def.id);
            string name = string.IsNullOrEmpty(def.displayName) ? def.id : def.displayName;
            string badge = def.isUltimate ? " [ULT]" : "";
            string label = $"{name}{badge} [{curLevel}/{def.maxLevel}]";

            GUILayout.BeginHorizontal();

            GUI.enabled = curLevel > 0;
            if (GUILayout.Button("<", GUILayout.Width(28f), GUILayout.Height(24f)))
            {
                service.DebugSetDraftLevel(def, curLevel - 1);
            }

            GUI.enabled = true;
            GUILayout.Label(label, GUILayout.ExpandWidth(true));

            GUI.enabled = curLevel < def.maxLevel;
            if (GUILayout.Button(">", GUILayout.Width(28f), GUILayout.Height(24f)))
            {
                service.DebugSetDraftLevel(def, curLevel + 1);
            }
            GUI.enabled = true;

            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();
    }

    private void DrawButtons()
    {
        string godModeText = isGodMode ? "God Mode: ON" : "God Mode: OFF";
        if (GUILayout.Button(godModeText, GUILayout.Height(ButtonHeight)))
        {
            isGodMode = !isGodMode;
        }

        if (GUILayout.Button("+10,000 Gold", GUILayout.Height(ButtonHeight)))
        {
            // Singletons are re-created on scene reload, so resolve them per click rather than caching.
            Wallet wallet = Player.Instance != null ? Player.Instance.Wallet : null;
            if (wallet != null)
            {
                wallet.Add(10000);
            }
        }

        if (GUILayout.Button("Die", GUILayout.Height(ButtonHeight)))
        {
            isGodMode = false;
            if (Player.Instance != null && Player.Instance.TryGetComponent(out Health health))
            {
                health.ReceiveDamage(new Damage { value = 999999f, unparryable = true });
            }
        }

        DrawScenarioControls();
        DrawWeaponControls();
        DrawElementalChargeControls();
        DrawArmourControls();
        DrawUltimateControls();
        DrawDraftControls();
        DrawSceneControls();

        DrawWaveControls();
        DrawObjectiveControls();
        DrawEnemySpawnControls();
        DrawLanguageControls();
        DrawRageReadout();
        DrawImbuementReadout();

        // Perf stress tests: burst-spawn into the current wave, ignoring the concurrent cap.
        GUILayout.Label("Spawn Goblins (stress test)");
        GUILayout.BeginHorizontal();
        DrawSpawnBurstButton(50);
        DrawSpawnBurstButton(100);
        DrawSpawnBurstButton(300);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Wipe Progress & Reload", GUILayout.Height(ButtonHeight)))
        {
            // Wipe only the progress half of the save (gold, both skill trees, Reincarnate points) —
            // settings survive, same as the settings menu's Delete Save. Save() also updates
            // SaveSystem's in-memory cache, so the reloaded scene's Wallet/tree services Load() the
            // wiped-progress-but-same-settings data.
            SaveData data = SaveSystem.Load();
            data.ResetProgress();
            SaveSystem.Save(data);
            RunState.StartingWave = 1;
            Time.timeScale = GameSettingsService.TargetTimeScale; // ensure normal speed resumes even if something paused time on death.
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    private void DrawSceneControls()
    {
        GUILayout.Label("Load Scene");
        DrawSceneLoadButton("Bladehold Frozen Pass Scene");
        DrawSceneLoadButton("Bladehold Meta Area Scene");
        DrawSceneLoadButton("Bladehold Rest Area Scene");
        DrawSceneLoadButton("Bladehold Survivors Scene");
        DrawSceneLoadButton("Bladehold Ancient Garden");
    }

    private void DrawSceneLoadButton(string sceneName)
    {
        if (GUILayout.Button(sceneName, GUILayout.Height(ButtonHeight)))
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    private PlayerWeaponManager GetWeaponManager()
    {
        return PlayerWeaponManager.GetInstance();
    }

    private PlayerUltimateController GetUltimateController()
    {
        if (Player.Instance != null)
        {
            var ctrl = Player.Instance.transform.root.GetComponentInChildren<PlayerUltimateController>(true);
            if (ctrl != null) return ctrl;
        }
        return FindFirstObjectByType<PlayerUltimateController>();
    }

    private void DrawWeaponControls()
    {
        PlayerWeaponManager pwm = GetWeaponManager();

        if (pwm != null)
        {
            // Melee Weapon
            string meleeName = pwm.ActiveMeleeDefinition != null ? pwm.ActiveMeleeDefinition.displayName : pwm.CurrentMeleeId;
            GUILayout.Label($"Melee Weapon: {meleeName}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(36f), GUILayout.Height(ButtonHeight)))
            {
                pwm.CycleMeleeWeapon(-1);
            }
            if (GUILayout.Button("Cycle Melee", GUILayout.Height(ButtonHeight)))
            {
                pwm.CycleMeleeWeapon(1);
            }
            if (GUILayout.Button(">", GUILayout.Width(36f), GUILayout.Height(ButtonHeight)))
            {
                pwm.CycleMeleeWeapon(1);
            }
            GUILayout.EndHorizontal();

            // Ranged Weapon
            string rangedName = pwm.ActiveRangedDefinition != null ? pwm.ActiveRangedDefinition.displayName : pwm.CurrentRangedId;
            GUILayout.Label($"Ranged Weapon: {rangedName}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(36f), GUILayout.Height(ButtonHeight)))
            {
                pwm.CycleRangedWeapon(-1);
            }
            if (GUILayout.Button("Cycle Ranged", GUILayout.Height(ButtonHeight)))
            {
                pwm.CycleRangedWeapon(1);
            }
            if (GUILayout.Button(">", GUILayout.Width(36f), GUILayout.Height(ButtonHeight)))
            {
                pwm.CycleRangedWeapon(1);
            }
            GUILayout.EndHorizontal();
        }
    }

    private void DrawArmourControls()
    {
        PlayerArmourManager pam = PlayerArmourManager.Instance;
        if (pam == null && Player.Instance != null)
        {
            pam = Player.Instance.GetComponentInChildren<PlayerArmourManager>();
        }

        if (pam != null && pam.availableArmourSets != null && pam.availableArmourSets.Length > 0)
        {
            string armourName = pam.ActiveArmourSet != null ? pam.ActiveArmourSet.displayName : "Default";
            GUILayout.Label($"Armour Set: {armourName}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(36f), GUILayout.Height(ButtonHeight)))
            {
                pam.CycleArmour(-1);
            }
            if (GUILayout.Button("Cycle Armour", GUILayout.Height(ButtonHeight)))
            {
                pam.CycleArmour(1);
            }
            if (GUILayout.Button(">", GUILayout.Width(36f), GUILayout.Height(ButtonHeight)))
            {
                pam.CycleArmour(1);
            }
            GUILayout.EndHorizontal();
        }
    }

    private void DrawElementalChargeControls()
    {
        if (GetWeaponManager() == null) return;
        GUILayout.Label("Draft Weapon Charges");
        DrawElementalChargeRow("Melee", "SLOT_MELEE");
        DrawElementalChargeRow("Ranged", "SLOT_RANGED");
    }

    private void DrawElementalChargeRow(string label, string slotName)
    {
        string element = RunSession.GetElementInSlot(slotName);
        GUILayout.Label($"{label}: {(string.IsNullOrEmpty(element) ? "None" : element)}");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Fire", GUILayout.Height(ButtonHeight))) RunSession.SetElementalSlot(slotName, "FIRE");
        if (GUILayout.Button("Ice", GUILayout.Height(ButtonHeight))) RunSession.SetElementalSlot(slotName, "ICE");
        if (GUILayout.Button("Lightning", GUILayout.Height(ButtonHeight))) RunSession.SetElementalSlot(slotName, "LIGHTNING");
        GUILayout.EndHorizontal();
        if (GUILayout.Button($"Clear {label} Charge", GUILayout.Height(ButtonHeight))) RunSession.ClearElementalSlot(slotName);
    }
    public bool enableHealthbars = true;
    private void DrawUltimateControls()
    {
        selectedUltimateIndex = Mathf.Clamp(selectedUltimateIndex, 0, AvailableUltimates.Length - 1);
        UltimateOption opt = AvailableUltimates[selectedUltimateIndex];

        GUILayout.Label($"Ultimate: {opt.displayName}");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<", GUILayout.Width(36f), GUILayout.Height(ButtonHeight)))
        {
            selectedUltimateIndex = (selectedUltimateIndex - 1 + AvailableUltimates.Length) % AvailableUltimates.Length;
        }
        if (GUILayout.Button("Cycle Ult", GUILayout.Height(ButtonHeight)))
        {
            selectedUltimateIndex = (selectedUltimateIndex + 1) % AvailableUltimates.Length;
        }
        if (GUILayout.Button(">", GUILayout.Width(36f), GUILayout.Height(ButtonHeight)))
        {
            selectedUltimateIndex = (selectedUltimateIndex + 1) % AvailableUltimates.Length;
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Unlock Ult", GUILayout.Height(ButtonHeight)))
        {
            UnlockSelectedUltimate(opt.id);
        }
        if (GUILayout.Button("Fill Charge 100%", GUILayout.Height(ButtonHeight)))
        {
            FillUltimateCharge();
        }
        if (GUILayout.Button("Toggle HUD", GUILayout.Height(ButtonHeight)))
        {
            if (hud == null)
            {
                hud = GameObject.FindGameObjectWithTag("HUD");
            }

            hud.SetActive(!hud.activeInHierarchy);
        }
        if (GUILayout.Button("Toggle HealthBars", GUILayout.Height(ButtonHeight)))
        {
            enableHealthbars = !enableHealthbars;
        }
            GUILayout.EndHorizontal();
    }

    private void UnlockSelectedUltimate(string ultId)
    {
        Player player = Player.Instance;
        if (player != null && player.Stats != null)
        {
            player.Stats.SetBase(StatType.UltimateUnlocked, 1f);
            RunSession.ActiveUltimateId = ultId;
            DraftUpgradeService.ConfigureUltimateHandler(player, ultId);

            DraftUpgradeService draftService = DraftUpgradeService.GetOrCreateInstance();
            if (draftService != null)
            {
                var def = draftService.GetById(ultId);
                if (def != null)
                {
                    RunSession.SetUpgradeLevel(def.id, 1);
                }
            }
            Debug.Log($"[DevConsole] Unlocked and configured ultimate: {ultId}");
        }
    }

    private void FillUltimateCharge()
    {
        Player player = Player.Instance != null ? Player.Instance : FindFirstObjectByType<Player>();
        PlayerUltimateController ultCtrl = GetUltimateController();

        if (player != null && player.Stats != null)
        {
            player.Stats.SetBase(StatType.UltimateUnlocked, 1f);
            if (string.IsNullOrEmpty(RunSession.ActiveUltimateId))
            {
                selectedUltimateIndex = Mathf.Clamp(selectedUltimateIndex, 0, AvailableUltimates.Length - 1);
                RunSession.ActiveUltimateId = AvailableUltimates[selectedUltimateIndex].id;
            }
            DraftUpgradeService.ConfigureUltimateHandler(player, RunSession.ActiveUltimateId);
        }

        if (ultCtrl != null)
        {
            ultCtrl.SetCharge(PlayerUltimateController.MaxCharge);
            Debug.Log($"[DevConsole] Ultimate charge set to {PlayerUltimateController.MaxCharge:F0}%. Active ult: {RunSession.ActiveUltimateId}. Press Q to unleash!");
        }
        else
        {
            Debug.LogError("[DevConsole] Failed to find PlayerUltimateController in scene!");
        }
    }

    private void DrawDraftControls()
    {
        DraftUpgradeService draftService = DraftUpgradeService.GetOrCreateInstance();
        if (draftService == null) return;

        GUILayout.Label("In-Run Draft Cheats");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Max All Drafts", GUILayout.Height(ButtonHeight)))
        {
            draftService.DebugMaxAllDrafts(true);
        }
        if (GUILayout.Button("Reset Drafts", GUILayout.Height(ButtonHeight)))
        {
            draftService.DebugResetAllDrafts();
        }
        GUILayout.EndHorizontal();
    }

    /// <summary>
    ///     Wave cheats: the current wave, a "Wipe Wave" kill-everything button, and a next-wave picker
    ///     (integer field + ▲/▼). Edits apply immediately — mid-wave they take effect when the wave
    ///     clears; during the intermission they retarget the wave about to start.
    /// </summary>
    private void DrawWaveControls()
    {
        WaveSpawner spawner = WaveSpawner.Instance;
        if (spawner == null)
        {
            return;
        }

        GUILayout.Label($"Wave {spawner.CurrentWave}");
        if (GUILayout.Button("Wipe Wave", GUILayout.Height(ButtonHeight)))
        {
            spawner.DebugWipeWave();
        }

        if (GUILayout.Button(spawner.IsSpawningPaused ? "Resume Wave Spawner" : "Pause Wave Spawner", GUILayout.Height(ButtonHeight)))
        {
            spawner.DebugSetSpawningPaused(!spawner.IsSpawningPaused);
        }

        GUILayout.Label("Next Wave");
        GUILayout.BeginHorizontal();

        // While the field isn't being edited, mirror the spawner's actual next wave so it stays live;
        // while focused, leave the user's in-progress text alone (it re-syncs on blur, so a garbage
        // entry just snaps back).
        if (GUI.GetNameOfFocusedControl() != NextWaveFieldName)
        {
            nextWaveText = spawner.NextWave.ToString();
        }
        GUI.SetNextControlName(NextWaveFieldName);
        string edited = GUILayout.TextField(nextWaveText, GUILayout.Height(ButtonHeight));
        if (edited != nextWaveText)
        {
            nextWaveText = edited;
            if (int.TryParse(edited, out int typed))
            {
                spawner.DebugSetNextWave(typed);
            }
        }

        if (GUILayout.Button("▲", GUILayout.Width(36f), GUILayout.Height(ButtonHeight)))
        {
            spawner.DebugSetNextWave(spawner.NextWave + 1);
            GUI.FocusControl(null); // unfocus the field so it re-syncs to the new value
        }
        if (GUILayout.Button("▼", GUILayout.Width(36f), GUILayout.Height(ButtonHeight)))
        {
            spawner.DebugSetNextWave(spawner.NextWave - 1);
            GUI.FocusControl(null);
        }

        GUILayout.EndHorizontal();
    }

    /// <summary>
    ///     Objective cheats: display current objective, force next objective, complete objective,
    ///     or pick a specific objective from the pool to force-start.
    /// </summary>
    private void DrawObjectiveControls()
    {
        SurvivorsObjectiveManager objManager = SurvivorsObjectiveManager.Instance;
        if (objManager == null)
        {
            return;
        }

        string currentTitle = objManager.CurrentObjective != null ? objManager.CurrentObjective.Title : "None";
        GUILayout.Label($"Objective: {currentTitle}");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Next Obj", GUILayout.Height(ButtonHeight)))
        {
            objManager.DebugNextObjective();
        }
        if (GUILayout.Button("Complete Obj", GUILayout.Height(ButtonHeight)))
        {
            objManager.DebugCompleteCurrentObjective();
        }
        GUILayout.EndHorizontal();

        var pool = objManager.ObjectivePool;
        if (pool == null || pool.Count == 0)
        {
            return;
        }

        objectiveIndex = Mathf.Clamp(objectiveIndex, 0, pool.Count - 1);

        GUILayout.Label("Select Objective");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<", GUILayout.Width(36f), GUILayout.Height(ButtonHeight)))
        {
            objectiveIndex = (objectiveIndex - 1 + pool.Count) % pool.Count;
        }
        ISurvivorsObjective selected = pool[objectiveIndex];
        string title = selected != null ? selected.Title : "<null>";
        GUILayout.Label(title, GUILayout.ExpandWidth(true));
        if (GUILayout.Button(">", GUILayout.Width(36f), GUILayout.Height(ButtonHeight)))
        {
            objectiveIndex = (objectiveIndex + 1) % pool.Count;
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button($"Start '{title}'", GUILayout.Height(ButtonHeight)))
        {
            objManager.DebugStartObjective(objectiveIndex);
        }
    }

    /// <summary>
    ///     Spawn-a-specific-type cheat: a ◄/► picker over <see cref="WaveSpawner.DebugSpawnableTypes" />
    ///     (all roster ids with a prefab mapping) plus a "Spawn" button that instantly places one at a
    ///     random spawn point via <see cref="WaveSpawner.DebugSpawnEnemyType" />.
    /// </summary>
    private void DrawEnemySpawnControls()
    {
        IReadOnlyList<EnemyDefinition> types = null;

        if (WaveSpawner.Instance != null)
        {
            types = WaveSpawner.Instance.DebugSpawnableTypes;
        }
        else if (SurvivorsSpawner.Instance != null)
        {
            types = SurvivorsSpawner.Instance.DebugSpawnableTypes;
        }

        if (types == null || types.Count == 0)
        {
            return;
        }
        spawnTypeIndex = Mathf.Clamp(spawnTypeIndex, 0, types.Count - 1);

        GUILayout.Label("Spawn Enemy Type");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<", GUILayout.Width(36f), GUILayout.Height(ButtonHeight)))
        {
            spawnTypeIndex = (spawnTypeIndex - 1 + types.Count) % types.Count;
        }
        EnemyDefinition selected = types[spawnTypeIndex];
        string label = string.IsNullOrEmpty(selected.displayName) ? selected.id : selected.displayName;
        GUILayout.Label(label, GUILayout.ExpandWidth(true));
        if (GUILayout.Button(">", GUILayout.Width(36f), GUILayout.Height(ButtonHeight)))
        {
            spawnTypeIndex = (spawnTypeIndex + 1) % types.Count;
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button($"Spawn {label}", GUILayout.Height(ButtonHeight)))
        {
            if (WaveSpawner.Instance != null)
            {
                WaveSpawner.Instance.DebugSpawnEnemyType(selected.id);
            }
            else if (SurvivorsSpawner.Instance != null)
            {
                SurvivorsSpawner.Instance.DebugSpawnEnemyType(selected.id);
            }
        }
    }

    // Class controls removed because the game is now classless.

    /// <summary>
    ///     Language cheats: one button per supported language plus the pseudo-locale ("XX" — wraps all
    ///     localized text as [«…»] so unconverted hardcoded strings stand out in a play-mode sweep).
    ///     Routes through GameSettingsService when present so the choice persists like the picker's;
    ///     falls back to a session-only Loc.SetLanguage without one.
    /// </summary>
    private void DrawLanguageControls()
    {
        GUILayout.Label($"Language ({Loc.Language})");
        GUILayout.BeginHorizontal();
        foreach (string code in Loc.SupportedLanguages)
        {
            if (GUILayout.Button(code.ToUpperInvariant(), GUILayout.Height(ButtonHeight)))
            {
                SetLanguage(code);
            }
        }
        if (GUILayout.Button("XX", GUILayout.Height(ButtonHeight)))
        {
            SetLanguage(Loc.PseudoLocale);
        }
        GUILayout.EndHorizontal();
    }

    private static void SetLanguage(string code)
    {
        if (GameSettingsService.Instance != null)
        {
            GameSettingsService.Instance.SetLanguage(code);
        }
        else
        {
            Loc.SetLanguage(code);
        }
    }

    /// <summary>
    ///     Live readout of the Berserker's rage meter (and any banked Pain-into-Power bonus) so the
    ///     loop is verifiable before the HUD rage bar exists. Hidden for classes without a RageBuff.
    /// </summary>
    private void DrawRageReadout()
    {
        RageBuff rage = Player.Instance != null ? Player.Instance.GetComponent<RageBuff>() : null;
        if (rage == null || !rage.isActiveAndEnabled)
        {
            return;
        }

        string line = $"Rage {rage.CurrentRage:0}/{rage.MaxRage:0}";
        PainIntoPower pain = Player.Instance.GetComponent<PainIntoPower>();
        if (pain != null && pain.isActiveAndEnabled && pain.StoredBonus > 0f)
        {
            line += $"  |  Pain +{pain.StoredBonus:0.#}";
        }
        GUILayout.Label(line);
    }

    /// <summary>
    ///     Live readout of the Mage's imbuement (element, charges, remaining seconds) so the loop is
    ///     verifiable before the HUD element widget exists. Hidden for classes without a MageImbuement
    ///     (the DrawRageReadout shape).
    /// </summary>
    private void DrawImbuementReadout()
    {
        MageImbuement imbuement = Player.Instance != null ? Player.Instance.GetComponentInChildren<MageImbuement>() : null;
        if (imbuement == null || !imbuement.isActiveAndEnabled)
        {
            return;
        }

        GUILayout.Label(imbuement.IsActive
            ? $"Imbue: {imbuement.CurrentElement} x{imbuement.ChargeCount} ({imbuement.RemainingSeconds:0.#}s)"
            : "Imbue: none");
    }

    private void DrawSpawnBurstButton(int count)
    {
        if (GUILayout.Button($"+{count}", GUILayout.Height(ButtonHeight)))
        {
            if (WaveSpawner.Instance != null)
            {
                WaveSpawner.Instance.DebugSpawnBurst(count);
            }
            else if (SurvivorsSpawner.Instance != null)
            {
                SurvivorsSpawner.Instance.DebugSpawnBurst(count);
            }
        }
    }

    #region 1-Click Combat Scenarios
    private readonly List<GameObject> activeScenarioSpawns = new List<GameObject>();
    private bool aiPassiveMode = true;

    private void DrawScenarioControls()
    {
        GUILayout.Space(6f);
        GUILayout.Label("=== 1-Click Combat Scenarios ===");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Sword vs Dummy", GUILayout.Height(ButtonHeight)))
        {
            SetupScenarioSwordVsDummy();
        }
        if (GUILayout.Button("Axe vs 3 Brutes", GUILayout.Height(ButtonHeight)))
        {
            SetupScenarioAxeVsBrutes();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Mace vs Bubbler", GUILayout.Height(ButtonHeight)))
        {
            SetupScenarioMaceVsBubbler();
        }
        if (GUILayout.Button("Fire Imbue Swarm", GUILayout.Height(ButtonHeight)))
        {
            SetupScenarioFireSwarm();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Bow Longshot", GUILayout.Height(ButtonHeight)))
        {
            SetupScenarioBowLongshot();
        }
        if (GUILayout.Button("100% Ult Unleash", GUILayout.Height(ButtonHeight)))
        {
            SetupScenarioUltUnleash();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Spawn Infinite Dummy", GUILayout.Height(ButtonHeight)))
        {
            SpawnScenarioEnemy("goblin", new Vector3(0f, 0f, 2.0f), asDummy: true);
        }
        string aiModeText = aiPassiveMode ? "AI: PASSIVE" : "AI: AGGRO";
        if (GUILayout.Button(aiModeText, GUILayout.Height(ButtonHeight)))
        {
            aiPassiveMode = !aiPassiveMode;
            UpdateScenarioAiStates();
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Clear All Test Spawns", GUILayout.Height(ButtonHeight)))
        {
            ClearAllScenarioSpawns();
        }
        GUILayout.Space(6f);
    }

    private GameObject SpawnScenarioEnemy(string enemyId, Vector3 forwardRightOffset, bool asDummy)
    {
        Player player = Player.Instance;
        Vector3 originPos = player != null ? player.transform.position : Vector3.zero;
        Vector3 forward = player != null ? player.transform.forward : Vector3.forward;
        Vector3 right = player != null ? player.transform.right : Vector3.right;

        Vector3 spawnTarget = originPos + forward * forwardRightOffset.z + right * forwardRightOffset.x;
        if (UnityEngine.AI.NavMesh.SamplePosition(spawnTarget, out UnityEngine.AI.NavMeshHit hit, 4f, UnityEngine.AI.NavMesh.AllAreas))
        {
            spawnTarget = hit.position;
        }

        Vector3 toPlayer = originPos - spawnTarget;
        toPlayer.y = 0f;
        Quaternion lookRot = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer) : Quaternion.identity;

        EnemyPrefabMapSO map = null;
        if (WaveSpawner.Instance != null) map = WaveSpawner.Instance.PrefabMap;
        if (map == null && SurvivorsSpawner.Instance != null) map = SurvivorsSpawner.Instance.PrefabMap;
        if (map == null)
        {
            var maps = Resources.FindObjectsOfTypeAll<EnemyPrefabMapSO>();
            if (maps != null && maps.Length > 0) map = maps[0];
        }

        GameObject prefab = map != null ? map.FindPrefab(enemyId) : null;
        if (prefab == null)
        {
            Debug.LogWarning($"[DevConsole] Could not find prefab for enemy id: '{enemyId}' in EnemyPrefabMapSO.");
            return null;
        }

        GameObject spawned = Instantiate(prefab, spawnTarget, lookRot);
        activeScenarioSpawns.Add(spawned);

        EnemyRosterSO roster = null;
        if (WaveSpawner.Instance != null) roster = WaveSpawner.Instance.Roster;
        if (roster == null && SurvivorsSpawner.Instance != null) roster = SurvivorsSpawner.Instance.Roster;
        EnemyDefinition def = roster != null ? roster.Find(enemyId) : new EnemyDefinition { id = enemyId };

        WaveSpawner.ApplyDefinition(spawned, def);

        if (asDummy || aiPassiveMode)
        {
            ApplyPassiveState(spawned, true);
        }

        return spawned;
    }

    private void ApplyPassiveState(GameObject enemy, bool passive)
    {
        if (enemy == null) return;

        if (enemy.TryGetComponent(out UnityEngine.AI.NavMeshAgent agent))
        {
            agent.isStopped = passive;
        }
        if (enemy.TryGetComponent(out AIMovement move))
        {
            move.enabled = !passive;
        }
        if (enemy.TryGetComponent(out AIAttack attack))
        {
            attack.enabled = !passive;
        }

        if (passive)
        {
            if (enemy.GetComponent<TrainingDummy>() == null)
            {
                enemy.AddComponent<TrainingDummy>();
            }
        }
        else
        {
            TrainingDummy td = enemy.GetComponent<TrainingDummy>();
            if (td != null) Destroy(td);
        }
    }

    private void UpdateScenarioAiStates()
    {
        activeScenarioSpawns.RemoveAll(go => go == null);
        foreach (var enemy in activeScenarioSpawns)
        {
            ApplyPassiveState(enemy, aiPassiveMode);
        }
        Debug.Log($"[DevConsole] Updated {activeScenarioSpawns.Count} test spawns to {(aiPassiveMode ? "PASSIVE" : "AGGRO")}");
    }

    private void ClearAllScenarioSpawns()
    {
        activeScenarioSpawns.RemoveAll(go => go == null);
        foreach (var obj in activeScenarioSpawns)
        {
            if (obj != null) Destroy(obj);
        }
        activeScenarioSpawns.Clear();

        var dummies = new List<TrainingDummy>(TrainingDummy.ActiveDummies);
        foreach (var d in dummies)
        {
            if (d != null) Destroy(d.gameObject);
        }

        Debug.Log("[DevConsole] Cleared all scenario test spawns.");
    }

    private void SetupScenarioSwordVsDummy()
    {
        ClearAllScenarioSpawns();
        isGodMode = true;

        PlayerWeaponManager pwm = GetWeaponManager();
        if (pwm != null) pwm.EquipMelee("sword");

        DraftUpgradeService draftService = DraftUpgradeService.GetOrCreateInstance();
        if (draftService != null) draftService.DebugResetAllDrafts();

        SpawnScenarioEnemy("goblin", new Vector3(0f, 0f, 2.0f), asDummy: true);
        Debug.Log("<color=#00FF88>[DevConsole]</color> Scenario: Sword vs. Dummy initialized.");
    }

    private void SetupScenarioAxeVsBrutes()
    {
        ClearAllScenarioSpawns();
        isGodMode = true;

        PlayerWeaponManager pwm = GetWeaponManager();
        if (pwm != null) pwm.EquipMelee("axe");

        DraftUpgradeService draftService = DraftUpgradeService.GetOrCreateInstance();
        if (draftService != null)
        {
            draftService.DebugResetAllDrafts();
            var def = draftService.GetById("draft_axe_whirlwind") ?? draftService.GetById("draft_axe_damage");
            if (def != null) draftService.DebugSetDraftLevel(def, 2);
        }

        SpawnScenarioEnemy("brute", new Vector3(-1.2f, 0f, 2.5f), asDummy: aiPassiveMode);
        SpawnScenarioEnemy("brute", new Vector3(0f, 0f, 3.0f), asDummy: aiPassiveMode);
        SpawnScenarioEnemy("brute", new Vector3(1.2f, 0f, 2.5f), asDummy: aiPassiveMode);
        Debug.Log("<color=#00FF88>[DevConsole]</color> Scenario: Two-Handed Axe vs 3 Brutes initialized.");
    }

    private void SetupScenarioMaceVsBubbler()
    {
        ClearAllScenarioSpawns();
        isGodMode = true;

        PlayerWeaponManager pwm = GetWeaponManager();
        if (pwm != null) pwm.EquipMelee("mace");

        SpawnScenarioEnemy("bubbler", new Vector3(0f, 0f, 2.2f), asDummy: aiPassiveMode);
        Debug.Log("<color=#00FF88>[DevConsole]</color> Scenario: Mace vs Bubbler Shield initialized.");
    }

    private void SetupScenarioFireSwarm()
    {
        ClearAllScenarioSpawns();
        isGodMode = true;

        DraftUpgradeService draftService = DraftUpgradeService.GetOrCreateInstance();
        if (draftService != null)
        {
            var def = draftService.GetById("draft_fire_weapon") ?? draftService.GetById("draft_fire_burn");
            if (def != null) draftService.DebugSetDraftLevel(def, 3);
        }

        SpawnScenarioEnemy("goblin", new Vector3(-1.5f, 0f, 2.5f), asDummy: aiPassiveMode);
        SpawnScenarioEnemy("goblin", new Vector3(-0.7f, 0f, 3.0f), asDummy: aiPassiveMode);
        SpawnScenarioEnemy("goblin", new Vector3(0f, 0f, 2.5f), asDummy: aiPassiveMode);
        SpawnScenarioEnemy("goblin", new Vector3(0.7f, 0f, 3.0f), asDummy: aiPassiveMode);
        SpawnScenarioEnemy("goblin", new Vector3(1.5f, 0f, 2.5f), asDummy: aiPassiveMode);
        Debug.Log("<color=#00FF88>[DevConsole]</color> Scenario: Fire Imbuement Swarm initialized.");
    }

    private void SetupScenarioBowLongshot()
    {
        ClearAllScenarioSpawns();
        isGodMode = true;

        PlayerWeaponManager pwm = GetWeaponManager();
        if (pwm != null) pwm.EquipRanged("bow");

        SpawnScenarioEnemy("goblin", new Vector3(0f, 0f, 12.0f), asDummy: true);
        Debug.Log("<color=#00FF88>[DevConsole]</color> Scenario: Bow Longshot initialized.");
    }

    private void SetupScenarioUltUnleash()
    {
        ClearAllScenarioSpawns();
        isGodMode = true;

        FillUltimateCharge();

        for (int i = 0; i < 6; i++)
        {
            float angle = (i - 2.5f) * 20f * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Sin(angle) * 3.5f, 0f, Mathf.Cos(angle) * 3.5f);
            SpawnScenarioEnemy("goblin", offset, asDummy: aiPassiveMode);
        }
        Debug.Log("<color=#00FF88>[DevConsole]</color> Scenario: 100% Ultimate Unleash initialized. Press Q!");
    }
    #endregion
}
