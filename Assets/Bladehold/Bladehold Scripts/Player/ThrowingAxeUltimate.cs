using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Throwing Axe ranged ultimate: Axe Storm (card <c>taxe_vortex_ult</c>). For the duration, throws
///     have no wind-up and a short cooldown, fly as a three-axe fan, and every axe ricochets to
///     <see cref="StatType.UltimateAxeRicochetCount" /> more enemies (<see cref="AxeProjectile" /> does
///     the bouncing). A ranged storm you aim, where the axe's Whirlwind owns the space around you.
/// </summary>
public class ThrowingAxeUltimate : MonoBehaviour, IUltimateHandler
{
    [SerializeField] private AxeStormConfigSO config;

    [Header("Feedback")]
    [Tooltip("Played on the player when the storm starts (war cry, red flash).")]
    [SerializeField] private MMF_Player activateFeedback;
    [Tooltip("Played at each ricochet point (metal ring, spark burst).")]
    [SerializeField] private MMF_Player ricochetFeedback;
    [Tooltip("Optional: played on the player when the storm ends. Leave empty for silence.")]
    [SerializeField] private MMF_Player endFeedback;
    [Tooltip("Optional: looping red aura on the player while the storm runs.")]
    [SerializeField] private GameObject auraVisual;

    public float BaseDuration => config != null && config.baseDuration > 0f ? config.baseDuration : 7f;

    private Player player;
    private PlayerThrownAxe playerThrownAxe;
    private PlayerUltimateController controller;
    private float endTime;
    private bool isRunning;
    private bool anyError;
    private bool validated;

    private void Awake()
    {
        player = transform.root.GetComponentInChildren<Player>(true);
        if (player != null && config != null && player.Stats != null)
        {
            player.Stats.SetBase(StatType.UltimateAxeRicochetCount, config.ricochetCount);
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

        if (player == null) { Debug.LogError("ThrowingAxeUltimate: no Player under the root.", this); anyError = true; }
        else if (player.Stats == null) { Debug.LogError("ThrowingAxeUltimate: Player has no PlayerStats.", this); anyError = true; }
        playerThrownAxe = transform.root.GetComponentInChildren<PlayerThrownAxe>(true);
        if (playerThrownAxe == null) { Debug.LogError("ThrowingAxeUltimate: no PlayerThrownAxe under the root.", this); anyError = true; }
        if (config == null) { Debug.LogError("ThrowingAxeUltimate: config is not assigned.", this); anyError = true; }
        if (activateFeedback == null) Debug.LogError("ThrowingAxeUltimate: activateFeedback is not assigned.", this);
        if (ricochetFeedback == null) Debug.LogError("ThrowingAxeUltimate: ricochetFeedback is not assigned.", this);
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

        playerThrownAxe.IsVortexUltimateActive = true;
        playerThrownAxe.UltimateRicochets = Mathf.Max(0, Mathf.RoundToInt(player.Stats.GetValue(StatType.UltimateAxeRicochetCount)));
        playerThrownAxe.UltimateRicochetRange = config.ricochetRange;
        playerThrownAxe.OnRicochet += HandleRicochet;

        if (activateFeedback != null) activateFeedback.PlayFeedbacks(player.transform.position);
        if (auraVisual != null) auraVisual.SetActive(true);
    }

    private void Update()
    {
        if (!isRunning) return;
        if (Time.time >= endTime || player.Health.IsDead) End();
    }

    private void HandleRicochet(Vector3 point, Vector3 direction)
    {
        if (ricochetFeedback == null) return;
        ricochetFeedback.transform.rotation = Quaternion.LookRotation(direction);
        ricochetFeedback.PlayFeedbacks(point);
    }

    private void End()
    {
        if (!isRunning) return;
        isRunning = false;

        if (playerThrownAxe != null)
        {
            playerThrownAxe.IsVortexUltimateActive = false;
            playerThrownAxe.UltimateRicochets = 0;
            playerThrownAxe.OnRicochet -= HandleRicochet;
        }
        if (auraVisual != null) auraVisual.SetActive(false);
        if (endFeedback != null && player != null) endFeedback.PlayFeedbacks(player.transform.position);

        controller?.EndUltimate();
    }

    private void OnDisable()
    {
        End();
    }
}
