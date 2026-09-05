using TMPro;
using UnityEngine;

/// <summary>
///     Dynamically updates the objective panel to reflect either:
///     1) Wave defense progress in classic mode (via <see cref="WaveSpawner"/>)
///     2) Active objective progress in survival mode (via <see cref="SurvivorsObjectiveManager"/>).
/// </summary>
public class ObjectiveTrackerUI : MonoBehaviour
{
    [SerializeField] private WaveSpawner spawner;
    [SerializeField] private SurvivorsObjectiveManager objectiveManager;

    [Header("References")]
    [Tooltip("The text displaying the current main objective (e.g. HOLD THE GATE: WAVE X)")]
    [SerializeField] private TMP_Text objectiveHeaderText;
    
    [Tooltip("The text displaying the specific task progress (e.g. Slay all enemies: 24/26)")]
    [SerializeField] private TMP_Text objectiveProgressText;

    private bool anyError;
    private bool waveInProgress;

    private void OnValidate()
    {
        if (spawner == null)
        {
            spawner = FindObjectOfType<WaveSpawner>();
        }
        if (objectiveManager == null)
        {
            objectiveManager = FindObjectOfType<SurvivorsObjectiveManager>();
        }
    }

    private void Start()
    {
        if (spawner == null)
        {
            spawner = FindObjectOfType<WaveSpawner>();
        }
        if (objectiveManager == null)
        {
            objectiveManager = SurvivorsObjectiveManager.Instance ?? FindObjectOfType<SurvivorsObjectiveManager>();
        }

        if (spawner == null && objectiveManager == null)
        {
            Debug.LogWarning("ObjectiveTrackerUI: Neither WaveSpawner nor SurvivorsObjectiveManager was found in the scene.");
            anyError = true;
            return;
        }

        if (spawner != null)
        {
            spawner.WaveStarted += HandleWaveStarted;
            spawner.WaveCleared += HandleWaveCleared;
            spawner.CountdownTick += HandleCountdownTick;
            UpdateProgressText(0, 0);
        }

        if (objectiveManager != null)
        {
            objectiveManager.OnObjectiveStarted += HandleSurvivorsObjectiveStarted;
            objectiveManager.OnObjectiveProgressChanged += HandleSurvivorsObjectiveProgress;
            objectiveManager.OnObjectiveCompleted += HandleSurvivorsObjectiveCompleted;
            objectiveManager.OnObjectiveFailed += HandleSurvivorsObjectiveFailed;

            if (objectiveManager.CurrentObjective != null)
            {
                HandleSurvivorsObjectiveStarted(objectiveManager.CurrentObjective);
            }
        }
    }

    private void OnDestroy()
    {
        if (spawner != null)
        {
            spawner.WaveStarted -= HandleWaveStarted;
            spawner.WaveCleared -= HandleWaveCleared;
            spawner.CountdownTick -= HandleCountdownTick;
        }

        if (objectiveManager != null)
        {
            objectiveManager.OnObjectiveStarted -= HandleSurvivorsObjectiveStarted;
            objectiveManager.OnObjectiveProgressChanged -= HandleSurvivorsObjectiveProgress;
            objectiveManager.OnObjectiveCompleted -= HandleSurvivorsObjectiveCompleted;
            objectiveManager.OnObjectiveFailed -= HandleSurvivorsObjectiveFailed;
        }
    }

    private void HandleSurvivorsObjectiveStarted(ISurvivorsObjective obj)
    {
        UpdateSurvivorsUI();
    }

    private void HandleSurvivorsObjectiveProgress(ISurvivorsObjective obj)
    {
        UpdateSurvivorsUI();
    }

    private void HandleSurvivorsObjectiveCompleted(ISurvivorsObjective obj)
    {
        UpdateSurvivorsUI();
    }

    private void HandleSurvivorsObjectiveFailed(ISurvivorsObjective obj)
    {
        UpdateSurvivorsUI();
    }

    private void HandleWaveStarted(int wave)
    {
        waveInProgress = true;
        if (objectiveHeaderText != null)
        {
            objectiveHeaderText.text = $"HOLD THE GATE: WAVE {wave}";
        }
    }

