using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Serializable snapshot of persisted player progress, written to disk by <see cref="SaveSystem" />.
///     Add new fields here as more progress needs saving; existing saves load missing fields as their
///     defaults. Every field belongs to exactly one of <see cref="ResetProgress" /> (gold/upgrades) or
///     <see cref="ResetSettings" /> (player-facing options) — add new fields to the matching reset so
///     "Delete Save" and "Reset Settings" keep wiping only their own half.
/// </summary>
[Serializable]
public class SaveData
{
    /// <summary>Permanent currency obtained from waves and drops, used to purchase permanent perks.</summary>
    public int goblinBlood;

    /// <summary>Permanent currency obtained from special enemies and drops, used to unlock weapons & tiers.</summary>
    public int orcishMetal;

    /// <summary>Permanent currency obtained from killing Diamond Fish in the Fishing Minigame, used for Fisherman gear.</summary>
    public int diamondFishBones;

    /// <summary>Permanently unlocked weapons. Sword and Bow are unlocked by default.</summary>
    public List<string> unlockedWeapons = new List<string> { "sword", "bow" };

    /// <summary>Currently equipped melee weapon id (e.g. 'sword', 'axe').</summary>
    public string equippedMeleeWeapon = "sword";

    /// <summary>Currently equipped ranged weapon id (e.g. 'bow', 'throwing_axe').</summary>
    public string equippedRangedWeapon = "bow";

    /// <summary>Currently equipped armour set id (e.g. 'default_armour').</summary>
    public string equippedArmourSet = "default_armour";

    /// <summary>Permanently unlocked armour sets. 'default_armour' is unlocked by default.</summary>
    public List<string> unlockedArmourSets = new List<string> { "default_armour" };

    /// <summary>Currently equipped mount id (e.g. 'basic_horse').</summary>
    public string equippedMount = "basic_horse";

    /// <summary>Permanently unlocked mounts. 'basic_horse' is unlocked by default.</summary>
    public List<string> unlockedMounts = new List<string> { "basic_horse" };

    /// <summary>Highest unlocked meta-progression tier (1 = default, 2 = costs 5 Orcish Metal, 3 = costs 10 Orcish Metal).</summary>
    public int unlockedMetaTier = 1;

    /// <summary>Purchased permanent meta perks (e.g. 'backstab', 'agility', 'regeneration').</summary>
    public List<string> purchasedMetaPerks = new List<string>();

    /// <summary>
    ///     True once the first-launch tutorial is done (set on entering its last scene) or skipped.
    ///     New Game routes to the tutorial while this is false. Saves written before the tutorial
    ///     existed load as completed (see <see cref="SaveSystem.Load" />).
    /// </summary>
    public bool tutorialCompleted;

    /// <summary>Ids of the one-time first-encounter hints already shown (see <see cref="FirstTimeHints" />).</summary>
    public List<string> seenHints = new List<string>();

    /// <summary>True once the first run's end has paid out the onboarding gift (see <see cref="FirstRunGift" />).</summary>
    public bool firstRunGiftGranted;

    /// <summary>When this save slot was started (UTC <see cref="DateTime.Ticks" />; 0 = unknown).</summary>
    public long createdUtcTicks;

    /// <summary>When this save slot was last written during play (UTC <see cref="DateTime.Ticks" />).</summary>
    public long lastPlayedUtcTicks;

    /// <summary>Real seconds spent with this slot active, banked by <see cref="SaveSystem" /> on every save.</summary>
    public double playTimeSeconds;

    /// <summary>
    ///     Total runs attempted across all sessions (default: 1). Read by the war-banner difficulty roll
    ///     (<see cref="BannerDifficultyHelper.RollTierForBanner" />), but nothing increments it yet, so
    ///     banners only ever roll Standard.
    /// </summary>
    public int runsAttempted = 1;

    /// <summary>Linear 0-1 volumes applied by <see cref="GameSettingsService" />.</summary>
    public float masterVolume = 0.5f;
    public float musicVolume = 0.5f;
    public float sfxVolume = 0.5f;

