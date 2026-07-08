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

        // Perf stress tests: burst-spawn into the current wave, ignoring the concurrent cap.
        GUILayout.Label("Spawn Goblins (stress test)");
        GUILayout.BeginHorizontal();
        DrawSpawnBurstButton(50);
        DrawSpawnBurstButton(100);
        DrawSpawnBurstButton(300);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Wipe Save & Reload", GUILayout.Height(ButtonHeight)))
        {
            // Deleting also drops SaveSystem's in-memory cache, and nothing saves during scene teardown,
            // so the reloaded scene's Wallet/tree services Load() fresh defaults.
            SaveSystem.DeleteCurrentSave();
            RunState.StartingWave = 1;
            Time.timeScale = 1f; // ensure normal speed resumes even if something paused time on death.
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
