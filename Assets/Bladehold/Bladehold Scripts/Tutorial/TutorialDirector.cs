using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
///     Scene singleton that runs one tutorial scene (plan 16): its <see cref="TutorialStep" />s in order,
///     the hint panel (<see cref="TutorialHintUI" />) and the HUD waypoint for the current step (as an
///     <see cref="IWaypointSource" />). When every step is done it unlocks <see cref="exit" /> (if any) and
///     points the player at it.
///     Per-scene rules: <see cref="reloadOnDeath" /> (the arena restarts instead of the run-over screen,
///     read by <see cref="DeathScreen" />), <see cref="startsFreshRun" /> (T1 and T3 reset
///     <see cref="RunSession" /> so ammo/supply start clean) and <see cref="marksTutorialCompleted" /> (T3).
///     In a battle scene (T3) it also owns the prep phase (plan 21): the current step shuts the Ready hold
///     (<see cref="WaveStartGate" />), narrows the BUILD markers (<see cref="BuildMarkerFocus" />) and can
///     make the towers hold fire (<see cref="DefenseStructure.HoldFire" />). All three let go on finish.
/// </summary>
[DefaultExecutionOrder(-200)]
public class TutorialDirector : MonoBehaviour, IWaypointSource
{
    public static TutorialDirector Instance { get; private set; }

    [Header("Steps (run in order)")]
    [SerializeField] private List<TutorialStep> steps = new List<TutorialStep>();

    [Header("Exit")]
    [Tooltip("Door to the next tutorial scene, unlocked when every step is done. Empty in the last scene.")]
    [SerializeField] private TutorialSceneExit exit;
    [SerializeField] private TutorialHint exitHint = TutorialHint.Of("tutorial.exit", "Head through the door", "Interact");

    [Header("Scene rules")]
    [Tooltip("Arena: a player death reloads this scene instead of ending the run.")]
    [SerializeField] private bool reloadOnDeath;
    [Tooltip("Reset RunSession on entry (T1 and T3), so ammo, gold and supply start clean.")]
    [SerializeField] private bool startsFreshRun;
    [Tooltip("Last scene: entering it marks the tutorial completed in the save.")]
    [SerializeField] private bool marksTutorialCompleted;
    [Tooltip("Dungeon scenes (no gate, no objectives): hide the objective panel and the fortress gate bar.")]
    [SerializeField] private bool hideBattleHud;
    [Tooltip("Telemetry scene id, e.g. T1.")]
    [SerializeField] private string telemetrySceneId = "T1";

    [Header("Waypoint")]
    [Tooltip("Optional marker icon. Empty = the tracker's default objective icon.")]
    [SerializeField] private Sprite waypointIcon;
    [SerializeField] private Color waypointTint = new Color(1f, 0.85f, 0.35f, 1f);

    [Header("Feedback (optional)")]
    [Tooltip("Optional: played when the player dies in a reloadOnDeath scene, before the reload. Leave empty for none.")]
    [SerializeField] private MMF_Player deathReloadFeedback;

    private readonly List<Object> focusBuffer = new List<Object>();
    private TutorialConfigSO config;
    private int currentIndex = -1;
    private bool reloading;
    private bool anyError;

    public bool ReloadOnDeath => reloadOnDeath && !anyError;
    public bool IsFinished { get; private set; }
    public TutorialStep CurrentStep => currentIndex >= 0 && currentIndex < steps.Count ? steps[currentIndex] : null;

    /// <summary>Where a fall respawn puts the player: the current step's waypoint, else the last one reached.</summary>
    public Transform RespawnPoint { get; private set; }

    private void Awake()
    {
        Instance = this;
        TutorialRun.Begin();
        if (startsFreshRun)
        {
            // Before any Start() reads ammo/supply.
            RunSession.StartNewRun();
        }
        if (marksTutorialCompleted)
        {
            TutorialRun.MarkCompleted();
        }
    }

    private void OnEnable()
    {
        ObjectiveWaypointTrackerUI.RegisterSource(this);
    }

    private void OnDisable()
    {
        ObjectiveWaypointTrackerUI.UnregisterSource(this);
    }

