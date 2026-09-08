using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerUltimateController : MonoBehaviour
{
    public float CurrentCharge { get; private set; }
    public const float MaxCharge = 100f;

    public event Action<float> OnChargeChanged;
    public event Action OnUltimateActivated;
    public event Action OnUltimateDeactivated;

    public bool IsUltimateActive { get; private set; }
    public float ActiveUltimateDuration { get; private set; }
    public float ActiveUltimateRemainingTime { get; private set; }

    private Player player;
    private InputAction ultimateAction;

    private int lastProcessedFrame = -1;
    private IDamageable lastProcessedTarget;
    private float lastProcessedDamage;

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

        if (RunSession.PlayerUltimateCharge > 0f)
        {
            SetCharge(RunSession.PlayerUltimateCharge);
        }

        SyncActiveUltimateHandler();
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

        if (player.Stats.GetValue(StatType.UltimateUnlocked) <= 0f) return;

        if (Time.time >= nextTrickleTime)
        {
            nextTrickleTime = Time.time + 1f;
            float trickleRate = player.Stats.GetValue(StatType.UltimatePassiveChargeRate);
            if (trickleRate > 0f)
            {
                AddCharge(trickleRate);
            }
        }
    }

    private void RegisterDefaultStats()
    {
        if (player != null && player.Stats != null)
        {
            player.Stats.SetBase(StatType.UltimateChargeMultiplier, 1f);
            player.Stats.SetBase(StatType.UltimateDurationSeconds, 6f);
            player.Stats.SetBase(StatType.UltimateUnlocked, 0f);
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
            trigger.OnHit -= HandleHitEvent;
            trigger.OnHit += HandleHitEvent;
        }

        var bow = player.GetComponentInChildren<PlayerBow>(true);
        if (bow != null)
        {
            bow.OnHit -= HandleHitEvent;
            bow.OnHit += HandleHitEvent;
        }

        var wand = player.GetComponentInChildren<PlayerWand>(true);
        if (wand != null)
        {
            wand.OnHit -= HandleHitEvent;
            wand.OnHit += HandleHitEvent;
        }

        var thrownAxe = player.GetComponentInChildren<PlayerThrownAxe>(true);
        if (thrownAxe != null)
        {
            thrownAxe.OnHit -= HandleHitEvent;
            thrownAxe.OnHit += HandleHitEvent;
        }
    }

    private void UnbindHitTriggers()
    {
        if (player == null) return;

        foreach (var trigger in player.GetComponentsInChildren<DamageTrigger>(true))
        {
            trigger.OnHit -= HandleHitEvent;
        }

        var bow = player.GetComponentInChildren<PlayerBow>(true);
        if (bow != null) bow.OnHit -= HandleHitEvent;

        var wand = player.GetComponentInChildren<PlayerWand>(true);
        if (wand != null) wand.OnHit -= HandleHitEvent;

        var thrownAxe = player.GetComponentInChildren<PlayerThrownAxe>(true);
        if (thrownAxe != null) thrownAxe.OnHit -= HandleHitEvent;
    }

    private void HandleHitEvent(IDamageable target, Damage damage, Vector3 hitPoint)
    {
        ProcessDamage(target, damage);
    }

    private void HandleAnyHealthDamaged(Health target, Damage damage)
    {
        ProcessDamage(target, damage);
    }

    private void ProcessDamage(IDamageable target, Damage damage)
    {
        if (IsUltimateActive || player == null || player.Stats == null) return;

        // Deduplicate multiple callbacks for the exact same hit in the same frame
        if (Time.frameCount == lastProcessedFrame && ReferenceEquals(target, lastProcessedTarget) && Mathf.Approximately(damage.value, lastProcessedDamage))
        {
            return;
        }

        lastProcessedFrame = Time.frameCount;
        lastProcessedTarget = target;
        lastProcessedDamage = damage.value;

        // Ignore damage dealt to the player
        if (player.Health != null && target == (IDamageable)player.Health) return;
        if (player.Damageable != null && target == player.Damageable) return;

        float unlocked = player.Stats.GetValue(StatType.UltimateUnlocked);
        if (unlocked <= 0f) return;

        float actualDamage = damage.value;
        if (target is Health h)
        {
            if (h.IsDead) return; // Corpse hit

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
        if (actualDamage <= 0f) return;

        float mult = player.Stats.GetValue(StatType.UltimateChargeMultiplier);
        if (mult <= 0f) mult = 1f;

        float chargeGained = actualDamage * 0.1f * mult;
        AddCharge(chargeGained);
    }

    public void SetCharge(float amount)
    {
        float oldCharge = CurrentCharge;
        CurrentCharge = Mathf.Clamp(amount, 0f, MaxCharge);
        RunSession.PlayerUltimateCharge = CurrentCharge;

        Debug.Log($"[PlayerUltimateController] Charge explicitly set: {oldCharge:F1} -> {CurrentCharge:F1} / {MaxCharge}");
        OnChargeChanged?.Invoke(CurrentCharge);
    }

    public void AddCharge(float amount)
    {
        if (IsUltimateActive || player == null || player.Stats == null) return;
        if (player.Stats.GetValue(StatType.UltimateUnlocked) <= 0f) return;

        float oldCharge = CurrentCharge;
        CurrentCharge = Mathf.Clamp(CurrentCharge + amount, 0f, MaxCharge);
        
        if (!Mathf.Approximately(oldCharge, CurrentCharge))
        {
            RunSession.PlayerUltimateCharge = CurrentCharge;
            Debug.Log($"[PlayerUltimateController] Charge updated: {oldCharge:F1} -> {CurrentCharge:F1} / {MaxCharge}");
            OnChargeChanged?.Invoke(CurrentCharge);
        }
    }

    private void HandleUltimateInput(InputAction.CallbackContext context)
    {
        if (IsUltimateActive || CurrentCharge < MaxCharge || player == null || player.Stats == null) return;
        if (player.Stats.GetValue(StatType.UltimateUnlocked) <= 0f) return;

        ActivateUltimate();
    }

    private void ActivateUltimate()
    {
        SyncActiveUltimateHandler();

        var handlers = transform.root.GetComponentsInChildren<IUltimateHandler>(true);
        IUltimateHandler activeHandler = null;
        foreach (var handler in handlers)
        {
            if (handler is MonoBehaviour mb && mb.enabled)
            {
                activeHandler = handler;
                break; // Only one ultimate activates per run
            }
        }

        if (activeHandler == null)
        {
            EndUltimate();
            return;
        }

        float baseDur = activeHandler.BaseDuration;
        if (player != null && player.Stats != null)
        {
            if (baseDur > 0f)
            {
                player.Stats.SetBase(StatType.UltimateDurationSeconds, baseDur);
            }
            float totalDuration = player.Stats.GetValue(StatType.UltimateDurationSeconds);
            if (totalDuration <= 0f) totalDuration = baseDur > 0f ? baseDur : 5f;

            ActiveUltimateDuration = totalDuration;
            ActiveUltimateRemainingTime = totalDuration;
        }
        else
        {
            ActiveUltimateDuration = baseDur > 0f ? baseDur : 5f;
            ActiveUltimateRemainingTime = ActiveUltimateDuration;
        }

        IsUltimateActive = true;
        CurrentCharge = 0f;
        RunSession.PlayerUltimateCharge = 0f;
        nextEyeOfStormStrikeTime = 0f;

        OnUltimateActivated?.Invoke();

        if (player != null && player.Stats != null && player.Stats.GetValue(StatType.FireInfernoBurstUnlocked) > 0f)
        {
            TriggerInfernoBurst();
        }

        activeHandler.Activate(this);
    }

    public void SyncActiveUltimateHandler()
    {
        if (player == null) player = GetComponentInChildren<Player>();
        if (player == null || player.Stats == null) return;

        float unlocked = player.Stats.GetValue(StatType.UltimateUnlocked);
        if (unlocked <= 0f) return;

        string targetUltId = RunSession.ActiveUltimateId;
        if (string.IsNullOrEmpty(targetUltId))
        {
            targetUltId = GetDefaultUltimateIdForEquippedWeapons();
            if (!string.IsNullOrEmpty(targetUltId))
            {
                RunSession.ActiveUltimateId = targetUltId;
            }
        }

        if (!string.IsNullOrEmpty(targetUltId))
        {
            DraftUpgradeService.ConfigureUltimateHandler(player, targetUltId);

            var handlers = transform.root.GetComponentsInChildren<IUltimateHandler>(true);
            foreach (var handler in handlers)
            {
                if (handler is MonoBehaviour mb && mb.enabled && handler.BaseDuration > 0f)
                {
                    player.Stats.SetBase(StatType.UltimateDurationSeconds, handler.BaseDuration);
                    break;
                }
            }
        }
    }

    private string GetDefaultUltimateIdForEquippedWeapons()
    {
        // 1. Check weapon manager melee weapon
        string meleeId = PlayerWeaponManager.Instance != null ? PlayerWeaponManager.Instance.CurrentMeleeId : "sword";
        if (string.IsNullOrEmpty(meleeId))
        {
            SaveData save = SaveSystem.Load();
            meleeId = save != null && !string.IsNullOrEmpty(save.equippedMeleeWeapon) ? save.equippedMeleeWeapon.ToLower() : "sword";
        }

        if (meleeId.Contains("mace")) return "mace_earthshaker_ult";
        if (meleeId.Contains("axe")) return "axe_bladestorm_ult";
        if (meleeId.Contains("staff")) return "mage_skyfall_ult";
        if (meleeId.Contains("sword")) return "sword_mount_ult";

        // 2. Check ranged weapon
        string rangedId = PlayerWeaponManager.Instance != null ? PlayerWeaponManager.Instance.CurrentRangedId : "bow";
        if (string.IsNullOrEmpty(rangedId))
        {
            SaveData save = SaveSystem.Load();
            rangedId = save != null && !string.IsNullOrEmpty(save.equippedRangedWeapon) ? save.equippedRangedWeapon.ToLower() : "bow";
        }

        if (rangedId.Contains("wand")) return "mage_skyfall_ult";
        if (rangedId.Contains("throwing") || rangedId.Contains("taxe")) return "taxe_vortex_ult";
        return "bow_stream_ult";
    }

    private void TriggerInfernoBurst()
    {
        Vector3 center = player != null ? player.transform.position : transform.position;

        if (ElementalEffectsManager.Instance != null && ElementalEffectsManager.Instance.thermalShockVfx != null)
        {
            Instantiate(ElementalEffectsManager.Instance.thermalShockVfx, center, Quaternion.identity);
            if (ElementalEffectsManager.Instance.thermalShockSfx != null)
            {
                AudioSource.PlayClipAtPoint(ElementalEffectsManager.Instance.thermalShockSfx, center);
            }
        }

        Collider[] hits = Physics.OverlapSphere(center, 12f);
        HashSet<Health> processed = new HashSet<Health>();

        float damageMultiplier = 1f;
        if (player != null && player.Stats != null)
        {
            float allMult = player.Stats.GetValue(StatType.AllDamageMultiplier);
            if (allMult > 0f) damageMultiplier = allMult;
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
            if (ElementalEffectsManager.Instance.superconductorVfx != null)
            {
                Instantiate(ElementalEffectsManager.Instance.superconductorVfx, targetPos, Quaternion.identity);
            }
            AudioClip zapClip = ElementalEffectsManager.Instance.superconductorSfx != null
                ? ElementalEffectsManager.Instance.superconductorSfx
                : ElementalEffectsManager.Instance.statusAppliedSfx;
            if (zapClip != null)
            {
                AudioSource.PlayClipAtPoint(zapClip, targetPos);
            }
        }
    }

    public void EndUltimate()
    {
        if (!IsUltimateActive) return;
        IsUltimateActive = false;
        ActiveUltimateRemainingTime = 0f;
        OnUltimateDeactivated?.Invoke();
        OnChargeChanged?.Invoke(CurrentCharge);
    }
}

public interface IUltimateHandler
{
    void Activate(PlayerUltimateController controller);
    float BaseDuration { get; }
}
