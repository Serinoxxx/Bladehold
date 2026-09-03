using UnityEngine;

/// <summary>
///     Definition of a permanent meta-progression perk purchased with Goblin Blood.
/// </summary>
[CreateAssetMenu(fileName = "MetaPerkDefinitionSO", menuName = "Scriptable Objects/MetaPerkDefinitionSO")]
public class MetaPerkDefinitionSO : ScriptableObject
{
    public string id = "backstab";
    public string displayName = "Backstab";
    [TextArea(2, 4)] public string description = "Deal +20% bonus damage when striking enemies from behind.";
    public int tier = 1;
    public int goblinBloodCost = 10;
    public Sprite icon;
}
