using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Central manager for mouse cursor lock state and visibility.
///     Systems (PauseMenu, DeathScreen, WaveIntermissionUI, DevConsole, etc.) request the cursor to be unlocked
///     by calling <see cref="SetUnlock(string, bool)"/>. When one or more unlock requests are active,
///     the cursor is unlocked (<see cref="CursorLockMode.None"/>) and visible.
///     When no unlock requests are active, the cursor is locked (<see cref="CursorLockMode.Locked"/>) and hidden for gameplay.
/// </summary>
public class CursorLockManager : MonoBehaviour
{
    public static CursorLockManager Instance { get; private set; }

    private static readonly HashSet<string> unlockRequests = new HashSet<string>();
    private static bool gamepadHidden;

    public static bool IsCursorUnlocked => unlockRequests.Count > 0;
    public static bool IsLocked => !IsCursorUnlocked;
    public static CursorLockMode CurrentLockMode => IsCursorUnlocked ? CursorLockMode.None : CursorLockMode.Locked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("CursorLockManager");
        go.AddComponent<CursorLockManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        ApplyState();
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
        ApplyState();
    }

    /// <summary>
    ///     Adds or removes an unlock request for the specified owner key.
    /// </summary>
    public static void SetUnlock(string ownerKey, bool unlock)
    {
        if (string.IsNullOrEmpty(ownerKey)) return;

        if (unlock)
        {
            unlockRequests.Add(ownerKey);
        }
        else
        {
            unlockRequests.Remove(ownerKey);
        }

        ApplyState();
    }

    /// <summary>
    ///     Informs the manager whether the gamepad auto-hider wants the hardware cursor hidden during gamepad play.
    /// </summary>
    public static void SetGamepadHidden(bool hidden)
    {
        gamepadHidden = hidden;
        ApplyState();
    }

    public static void ApplyState()
    {
        if (IsCursorUnlocked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = !gamepadHidden;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
