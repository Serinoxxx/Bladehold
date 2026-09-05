using UnityEngine;

/// <summary>
///     ScriptableObject defining a Clan and its associated enemy buff modifier for War Banners.
///     Enables designers to easily modify clan buffs, quick facts, icons, and magnitude values in the Inspector.
/// </summary>
[CreateAssetMenu(fileName = "ClanBuff_", menuName = "Scriptable Objects/War Banner/Clan Buff")]
public class WarBannerClanSO : ScriptableObject
{
    [Header("Clan Identity")]
    [Tooltip("Display name of the clan (e.g. 'Swarm-Blight Clan').")]
    public string clanName = "Clan Name";

    [Tooltip("Sigil or badge sprite representing this clan.")]
    public Sprite clanIcon;

    [Tooltip("Gameplay buff type applied to enemies when this banner is active.")]
    public BannerBuffType buffType = BannerBuffType.None;

    [Header("Quick Fact (1 Line)")]
    [Tooltip("Punchy single-line description of the buff shown on the banner (e.g. 'Enemies heal 2 HP/s').")]
    public string quickFact = "Enemies heal 2 HP/s";

    [Header("Buff Balance / Magnitude")]
    [Tooltip("Tunable magnitude scalar passed to enemy buff logic (e.g. 2.0 for 2 HP/s heal, 1.25 for +25% shield, 1.35 for +35% speed, 0.25 for 25% damage reduction).")]
    public float buffMagnitude = 1f;

    [Header("Designer Notes")]
    [TextArea(2, 4)]
    [Tooltip("Extended description or lore notes for designers.")]
    public string detailedEffect = "";
}
