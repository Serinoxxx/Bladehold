using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Picks the 3 cards a draft offers, from <see cref="DraftUpgradeService" /> (DraftUpgrades.csv),
///     and tracks cards banished for the rest of the run. Cards are handed to the UI as
///     <see cref="SkillNode" />s (<see cref="DraftUpgradeService.ConvertToSkillNode" />).
/// </summary>
public class SurvivorsCardSelector : MonoBehaviour
{
    public static SurvivorsCardSelector Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private readonly HashSet<string> banishedNodeIds = new HashSet<string>();

    /// <summary>
    ///     Returns up to <paramref name="count"/> candidate cards for <paramref name="category"/>
    ///     (Weapon when null), skipping banished cards and anything in <paramref name="excludeList"/>.
    /// </summary>
    public List<SkillNode> GetRandomSkillCards(int count = 3, List<SkillNode> excludeList = null, DraftCategory? category = null)
    {
        List<SkillNode> resultNodes = new List<SkillNode>();
        DraftUpgradeService draftService = DraftUpgradeService.GetOrCreateInstance();
        if (draftService == null || draftService.AllDefinitions.Count == 0)
        {
            Debug.LogError("[SurvivorsCardSelector] DraftUpgradeService has no draft definitions to offer.");
            return resultNodes;
        }

        HashSet<string> excludeIds = new HashSet<string>(banishedNodeIds);
        if (excludeList != null)
        {
            foreach (var ex in excludeList)
            {
                if (ex != null && !string.IsNullOrEmpty(ex.id)) excludeIds.Add(ex.id);
            }
        }

        List<DraftUpgradeDefinition> draftCandidates = draftService.GetCandidateUpgrades(category ?? DraftCategory.Weapon, count, excludeIds);
        if (draftCandidates != null)
        {
            foreach (var def in draftCandidates)
            {
                resultNodes.Add(draftService.ConvertToSkillNode(def));
            }
        }
        return resultNodes;
    }

    /// <summary>
    ///     Fetches a single replacement card that is eligible and not currently displayed.
    /// </summary>
    public SkillNode GetSingleReplacementCard(List<SkillNode> currentOffered, DraftCategory? category = null)
    {
        List<SkillNode> choices = GetRandomSkillCards(1, currentOffered, category);
        return choices.Count > 0 ? choices[0] : null;
    }

    /// <summary>
    ///     Permanently banishes a card from being offered again for the rest of the current run.
    /// </summary>
    public bool BanishCard(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        return banishedNodeIds.Add(id);
    }
}
