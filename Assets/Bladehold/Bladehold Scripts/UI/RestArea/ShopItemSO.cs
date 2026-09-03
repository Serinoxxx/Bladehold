using UnityEngine;

public enum ShopItemEffectType
{
    HealInstant,
    MaxHealthRun,
    MoveSpeedTemporary,
    WaveEndHealTemporary
}

/// <summary>
///     Configurable Rest Area Shop item definition.
/// </summary>
[CreateAssetMenu(fileName = "ShopItemSO", menuName = "Scriptable Objects/ShopItemSO")]
public class ShopItemSO : ScriptableObject
{
    public string itemId;
    public string displayName;
    [TextArea(2, 4)] public string description;
    public int goldCost = 10;
    public Sprite icon;
    public ShopItemEffectType effectType;
    public float effectValue = 5f;
    public int durationWaves = 5;
}
