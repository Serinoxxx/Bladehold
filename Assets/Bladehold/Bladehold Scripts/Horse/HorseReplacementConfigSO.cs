using UnityEngine;

/// <summary>
///     The one price for a replacement warhorse bought in the field: the gate's <see cref="HorseStall" />
///     and the "buy a new horse?" prompt that the Summon Mount button opens while the horse is dead
///     (<see cref="HorseReplacementDialog" />) both read <see cref="HorseReplacement.GoldCost" />, which
///     loads this asset from <c>Resources/HorseReplacementConfig</c>. (The Rest Area shop's Replacement
///     Warhorse is a separate <see cref="ShopItemSO" /> with its own price.)
/// </summary>
[CreateAssetMenu(fileName = "HorseReplacementConfig", menuName = "Scriptable Objects/Horse/Horse Replacement Config")]
public class HorseReplacementConfigSO : ScriptableObject
{
    [Tooltip("In-run gold for a new warhorse at the stall or from the mount-button prompt.")]
    [Min(0)] public int goldCost = 500;
}

/// <summary>
///     Static access to the field replacement-warhorse purchase: the shared price, when it may be bought
///     (<see cref="CanBuyNow" />) and the purchase itself, so the stall and the mount-button prompt can't drift apart.
/// </summary>
public static class HorseReplacement
{
    public const string ResourcePath = "HorseReplacementConfig";
    /// <summary>Used only when the Resources asset is missing (logged once).</summary>
    public const int FallbackGoldCost = 500;

    private static HorseReplacementConfigSO config;
    private static bool loggedMissing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        config = null;
        loggedMissing = false;
    }

    /// <summary>The replacement price in in-run gold.</summary>
    public static int GoldCost
    {
        get
        {
            if (config == null)
            {
                config = Resources.Load<HorseReplacementConfigSO>(ResourcePath);
                if (config == null && !loggedMissing)
                {
                    loggedMissing = true;
                    Debug.LogError($"[HorseReplacement] No HorseReplacementConfigSO at Resources/{ResourcePath}; using {FallbackGoldCost}g.");
                }
            }
            return config != null ? Mathf.Max(0, config.goldCost) : FallbackGoldCost;
        }
    }

    /// <summary>True while the run's warhorse is dead and the player has the gold for a new one.</summary>
    public static bool CanAfford => RunSession.MountLost && (GoldCost <= 0 || RunSession.InRunGold >= GoldCost);

    /// <summary>
    ///     The one timing rule for field purchases (stall, mount-button prompt and HUD tag all read it): a new
    ///     horse can be bought between waves (<see cref="GameLoopManager.IsPrepPhase" />, or whenever no wave is
    ///     running, including scenes without a <see cref="GameLoopManager" />), and mid-wave only with the
    ///     Field Stables meta perk (<see cref="RunSession.FieldStablesPerkId" />). When false,
    ///     <paramref name="reason" /> is the localized line to show the player; otherwise it is null.
    ///     Says nothing about gold or whether the horse is dead (<see cref="CanAfford" />).
    /// </summary>
    public static bool CanBuyNow(out string reason)
    {
        reason = null;
        if (IsBuyWindowOpen) return true;
        reason = Loc.Get("horse_replace.between_waves_only", "Only between waves");
        return false;
    }

    /// <summary><see cref="CanBuyNow" /> without the reason string (cheap enough to poll every frame).</summary>
    public static bool IsBuyWindowOpen
    {
        get
        {
            GameLoopManager loop = GameLoopManager.Instance;
            if (loop == null || loop.IsPrepPhase || !loop.IsWaveActive) return true;
            return RunSession.HasMetaPerk(RunSession.FieldStablesPerkId);
        }
    }

    /// <summary>
    ///     Spends the gold and replaces the horse (<see cref="RunSession.ReplaceMount" />: full health and charge,
    ///     summonable again). False, spending nothing, when the horse is alive, gold is short, or a wave is
    ///     running without the Field Stables perk (<see cref="CanBuyNow" />).
    /// </summary>
    public static bool TryBuy()
    {
        if (!RunSession.MountLost) return false;
        if (!IsBuyWindowOpen) return false;
        if (!RunSession.TrySpendInRunGold(GoldCost)) return false;
        RunSession.ReplaceMount();
        return true;
    }
}
