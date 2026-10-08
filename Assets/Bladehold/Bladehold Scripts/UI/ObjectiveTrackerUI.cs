using TMPro;
using UnityEngine;

/// <summary>
///     Dynamically updates the objective panel to reflect the active sector objective
///     (via <see cref="SurvivorsObjectiveManager"/>), or static guidance in scenes without one.
/// </summary>
public class ObjectiveTrackerUI : MonoBehaviour
{
    [SerializeField] private SurvivorsObjectiveManager objectiveManager;

    [Header("References")]
    [Tooltip("The text displaying the current main objective (e.g. HOLD THE GATE: WAVE X)")]
    [SerializeField] private TMP_Text objectiveHeaderText;
    
    [Tooltip("The text displaying the specific task progress (e.g. Slay all enemies: 24/26)")]
    [SerializeField] private TMP_Text objectiveProgressText;

    [Header("Scene Guidance")]
    [Tooltip("Persistent guidance shown in scenes without an objective manager.")]
    [TextArea]
    [SerializeField] private string guidanceObjectiveText;

    private bool anyError;
    private ObjectiveCountdownUI countdown;

    private void OnValidate()
    {
        if (objectiveManager == null)
        {
            objectiveManager = FindObjectOfType<SurvivorsObjectiveManager>();
        }
    }

    private void Start()
    {
        if (objectiveManager == null)
        {
            objectiveManager = SurvivorsObjectiveManager.Instance ?? FindObjectOfType<SurvivorsObjectiveManager>();
        }

        if (objectiveManager == null)
        {
            if (!string.IsNullOrWhiteSpace(guidanceObjectiveText))
            {
                if (objectiveProgressText != null)
                {
                    objectiveProgressText.text = guidanceObjectiveText;
                }
                return;
            }

            Debug.LogWarning("ObjectiveTrackerUI: no SurvivorsObjectiveManager was found in the scene.");
            anyError = true;
            return;
        }

        objectiveManager.OnObjectiveStarted += HandleSurvivorsObjectiveStarted;
        objectiveManager.OnObjectiveProgressChanged += HandleSurvivorsObjectiveProgress;
        objectiveManager.OnObjectiveCompleted += HandleSurvivorsObjectiveCompleted;
        objectiveManager.OnObjectiveFailed += HandleSurvivorsObjectiveFailed;

        // The big top-centre clock for timed objectives and the rout, built from code (no prefab wiring).
        countdown = ObjectiveCountdownUI.Create(GetComponentInParent<Canvas>(), objectiveProgressText != null ? objectiveProgressText : objectiveHeaderText);
        if (countdown == null) Debug.LogWarning("ObjectiveTrackerUI: no parent Canvas, so the top-centre objective countdown was not created.", this);

        if (objectiveManager.CurrentObjective != null)
        {
            HandleSurvivorsObjectiveStarted(objectiveManager.CurrentObjective);
        }
    }

    private void OnDestroy()
    {
        if (objectiveManager != null)
        {
            objectiveManager.OnObjectiveStarted -= HandleSurvivorsObjectiveStarted;
            objectiveManager.OnObjectiveProgressChanged -= HandleSurvivorsObjectiveProgress;
            objectiveManager.OnObjectiveCompleted -= HandleSurvivorsObjectiveCompleted;
            objectiveManager.OnObjectiveFailed -= HandleSurvivorsObjectiveFailed;
        }
        if (countdown != null) Destroy(countdown.gameObject);
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

    private void Update()
    {
        if (anyError) return;

        UpdateSurvivorsUI();
    }

    private void UpdateSurvivorsUI()
    {
        var sgm = SurvivorsGameManager.Instance ?? FindObjectOfType<SurvivorsGameManager>();
        if (sgm == null) return;
        if (objectiveManager == null) objectiveManager = SurvivorsObjectiveManager.Instance ?? FindObjectOfType<SurvivorsObjectiveManager>();


        // 2. Active rotating sub-objective / cleanup / intermission / boss status
        if (objectiveProgressText != null)
        {
            GameLoopManager loop = GameLoopManager.Instance;
            if (loop != null && !string.IsNullOrEmpty(loop.StatusText))
            {
                // Between waves (plan 15): the picked card's objective, then the Ready prompt / countdown / rout.
                WaveCard card = loop.CurrentWaveCard;
                string title = card != null && card.objective != null
                    ? card.objective.TitleText
                    : Loc.Get("wave.status.prep_header", "Prepare for wave {0}").Replace("{0}", loop.UpcomingWave.ToString());
                objectiveProgressText.text = $"[{title}]\n{loop.StatusText}";
            }
            else if (objectiveManager != null)
            {
                if (objectiveManager.Phase == SurvivorsObjectivePhase.Cleanup || objectiveManager.CurrentObjective is KillRemainingEnemiesObjective)
                {
                    if (objectiveHeaderText != null)
                    {
                        objectiveHeaderText.text = "KILL ALL REMAINING ENEMIES";
                    }
                    int alive = SurvivorsSpawner.Instance != null ? SurvivorsSpawner.Instance.AliveCount : 0;
                    objectiveProgressText.text = $"Kill all remaining enemies: {alive} left";
                }
                else if (objectiveManager.CurrentObjective != null && objectiveManager.CurrentObjective.IsActive)
                {
                    var cur = objectiveManager.CurrentObjective;
                    objectiveProgressText.text = $"[{cur.Title}]\n{cur.ProgressText}";
                }
                else
                {
                    objectiveProgressText.text = "Prepare for incoming siege objective...";
                }
            }
            else
            {
                objectiveProgressText.text = "Prepare for incoming siege objective...";
            }
        }
    }
}
