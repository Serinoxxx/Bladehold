using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Which weapon an ultimate belongs to: the held melee weapon's or the held ranged weapon's.</summary>
public enum UltimateSlot
{
    Melee,
    Ranged
}

/// <summary>Why an ultimate can't be fired right now (<see cref="None" /> when it can).</summary>
public enum UltimateBlockReason
{
    None,
    NotAllowedHere,
    NoUltimate,
    NoArcaneCore,
    AlreadyActive
}

/// <summary>
///     Runs the player's ultimates. Every held weapon's ultimate is always unlocked (the melee weapon's and the
///     ranged weapon's, see <see cref="DraftUpgradeService.GetUltimate" />), and each activation spends one
///     Arcane Core (<see cref="RunSession.ArcaneCores" />). The Ultimate Wheel (<see cref="UltimateWheelUI" />)
///     picks which one to fire through <see cref="TryActivate" />. Only one ultimate runs at a time.
/// </summary>
public class PlayerUltimateController : MonoBehaviour
{
    public event Action OnUltimateActivated;
    public event Action OnUltimateDeactivated;

    public bool IsUltimateActive { get; private set; }
    public UltimateSlot ActiveSlot { get; private set; }
    public float ActiveUltimateDuration { get; private set; }
    public float ActiveUltimateRemainingTime { get; private set; }

    private Player player;

    public bool HasUltimate(UltimateSlot slot) => DraftUpgradeService.GetUltimateId(slot) != null;

    private void Awake()
    {
        player = GetComponentInChildren<Player>();
        RegisterDefaultStats();
    }

    private void Start()
    {
        RegisterDefaultStats();
        SyncUltimateHandlers();
    }

    private float nextEyeOfStormStrikeTime = 0f;

    private void Update()
    {
        if (player == null || player.Stats == null || !IsUltimateActive) return;

        ActiveUltimateRemainingTime = Mathf.Max(0f, ActiveUltimateRemainingTime - Time.deltaTime);

        float eyeDmg = player.Stats.GetValue(StatType.LightningEyeOfTheStormDamage);
        if (eyeDmg > 0f && Time.time >= nextEyeOfStormStrikeTime)
        {
            nextEyeOfStormStrikeTime = Time.time + 1.0f;
            TriggerEyeOfTheStorm(eyeDmg);
        }
    }

    private void RegisterDefaultStats()
    {
        if (player != null && player.Stats != null)
        {
            player.Stats.SetBase(StatType.UltimateDurationSeconds, 6f);
            player.Stats.SetBase(StatType.FireInfernoBurstUnlocked, 0f);
            player.Stats.SetBase(StatType.LightningEyeOfTheStormDamage, 0f);
        }
    }

    /// <summary>Why <paramref name="slot" />'s ultimate can't fire now, or <see cref="UltimateBlockReason.None" />.</summary>
    public UltimateBlockReason GetBlockReason(UltimateSlot slot)
    {
        if (!SceneAbilityRules.UltimateAllowed) return UltimateBlockReason.NotAllowedHere;
        if (!HasUltimate(slot)) return UltimateBlockReason.NoUltimate;
        if (IsUltimateActive) return UltimateBlockReason.AlreadyActive;
        if (RunSession.ArcaneCores <= 0) return UltimateBlockReason.NoArcaneCore;
        return UltimateBlockReason.None;
    }

    /// <summary>
    ///     Spends one Arcane Core and fires <paramref name="slot" />'s ultimate. Returns false (and spends
    ///     nothing) when <see cref="GetBlockReason" /> says it can't fire; the caller plays the denied feedback.
    /// </summary>
    public bool TryActivate(UltimateSlot slot)
    {
        if (player == null || player.Stats == null) return false;
        if (GetBlockReason(slot) != UltimateBlockReason.None) return false;

        SyncUltimateHandlers();
        IUltimateHandler handler = DraftUpgradeService.GetUltimateHandler(player, DraftUpgradeService.GetUltimateId(slot));
        if (handler == null)
        {
            Debug.LogError($"[PlayerUltimateController] No handler for {slot} ultimate '{DraftUpgradeService.GetUltimateId(slot)}'.");
            return false;
        }

        if (!RunSession.TrySpendArcaneCore()) return false;
        ActivateUltimate(slot, handler);
        return true;
    }

    private void ActivateUltimate(UltimateSlot slot, IUltimateHandler activeHandler)
    {
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
        nextEyeOfStormStrikeTime = 0f;

        OnUltimateActivated?.Invoke();

        if (player.Stats.GetValue(StatType.FireInfernoBurstUnlocked) > 0f)
        {
            TriggerInfernoBurst();
        }

        activeHandler.Activate(this);
    }

    /// <summary>Enables the handlers for the held weapons' ultimates (and disables the rest).</summary>
    public void SyncUltimateHandlers()
    {
        if (player == null) player = GetComponentInChildren<Player>();
        if (player == null || player.Stats == null) return;

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
    }
}

public interface IUltimateHandler
{
    void Activate(PlayerUltimateController controller);
    float BaseDuration { get; }
}
