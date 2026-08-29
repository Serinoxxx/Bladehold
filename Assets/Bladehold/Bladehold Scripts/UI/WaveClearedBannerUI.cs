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

    [Header("Headers")]
    [Tooltip("Header text displayed when a new quest/challenge starts.")]
    [SerializeField] private string newQuestHeader = "NEW QUEST";
    [Tooltip("Header text displayed when an objective/quest is completed.")]
    [SerializeField] private string questCompletedHeader = "QUEST COMPLETE";
    [Tooltip("Header text displayed when an objective/quest fails.")]
    [SerializeField] private string questFailedHeader = "OBJECTIVE FAILED";

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
    [Tooltip("Sound played when announcing a new quest/challenge.")]
    [SerializeField] private AudioClip[] newQuestSounds;
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
        EnsureTextReferences();
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

    private void EnsureTextReferences()
    {
        if (bannerRoot == null)
        {
            bannerRoot = gameObject;
        }

        if (waveClearedText == null || questNameText == null || waveClearedText == questNameText)
        {
            var allTexts = bannerRoot.GetComponentsInChildren<TMP_Text>(true);
            var questCompleteLabel = System.Array.Find(allTexts, t => t.name == "Label_QuestComplete");
            var questNameLabel = System.Array.Find(allTexts, t => t.name == "Label_QuestName");

            if (questNameText == null && questNameLabel != null)
            {
                questNameText = questNameLabel;
            }

            if (questCompleteLabel != null && (waveClearedText == null || waveClearedText == questNameText))
            {
                waveClearedText = questCompleteLabel;
            }
        }
    }

    private void Start()
    {
        EnsureTextReferences();

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
            objectiveManager.OnObjectiveStarted += HandleSurvivorsObjectiveStarted;
            objectiveManager.OnObjectiveCompleted += HandleSurvivorsObjectiveCleared;
            objectiveManager.OnObjectiveFailed += HandleSurvivorsObjectiveFailed;

            // If an objective was already active before Start (e.g. introductory objective), announce it
            if (objectiveManager.CurrentObjective != null && objectiveManager.CurrentObjective.IsActive)
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
        }

        if (objectiveManager != null)
        {
            objectiveManager.OnObjectiveStarted -= HandleSurvivorsObjectiveStarted;
            objectiveManager.OnObjectiveCompleted -= HandleSurvivorsObjectiveCleared;
            objectiveManager.OnObjectiveFailed -= HandleSurvivorsObjectiveFailed;
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

        ShowBanner($"WAVE {wave} CLEARED", null, Mathf.Max(0, goldEarned), Mathf.Max(0, kills), isNewQuest: false);
    }

    private void HandleSurvivorsObjectiveStarted(ISurvivorsObjective obj)
    {
        if (obj == null) return;
        ShowBanner(newQuestHeader, obj.Title, 0, 0, isNewQuest: true);
    }

    private void HandleSurvivorsObjectiveCleared(ISurvivorsObjective obj)
    {
        ShowBanner(questCompletedHeader, obj != null ? obj.Title : null, 0, 0, isNewQuest: false);
    }

    private void HandleSurvivorsObjectiveFailed(ISurvivorsObjective obj)
    {
        ShowBanner(questFailedHeader, obj != null ? obj.Title : null, 0, 0, isNewQuest: false);
    }

    private void ShowBanner(string mainHeader, string questSubTitle, int gold, int kills, bool isNewQuest = false)
    {
        if (!string.IsNullOrEmpty(questSubTitle))
        {
            if (waveClearedText != null)
            {
                waveClearedText.text = mainHeader;
                waveClearedText.gameObject.SetActive(true);
            }

            if (questNameText != null)
            {
                questNameText.text = questSubTitle;
                questNameText.gameObject.SetActive(true);
            }
            else if (waveClearedText != null)
            {
                waveClearedText.text = $"{mainHeader}: {questSubTitle}";
            }
        }
        else
        {
            // Classic wave cleared (no subtitle)
            if (questNameText != null && waveClearedText != null && waveClearedText.name == "Label_QuestComplete")
            {
                waveClearedText.text = "WAVE CLEARED";
                waveClearedText.gameObject.SetActive(true);
                questNameText.text = mainHeader;
                questNameText.gameObject.SetActive(true);
            }
            else
            {
                if (waveClearedText != null)
                {
                    waveClearedText.text = mainHeader;
                    waveClearedText.gameObject.SetActive(true);
                }
                if (questNameText != null)
                {
                    questNameText.gameObject.SetActive(false);
                }
            }
        }

        if (goldEarnedText != null)
        {
            goldEarnedText.text = gold.ToString();
            if (goldEarnedText.transform.parent != null)
            {
                goldEarnedText.transform.parent.gameObject.SetActive(gold > 0);
            }
        }

        if (enemiesKilledText != null)
        {
            enemiesKilledText.text = kills.ToString();
            if (enemiesKilledText.transform.parent != null)
            {
                enemiesKilledText.transform.parent.gameObject.SetActive(kills > 0);
            }
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
        if (isNewQuest && newQuestSounds != null && newQuestSounds.Length > 0)
        {
            clipToPlay = newQuestSounds[Random.Range(0, newQuestSounds.Length)];
        }
        else if (!isNewQuest && waveClearedSounds != null && waveClearedSounds.Length > 0)
        {
            clipToPlay = waveClearedSounds[Random.Range(0, waveClearedSounds.Length)];
        }
#if UNITY_EDITOR
        if (clipToPlay == null)
        {
            if (isNewQuest)
            {
                clipToPlay = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Audio/battle_viking_horn_call_far_03.wav");
            }
            if (clipToPlay == null)
            {
                clipToPlay = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Audio/Bells/chime_bell_10.wav");
            }
        }
#endif
        if (clipToPlay == null && waveClearedSounds != null && waveClearedSounds.Length > 0)
        {
            clipToPlay = waveClearedSounds[Random.Range(0, waveClearedSounds.Length)];
        }

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
