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
        }
    }

    private void HandleSurvivorsObjectiveStarted(ISurvivorsObjective obj)
    {
        if (objectiveHeaderText != null)
        {
            objectiveHeaderText.text = $"OBJECTIVE: {obj.Title.ToUpper()}";
        }
        if (objectiveProgressText != null)
        {
            objectiveProgressText.text = obj.ProgressText;
        }
    }

    private void HandleSurvivorsObjectiveProgress(ISurvivorsObjective obj)
    {
        if (objectiveProgressText != null)
        {
            objectiveProgressText.text = obj.ProgressText;
        }
    }

    private void HandleSurvivorsObjectiveCompleted(ISurvivorsObjective obj)
    {
        if (objectiveHeaderText != null)
        {
            objectiveHeaderText.text = $"{obj.Title.ToUpper()} CLEARED!";
        }
        if (objectiveProgressText != null)
        {
            objectiveProgressText.text = "Objective complete! Next objective incoming...";
        }
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
        if (anyError || !waveInProgress || spawner == null) return;

        UpdateProgressText(spawner.KilledThisWave, spawner.WaveGoblinTotal);
    }

    private void UpdateProgressText(int killed, int total)
    {
        if (objectiveProgressText != null)
        {
            objectiveProgressText.text = $"Slay all enemies: {killed}/{total}";
        }
    }
}
