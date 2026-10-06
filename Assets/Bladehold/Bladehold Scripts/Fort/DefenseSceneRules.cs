using UnityEngine;

/// <summary>
///     Marks a gate-defense scene (Outer Gate, Desert Gate, Graveyard, Valley Stronghold) as using the
///     plan-17 fort rules: towers open the pick-your-upgrade wheel instead of E-to-refill/auto-level, and
///     wall plots are live. Scenes without one keep the old tower behaviour, the same "no component means
///     the default" convention as <see cref="SceneAbilityRules" />. The generator adds it; it also hands
///     every tower and wall the shared <see cref="FortUpgradeConfigSO" />.
/// </summary>
public class DefenseSceneRules : MonoBehaviour
{
    public static DefenseSceneRules Instance { get; private set; }

    [SerializeField] private FortUpgradeConfigSO upgradeConfig;
    [Tooltip("Towers open the upgrade wheel on [E] (refill is a slice) instead of refill-then-level-up.")]
    [SerializeField] private bool useUpgradeWheel = true;

    private bool anyError;

    /// <summary>True in a scene whose rules turn the upgrade wheel on (and the config is wired).</summary>
    public static bool UpgradeWheelActive => Instance != null && !Instance.anyError && Instance.useUpgradeWheel && Instance.upgradeConfig != null;

    /// <summary>The scene's upgrade numbers, or null outside a defense scene.</summary>
    public static FortUpgradeConfigSO Config => Instance != null ? Instance.upgradeConfig : null;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (upgradeConfig == null)
        {
            Debug.LogError($"[DefenseSceneRules] {name}: upgradeConfig is not assigned; towers fall back to the old refill/level-up.", this);
            anyError = true;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Generator / test hook: wire the config without a SerializedObject dance.</summary>
    public void SetConfig(FortUpgradeConfigSO config)
    {
        Instance = this;
        upgradeConfig = config;
        anyError = config == null;
    }
}
