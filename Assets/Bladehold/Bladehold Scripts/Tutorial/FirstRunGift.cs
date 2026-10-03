using UnityEngine;

/// <summary>
///     The onboarding payout: the first time a run ends (a defeat, or the campaign or demo ending) the
///     save gets <see cref="TutorialConfigSO.firstRunGiftGoblinBlood" /> Goblin Blood and
///     <see cref="TutorialConfigSO.firstRunGiftOrcishMetal" /> Orcish Metal, enough for a new weapon and
///     a few tier-1 perks. Paid once per save (<see cref="SaveData.firstRunGiftGranted" />); the Meta Area's
///     <see cref="FirstTimeHintsWatcher" /> then points the player at the weapon rack and the Spirit.
/// </summary>
public static class FirstRunGift
{
    public static bool IsGranted => SaveSystem.Load().firstRunGiftGranted;

    /// <summary>Pays the gift if this save hasn't had it yet. Call where a run ends, before the Meta Area loads.</summary>
    public static void TryGrant()
    {
        SaveData data = SaveSystem.Load();
        if (data.firstRunGiftGranted) return;

        TutorialConfigSO config = TutorialConfigSO.Load();
        if (config == null)
        {
            Debug.LogError("[FirstRunGift] No TutorialConfig asset in Resources. Skipping the first-run gift.");
            return;
        }

        // Latch first, so a failure further down can't pay it twice.
        data.firstRunGiftGranted = true;
        SaveSystem.Save(data);

        RunSession.AddGoblinBlood(config.firstRunGiftGoblinBlood);
        RunSession.AddOrcishMetal(config.firstRunGiftOrcishMetal);
        Debug.Log($"[FirstRunGift] Granted {config.firstRunGiftGoblinBlood} Goblin Blood and {config.firstRunGiftOrcishMetal} Orcish Metal.");
    }
}
