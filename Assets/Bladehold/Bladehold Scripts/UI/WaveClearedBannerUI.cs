using System.Collections;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;
using MoreMountains.Tools;

/// <summary>
///     Listens for wave clear events (in classic mode via <see cref="WaveSpawner"/>) or
///     objective completion events (in survival mode via <see cref="SurvivorsObjectiveManager"/>)
///     and pops in a banner displaying rewards/stats. Uses <see cref="MMF_Player"/> to animate the banner in and out.
/// </summary>
public class WaveClearedBannerUI : MonoBehaviour
{
    [SerializeField] private WaveSpawner spawner;
    [SerializeField] private SurvivorsObjectiveManager objectiveManager;

    [Header("References")]
    [Tooltip("The parent GameObject containing the banner visual elements. Used to hide it completely when not active.")]
    [SerializeField] private GameObject bannerRoot;
    [SerializeField] private TMP_Text questNameText;
    [SerializeField] private TMP_Text waveClearedText;
    [SerializeField] private TMP_Text goldEarnedText;
    [SerializeField] private TMP_Text enemiesKilledText;

    [Header("Animation & Juiciness")]
    [Tooltip("Animator driving the text banner entrance and exit states.")]
    [SerializeField] private Animator textAnimator;
    [Tooltip("Animator boolean parameter name to set true on open and false on close.")]
    [SerializeField] private string activeParamName = "Active";
    [Tooltip("Time in seconds before closing to set the Active parameter to false to trigger the exit animation.")]
    [SerializeField] private float activeOutLeadTime = 0.5f;
    [Tooltip("Played when the banner appears. Should handle its own reset/outro or be paired with a separate outro if needed.")]
    [SerializeField] private MMF_Player bannerAnimationFeedback;
    [SerializeField] private AudioClip[] waveClearedSounds;
    [Tooltip("How long the banner stays on screen before hiding itself.")]
    [SerializeField] private float displayDuration = 3f;

    private int goldAtWaveStart;
    private int killsAtWaveStart;
    private Coroutine hideRoutine;
    private bool anyError;

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
        if (textAnimator == null && waveClearedText != null)
        {
            textAnimator = waveClearedText.GetComponent<Animator>();
        }
        if (textAnimator == null && bannerRoot != null)
        {
            textAnimator = bannerRoot.GetComponentInChildren<Animator>();
        }
        if (textAnimator == null)
        {
            textAnimator = GetComponentInChildren<Animator>();
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
            Debug.LogWarning("WaveClearedBannerUI: Neither WaveSpawner nor SurvivorsObjectiveManager was found in the scene.");
            anyError = true;
            return;
        }

        if (bannerRoot != null)
        {
            bannerRoot.SetActive(false);
        }

        if (spawner != null)
        {
            spawner.WaveStarted += HandleWaveStarted;
            spawner.WaveCleared += HandleWaveCleared;
        }

        if (objectiveManager != null)
        {
            objectiveManager.OnObjectiveCompleted += HandleSurvivorsObjectiveCleared;
        }
    }

    private void OnDestroy()
    {
        if (spawner != null)
        {
            spawner.WaveStarted -= HandleWaveStarted;
            spawner.WaveCleared -= HandleWaveCleared;
        }

        if (objectiveManager != null)
        {
            objectiveManager.OnObjectiveCompleted -= HandleSurvivorsObjectiveCleared;
        }
    }

    private void HandleWaveStarted(int wave)
    {
        goldAtWaveStart = GameStats.Instance != null ? GameStats.Instance.GoldEarnedThisRun : 0;
        killsAtWaveStart = GameStats.Instance != null ? GameStats.Instance.GoblinsKilled : 0;
    }

    private void HandleWaveCleared(int wave)
    {
        int goldEarned = (GameStats.Instance != null ? GameStats.Instance.GoldEarnedThisRun : 0) - goldAtWaveStart;
        int kills = (GameStats.Instance != null ? GameStats.Instance.GoblinsKilled : 0) - killsAtWaveStart;

        ShowBanner($"WAVE {wave} CLEARED", null, Mathf.Max(0, goldEarned), Mathf.Max(0, kills));
    }

    private void HandleSurvivorsObjectiveCleared(ISurvivorsObjective obj)
    {
        ShowBanner("OBJECTIVE COMPLETE", obj != null ? obj.Title : null, 0, 0);
    }

    private void ShowBanner(string mainHeader, string questSubTitle, int gold, int kills)
    {
        if (waveClearedText != null)
        {
            waveClearedText.text = mainHeader;
        }

        if (questNameText != null)
        {
            questNameText.text = questSubTitle ?? string.Empty;
            questNameText.gameObject.SetActive(!string.IsNullOrEmpty(questSubTitle));
        }

        if (goldEarnedText != null)
        {
            goldEarnedText.text = gold.ToString();
            goldEarnedText.transform.parent.gameObject.SetActive(gold > 0);
        }

        if (enemiesKilledText != null)
        {
            enemiesKilledText.text = kills.ToString();
            enemiesKilledText.transform.parent.gameObject.SetActive(kills > 0);
        }

        if (bannerRoot != null)
        {
            bannerRoot.SetActive(true);
        }

        if (textAnimator != null)
        {
            textAnimator.SetBool(activeParamName, true);
        }

        if (bannerAnimationFeedback != null)
        {
            bannerAnimationFeedback.Initialization();
            bannerAnimationFeedback.PlayFeedbacks();
        }

        AudioClip clipToPlay = null;
        if (waveClearedSounds != null && waveClearedSounds.Length > 0)
        {
            clipToPlay = waveClearedSounds[Random.Range(0, waveClearedSounds.Length)];
        }
#if UNITY_EDITOR
        if (clipToPlay == null)
        {
            clipToPlay = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Audio/Bells/chime_bell_10.wav");
        }
#endif
        if (clipToPlay != null)
        {
            MMSoundManagerPlayOptions options = MMSoundManagerPlayOptions.Default;
            options.MmSoundManagerTrack = MMSoundManager.MMSoundManagerTracks.UI;
            options.Location = transform.position;
            options.Volume = 0.9f;
            options.Pitch = Random.Range(0.95f, 1.05f);
            MMSoundManagerSoundPlayEvent.Trigger(clipToPlay, options);
        }

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
        }
        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        float mainWait = Mathf.Max(0f, displayDuration - activeOutLeadTime);
        yield return new WaitForSecondsRealtime(mainWait);

        if (textAnimator != null)
        {
            textAnimator.SetBool(activeParamName, false);
        }

        yield return new WaitForSecondsRealtime(activeOutLeadTime);

        if (bannerRoot != null)
        {
            bannerRoot.SetActive(false);
        }
        hideRoutine = null;
    }
}
