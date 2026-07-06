using UnityEngine;

public class Player : MonoBehaviour
{
    public static Player Instance;

    /// <summary>
    ///     The player's <see cref="global::Health" />, so enemies can reach the player's health
    ///     (to damage it, or to react to <see cref="global::Health.OnDied" />) through the singleton
    ///     rather than scene lookups. Null if the player has no Health.
    /// </summary>
    public Health Health { get; private set; }

    /// <summary>
    ///     The player's damage sink (its <see cref="global::Health" />), so enemies can damage the
    ///     player through the singleton. Null if the player has no <see cref="IDamageable" />.
    /// </summary>
    public IDamageable Damageable { get; private set; }

    /// <summary>The player's coin purse, so pickups and UI can reach it through the singleton.</summary>
    public Wallet Wallet { get; private set; }

    /// <summary>
    ///     The player's stat aggregation layer, so weapons, movement and the upgrade tree can read and
    ///     modify effective stats through the singleton. Null if the player has no <see cref="PlayerStats" />.
    /// </summary>
    public PlayerStats Stats { get; private set; }

    /// <summary>
    ///     Reaches the vendored camera controller/input reader for sensitivity, invert, and button-remap
    ///     settings, so <see cref="GameSettingsService" /> and the settings UI can apply them through the
    ///     singleton. Null if the player has no <see cref="InputSettingsBinder" />.
    /// </summary>
    public InputSettingsBinder InputSettings { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Health = GetComponent<Health>();
            Damageable = GetComponent<IDamageable>();
            Wallet = GetComponent<Wallet>();
            Stats = GetComponent<PlayerStats>();
            InputSettings = GetComponent<InputSettingsBinder>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // The global damage multiplier belongs to no single weapon, so the Player itself registers
        // its base (1.0, the MoveSpeed multiplier convention). The "Raw Power" skill family layers
        // percent modifiers on top; every player-owned damage source multiplies by the effective value.
        if (Stats != null)
        {
            Stats.SetBase(StatType.AllDamageMultiplier, 1f);
        }
    }
}
