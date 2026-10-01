using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Which weapon an ultimate belongs to. Each slot has its own charge bar.</summary>
public enum UltimateSlot
{
    Melee,
    Ranged
}

/// <summary>
///     Runs the player's ultimates: one per weapon slot (bought at the Rest Area shop), each with its own
///     charge bar. Melee hits fill the melee bar and ranged hits the ranged bar; any other damage enemies take
///     (towers, burns, duo explosions) fills both at <see cref="SharedChargeRate" />. The Ultimate input fires
///     the ranged ultimate while aiming the ranged weapon and the melee one otherwise (or whichever one you own).
///     Only one ultimate runs at a time, and no bar charges while one is running.
/// </summary>
public class PlayerUltimateController : MonoBehaviour
{
    public const float MaxCharge = 100f;

    // Charge per point of damage dealt, and the share of that which non-weapon damage gives each bar.
    private const float ChargePerDamage = 0.1f;
    private const float SharedChargeRate = 0.5f;

    public event Action<UltimateSlot, float> OnChargeChanged;
    public event Action OnUltimateActivated;
    public event Action OnUltimateDeactivated;

    public bool IsUltimateActive { get; private set; }
    public UltimateSlot ActiveSlot { get; private set; }
    public float ActiveUltimateDuration { get; private set; }
    public float ActiveUltimateRemainingTime { get; private set; }

    private readonly float[] charges = new float[2];

    private Player player;
    private InputAction ultimateAction;

    // Weapon hits and raw damage events are collected during the frame and settled in LateUpdate, so a
    // weapon hit claims the damage event it caused regardless of which callback fires first.
    private struct PendingHit
    {
        public IDamageable target;
        public UltimateSlot slot;
        public float amount;
    }

    private struct PendingDamage
    {
        public Health target;
        public float amount;
    }

    private readonly List<PendingHit> pendingHits = new List<PendingHit>();
    private readonly List<PendingDamage> pendingDamage = new List<PendingDamage>();

    public float GetCharge(UltimateSlot slot) => charges[(int)slot];

    public bool HasUltimate(UltimateSlot slot) => !string.IsNullOrEmpty(RunSession.GetUltimateId(slot));

    private void Awake()
    {
        player = GetComponentInChildren<Player>();
        RegisterDefaultStats();
    }

    private void OnEnable()
    {
        Health.OnAnyHealthDamaged += HandleAnyHealthDamaged;
        BindHitTriggers();
        BindInput();
    }

    private void OnDisable()
    {
        Health.OnAnyHealthDamaged -= HandleAnyHealthDamaged;
        UnbindHitTriggers();
        if (ultimateAction != null)
        {
            ultimateAction.performed -= HandleUltimateInput;
            ultimateAction = null;
        }
    }

    private void Start()
    {
        RegisterDefaultStats();
        BindHitTriggers();
        if (ultimateAction == null)
        {
            BindInput();
        }

        if (RunSession.MeleeUltimateCharge > 0f) SetCharge(UltimateSlot.Melee, RunSession.MeleeUltimateCharge);
        if (RunSession.RangedUltimateCharge > 0f) SetCharge(UltimateSlot.Ranged, RunSession.RangedUltimateCharge);

        SyncUltimateHandlers();
    }

    private float nextTrickleTime;
    private float nextEyeOfStormStrikeTime = 0f;

    private void Update()
    {
        if (player == null || player.Stats == null) return;

        if (IsUltimateActive)
        {
            ActiveUltimateRemainingTime = Mathf.Max(0f, ActiveUltimateRemainingTime - Time.deltaTime);

            float eyeDmg = player.Stats.GetValue(StatType.LightningEyeOfTheStormDamage);
            if (eyeDmg > 0f && Time.time >= nextEyeOfStormStrikeTime)
            {
                nextEyeOfStormStrikeTime = Time.time + 1.0f;
                TriggerEyeOfTheStorm(eyeDmg);
            }
            return;
        }

        if (!RunSession.HasAnyUltimate) return;

        if (Time.time >= nextTrickleTime)
        {
            nextTrickleTime = Time.time + 1f;
            float trickleRate = player.Stats.GetValue(StatType.UltimatePassiveChargeRate);
            if (trickleRate > 0f)
            {
                AddCharge(UltimateSlot.Melee, trickleRate);
                AddCharge(UltimateSlot.Ranged, trickleRate);
            }
        }
    }

