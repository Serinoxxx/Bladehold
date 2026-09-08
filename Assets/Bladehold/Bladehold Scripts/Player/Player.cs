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
        if (Health != null)
        {
            Health.ScaleDamageTaken -= HandleScaleDamageTaken;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        if (Health != null)
        {
            Health.ScaleDamageTaken += HandleScaleDamageTaken;
        }

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
            Stats.SetBase(StatType.DodgeCooldown, 1f);
            Stats.SetBase(StatType.DodgeMaxCharges, 2f);
            Stats.SetBase(StatType.DodgeDistance, 2f);
            Stats.SetBase(StatType.DodgeDamageMultiplier, 0f);
            Stats.SetBase(StatType.DodgeKnockbackForce, 0f);
            Stats.SetBase(StatType.DodgeChainCooldownReduction, 0f);

            // Vampire blade elemental vulnerability penalty base (0 = no extra damage taken)
            Stats.SetBase(StatType.SwordVampireExtraElementalDamage, 0f);

            // Deep freeze perk base (0 = consecutive ice hits only refresh chill, 1 = freezes solid)
            Stats.SetBase(StatType.IceDeepFreezeUnlocked, 0f);

            // Ice frost step slow percent base (0 = locked, >0 = dashing chills nearby enemies for 3s)
            Stats.SetBase(StatType.IceFrostStepSlowPercent, 0f);

            // Ice shatter bonus base (0 = no bonus, >0 = amplified damage against frozen targets)
            Stats.SetBase(StatType.IceShatterBonus, 0f);

            // Ice permafrost wall aura base (0 = locked, >0 = fortress walls passively chill adjacent foes)
            Stats.SetBase(StatType.IcePermafrostUnlocked, 0f);

            // Ice shards burst damage base (0 = locked, >0 = ranged kills trigger 8-way ice shard burst)
            Stats.SetBase(StatType.IceShardsBurstDamage, 0f);

            // Nimble strike perk base (0 = locked, 1 = dashing performs an attack along movement path)
            Stats.SetBase(StatType.SwordNimbleStrike, 0f);

            // Lunge mastery bases (0 = no bonus)
            Stats.SetBase(StatType.SwordLungeDamageBonus, 0f);
            Stats.SetBase(StatType.SwordLungeCritBonus, 0f);

            // Bow auto-shot on dash base (0 = locked, 1 = dash fires fully charged auto-shot)
            Stats.SetBase(StatType.BowAutoShotOnDash, 0f);

            // Bow pierce count base (0 = no pierces, arrows stop on first target)
            Stats.SetBase(StatType.BowPierceCount, 0f);

            // Bow desperate volley base (0 = locked, >0 = number of radial arrows fired below 50% HP)
            Stats.SetBase(StatType.BowDesperateVolleyArrows, 0f);

            // Axe fear duration base (0 = locked, >0 = freeze nearby enemies on kill)
            Stats.SetBase(StatType.AxeFearDuration, 0f);

            // Axe power dash base (0 = locked, >0 = charge speed bonus after dashing)
            Stats.SetBase(StatType.AxePowerDashChargeSpeed, 0f);

            // Axe heavy stance shield base (0 = locked, >0 = shield HP on fully charged attacks)
            Stats.SetBase(StatType.AxeHeavyStanceShield, 0f);

            // Axe first strike bonus base (0 = no bonus, >0 = bonus damage against full-health targets)
            Stats.SetBase(StatType.AxeFirstStrikeBonus, 0f);

            // Axe bloodsplosion damage base (0 = locked, >0 = AoE damage on kill)
            Stats.SetBase(StatType.AxeBloodsplosionDamage, 0f);

            // Axe spin top DPS base (0 = locked, >0 = slow-moving vortex dealing AoE DPS)
            Stats.SetBase(StatType.AxeSpinTopDPS, 0f);

            // Fire combustion DPS base (0 = default burn DPS, >0 = bonus burn tick DPS)
            Stats.SetBase(StatType.FireCombustionDPS, 0f);

            // Fire kindling damage bonus base (0 = locked, >0 = amplified damage against ignited targets)
            Stats.SetBase(StatType.FireKindlingDamageBonus, 0f);

            // Fire blazing trail DPS base (0 = locked, >0 = leaves fire trail on dash)
            Stats.SetBase(StatType.FireBlazingTrailDPS, 0f);

            // Fire inferno burst unlocked base (0 = locked, >0 = ignites all nearby enemies on ultimate)
            Stats.SetBase(StatType.FireInfernoBurstUnlocked, 0f);

            // Fire fortress pyre bonus base (0 = locked, >0 = fortress attacks deal bonus damage to ignited foes)
            Stats.SetBase(StatType.FireFortressPyreBonus, 0f);

            // Lightning static edge damage base (0 = locked, >0 = first hit of melee chain deals bonus lightning damage)
            Stats.SetBase(StatType.LightningStaticEdgeDamage, 0f);

            // Lightning eye of the storm damage base (0 = locked, >0 = periodic lightning strikes during ultimate)
            Stats.SetBase(StatType.LightningEyeOfTheStormDamage, 0f);

            // Lightning tesla spire damage base (0 = locked, >0 = fortress discharges lightning every 5s)
            Stats.SetBase(StatType.LightningTeslaSpireDamage, 0f);

            // Fort arrow slits count base (0 = default burst count, >0 adds extra arrows per volley)
            Stats.SetBase(StatType.FortArrowSlitsCount, 0f);

            // Fort sniper nest base (0 = locked, >0 = increased range and +200% damage when no foes are near walls)
            Stats.SetBase(StatType.FortSniperNestUnlocked, 0f);

            // Fort focus fire bonus base (0 = locked, >0 = bonus damage to foes hit by player ranged attacks)
            Stats.SetBase(StatType.FortFocusFireBonus, 0f);

            // Fort fiery pitch base (0 = locked, >0 = boiling oil ignites all enemies caught in pool)
            Stats.SetBase(StatType.FortFieryPitchUnlocked, 0f);

            // Fort scalding heat bonus base (0 = default, >0 = enemies in boiling oil take increased damage from all sources)
            Stats.SetBase(StatType.FortScaldingHeatBonus, 0f);

            // Fort expanded vats base (0 = default, >0 = increases boiling oil pool radius)
            Stats.SetBase(StatType.FortExpandedVatsPercent, 0f);

            // Fort concussive spikes duration base (0 = locked, >0 = barricade impacts stun enemies)
            Stats.SetBase(StatType.FortConcussiveSpikesDuration, 0f);

            // Fort shove interval base (0 = locked, >0 = knocks enemies back 2m toward their spawn point periodically)
            Stats.SetBase(StatType.FortShoveInterval, 0f);

            // Fort vulnerability field bonus base (0 = default, >0 = enemies in barricade zone take increased damage from all sources)
            Stats.SetBase(StatType.FortVulnerabilityFieldBonus, 0f);

            // Fort electrified oil base (0 = locked, >0 = boiling oil shocks enemies with lightning damage & status)
            Stats.SetBase(StatType.FortElectrifiedOil, 0f);

            // Fort permafrost spikes base (0 = locked, >0 = spiked barricades chill and slow enemies)
            Stats.SetBase(StatType.FortPermafrostSpikes, 0f);

            // Restore in-run upgrades across scene transitions
            RunSession.RestoreInRunUpgrades(this);
        }
    }

    private float HandleScaleDamageTaken(Damage damage)
    {
        if (damage != null && (damage.type == DamageType.elemental || !string.IsNullOrEmpty(damage.elementId)))
        {
            if (Stats != null && Stats.GetValue(StatType.SwordVampireExtraElementalDamage) > 0f)
            {
                return 1f + Stats.GetValue(StatType.SwordVampireExtraElementalDamage);
            }
        }

        return 1f;
    }
}
