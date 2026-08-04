using TMPro;
using UnityEngine;

/// <summary>
///     Dynamically updates the objective panel to reflect the current wave and how many
///     enemies have been slain out of the total required for the wave.
///     Listens to <see cref="WaveSpawner"/> events for wave state and checks progress in Update.
/// </summary>
public class ObjectiveTrackerUI : MonoBehaviour
{
    [SerializeField] private WaveSpawner spawner;

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
    }

    private void Start()
    {
        if (spawner == null)
        {
            spawner = FindObjectOfType<WaveSpawner>();
        }

        if (spawner == null)
        {
            Debug.LogError("ObjectiveTrackerUI has no WaveSpawner assigned.");
            anyError = true;
        }

        if (anyError) return;

        spawner.WaveStarted += HandleWaveStarted;
        spawner.WaveCleared += HandleWaveCleared;
        spawner.CountdownTick += HandleCountdownTick;

        UpdateProgressText(0, 0);
    }

    private void OnDestroy()
    {
        if (spawner != null)
        {
            spawner.WaveStarted -= HandleWaveStarted;
            spawner.WaveCleared -= HandleWaveCleared;
            spawner.CountdownTick -= HandleCountdownTick;
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
        if (anyError || !waveInProgress) return;

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
