using UnityEngine;
using UnityEngine.InputSystem;

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
    }
}
