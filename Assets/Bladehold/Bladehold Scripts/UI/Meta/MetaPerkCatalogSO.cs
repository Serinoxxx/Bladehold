using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Every meta perk in the game. Lives at Resources/MetaPerkCatalog so run code can read a perk's
///     per-rank values by id (<see cref="RunSession.GetMetaPerkValue" />) and the Spirit's shop UI can list them.
/// </summary>
[CreateAssetMenu(fileName = "MetaPerkCatalog", menuName = "Scriptable Objects/MetaPerkCatalogSO")]
public class MetaPerkCatalogSO : ScriptableObject
{
    public const string ResourcePath = "MetaPerkCatalog";

    [SerializeField] private List<MetaPerkDefinitionSO> perks = new List<MetaPerkDefinitionSO>();

    private static MetaPerkCatalogSO cached;

    public IReadOnlyList<MetaPerkDefinitionSO> Perks => perks;

    public static MetaPerkCatalogSO Instance
    {
        get
        {
            if (cached == null) cached = Resources.Load<MetaPerkCatalogSO>(ResourcePath);
            return cached;
        }
    }

    public MetaPerkDefinitionSO Find(string perkId)
    {
        foreach (MetaPerkDefinitionSO perk in perks)
        {
            if (perk != null && perk.id == perkId) return perk;
        }
        return null;
    }
}