    private void HandleWaveCleared(int wave)
    {
        waveInProgress = false;
        if (objectiveHeaderText != null)
        {
            objectiveHeaderText.text = $"HOLD THE GATE: WAVE {wave} CLEARED";
        }
        if (objectiveProgressText != null)
        {
            objectiveProgressText.text = "Prepare for next wave...";
        }
    }

    private void HandleCountdownTick(int secondsRemaining)
    {
        if (waveInProgress) return;

        if (objectiveHeaderText != null)
        {
            objectiveHeaderText.text = $"HOLD THE GATE: NEXT WAVE IN {secondsRemaining}s";
        }
        if (objectiveProgressText != null)
        {
            objectiveProgressText.text = $"Wave starting in {secondsRemaining}s...";
        }
    }

    private void Update()
    {
        if (anyError) return;

        if (SurvivorsGameManager.Instance != null || objectiveManager != null || FindObjectOfType<SurvivorsGameManager>() != null)
        {
            UpdateSurvivorsUI();
        }
        else if (waveInProgress && spawner != null)
        {
            UpdateProgressText(spawner.KilledThisWave, spawner.WaveGoblinTotal);
        }
    }

    private void UpdateSurvivorsUI()
    {
        var sgm = SurvivorsGameManager.Instance ?? FindObjectOfType<SurvivorsGameManager>();
        if (sgm == null) return;
        if (objectiveManager == null) objectiveManager = SurvivorsObjectiveManager.Instance ?? FindObjectOfType<SurvivorsObjectiveManager>();


        // 2. Active rotating sub-objective / cleanup / intermission / boss status
        if (objectiveProgressText != null)
        {
            if (GameLoopManager.Instance != null && GameLoopManager.Instance.IsRestGateOpen)
            {
                if (objectiveHeaderText != null) objectiveHeaderText.text = "ROUND COMPLETE";
                objectiveProgressText.text = "[Round Cleared!]\nReturn to the Fortress via the gate.";
            }
            else if (GameLoopManager.Instance != null && GameLoopManager.Instance.ActivePowerup != null)
            {
                objectiveProgressText.text = $"[Wave Cleared!]\nClaim {GameLoopManager.Instance.ActivePowerup.BountyName} in the arena.";
            }
            else if (sgm.HasSurvivedSiege)
            {
                objectiveProgressText.text = "The Siegebreaker has arrived! Defend the fortress gate!";
            }
            else if (objectiveManager != null)
            {
                if (objectiveManager.Phase == SurvivorsObjectivePhase.Cleanup)
                {
                    int remSec = Mathf.CeilToInt(objectiveManager.PhaseTimeRemaining);
                    int alive = SurvivorsSpawner.Instance != null ? SurvivorsSpawner.Instance.AliveCount : 0;
                    objectiveProgressText.text = $"[Objective Completed!]\nClean up remaining enemies: {remSec}s ({alive} left)";
                }
                else if (objectiveManager.Phase == SurvivorsObjectivePhase.Intermission)
                {
                    int remSec = Mathf.CeilToInt(objectiveManager.PhaseTimeRemaining);
                    int nextWave = objectiveManager.CurrentWave + 1;
                    objectiveProgressText.text = $"[Prepare for Wave {nextWave}]\nNext wave starts in: {remSec}s";
                }
                else if (objectiveManager.CurrentObjective != null && objectiveManager.CurrentObjective.IsActive)
                {
                    var cur = objectiveManager.CurrentObjective;
                    objectiveProgressText.text = $"[{cur.Title}]\n{cur.ProgressText}";
                }
                else if (sgm.IsInFinalCountdown)
                {
                    objectiveProgressText.text = "Prepare for the final assault! The Siegebreaker approaches...";
                }
                else
                {
                    objectiveProgressText.text = "Prepare for incoming siege objective...";
                }
            }
            else if (sgm.IsInFinalCountdown)
            {
                objectiveProgressText.text = "Prepare for the final assault! The Siegebreaker approaches...";
            }
            else
            {
                objectiveProgressText.text = "Prepare for incoming siege objective...";
            }
        }
    }

    private void UpdateProgressText(int killed, int total)
    {
        if (objectiveProgressText != null)
        {
            objectiveProgressText.text = $"Slay all enemies: {killed}/{total}";
        }
    }
}
