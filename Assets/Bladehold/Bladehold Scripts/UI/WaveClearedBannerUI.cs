using System.Collections;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;

/// <summary>
///     Listens for objective start/complete/fail events (via <see cref="SurvivorsObjectiveManager"/>)
///     and pops in a banner displaying rewards/stats. Uses <see cref="MMF_Player"/> to animate the banner in and out.
/// </summary>
public class WaveClearedBannerUI : MonoBehaviour
{
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
    [Tooltip("Played when the banner announces a cleared wave (random chime, UI track).")]
    [SerializeField] private MMF_Player waveClearedFeedback;
    [Tooltip("Played when the banner announces a new quest/challenge (horn, UI track).")]
    [SerializeField] private MMF_Player newQuestFeedback;
    [Tooltip("How long the banner stays on screen before hiding itself.")]
    [SerializeField] private float displayDuration = 3f;

    private Coroutine hideRoutine;
    private bool anyError;

    private void OnValidate()
    {
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

    private void Awake()
    {
        EnsureTextReferences();
        if (bannerRoot != null)
        {
            bannerRoot.SetActive(false);
        }
    }

    private void Start()
    {
        EnsureTextReferences();
        if (waveClearedFeedback == null) Debug.LogError("WaveClearedBannerUI: waveClearedFeedback is not assigned.", this);
        if (newQuestFeedback == null) Debug.LogError("WaveClearedBannerUI: newQuestFeedback is not assigned.", this);

        if (objectiveManager == null)
        {
            objectiveManager = SurvivorsObjectiveManager.Instance ?? FindObjectOfType<SurvivorsObjectiveManager>();
        }

        if (objectiveManager == null)
        {
            Debug.LogWarning("WaveClearedBannerUI: no SurvivorsObjectiveManager was found in the scene.");
            anyError = true;
            return;
        }

        if (bannerRoot != null)
        {
            bannerRoot.SetActive(false);
        }

        objectiveManager.OnObjectiveStarted += HandleSurvivorsObjectiveStarted;
        objectiveManager.OnObjectiveCompleted += HandleSurvivorsObjectiveCleared;
        objectiveManager.OnObjectiveFailed += HandleSurvivorsObjectiveFailed;

        // If an objective was already active before Start (e.g. introductory objective), announce it
        if (objectiveManager.CurrentObjective != null && objectiveManager.CurrentObjective.IsActive)
        {
            HandleSurvivorsObjectiveStarted(objectiveManager.CurrentObjective);
        }
    }

    private void OnDestroy()
    {
        if (objectiveManager != null)
        {
            objectiveManager.OnObjectiveStarted -= HandleSurvivorsObjectiveStarted;
            objectiveManager.OnObjectiveCompleted -= HandleSurvivorsObjectiveCleared;
            objectiveManager.OnObjectiveFailed -= HandleSurvivorsObjectiveFailed;
        }
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
        Debug.Log($"[WaveClearedBannerUI] ShowBanner: Header='{mainHeader}', Sub='{questSubTitle}', gold={gold}, kills={kills}, isNewQuest={isNewQuest}");
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

        MMF_Player sound = isNewQuest ? newQuestFeedback : waveClearedFeedback;
        if (sound != null)
        {
            sound.PlayFeedbacks(transform.position);
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
