using System;
using System.Collections.Generic;

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
    /// <summary>The player's accumulated total gold, persisted across runs.</summary>
    public int totalGold;

    /// <summary>
    ///     Ids of every skill-tree node the player has purchased. Re-applied as stat modifiers on each run
    ///     by <see cref="SkillTreeService" />, making upgrades permanent meta-progression like gold. Cleared
    ///     by <see cref="ReincarnateService.Reincarnate" /> when the player reincarnates.
    /// </summary>
    public List<string> purchasedNodeIds = new List<string>();

    /// <summary>The player's accumulated Reincarnate Points, persisted across runs and never reset.</summary>
    public int reincarnatePoints;

    /// <summary>
    ///     Ids of every Reincarnate-tree node purchased. Re-applied as stat modifiers on each run by
    ///     <see cref="ReincarnateService" />, exactly like <see cref="purchasedNodeIds" /> — but these survive
    ///     reincarnating, since the point tree is the permanent progression layer.
    /// </summary>
    public List<string> purchasedReincarnateNodeIds = new List<string>();

    /// <summary>Linear 0-1 volumes applied by <see cref="GameSettingsService" />.</summary>
    public float masterVolume = 1f;
    public float musicVolume = 1f;
    public float sfxVolume = 1f;

    /// <summary>
    ///     Max ragdolls simulating at once (0-50), applied by <see cref="GameSettingsService" /> to
    ///     <see cref="EnemyRagdoll.MaxActive" />. Trades physics fidelity for performance — kills/flings
    ///     beyond this cap fall back to a normal animated death/knockdown.
    /// </summary>
    public int maxRagdolls = 12;

    /// <summary>
    ///     Mouse look sensitivity. 5 is the authored default, sitting at the middle of the settings
    ///     slider's 0-10 range.
    /// </summary>
    public float mouseSensitivity = 5f;
    public bool invertLookX;
    public bool invertLookY;

    /// <summary>
    ///     Gameplay camera field of view in degrees, applied by <see cref="GameSettingsService" /> via
    ///     <see cref="BowAimCamera.SetRestingFieldOfView" />. Matches the rig's authored default.
    /// </summary>
    public float fieldOfView = 40f;

    /// <summary>
    ///     Serialized Input System binding overrides for the vendored gameplay Controls asset (button
    ///     remapping), produced by <see cref="InputSettingsBinder.SaveBindingOverridesToJson" />. Empty
    ///     string means no overrides — every binding stays at its authored default.
    /// </summary>
    public string inputBindingOverridesJson = "";

    /// <summary>
    ///     Wipes all progress (gold, both skill trees' purchases, Reincarnate points) back to a fresh
    ///     save while leaving every settings field untouched. Used by the settings menu's Delete Save.
    /// </summary>
    public void ResetProgress()
    {
        SaveData defaults = new SaveData();
        totalGold = defaults.totalGold;
        reincarnatePoints = defaults.reincarnatePoints;
        purchasedNodeIds.Clear();
        purchasedReincarnateNodeIds.Clear();
    }

    /// <summary>
    ///     Restores every player-facing setting (audio, controls, video, performance, button remaps)
    ///     to its authored default while leaving all progress untouched. Used by the settings menu's
    ///     Reset Settings via <see cref="GameSettingsService.ResetToDefaults" />.
    /// </summary>
    public void ResetSettings()
    {
        SaveData defaults = new SaveData();
        masterVolume = defaults.masterVolume;
        musicVolume = defaults.musicVolume;
        sfxVolume = defaults.sfxVolume;
        maxRagdolls = defaults.maxRagdolls;
        mouseSensitivity = defaults.mouseSensitivity;
        invertLookX = defaults.invertLookX;
        invertLookY = defaults.invertLookY;
        fieldOfView = defaults.fieldOfView;
        inputBindingOverridesJson = defaults.inputBindingOverridesJson;
    }
}
