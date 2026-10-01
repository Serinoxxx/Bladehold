using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
///     Central manager for mouse cursor lock state and visibility.
///     Systems (PauseMenu, DeathScreen, WaveIntermissionUI, DevConsole, etc.) request the cursor to be unlocked
///     by calling <see cref="SetUnlock(string, bool, bool)"/>. When one or more unlock requests are active,
///     the cursor is unlocked (<see cref="CursorLockMode.None"/>) and visible.
///     When no unlock requests are active, the cursor is locked (<see cref="CursorLockMode.Locked"/>) and hidden for gameplay.
///
///     Requests are scene-scoped by default: a single-mode scene load drops every request not
///     flagged <c>persistAcrossScenes</c>, so a scene UI that unlocked the cursor and was torn down
///     by a scene change without releasing it (paused, mid-draft, victory screen…) can't leave the
///     next scene's camera frozen. The clear runs on <see cref="SceneManager.sceneLoaded"/>, which
///     fires after the new scene's Awake/OnEnable but before its Start — so request unlocks from
///     Start or later (every current caller does), never from Awake/OnEnable. Only DontDestroyOnLoad
///     owners that genuinely stay open across a load (the DevConsole) should persist.
/// </summary>
public class CursorLockManager : MonoBehaviour
{
    public static CursorLockManager Instance { get; private set; }

    private static readonly HashSet<string> unlockRequests = new HashSet<string>();
    private static readonly HashSet<string> persistentRequests = new HashSet<string>();
    private static bool gamepadHidden;

    public static bool IsCursorUnlocked => unlockRequests.Count > 0;
    public static bool IsLocked => !IsCursorUnlocked;
    public static CursorLockMode CurrentLockMode => IsCursorUnlocked ? CursorLockMode.None : CursorLockMode.Locked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        ForceUnlock();
        if (Instance != null) return;
        GameObject go = new GameObject("CursorLockManager");
        go.AddComponent<CursorLockManager>();
        DontDestroyOnLoad(go);
    }

    private static void ForceUnlock()
    {
        unlockRequests.Clear();
        persistentRequests.Clear();
        ApplyState();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ApplyState();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Instance = null;
        }
    }

    private void Update()
    {
        ApplyState();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;

        if (unlockRequests.Count > persistentRequests.Count)
        {
            List<string> dropped = new List<string>();
            foreach (string key in unlockRequests)
            {
                if (!persistentRequests.Contains(key))
                {
                    dropped.Add(key);
                }
            }
            Debug.Log($"[CursorLockManager] Scene '{scene.name}' loaded; dropping stale cursor unlock request(s): {string.Join(", ", dropped)}");
            unlockRequests.IntersectWith(persistentRequests);
        }

        ApplyState();
    }

    /// <summary>
    ///     Adds or removes an unlock request for the specified owner key. Requests are cleared on the
    ///     next single-mode scene load unless <paramref name="persistAcrossScenes"/> is true.
    /// </summary>
    public static void SetUnlock(string ownerKey, bool unlock, bool persistAcrossScenes = false)
    {
        if (string.IsNullOrEmpty(ownerKey)) return;

        if (unlock)
        {
            unlockRequests.Add(ownerKey);
            if (persistAcrossScenes)
            {
                persistentRequests.Add(ownerKey);
            }
            else
            {
                persistentRequests.Remove(ownerKey);
            }
        }
        else
        {
            unlockRequests.Remove(ownerKey);
            persistentRequests.Remove(ownerKey);
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
