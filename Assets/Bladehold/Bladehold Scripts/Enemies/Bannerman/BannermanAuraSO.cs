using HighlightPlus;
using UnityEngine;

[CreateAssetMenu(fileName = "BannermanAuraSO", menuName = "Scriptable Objects/BannermanAuraSO")]
public class BannermanAuraSO : ScriptableObject
{
    [Header("Aura Tuning")]
    [Tooltip("Radius around the Bannerman where friendly enemies receive the active wave buff.")]
    public float auraRadius = 8f;

    [Tooltip("How frequently (in seconds) the aura scans for nearby units.")]
    public float checkInterval = 0.2f;

    [Tooltip("Default buff type applied if no active wave banner buff is selected in GameLoopManager.")]
    public BannerBuffType defaultBuffType = BannerBuffType.Berserk;

    [Header("Buff Magnitudes")]
    [Tooltip("Attack damage multiplier applied to units with the Damage/Berserk buff.")]
    public float damageBuffMultiplier = 1.5f;

    [Tooltip("Health regenerated per second for units with the Healing/Regen buff.")]
    public float healingPerSecond = 3f;

    [Tooltip("Bonus health/shield fraction applied to units with the Shield buff.")]
    public float shieldFraction = 0.3f;

    [Header("Banner Tuning")]
    [Tooltip("Maximum health of the destructible banner held above his head.")]
    public float bannerHealth = 25f;

    [Header("Highlight Profiles")]
    [Tooltip("Highlight profile with red glow applied to units with the Damage/Berserk buff.")]
    public HighlightProfile damageBuffProfile;

    [Tooltip("Highlight profile with green glow applied to units with the Healing/Regen buff.")]
    public HighlightProfile healingBuffProfile;

    [Tooltip("Highlight profile with yellow glow applied to units with the Shield buff.")]
    public HighlightProfile shieldBuffProfile;
}
