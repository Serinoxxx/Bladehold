using System;
using System.Collections;
using TMPro;
using UnityEngine;
using MoreMountains.Feedbacks;

/// <summary>
///     Manages the visual presentation of the end-of-run statistics panel in Survivors Mode.
///     Reveals each statistic sequentially with an MMF_Player scale spring bump and an
///     unscaled count-up ticker accompanied by rising-pitch audio feedback.
/// </summary>
public class SurvivorsStatsPanelUI : MonoBehaviour
{
    [Serializable]
    public class StatRow
    {
        [Tooltip("Identifier name for debugging/inspector reference.")]
        public string statName;
        [Tooltip("CanvasGroup controlling visibility and alpha fade for this row.")]
        public CanvasGroup canvasGroup;
        [Tooltip("Transform of the row or value for scaling / bumps.")]
        public RectTransform rowTransform;
        [Tooltip("TMP_Text displaying the numerical/formatted value.")]
        public TMP_Text valueText;
        [Tooltip("Optional Feel MMF_Player feedback component for scale bounce / spring bump.")]
        public MMF_Player bumpFeedback;
    }

    [Header("Stat Rows")]
    [SerializeField] private StatRow timeSurvivedRow = new StatRow { statName = "Time Survived" };
    [SerializeField] private StatRow levelReachedRow = new StatRow { statName = "Level Reached" };
    [SerializeField] private StatRow enemiesSlainRow = new StatRow { statName = "Enemies Slain" };
    [SerializeField] private StatRow goldCollectedRow = new StatRow { statName = "Gold Collected" };
    [SerializeField] private StatRow damageDealtRow = new StatRow { statName = "Damage Dealt" };
    [SerializeField] private StatRow damageTakenRow = new StatRow { statName = "Damage Taken" };
    [SerializeField] private StatRow critsRow = new StatRow { statName = "Critical Hits" };

    [Header("Audio Settings")]
    [Tooltip("AudioSource used to play the count-up dings.")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("AudioClip played rapidly on each count-up tick.")]
    [SerializeField] private AudioClip countUpTickSound;
    [Tooltip("AudioClip played when a stat row finishes counting up.")]
    [SerializeField] private AudioClip rowCompleteSound;
    [Tooltip("Base audio pitch for the first stat row.")]
    [SerializeField] private float basePitch = 0.9f;
    [Tooltip("Pitch increment applied per tick during count-up.")]
    [SerializeField] private float pitchStepPerTick = 0.025f;
    [Tooltip("Pitch increment applied per sequential stat row.")]
    [SerializeField] private float pitchStepPerRow = 0.08f;
    [Tooltip("Volume multiplier for tick sounds.")]
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 0.7f;

    [Header("Timing")]
    [Tooltip("Duration in unscaled seconds for counting up a single stat row.")]
    [SerializeField] private float countUpDuration = 0.35f;
    [Tooltip("Delay in unscaled seconds between consecutive stat rows.")]
    [SerializeField] private float staggerDelay = 0.12f;

