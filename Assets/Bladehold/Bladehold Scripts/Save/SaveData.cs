using System;
using System.Collections.Generic;

/// <summary>
///     Serializable snapshot of persisted player progress, written to disk by <see cref="SaveSystem" />.
///     Add new fields here as more progress needs saving; existing saves load missing fields as their
///     defaults.
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

    /// <summary>Mouse look sensitivity, matching the vendored camera controller's own default.</summary>
    public float mouseSensitivity = 5f;
    public bool invertLookX;
    public bool invertLookY;

    /// <summary>
    ///     Serialized Input System binding overrides for the vendored gameplay Controls asset (button
    ///     remapping), produced by <see cref="InputSettingsBinder.SaveBindingOverridesToJson" />. Empty
    ///     string means no overrides — every binding stays at its authored default.
    /// </summary>
    public string inputBindingOverridesJson = "";
}