    private void LateUpdate()
    {
        if (pendingHits.Count == 0 && pendingDamage.Count == 0) return;

        float melee = 0f;
        float ranged = 0f;

        foreach (PendingHit hit in pendingHits)
        {
            float amount = hit.amount;
            for (int i = 0; i < pendingDamage.Count; i++)
            {
                if (pendingDamage[i].target != null && ReferenceEquals(pendingDamage[i].target, hit.target))
                {
                    amount += pendingDamage[i].amount;
                    pendingDamage[i] = default;
                }
            }

            if (hit.slot == UltimateSlot.Melee) melee += amount;
            else ranged += amount;
        }

        float shared = 0f;
        foreach (PendingDamage d in pendingDamage)
        {
            if (d.target != null) shared += d.amount;
        }

        pendingHits.Clear();
        pendingDamage.Clear();

        melee += shared * SharedChargeRate;
        ranged += shared * SharedChargeRate;
        if (melee > 0f) AddCharge(UltimateSlot.Melee, melee);
        if (ranged > 0f) AddCharge(UltimateSlot.Ranged, ranged);
    }

    private void RegisterDefaultStats()
    {
        if (player != null && player.Stats != null)
        {
            player.Stats.SetBase(StatType.UltimateChargeMultiplier, 1f);
            player.Stats.SetBase(StatType.UltimateDurationSeconds, 6f);
            player.Stats.SetBase(StatType.UltimateUnlocked, RunSession.HasAnyUltimate ? 1f : 0f);
            player.Stats.SetBase(StatType.UltimatePassiveChargeRate, 0.5f); // 0.5 charge per second = 200s to full without damage
            player.Stats.SetBase(StatType.FireInfernoBurstUnlocked, 0f);
            player.Stats.SetBase(StatType.LightningEyeOfTheStormDamage, 0f);
        }
    }

    private void BindInput()
    {
        if (player != null && player.InputSettings != null)
        {
            var map = player.InputSettings.GetRebindableActionMap();
            if (map != null)
            {
                ultimateAction = map.FindAction("Ultimate");
                if (ultimateAction != null)
                {
                    ultimateAction.performed -= HandleUltimateInput;
                    ultimateAction.performed += HandleUltimateInput;
                    if (!ultimateAction.enabled) ultimateAction.Enable();
                }
            }
        }
    }

    private void BindHitTriggers()
    {
        if (player == null) return;

        foreach (var trigger in player.GetComponentsInChildren<DamageTrigger>(true))
        {
            trigger.OnHit -= HandleMeleeHit;
            trigger.OnHit += HandleMeleeHit;
        }

        var bow = player.GetComponentInChildren<PlayerBow>(true);
        if (bow != null)
        {
            bow.OnHit -= HandleRangedHit;
            bow.OnHit += HandleRangedHit;
        }

        var wand = player.GetComponentInChildren<PlayerWand>(true);
        if (wand != null)
        {
            wand.OnHit -= HandleRangedHit;
            wand.OnHit += HandleRangedHit;
        }

        var thrownAxe = player.GetComponentInChildren<PlayerThrownAxe>(true);
        if (thrownAxe != null)
        {
            thrownAxe.OnHit -= HandleRangedHit;
            thrownAxe.OnHit += HandleRangedHit;
        }
    }

    private void UnbindHitTriggers()
    {
        if (player == null) return;

        foreach (var trigger in player.GetComponentsInChildren<DamageTrigger>(true))
        {
            trigger.OnHit -= HandleMeleeHit;
        }

        var bow = player.GetComponentInChildren<PlayerBow>(true);
        if (bow != null) bow.OnHit -= HandleRangedHit;

        var wand = player.GetComponentInChildren<PlayerWand>(true);
        if (wand != null) wand.OnHit -= HandleRangedHit;

        var thrownAxe = player.GetComponentInChildren<PlayerThrownAxe>(true);
        if (thrownAxe != null) thrownAxe.OnHit -= HandleRangedHit;
    }

    private void HandleMeleeHit(IDamageable target, Damage damage, Vector3 hitPoint) => QueueWeaponHit(UltimateSlot.Melee, target, damage);

    private void HandleRangedHit(IDamageable target, Damage damage, Vector3 hitPoint) => QueueWeaponHit(UltimateSlot.Ranged, target, damage);

    private void QueueWeaponHit(UltimateSlot slot, IDamageable target, Damage damage)
    {
        if (IsUltimateActive || target == null || damage == null) return;

        // A Health target already raised OnAnyHealthDamaged for this hit; LateUpdate moves that amount onto
        // this weapon's bar. Anything else (banners, barrels) only reports through the hit, so charge it here.
        float amount = target is Health ? 0f : ChargeFor(target, damage);
        pendingHits.Add(new PendingHit { target = target, slot = slot, amount = amount });
    }

    private void HandleAnyHealthDamaged(Health target, Damage damage)
    {
        if (IsUltimateActive || target == null || damage == null) return;
        // The loading-screen rehearsal kills (EnemyPrewarmer) aren't the player's work.
        if (EnemyPrewarmer.IsRehearsing) return;

        float amount = ChargeFor(target, damage);
        if (amount > 0f) pendingDamage.Add(new PendingDamage { target = target, amount = amount });
    }