    private Coroutine sequenceCoroutine;
    private bool isPlaying = false;

    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f; // 2D UI sound
                audioSource.ignoreListenerPause = true;
            }
        }
    }

    private void OnValidate()
    {
        AutoWireRows();
    }

    /// <summary>
    ///     Automatically wires rows from children if not already assigned.
    /// </summary>
    public void AutoWireRows()
    {
        WireRow(ref timeSurvivedRow, "Time Survived");
        WireRow(ref levelReachedRow, "Level Reached");
        WireRow(ref enemiesSlainRow, "Enemies Slain");
        WireRow(ref goldCollectedRow, "Gold Collected");
        WireRow(ref damageDealtRow, "Damage Dealt");
        WireRow(ref damageTakenRow, "Damage Taken");
        WireRow(ref critsRow, "Critical Hits");
    }

    private void WireRow(ref StatRow row, string childName)
    {
        if (row == null)
        {
            row = new StatRow { statName = childName };
        }

        Transform t = transform.Find(childName);
        if (t == null) return;

        if (row.rowTransform == null)
        {
            row.rowTransform = t as RectTransform;
        }
        if (row.canvasGroup == null)
        {
            row.canvasGroup = t.GetComponent<CanvasGroup>();
        }
        if (row.valueText == null)
        {
            Transform valT = t.Find("Value");
            if (valT != null)
            {
                row.valueText = valT.GetComponent<TMP_Text>();
            }
        }
        if (row.bumpFeedback == null)
        {
            row.bumpFeedback = t.GetComponent<MMF_Player>();
        }
    }

    /// <summary>
    ///     Hides all stat rows immediately in preparation for the reveal sequence.
    /// </summary>
    public void HideAllRows()
    {
        SetRowVisible(timeSurvivedRow, false);
        SetRowVisible(levelReachedRow, false);
        SetRowVisible(enemiesSlainRow, false);
        SetRowVisible(goldCollectedRow, false);
        SetRowVisible(damageDealtRow, false);
        SetRowVisible(damageTakenRow, false);
        SetRowVisible(critsRow, false);
    }

    private void SetRowVisible(StatRow row, bool visible)
    {
        if (row == null) return;
        if (row.canvasGroup != null)
        {
            row.canvasGroup.alpha = visible ? 1f : 0f;
        }
        else if (row.rowTransform != null)
        {
            row.rowTransform.localScale = visible ? Vector3.one : Vector3.zero;
        }
    }

    /// <summary>
    ///     Starts the unscaled staggered reveal sequence.
    /// </summary>
    public void PlaySequence(float timeSurvivedSeconds, int levelReached, int enemiesSlain, int goldCollected, int damageDealt, int damageTaken, int crits)
    {
        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
        }
        sequenceCoroutine = StartCoroutine(PlaySequenceRoutine(timeSurvivedSeconds, levelReached, enemiesSlain, goldCollected, damageDealt, damageTaken, crits));
    }

    public IEnumerator PlaySequenceRoutine(float timeSurvivedSeconds, int levelReached, int enemiesSlain, int goldCollected, int damageDealt, int damageTaken, int crits)
    {
        isPlaying = true;
        HideAllRows();

        yield return new WaitForSecondsRealtime(0.1f);

        int rowIndex = 0;

        // 1. Time Survived
        yield return AnimateTimeRow(timeSurvivedRow, timeSurvivedSeconds, rowIndex++);
        yield return new WaitForSecondsRealtime(staggerDelay);

        // 2. Level Reached
        yield return AnimateNumberRow(levelReachedRow, 1, levelReached, rowIndex++, formatAsInteger: true);
        yield return new WaitForSecondsRealtime(staggerDelay);

        // 3. Enemies Slain
        yield return AnimateNumberRow(enemiesSlainRow, 0, enemiesSlain, rowIndex++, formatAsInteger: true);
        yield return new WaitForSecondsRealtime(staggerDelay);

        // 4. Gold Collected
        yield return AnimateNumberRow(goldCollectedRow, 0, goldCollected, rowIndex++, formatAsInteger: true);
        yield return new WaitForSecondsRealtime(staggerDelay);

        // 5. Damage Dealt
        yield return AnimateNumberRow(damageDealtRow, 0, damageDealt, rowIndex++, formatAsInteger: true);
        yield return new WaitForSecondsRealtime(staggerDelay);

        // 6. Damage Taken
        yield return AnimateNumberRow(damageTakenRow, 0, damageTaken, rowIndex++, formatAsInteger: true);
        yield return new WaitForSecondsRealtime(staggerDelay);

        // 7. Critical Hits
        yield return AnimateNumberRow(critsRow, 0, crits, rowIndex++, formatAsInteger: true);

        isPlaying = false;
    }

    private IEnumerator AnimateTimeRow(StatRow row, float targetSeconds, int rowIndex)
    {
        if (row == null) yield break;

        SetRowVisible(row, true);
        TriggerBump(row);

        float rowBasePitch = basePitch + (rowIndex * pitchStepPerRow);
        int finalMinutes = Mathf.FloorToInt(targetSeconds / 60f);
        int finalSecs = Mathf.FloorToInt(targetSeconds % 60f);
        string finalStr = $"{finalMinutes:00}:{finalSecs:00}";

        if (targetSeconds <= 0f)
        {
            if (row.valueText != null) row.valueText.text = "00:00";
            PlayTickSound(rowBasePitch);
            yield break;
        }

        int stepCount = 10;
        float stepInterval = countUpDuration / stepCount;

        for (int i = 1; i <= stepCount; i++)
        {
            float t = (float)i / stepCount;
            float currentSecs = Mathf.Lerp(0f, targetSeconds, t);
            int m = Mathf.FloorToInt(currentSecs / 60f);
            int s = Mathf.FloorToInt(currentSecs % 60f);

            if (row.valueText != null)
            {
                row.valueText.text = $"{m:00}:{s:00}";
            }

            float currentPitch = rowBasePitch + (i * pitchStepPerTick);
            PlayTickSound(currentPitch);

            yield return new WaitForSecondsRealtime(stepInterval);
        }

        if (row.valueText != null)
        {
            row.valueText.text = finalStr;
        }
        PlayCompleteSound(rowBasePitch + (stepCount * pitchStepPerTick) + 0.1f);
    }

    private IEnumerator AnimateNumberRow(StatRow row, int startVal, int targetVal, int rowIndex, bool formatAsInteger)
    {
        if (row == null) yield break;

        SetRowVisible(row, true);
        TriggerBump(row);

        float rowBasePitch = basePitch + (rowIndex * pitchStepPerRow);
        string finalStr = targetVal.ToString("#,##0");
        if (targetVal == 0) finalStr = "0";

        if (targetVal <= startVal)
        {
            if (row.valueText != null) row.valueText.text = finalStr;
            PlayTickSound(rowBasePitch);
            yield break;
        }

        int totalDelta = targetVal - startVal;
        int stepCount = Mathf.Clamp(totalDelta, 6, 14);
        float stepInterval = countUpDuration / stepCount;

        for (int i = 1; i <= stepCount; i++)
        {
            float t = (float)i / stepCount;
            // Smooth ease out for nice count up feel
            float easeT = Mathf.Sin(t * Mathf.PI * 0.5f);
            int currentVal = Mathf.RoundToInt(Mathf.Lerp(startVal, targetVal, easeT));

            if (row.valueText != null)
            {
                row.valueText.text = currentVal == 0 ? "0" : currentVal.ToString("#,##0");
            }

            float currentPitch = rowBasePitch + (i * pitchStepPerTick);
            PlayTickSound(currentPitch);

            yield return new WaitForSecondsRealtime(stepInterval);
        }

        if (row.valueText != null)
        {
            row.valueText.text = finalStr;
        }
        PlayCompleteSound(rowBasePitch + (stepCount * pitchStepPerTick) + 0.1f);
    }

    private void TriggerBump(StatRow row)
    {
        if (row.bumpFeedback != null)
        {
            row.bumpFeedback.PlayFeedbacks();
        }
    }

    private void PlayTickSound(float pitch)
    {
        if (audioSource == null || countUpTickSound == null) return;
        audioSource.pitch = Mathf.Clamp(pitch, 0.5f, 2.5f);
        audioSource.PlayOneShot(countUpTickSound, sfxVolume);
    }

    private void PlayCompleteSound(float pitch)
    {
        if (audioSource == null) return;
        AudioClip clip = rowCompleteSound != null ? rowCompleteSound : countUpTickSound;
        if (clip == null) return;

        audioSource.pitch = Mathf.Clamp(pitch, 0.5f, 2.5f);
        audioSource.PlayOneShot(clip, sfxVolume * 1.1f);
    }

    /// <summary>
    ///     Instantly fills all stats with their final values and stops any running animation.
    /// </summary>
    public void SkipToFinalValues(float timeSurvivedSeconds, int levelReached, int enemiesSlain, int goldCollected, int damageDealt, int damageTaken, int crits)
    {
        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = null;
        }
        isPlaying = false;

        SetRowVisible(timeSurvivedRow, true);
        SetRowVisible(levelReachedRow, true);
        SetRowVisible(enemiesSlainRow, true);
        SetRowVisible(goldCollectedRow, true);
        SetRowVisible(damageDealtRow, true);
        SetRowVisible(damageTakenRow, true);
        SetRowVisible(critsRow, true);

        int m = Mathf.FloorToInt(timeSurvivedSeconds / 60f);
        int s = Mathf.FloorToInt(timeSurvivedSeconds % 60f);
        if (timeSurvivedRow?.valueText != null) timeSurvivedRow.valueText.text = $"{m:00}:{s:00}";
        if (levelReachedRow?.valueText != null) levelReachedRow.valueText.text = levelReached.ToString();
        if (enemiesSlainRow?.valueText != null) enemiesSlainRow.valueText.text = enemiesSlain == 0 ? "0" : enemiesSlain.ToString("#,##0");
        if (goldCollectedRow?.valueText != null) goldCollectedRow.valueText.text = goldCollected == 0 ? "0" : goldCollected.ToString("#,##0");
        if (damageDealtRow?.valueText != null) damageDealtRow.valueText.text = damageDealt == 0 ? "0" : damageDealt.ToString("#,##0");
        if (damageTakenRow?.valueText != null) damageTakenRow.valueText.text = damageTaken == 0 ? "0" : damageTaken.ToString("#,##0");
        if (critsRow?.valueText != null) critsRow.valueText.text = crits == 0 ? "0" : crits.ToString("#,##0");
    }
}
