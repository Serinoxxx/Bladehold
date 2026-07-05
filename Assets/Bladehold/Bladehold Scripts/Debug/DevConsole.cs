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

    private bool visible;

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

        if (GUILayout.Button("Advance Wave", GUILayout.Height(ButtonHeight)))
        {
            if (WaveSpawner.Instance != null)
            {
                WaveSpawner.Instance.DebugAdvanceWave();
            }
        }

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