    /// <summary>Charge earned by one damage event before it is assigned to a bar (0 for the player's own health).</summary>
    private float ChargeFor(IDamageable target, Damage damage)
    {
        if (player == null || player.Stats == null) return 0f;
        if (player.Health != null && target == (IDamageable)player.Health) return 0f;
        if (player.Damageable != null && target == player.Damageable) return 0f;

        float actualDamage = damage.value;
        if (target is Health h)
        {
            if (h.IsDead) return 0f; // Corpse hit

            if (h.CurrentHealth > 0)
            {
                actualDamage = Mathf.Min(damage.value, h.CurrentHealth);
            }
            else
            {
                actualDamage = damage.value + h.CurrentHealth; // subtract overkill
            }
        }

        actualDamage = Mathf.Max(0f, actualDamage);
        if (actualDamage <= 0f) return 0f;

        float mult = player.Stats.GetValue(StatType.UltimateChargeMultiplier);
        if (mult <= 0f) mult = 1f;

        return actualDamage * ChargePerDamage * mult;
    }

    public void SetCharge(UltimateSlot slot, float amount)
    {
        float oldCharge = charges[(int)slot];
        charges[(int)slot] = Mathf.Clamp(amount, 0f, MaxCharge);
        RunSession.SetUltimateCharge(slot, charges[(int)slot]);

        Debug.Log($"[PlayerUltimateController] {slot} charge set: {oldCharge:F1} -> {charges[(int)slot]:F1} / {MaxCharge}");
        OnChargeChanged?.Invoke(slot, charges[(int)slot]);
    }

    public void AddCharge(UltimateSlot slot, float amount)
    {
        if (IsUltimateActive || !HasUltimate(slot)) return;

        float oldCharge = charges[(int)slot];
        charges[(int)slot] = Mathf.Clamp(oldCharge + amount, 0f, MaxCharge);

        if (!Mathf.Approximately(oldCharge, charges[(int)slot]))
        {
            RunSession.SetUltimateCharge(slot, charges[(int)slot]);
            OnChargeChanged?.Invoke(slot, charges[(int)slot]);
        }
    }

    /// <summary>
    ///     The ultimate the Ultimate input fires now: ranged while aiming the ranged weapon, melee otherwise,
    ///     and whichever one you own if you only have one. Null when no ultimate is owned.
    /// </summary>
    public UltimateSlot? SlotForInput()
    {
        bool hasMelee = HasUltimate(UltimateSlot.Melee);
        bool hasRanged = HasUltimate(UltimateSlot.Ranged);
        if (!hasMelee && !hasRanged) return null;
        if (!hasMelee) return UltimateSlot.Ranged;
        if (!hasRanged) return UltimateSlot.Melee;

        IChargedAimWeapon aimWeapon = PlayerWeaponManager.Instance != null ? PlayerWeaponManager.Instance.ActiveAimWeapon : null;
        return aimWeapon != null && aimWeapon.IsAiming ? UltimateSlot.Ranged : UltimateSlot.Melee;
    }

    private void HandleUltimateInput(InputAction.CallbackContext context)
    {
        if (IsUltimateActive || player == null || player.Stats == null) return;
        if (!SceneAbilityRules.UltimateAllowed) return;

        UltimateSlot? slot = SlotForInput();
        if (slot == null || charges[(int)slot.Value] < MaxCharge) return;

        ActivateUltimate(slot.Value);
    }

    private void ActivateUltimate(UltimateSlot slot)
    {
        SyncUltimateHandlers();

        IUltimateHandler activeHandler = DraftUpgradeService.GetUltimateHandler(player, RunSession.GetUltimateId(slot));
        if (activeHandler == null)
        {
            Debug.LogError($"[PlayerUltimateController] No handler for {slot} ultimate '{RunSession.GetUltimateId(slot)}'.");
            return;
        }

        float baseDur = activeHandler.BaseDuration;
        if (baseDur > 0f)
        {
            player.Stats.SetBase(StatType.UltimateDurationSeconds, baseDur);
        }
        float totalDuration = player.Stats.GetValue(StatType.UltimateDurationSeconds);
        if (totalDuration <= 0f) totalDuration = baseDur > 0f ? baseDur : 5f;

        ActiveUltimateDuration = totalDuration;
        ActiveUltimateRemainingTime = totalDuration;

        IsUltimateActive = true;
        ActiveSlot = slot;
        pendingHits.Clear();
        pendingDamage.Clear();
        charges[(int)slot] = 0f;
        RunSession.SetUltimateCharge(slot, 0f);
        nextEyeOfStormStrikeTime = 0f;

        OnUltimateActivated?.Invoke();

        if (player.Stats.GetValue(StatType.FireInfernoBurstUnlocked) > 0f)
        {
            TriggerInfernoBurst();
        }

        activeHandler.Activate(this);
    }

