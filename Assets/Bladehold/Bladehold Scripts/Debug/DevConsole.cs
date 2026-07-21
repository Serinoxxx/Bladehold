using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
///     In-game developer cheat console, toggled with the backquote/tilde key. Draws an IMGUI panel of
///     cheat buttons (add gold, advance wave, …); extend it by adding buttons in <see cref="DrawButtons" />.
///     It bootstraps itself when play starts (Editor and development builds only), so it needs no scene
///     object and survives the death screen's scene reloads.
/// </summary>
public class DevConsole : MonoBehaviour
{
    private const float PanelWidth = 220f;
    private const float Padding = 10f;
    private const float ButtonHeight = 32f;
    private const string NextWaveFieldName = "DevConsoleNextWave";

    private bool visible;
    private string nextWaveText = "";
    private int spawnTypeIndex;
    private int classIndex = -1;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        GameObject consoleObject = new GameObject("DevConsole");
        consoleObject.AddComponent<DevConsole>();
        DontDestroyOnLoad(consoleObject);
    }
#endif

    private void Update()
    {
        // The project is new-Input-System-only, so read the key via Keyboard rather than legacy Input.
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[Key.Backquote].wasPressedThisFrame)
        {
            visible = !visible;
        }
    }

    private void OnGUI()
    {
        if (!visible)
        {
            return;
        }

        GUILayout.BeginArea(new Rect(Padding, Padding, PanelWidth, Screen.height - 2f * Padding), GUI.skin.box);
        GUILayout.Label("Dev Console");
        DrawButtons();
        GUILayout.EndArea();
    }

    private void DrawButtons()
    {
        if (GUILayout.Button("+10,000 Gold", GUILayout.Height(ButtonHeight)))
        {
            // Singletons are re-created on scene reload, so resolve them per click rather than caching.
            Wallet wallet = Player.Instance != null ? Player.Instance.Wallet : null;
            if (wallet != null)
            {
                wallet.Add(10000);
            }
        }

        DrawWaveControls();
        DrawEnemySpawnControls();
        DrawClassControls();
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
    ///     Spawn-a-specific-type cheat: a ◄/► picker over <see cref="WaveSpawner.DebugSpawnableTypes" />
    ///     (all roster ids with a prefab mapping) plus a "Spawn" button that instantly places one at a
    ///     random spawn point via <see cref="WaveSpawner.DebugSpawnEnemyType" />.
    /// </summary>
    private void DrawEnemySpawnControls()
    {
        WaveSpawner spawner = WaveSpawner.Instance;
        if (spawner == null)
        {
            return;
        }

        IReadOnlyList<EnemyDefinition> types = spawner.DebugSpawnableTypes;
        if (types.Count == 0)
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
            spawner.DebugSpawnEnemyType(selected.id);
        }
    }

    /// <summary>
    ///     Class-switch cheat: a ◄/► picker over <see cref="PlayerClassController.Slots" /> plus a
    ///     switch button. Switching is reload-based (never hot-swapped): it persists the chosen id via
    ///     <see cref="PlayerClassController.SetSavedClass" /> and reloads the scene, the same shape as
    ///     "Wipe Progress &amp; Reload". Purchased nodes of the other class's tree just go dormant —
    ///     <see cref="SkillTreeService" /> skips ids the active tree doesn't know.
    /// </summary>
    private void DrawClassControls()
    {
        PlayerClassController controller = Player.Instance != null
            ? Player.Instance.GetComponent<PlayerClassController>()
            : null;
        if (controller == null || controller.Slots.Count == 0)
        {
            return;
        }

        var slots = controller.Slots;
        if (classIndex < 0)
        {
            // First draw this scene: start the picker on the class actually in play.
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i]?.definition != null && slots[i].definition == controller.ActiveClass)
                {
                    classIndex = i;
                    break;
                }
            }
        }
        classIndex = Mathf.Clamp(classIndex, 0, slots.Count - 1);

        GUILayout.Label($"Class (current: {(controller.ActiveClass != null ? controller.ActiveClass.id : "?")})");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<", GUILayout.Width(36f), GUILayout.Height(ButtonHeight)))
        {
            classIndex = (classIndex - 1 + slots.Count) % slots.Count;
        }
        ClassDefinitionSO selected = slots[classIndex]?.definition;
        string label = selected != null
            ? (string.IsNullOrEmpty(selected.displayName) ? selected.id : selected.displayName)
            : "<no definition>";
        GUILayout.Label(label, GUILayout.ExpandWidth(true));
        if (GUILayout.Button(">", GUILayout.Width(36f), GUILayout.Height(ButtonHeight)))
        {
            classIndex = (classIndex + 1) % slots.Count;
        }
        GUILayout.EndHorizontal();

        if (selected != null)
        {
            if (GUILayout.Button($"Switch to {label} & Reload", GUILayout.Height(ButtonHeight)))
            {
                PlayerClassController.SetSavedClass(selected.id);
                Time.timeScale = GameSettingsService.TargetTimeScale;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
            if (GUILayout.Button($"Switch to {label} & Wipe Progress", GUILayout.Height(ButtonHeight)))
            {
                PlayerClassController.SetSavedClass(selected.id);
                SaveData data = SaveSystem.Load();
                data.ResetProgress();
                SaveSystem.Save(data);
                RunState.StartingWave = 1;
                Time.timeScale = GameSettingsService.TargetTimeScale;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }
    }

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
        }
    }
}
