using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     One switch for a scene that is both a tutorial scene and a campaign sector (plan 21: the Valley
///     Stronghold is T3 of the tutorial and campaign node 1). Runs before every other Awake in the scene
///     and picks the mode once:
///     <list type="bullet">
///         <item><b>Campaign</b>: a campaign run deployed to a node. Every <see cref="tutorialOnly" /> object
///         (the <see cref="TutorialDirector" />, steps, encounters, carrots, the tutorial
///         <see cref="SceneAbilityRules" />) is switched off before its Awake, and the wave loop gets
///         <see cref="campaignPacing" /> (wave drafts and clans on).</item>
///         <item><b>Tutorial</b>: everything else (arriving from T2, or the scene played straight from the
///         Editor). The scene stays as saved (<see cref="campaignOnly" /> objects are saved inactive) and the
///         loop gets <see cref="tutorialPacing" />.</item>
///     </list>
/// </summary>
[DefaultExecutionOrder(-1000)]
public class TutorialSceneMode : MonoBehaviour
{
    [Header("Objects")]
    [Tooltip("Switched off in campaign mode: the TutorialDirector, the Tutorial step root, the tutorial SceneAbilityRules.")]
    [SerializeField] private List<GameObject> tutorialOnly = new List<GameObject>();
    [Tooltip("Saved inactive; switched on in campaign mode. Optional.")]
    [SerializeField] private List<GameObject> campaignOnly = new List<GameObject>();

    [Header("Wave pacing")]
    [SerializeField] private GameLoopManager gameLoop;
    [SerializeField] private SurvivorsSpawner spawner;
    [Tooltip("Tutorial mode: TutorialRoundPacingConfig (fixed kill-quota waves, no drafts, no clans).")]
    [SerializeField] private RoundPacingConfigSO tutorialPacing;
    [Tooltip("Campaign mode: a normal sector pacing config.")]
    [SerializeField] private RoundPacingConfigSO campaignPacing;

    /// <summary>True while a scene with this switch runs in tutorial mode. False in every other scene.</summary>
    public static bool TutorialMode { get; private set; }

    /// <summary>A campaign run deployed to a node. Read from <see cref="RunSession" /> so no manager gets spawned.</summary>
    public static bool IsCampaignDeploy => RunSession.IsCampaignRun && !string.IsNullOrEmpty(RunSession.CampaignCurrentNodeId);

    private void OnValidate()
    {
        if (gameLoop == null) gameLoop = FindAnyObjectByType<GameLoopManager>();
        if (spawner == null) spawner = FindAnyObjectByType<SurvivorsSpawner>();
    }

    private void Awake()
    {
        bool anyError = false;
        if (gameLoop == null) { Debug.LogError("[TutorialSceneMode] gameLoop is not assigned.", this); anyError = true; }
        if (spawner == null) { Debug.LogError("[TutorialSceneMode] spawner is not assigned.", this); anyError = true; }
        if (tutorialPacing == null) { Debug.LogError("[TutorialSceneMode] tutorialPacing is not assigned.", this); anyError = true; }
        if (campaignPacing == null) { Debug.LogError("[TutorialSceneMode] campaignPacing is not assigned.", this); anyError = true; }

        // A campaign deploy wins over a stale tutorial flag (the tutorial left through the main menu).
        TutorialMode = !IsCampaignDeploy;
        if (!TutorialMode)
        {
            TutorialRun.End();
            SetAll(tutorialOnly, false);
            SetAll(campaignOnly, true);
        }
        if (anyError) return;

        RoundPacingConfigSO pacing = TutorialMode ? tutorialPacing : campaignPacing;
        gameLoop.UsePacingConfig(pacing);
        spawner.UsePacingConfig(pacing);
    }

    private void OnDestroy()
    {
        TutorialMode = false;
    }

    private static void SetAll(List<GameObject> objects, bool active)
    {
        foreach (GameObject go in objects)
        {
            if (go != null) go.SetActive(active);
        }
    }
}
