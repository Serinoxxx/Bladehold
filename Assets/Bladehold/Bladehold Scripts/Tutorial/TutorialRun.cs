using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
///     Within-session state for the first-launch tutorial (T1 Dungeon → T2 Arena → T3 Gate). Like
///     <see cref="RunState" />, it survives scene loads but not a restart of the game. Set when the
///     tutorial is entered, cleared when it's skipped or its last scene ends (death or victory).
/// </summary>
public static class TutorialRun
{
    public static bool Active { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Active = false;
    }

    public static void Begin()
    {
        Active = true;
    }

    public static void End()
    {
        Active = false;
    }

    /// <summary>Persists <see cref="SaveData.tutorialCompleted" />. Called on entering the last scene and on skip.</summary>
    public static void MarkCompleted()
    {
        SaveData data = SaveSystem.Load();
        if (data.tutorialCompleted) return;
        data.tutorialCompleted = true;
        SaveSystem.Save(data);
    }

    /// <summary>Pause-menu Skip Tutorial: mark it done, drop the sandbox run and go to the Meta Area.</summary>
    public static void Skip()
    {
        TutorialTelemetry.Skipped(SceneManager.GetActiveScene().name);
        MarkCompleted();
        End();
        Time.timeScale = 1f;
        MoreMountains.Feedbacks.MMTimeScaleEvent.Reset();
        RunSession.ClearRun();
        TutorialConfigSO config = TutorialConfigSO.Load();
        string meta = config != null ? config.metaAreaSceneName : "Bladehold Meta Area Scene";
        LoadScene(meta);
    }

    /// <summary>Settings/Main Menu Replay Tutorial: starts T1 without touching the completed flag.</summary>
    public static void Replay()
    {
        TutorialConfigSO config = TutorialConfigSO.Load();
        if (config == null || string.IsNullOrEmpty(config.firstSceneName))
        {
            Debug.LogError("[TutorialRun] Replay: Resources/TutorialConfig is missing or has no firstSceneName.");
            return;
        }
        Time.timeScale = 1f;
        MoreMountains.Feedbacks.MMTimeScaleEvent.Reset();
        RunSession.ClearRun();
        Begin();
        LoadScene(config.firstSceneName);
    }

    public static void LoadScene(string sceneName)
    {
        if (Bladehold.UI.LoadingScreenManager.Instance != null)
        {
            Bladehold.UI.LoadingScreenManager.Instance.LoadScene(sceneName, Bladehold.UI.AreaDatabase.GetMetadata(sceneName));
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}
