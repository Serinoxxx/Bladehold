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

    private const float PanelWidth = 260f;
    private const float SkillsPanelWidth = 360f;
    private const float Padding = 10f;
    private const float ButtonHeight = 30f;
    private const string NextWaveFieldName = "DevConsoleNextWave";

    private bool visible;
    private string nextWaveText = "";
    private int spawnTypeIndex;
    private int objectiveIndex;
    private int classIndex = -1;
    private bool isGodMode;
    private Vector2 mainScrollPos;
    private Vector2 skillsScrollPos;
    private Vector2 draftScrollPos;
    private int activeRightPanelTab = 0;
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

        activeRightPanelTab = GUILayout.Toolbar(activeRightPanelTab, new string[] { "Meta Skills (Gold)", "Draft Abilities" }, GUILayout.Height(28f));
        GUILayout.Space(6f);

        if (activeRightPanelTab == 0)
        {
            DrawMetaSkillsTab();
        }
        else
        {
            DrawDraftAbilitiesTab();
        }

        GUILayout.EndArea();
    }

    private void DrawMetaSkillsTab()
    {
        SkillTreeService service = SkillTreeService.Instance;
        if (service == null || service.Tree == null || service.Tree.Nodes == null)
        {
            GUILayout.Label("SkillTreeService not ready.");
            return;
        }

        var nodes = service.Tree.Nodes;
        GUILayout.Label($"Skill Upgrades ({nodes.Count})");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Max All", GUILayout.Height(ButtonHeight)))
        {
            foreach (var node in nodes)
            {
                if (node != null) service.DebugSetLevel(node.id, node.maxLevel);
            }
        }
        if (GUILayout.Button("Reset All", GUILayout.Height(ButtonHeight)))
        {
            foreach (var node in nodes)
            {
                if (node != null) service.DebugSetLevel(node.id, 0);
            }
        }
        GUILayout.EndHorizontal();

        skillsScrollPos = GUILayout.BeginScrollView(skillsScrollPos, false, true);

        foreach (SkillNode node in nodes)
        {
            if (node == null) continue;

            int curLevel = service.GetLevel(node);
            string name = string.IsNullOrEmpty(node.displayName) ? node.id : node.displayName;
            string label = $"{name} [{curLevel}/{node.maxLevel}]";

            GUILayout.BeginHorizontal();

            GUI.enabled = curLevel > 0;
            if (GUILayout.Button("<", GUILayout.Width(28f), GUILayout.Height(24f)))
            {
                service.DebugSetLevel(node.id, curLevel - 1);
            }

            GUI.enabled = true;
            GUILayout.Label(label, GUILayout.ExpandWidth(true));

            GUI.enabled = curLevel < node.maxLevel;
            if (GUILayout.Button(">", GUILayout.Width(28f), GUILayout.Height(24f)))
            {
                service.DebugSetLevel(node.id, curLevel + 1);
            }
            GUI.enabled = true;

            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();
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

        DrawWeaponControls();
        DrawArmourControls();
        DrawUltimateControls();
        DrawDraftControls();

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
}