    /// <summary>Enables the handlers for the owned melee and ranged ultimates (and disables the rest).</summary>
    public void SyncUltimateHandlers()
    {
        if (player == null) player = GetComponentInChildren<Player>();
        if (player == null || player.Stats == null || !RunSession.HasAnyUltimate) return;

        DraftUpgradeService.ConfigureUltimateHandlers(player);
    }

    private void TriggerInfernoBurst()
    {
        Vector3 center = player != null ? player.transform.position : transform.position;

        if (ElementalEffectsManager.Instance != null)
        {
            ElementalEffectsManager.Instance.PlayAt(ElementalEffectsManager.Instance.thermalShockFeedback, center);
        }

        Collider[] hits = Physics.OverlapSphere(center, 12f);
        HashSet<Health> processed = new HashSet<Health>();

        float damageMultiplier = 1f;
        if (player != null && player.Stats != null)
        {
            float allMult = player.Stats.GetValue(StatType.AllDamageMultiplier);
            if (allMult > 0f) damageMultiplier = allMult;
            // The unlock stat doubles as the card's level scaling: 1 at level 1, higher per level.
            damageMultiplier *= Mathf.Max(1f, player.Stats.GetValue(StatType.FireInfernoBurstUnlocked));
        }

        float blastDamage = 25f * damageMultiplier;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null) continue;

            Health enemyHealth = hit.GetComponentInParent<Health>();
            if (enemyHealth == null || enemyHealth.IsDead) continue;
            if (player != null && (enemyHealth.gameObject == player.gameObject || enemyHealth.transform.root == player.transform.root)) continue;
            if (processed.Contains(enemyHealth)) continue;

            processed.Add(enemyHealth);

            EnemyStatusManager.GetOrAdd(enemyHealth)?.ApplyStatus("Fire");

            enemyHealth.ReceiveDamage(new Damage
            {
                value = blastDamage,
                type = DamageType.elemental,
                elementId = "Fire",
                sourcePosition = center,
                source = player != null ? player.Damageable : null,
                isPlayerDamage = true
            });
        }
    }

    private void TriggerEyeOfTheStorm(float eyeDmg)
    {
        Vector3 center = player != null ? player.transform.position : transform.position;
        Collider[] hits = Physics.OverlapSphere(center, 15f);
        List<Health> validEnemies = new List<Health>();
        HashSet<Health> processed = new HashSet<Health>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null) continue;

            Health enemyHealth = hit.GetComponentInParent<Health>();
            if (enemyHealth == null || enemyHealth.IsDead) continue;
            if (player != null && (enemyHealth.gameObject == player.gameObject || enemyHealth.transform.root == player.transform.root)) continue;
            if (processed.Contains(enemyHealth)) continue;

            processed.Add(enemyHealth);
            validEnemies.Add(enemyHealth);
        }

        if (validEnemies.Count == 0) return;

        Health targetHealth = validEnemies[UnityEngine.Random.Range(0, validEnemies.Count)];
        Vector3 targetPos = targetHealth.transform.position;

        float allMult = player != null && player.Stats != null ? player.Stats.GetValue(StatType.AllDamageMultiplier) : 1f;
        if (allMult <= 0f) allMult = 1f;

        targetHealth.ReceiveDamage(new Damage
        {
            value = eyeDmg * allMult,
            type = DamageType.elemental,
            elementId = "Lightning",
            sourcePosition = center,
            source = player != null ? player.Damageable : null,
            isPlayerDamage = true
        });

        EnemyStatusManager.GetOrAdd(targetHealth)?.ApplyStatus("Lightning");

        if (ElementalEffectsManager.Instance != null)
        {
            ElementalEffectsManager.Instance.PlayAt(ElementalEffectsManager.Instance.superconductorFeedback, targetPos);
        }
    }

    public void EndUltimate()
    {
        if (!IsUltimateActive) return;
        IsUltimateActive = false;
        ActiveUltimateRemainingTime = 0f;
        OnUltimateDeactivated?.Invoke();
        OnChargeChanged?.Invoke(UltimateSlot.Melee, charges[(int)UltimateSlot.Melee]);
        OnChargeChanged?.Invoke(UltimateSlot.Ranged, charges[(int)UltimateSlot.Ranged]);
    }
}

public interface IUltimateHandler
{
    void Activate(PlayerUltimateController controller);
    float BaseDuration { get; }
}
