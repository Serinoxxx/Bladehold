using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
///     Drives the wave canvas from <see cref="WaveSpawner" /> events: shows "Wave starts in {n}" during the
///     intermission countdown, a brief "BEGIN" when a wave starts, and "Wave Cleared!" when one is finished.
///     An optional persistent label shows the current wave number. Listens to the spawner; the spawner stays
///     unaware of the UI.
/// </summary>
public class WaveUI : MonoBehaviour
{
    [SerializeField] private WaveSpawner spawner;
    [Tooltip("Transient message line: countdown, BEGIN, Wave Cleared!.")]
    [SerializeField] private TMP_Text messageText;
    [Tooltip("Optional persistent label, e.g. \"Wave 3\". Leave unassigned to omit.")]
    [SerializeField] private TMP_Text waveLabel;
    [Tooltip("Seconds the \"BEGIN\" message stays on screen before clearing.")]
    [SerializeField] private float beginMessageDuration = 1.5f;

    private Coroutine clearMessageRoutine;
    private bool anyError = false;

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
            Debug.LogError("WaveSpawner is not assigned or found in the scene.");
            anyError = true;
        }
        if (messageText == null)
        {
            Debug.LogError("Message text is not assigned in the inspector.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        messageText.text = string.Empty;
        if (waveLabel != null)
        {
            waveLabel.text = string.Empty;
        }

        spawner.CountdownTick += HandleCountdownTick;
        spawner.WaveStarted += HandleWaveStarted;
        spawner.WaveCleared += HandleWaveCleared;
    }

    private void OnDestroy()
    {
        if (spawner != null)
        {
            spawner.CountdownTick -= HandleCountdownTick;
            spawner.WaveStarted -= HandleWaveStarted;
            spawner.WaveCleared -= HandleWaveCleared;
        }
    }

    private void HandleCountdownTick(int secondsRemaining)
    {
        SetMessage($"Wave starts in {secondsRemaining}");
    }

    private void HandleWaveStarted(int waveNumber)
    {
        if (waveLabel != null)
        {
            waveLabel.text = $"Wave {waveNumber}";
        }

        SetMessage("BEGIN");
        clearMessageRoutine = StartCoroutine(ClearMessageAfter(beginMessageDuration));
    }

    private void HandleWaveCleared(int waveNumber)
    {
        SetMessage("Wave Cleared!");
    }

    private void SetMessage(string text)
    {
        // Cancel any pending auto-clear so a new message isn't wiped by the previous one's timer.
        if (clearMessageRoutine != null)
        {
            StopCoroutine(clearMessageRoutine);
            clearMessageRoutine = null;
        }
        messageText.text = text;
    }

    private IEnumerator ClearMessageAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        messageText.text = string.Empty;
        clearMessageRoutine = null;
    }
}