    private void Start()
    {
        config = TutorialConfigSO.Load();
        if (config == null)
        {
            Debug.LogError("[TutorialDirector] Resources/TutorialConfig is missing.", this);
            anyError = true;
        }
        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i] == null)
            {
                Debug.LogError($"[TutorialDirector] Step {i} is not assigned.", this);
                anyError = true;
            }
        }
        if (TutorialHintUI.Instance == null)
        {
            Debug.LogError("[TutorialDirector] No TutorialHintUI in the scene (it lives in the HUD).", this);
        }
        if (anyError) return;

        if (hideBattleHud) HideBattleHud();
        StartCoroutine(BeginNextStep(0f));
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        ReleaseBattleHolds();
    }

    private void Update() => RefreshBattleHolds();

    // Also run straight from NotifyStepCompleted, so a Ready press in the same frame can't slip past.
    private void RefreshBattleHolds()
    {
        if (ShouldBlockWaveStart()) WaveStartGate.Block(this);
        else WaveStartGate.Release(this);

        TutorialStep step = CurrentStep;
        if (!anyError && !IsFinished && step != null && step.IsActive && step.SuspendDefenses) DefenseStructure.HoldFire(this);
        else DefenseStructure.ResumeFire(this);
    }

    private bool ShouldBlockWaveStart()
    {
        if (anyError || IsFinished) return false;
        TutorialStep step = CurrentStep;
        if (step != null && step.IsActive) return step.BlocksWaveStart;
        // Between steps (the advance delay): hold the gate for the next one, so Ready can't slip through the gap.
        int next = currentIndex + 1;
        return next < steps.Count && steps[next] != null && steps[next].BlocksWaveStart;
    }

    private void ReleaseBattleHolds()
    {
        WaveStartGate.Release(this);
        DefenseStructure.ResumeFire(this);
        BuildMarkerFocus.Clear(this);
    }

    private void ApplyBuildMarkerFocus(TutorialStep step)
    {
        focusBuffer.Clear();
        if (step.GetBuildMarkerFocus(focusBuffer)) BuildMarkerFocus.Set(this, focusBuffer);
        else BuildMarkerFocus.Clear(this);
    }

    private static void HideBattleHud()
    {
        foreach (ObjectiveTrackerUI ui in FindObjectsByType<ObjectiveTrackerUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            ui.gameObject.SetActive(false);
        foreach (FortressGateHealthBarUI ui in FindObjectsByType<FortressGateHealthBarUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            ui.gameObject.SetActive(false);
    }

    public void NotifyStepCompleted(TutorialStep step)
    {
        if (anyError || step != CurrentStep) return;
        TutorialTelemetry.StepCompleted(telemetrySceneId, step.StepId);
        if (TutorialHintUI.Instance != null) TutorialHintUI.Instance.PlayComplete();
        RefreshBattleHolds();
        StartCoroutine(BeginNextStep(config.stepAdvanceDelay));
    }

    private IEnumerator BeginNextStep(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        currentIndex++;
        TutorialStep step = CurrentStep;
        if (step == null)
        {
            Finish();
            yield break;
        }

        if (step.WaypointTarget != null) RespawnPoint = step.WaypointTarget;
        TutorialTelemetry.StepStarted(telemetrySceneId, step.StepId);
        ShowHint(step.Hint, step.SecondaryHint);
        ApplyBuildMarkerFocus(step);
        step.Begin(this);
    }

    private void Finish()
    {
        IsFinished = true;
        ReleaseBattleHolds();
        if (exit != null)
        {
            exit.Unlock();
            ShowHint(exitHint, null);
        }
        else if (TutorialHintUI.Instance != null)
        {
            TutorialHintUI.Instance.Hide();
        }
    }

    private void ShowHint(TutorialHint hint, TutorialHint secondary)
    {
        if (TutorialHintUI.Instance == null || hint == null || hint.IsEmpty) return;
        TutorialHintUI.Instance.Show(hint, secondary);
    }

    /// <summary>Steps update the counter line ("Banners 1/3") through here.</summary>
    public void SetCounter(string text)
    {
        if (TutorialHintUI.Instance != null) TutorialHintUI.Instance.SetCounter(text);
    }

    /// <summary>Steps that teach in stages (the Ultimate Trial) swap their main line through here, keeping the secondary.</summary>
    public void SetHint(TutorialHint hint)
    {
        TutorialStep step = CurrentStep;
        ShowHint(hint, step != null ? step.SecondaryHint : null);
    }

    /// <summary>Steps swap the secondary line (heavy-attack nudge, low-ammo tip) through here. Null restores the step's own.</summary>
    public void SetSecondaryHint(TutorialHint hint)
    {
        if (TutorialHintUI.Instance == null) return;
        TutorialStep step = CurrentStep;
        TutorialHintUI.Instance.SetSecondary(hint ?? (step != null ? step.SecondaryHint : null));
    }

    public void GetWaypointTargets(List<ObjectiveWaypointTarget> results)
    {
        if (anyError) return;
        Sprite icon = waypointIcon;
        if (icon == null && ObjectiveWaypointTrackerUI.Instance != null) icon = ObjectiveWaypointTrackerUI.Instance.DefaultObjectiveIcon;

        if (IsFinished)
        {
            if (exit != null) results.Add(new ObjectiveWaypointTarget(exit.WaypointAnchor, new Vector3(0f, 2f, 0f), icon, waypointTint, null));
            return;
        }

        TutorialStep step = CurrentStep;
        if (step == null || !step.IsActive) return;
        if (step.WaypointTarget != null) results.Add(step.BuildWaypoint(icon, waypointTint));
        step.GetExtraWaypoints(results);
    }

    /// <summary>Called by <see cref="DeathScreen" /> instead of the run-over screen when <see cref="ReloadOnDeath" />.</summary>
    public void ReloadAfterDeath()
    {
        if (reloading) return;
        reloading = true;
        if (deathReloadFeedback != null) deathReloadFeedback.PlayFeedbacks();
        StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        yield return new WaitForSecondsRealtime(config != null ? config.reloadAfterDeathDelay : 2f);
        Time.timeScale = 1f;
        MMTimeScaleEvent.Reset();
        TutorialRun.LoadScene(SceneManager.GetActiveScene().name);
    }
}