    /// <summary>
    ///     Max ragdolls simulating at once (0-50), applied by <see cref="GameSettingsService" /> to
    ///     <see cref="EnemyRagdoll.MaxActive" />. Trades physics fidelity for performance — kills/flings
    ///     beyond this cap fall back to a normal animated death/knockdown.
    /// </summary>
    public int maxRagdolls = 12;

    /// <summary>
    ///     Mouse look sensitivity: a straight multiplier on the raw mouse delta. 0.1 is the authored
    ///     default; the settings slider spans <see cref="MinMouseSensitivity" />-<see cref="MaxMouseSensitivity" />
    ///     (never 0, which would disable mouse look). Saves from before <see cref="settingsVersion" /> 1 used
    ///     a 0.5 default that testers found far too fast; <see cref="MigrateSettings" /> moves them over.
    /// </summary>
    public float mouseSensitivity = 0.1f;
    public const float MinMouseSensitivity = 0.01f;
    public const float MaxMouseSensitivity = 1f;

    /// <summary>
    ///     Version of the settings half of the save, bumped when a settings default or range changes in a
    ///     way old saves need moving over (see <see cref="MigrateSettings" />). A file with no such key is
    ///     treated as version 0 by <see cref="SaveSystem" />.
    /// </summary>
    public int settingsVersion = CurrentSettingsVersion;
    public const int CurrentSettingsVersion = 1;
    public bool invertLookX;
    public bool invertLookY;

    /// <summary>
    ///     Gameplay camera field of view in degrees, applied by <see cref="GameSettingsService" /> via
    ///     <see cref="BowAimCamera.SetRestingFieldOfView" />.
    /// </summary>
    public float fieldOfView = 90f;

    /// <summary>Global game speed multiplier (0.1 - 2.0).</summary>
    public float gameSpeed = 1f;


    /// <summary>
    ///     Serialized Input System binding overrides for the vendored gameplay Controls asset (button
    ///     remapping), produced by <see cref="InputSettingsBinder.SaveBindingOverridesToJson" />. Empty
    ///     string means no overrides — every binding stays at its authored default.
    /// </summary>
    public string inputBindingOverridesJson = "";

    /// <summary>
    ///     UI language code ("en", "fr", … — see <see cref="Loc.SupportedLanguages" />). Empty string
    ///     means auto-detect from <see cref="UnityEngine.Application.systemLanguage" />.
    /// </summary>
    public string languageCode = "";

    /// <summary>
    ///     Gamepad right-stick look speed in degrees per second at full deflection, applied by
    ///     <see cref="GameSettingsService" /> via <see cref="InputSettingsBinder.ApplyGamepadSensitivity" />.
    ///     Separate from <see cref="mouseSensitivity" /> because stick input is a held ±1 value scaled by
    ///     time, not a per-frame pixel delta.
    /// </summary>
    public float gamepadLookSensitivity = 180f;

    /// <summary>
    ///     Thumbstick dead zone: the fraction of stick deflection ignored before input registers, applied to
    ///     every stick (movement, look, wheels, menus) by <see cref="GameSettingsService" />.
    /// </summary>
    public float stickDeadzone = 0.05f;
    public const float MinStickDeadzone = 0f;
    public const float MaxStickDeadzone = 0.5f;

    /// <summary>
    ///     Controller aim assist while aiming a ranged weapon (0 = off, 1 = full): how hard the view is
    ///     slowed over, and pulled toward, an enemy's head. Applied via <see cref="InputSettingsBinder.ApplyAimAssist" />.
    /// </summary>
    public float aimAssistStrength = 0.5f;
    public const float MinAimAssistStrength = 0f;
    public const float MaxAimAssistStrength = 1f;

    /// <summary>Aim assist window: how many degrees off the crosshair a head can be and still be assisted.</summary>
    public float aimAssistWindow = 6f;
    public const float MinAimAssistWindow = 2f;
    public const float MaxAimAssistWindow = 15f;

