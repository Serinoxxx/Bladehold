using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Sword ultimate: Moonlight Edge (card <c>sword_blade_tempest</c>, id kept for save compatibility).
///     For the duration, every sword swing also fires a <see cref="MoonlightCrescent" />: a wave of light
///     that flies out in a line and cuts through everything in its path. Charged swings fire bigger,
///     harder-hitting crescents. Where the axe's Whirlwind clears the crowd around you, this one
///     reaches out: lanes, backliners and elites at range.
///
///     Hooks <see cref="DamageTrigger.OnActivated" /> on the active melee weapon only while running.
/// </summary>
public class SwordMoonlightEdgeUltimate : MonoBehaviour, IUltimateHandler
{
    [SerializeField] private MoonlightEdgeConfigSO config;

    [Tooltip("The crescent wave prefab (MoonlightCrescent + its authored visual).")]
    [SerializeField] private MoonlightCrescent crescentPrefab;

    [Header("Feedback")]
    [Tooltip("Played on the player when the ultimate starts (moon flash, ringing blade sound).")]
    [SerializeField] private MMF_Player activateFeedback;
    [Tooltip("Played at the launch point of every crescent (whoosh).")]
    [SerializeField] private MMF_Player crescentLaunchFeedback;
    [Tooltip("Optional: played where a crescent cuts an enemy. Leave empty for silence.")]
    [SerializeField] private MMF_Player crescentHitFeedback;
    [Tooltip("Optional: played on the player when the ultimate ends. Leave empty for silence.")]
    [SerializeField] private MMF_Player endFeedback;
    [Tooltip("Optional: looping glow around the blade, shown while the ultimate runs.")]
    [SerializeField] private GameObject auraVisual;

    public float BaseDuration => config != null && config.baseDuration > 0f ? config.baseDuration : 8f;

    private Player player;
    private PlayerAttack playerAttack;
    private PlayerUltimateController controller;
    private DamageTrigger hookedTrigger;
    private bool isRunning;
    private float endTime;
    private float lastCrescentTime = -999f;
    private bool anyError;
    private bool validated;

    private void Awake()
    {
        player = transform.root.GetComponentInChildren<Player>(true);
        if (player != null && config != null && player.Stats != null)
        {
            player.Stats.SetBase(StatType.UltimateMoonlightCrescentDamage, config.crescentDamageMultiplier);
        }
    }

    private void Start()
    {
        Validate();
    }

    /// <summary>Resolves and checks dependencies once. Runs from Start, or from Activate when the
    /// handler is enabled and fired in the same frame (before its Start).</summary>
    private void Validate()
    {
        if (validated) return;
        validated = true;

        if (player == null) { Debug.LogError("SwordMoonlightEdgeUltimate: no Player under the root.", this); anyError = true; }
        else if (player.Stats == null) { Debug.LogError("SwordMoonlightEdgeUltimate: Player has no PlayerStats.", this); anyError = true; }
        if (config == null) { Debug.LogError("SwordMoonlightEdgeUltimate: config is not assigned.", this); anyError = true; }
        if (crescentPrefab == null) { Debug.LogError("SwordMoonlightEdgeUltimate: crescentPrefab is not assigned.", this); anyError = true; }
        if (activateFeedback == null) Debug.LogError("SwordMoonlightEdgeUltimate: activateFeedback is not assigned.", this);
        if (crescentLaunchFeedback == null) Debug.LogError("SwordMoonlightEdgeUltimate: crescentLaunchFeedback is not assigned.", this);

        if (player != null) playerAttack = player.GetComponent<PlayerAttack>();
        if (auraVisual != null) auraVisual.SetActive(false);
    }

    public void Activate(PlayerUltimateController controller)
    {
        this.controller = controller;
        Validate();
        if (anyError)
        {
            controller?.EndUltimate();
            return;
        }

        float duration = player.Stats.GetValue(StatType.UltimateDurationSeconds);
        if (duration <= 0f) duration = BaseDuration;
        endTime = Time.time + duration;
        isRunning = true;

        HookTrigger();
        if (PlayerWeaponManager.Instance != null) PlayerWeaponManager.Instance.OnMeleeChanged += HandleMeleeChanged;

        if (activateFeedback != null) activateFeedback.PlayFeedbacks(player.transform.position);
        if (auraVisual != null) auraVisual.SetActive(true);
    }

    private void Update()
    {
        if (!isRunning) return;
        if (Time.time >= endTime || player.Health.IsDead) End();
    }

    private void HandleMeleeChanged(WeaponDefinitionSO def)
    {
        UnhookTrigger();
        HookTrigger();
    }

    private void HookTrigger()
    {
        if (PlayerWeaponManager.Instance == null) return;
        hookedTrigger = PlayerWeaponManager.Instance.ActiveMeleeTrigger;
        if (hookedTrigger != null) hookedTrigger.OnActivated += HandleSwing;
    }

    private void UnhookTrigger()
    {
        if (hookedTrigger != null) hookedTrigger.OnActivated -= HandleSwing;
        hookedTrigger = null;
    }

    private void HandleSwing()
    {
        if (!isRunning || Time.time - lastCrescentTime < config.minInterval) return;
        lastCrescentTime = Time.time;

        // 0 on a tap swing, 1 on a fully charged one (the charge is latched on release, before the hitbox opens).
        float charge01 = 0f;
        if (playerAttack != null && playerAttack.MaxChargeLevels > 0)
        {
            charge01 = Mathf.Clamp01((float)playerAttack.ChargeLevel / playerAttack.MaxChargeLevels);
        }

        Vector3 forward = player.transform.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        Vector3 origin = player.transform.position + Vector3.up * config.height + forward * 0.8f;

        float allDamage = player.Stats.GetValue(StatType.AllDamageMultiplier);
        float damage = player.Stats.GetValue(StatType.SwordDamage)
                       * player.Stats.GetValue(StatType.UltimateMoonlightCrescentDamage)
                       * (1f + config.fullChargeDamageBonus * charge01)
                       * (allDamage > 0f ? allDamage : 1f);

        MoonlightCrescent crescent = Instantiate(crescentPrefab, origin, Quaternion.LookRotation(forward));
        crescent.Launch(new MoonlightCrescent.LaunchSpec
        {
            direction = forward,
            speed = config.speed,
            range = config.range,
            halfWidth = config.halfWidth,
            scale = Mathf.Lerp(1f, config.fullChargeScale, charge01),
            damageTemplate = new Damage
            {
                value = damage,
                type = DamageType.slash,
                knockbackForce = config.knockback,
                source = player.Damageable,
                isPlayerDamage = true,
                elementId = RunSession.GetActiveElement("SLOT_ULTIMATE"),
            },
            ownerRoot = player.transform.root,
            onHit = HandleCrescentHit,
        });

        if (crescentLaunchFeedback != null) crescentLaunchFeedback.PlayFeedbacks(origin);
    }

    private void HandleCrescentHit(Vector3 point)
    {
        if (crescentHitFeedback != null) crescentHitFeedback.PlayFeedbacks(point);
    }

    private void End()
    {
        if (!isRunning) return;
        isRunning = false;

        UnhookTrigger();
        if (PlayerWeaponManager.Instance != null) PlayerWeaponManager.Instance.OnMeleeChanged -= HandleMeleeChanged;
        if (auraVisual != null) auraVisual.SetActive(false);
        if (endFeedback != null && player != null) endFeedback.PlayFeedbacks(player.transform.position);

        controller?.EndUltimate();
    }

    private void OnDisable()
    {
        End();
    }
}
