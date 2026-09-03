using UnityEngine;

public class Player : MonoBehaviour
{
    public static Player Instance;

    /// <summary>
    ///     The player's <see cref="global::Health" />, so enemies can reach the player's health
    ///     (to damage it, or to react to <see cref="global::Health.OnDied" />) through the singleton
    ///     rather than scene lookups. Null if the player has no Health.
    /// </summary>
    private Health health;
    public Health Health => health != null ? health : (health = GetComponent<Health>());

    private IDamageable damageable;
    public IDamageable Damageable => damageable != null ? damageable : (damageable = GetComponent<IDamageable>());

    private Wallet wallet;
    public Wallet Wallet => wallet != null ? wallet : (wallet = GetComponent<Wallet>());

    private PlayerStats stats;
    public PlayerStats Stats => stats != null ? stats : (stats = GetComponent<PlayerStats>());

    /// <summary>
    ///     Reaches the camera pivot/vendored input reader for sensitivity, invert, and button-remap
    ///     settings, so <see cref="GameSettingsService" /> and the settings UI can apply them through the
    ///     singleton. Null if the player has no <see cref="InputSettingsBinder" />.
    /// </summary>
    public InputSettingsBinder InputSettings { get; private set; }

    /// <summary>
    ///     The gameplay camera controller, so <see cref="GameSettingsService" /> can apply the field-of-view
    ///     setting through the singleton. Null if the player has no <see cref="BowAimCamera" />.
    /// </summary>
    public BowAimCamera AimCamera { get; private set; }

    /// <summary>The player's periodic elemental imbuement controller.</summary>
    public PeriodicImbuementController Imbuements { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            health = GetComponent<Health>();
            damageable = GetComponent<IDamageable>();
            wallet = GetComponent<Wallet>();
            stats = GetComponent<PlayerStats>();
            InputSettings = GetComponent<InputSettingsBinder>();
            AimCamera = GetComponent<BowAimCamera>();
            Imbuements = GetComponent<PeriodicImbuementController>();
            if (Imbuements == null)
            {
                Imbuements = gameObject.AddComponent<PeriodicImbuementController>();
            }
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
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

            // Health Packs are transient pickups with no persistent owner component, so the Player
            // registers their heal fraction here (base 10% of max health; the "Field Medic" skill
            // family layers flat modifiers on top).
            Stats.SetBase(StatType.HealthPackHealPercent, 0.10f);

            // Dodge bases: unlocked by default
            Stats.SetBase(StatType.DodgeUnlocked, 1f);
            Stats.SetBase(StatType.DodgeCooldown, 10f);
            Stats.SetBase(StatType.DodgeDistance, 2f);
            Stats.SetBase(StatType.DodgeDamageMultiplier, 0f);
            Stats.SetBase(StatType.DodgeKnockbackForce, 0f);
            Stats.SetBase(StatType.DodgeChainCooldownReduction, 0f);

            // Restore in-run upgrades across scene transitions
            RunSession.RestoreInRunUpgrades(this);
        }
    }
}
