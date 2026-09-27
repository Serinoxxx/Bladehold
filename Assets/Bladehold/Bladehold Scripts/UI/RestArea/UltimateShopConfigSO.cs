using UnityEngine;

/// <summary>
///     Prices for ultimates sold at the Rest Area shop. The shop offers the ultimates of your equipped weapons
///     while you have a free slot: one slot per run, two with the second_ultimate meta perk.
/// </summary>
[CreateAssetMenu(fileName = "UltimateShopConfigSO", menuName = "Scriptable Objects/UltimateShopConfigSO")]
public class UltimateShopConfigSO : ScriptableObject
{
    [Tooltip("Gold price of the run's first ultimate.")]
    [Min(0)] public int firstUltimateCost = 100;

    [Tooltip("Gold price of the second ultimate (needs the second_ultimate meta perk).")]
    [Min(0)] public int secondUltimateCost = 400;

    public int CostForNextUltimate(int ownedUltimates) => ownedUltimates <= 0 ? firstUltimateCost : secondUltimateCost;
}