    public bool postProcessingEnabled = true;
    public float postProcessingBloom = 1f;
    public float postProcessingVignette = 0.25f;
    public float postProcessingExposure = 0f;

    /// <summary>
    ///     Wipes all progress (gold, permanent currencies, unlocks, meta perks) back to a fresh
    ///     save while leaving every settings field untouched. Used by the settings menu's Delete Save.
    /// </summary>
    public void ResetProgress()
    {
        SaveData defaults = new SaveData();
        goblinBlood = defaults.goblinBlood;
        orcishMetal = defaults.orcishMetal;
        diamondFishBones = defaults.diamondFishBones;
        unlockedWeapons = new List<string>(defaults.unlockedWeapons);
        equippedMeleeWeapon = defaults.equippedMeleeWeapon;
        equippedRangedWeapon = defaults.equippedRangedWeapon;
        equippedArmourSet = defaults.equippedArmourSet;
        unlockedArmourSets = new List<string>(defaults.unlockedArmourSets);
        equippedMount = defaults.equippedMount;
        unlockedMounts = new List<string>(defaults.unlockedMounts);
        unlockedMetaTier = defaults.unlockedMetaTier;
        purchasedMetaPerks.Clear();
        tutorialCompleted = defaults.tutorialCompleted;
        seenHints.Clear();
        firstRunGiftGranted = defaults.firstRunGiftGranted;
    }

    /// <summary>
    ///     Restores every player-facing setting (audio, controls, video, performance, button remaps)
    ///     to its authored default while leaving all progress untouched. Used by the settings menu's
    ///     Reset Settings via <see cref="GameSettingsService.ResetToDefaults" />.
    /// </summary>
    public void ResetSettings()
    {
        CopySettingsFrom(new SaveData());
    }

    /// <summary>
    ///     Brings settings saved under an older <see cref="settingsVersion" /> up to date. v1: the mouse
    ///     sensitivity range shrank from 0-10 to 0.01-1, so a save still on the old 0.5 default moves to the
    ///     new 0.1 default and any custom value is clamped into the new range.
    /// </summary>
    public void MigrateSettings()
    {
        if (settingsVersion < 1)
        {
            mouseSensitivity = Mathf.Approximately(mouseSensitivity, 0.5f) ? 0.1f : mouseSensitivity;
        }
        mouseSensitivity = Mathf.Clamp(mouseSensitivity, MinMouseSensitivity, MaxMouseSensitivity);
        settingsVersion = CurrentSettingsVersion;
    }

    /// <summary>
    ///     Copies every settings field (the <see cref="ResetSettings" /> half) from another save, leaving
    ///     progress untouched. Settings are global across save slots, so <see cref="SaveSystem" /> overlays
    ///     the shared settings file onto whichever slot it loads.
    /// </summary>
    public void CopySettingsFrom(SaveData other)
    {
        masterVolume = other.masterVolume;
        musicVolume = other.musicVolume;
        sfxVolume = other.sfxVolume;
        maxRagdolls = other.maxRagdolls;
        mouseSensitivity = other.mouseSensitivity;
        settingsVersion = other.settingsVersion;
        invertLookX = other.invertLookX;
        invertLookY = other.invertLookY;
        fieldOfView = other.fieldOfView;
        gameSpeed = other.gameSpeed;
        inputBindingOverridesJson = other.inputBindingOverridesJson;
        languageCode = other.languageCode;
        gamepadLookSensitivity = other.gamepadLookSensitivity;
        stickDeadzone = other.stickDeadzone;
        aimAssistStrength = other.aimAssistStrength;
        aimAssistWindow = other.aimAssistWindow;
        postProcessingEnabled = other.postProcessingEnabled;
        postProcessingBloom = other.postProcessingBloom;
        postProcessingVignette = other.postProcessingVignette;
        postProcessingExposure = other.postProcessingExposure;
    }
}
