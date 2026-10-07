using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
///     Within-session state for the first-launch tutorial (T1 Dungeon → T2 Arena → T3 Valley Stronghold). Like
///     <see cref="RunState" />, it survives scene loads but not a restart of the game. Set when the
///     tutorial is entered, cleared when it's skipped or its last scene ends (death or victory).
/// </summary>
public static class TutorialRun
{
    public static bool Active { get; private set; }

    /// <summary>
    ///     Free Arcane Cores the Ultimate Trial (T2) granted that the player hasn't spent yet (plan 21 phase 6).
    ///     Every drop in <see cref="RunSession.ArcaneCores" /> counts as spending them first, and
    ///     <see cref="StripTrialArcaneCores" /> takes back whatever is left, so they never reach the campaign.
    /// </summary>
    public static int TrialArcaneCores { get; private set; }

    private static int lastArcaneCores;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Active = false;
        RunSession.OnArcaneCoresChanged -= HandleArcaneCoresChanged;
        TrialArcaneCores = 0;
        lastArcaneCores = 0;
    }

    public static void Begin()
    {
        Active = true;
    }

    /// <summary>Ends the tutorial run. Any unspent trial cores are taken back first.</summary>
    public static void End()
    {
        StripTrialArcaneCores();
        Active = false;
    }

    /// <summary>Adds <paramref name="count" /> free Arcane Cores for the Ultimate Trial, tracked so they can be stripped later.</summary>
    public static void GrantTrialArcaneCores(int count)
    {
        if (count <= 0) return;
        if (TrialArcaneCores == 0) RunSession.OnArcaneCoresChanged += HandleArcaneCoresChanged;
        TrialArcaneCores += count;
        lastArcaneCores = RunSession.ArcaneCores + count;
        RunSession.AddArcaneCores(count);
    }

    /// <summary>Removes the trial cores the player still holds. Cores they earned or brought in stay.</summary>
    public static void StripTrialArcaneCores()
    {
        if (TrialArcaneCores <= 0) return;
        RunSession.OnArcaneCoresChanged -= HandleArcaneCoresChanged;
        int strip = TrialArcaneCores;
        TrialArcaneCores = 0;
        RunSession.SetArcaneCores(RunSession.ArcaneCores - strip);
    }

    private static void HandleArcaneCoresChanged(int cores)
    {
        if (cores < lastArcaneCores) TrialArcaneCores = Mathf.Max(0, TrialArcaneCores - (lastArcaneCores - cores));
        lastArcaneCores = cores;
        if (TrialArcaneCores == 0) RunSession.OnArcaneCoresChanged -= HandleArcaneCoresChanged;
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
