using UnityEngine;

/// <summary>
///     The "Freezing Draw" skill line: while the bow is drawn (<see cref="PlayerBow.IsAiming" />),
///     enemies within <see cref="BowSO.freezingDrawRadius" /> of the player are slowed by
///     <see cref="StatType.FreezingDrawSlowPercent" /> (base 0 = locked). Polls the bow the way
///     <see cref="SwordChargeFeedback" /> polls <see cref="PlayerAttack" />, re-applying a short
///     <see cref="SlowStatus" /> each tick so the chill fades naturally once the draw ends or the
///     enemy leaves the ring — unless "Elongated Freeze" (<see cref="StatType.SlowDurationBonusSeconds" />)
///     makes it linger.
/// </summary>
public class FreezingDraw : MonoBehaviour
{
    [Tooltip("The player's bow, polled for IsAiming. Auto-wired from this GameObject.")]
    [SerializeField] private PlayerBow bow;
    [Tooltip("Optional; defaults to Player.Instance.Stats.")]
    [SerializeField] private PlayerStats stats;
    [Tooltip("The bow's config — freezingDrawRadius lives there with the rest of the bow tunables.")]
    [SerializeField] private BowSO config;
    [Tooltip("Layers enemies live on (the ChainLightning convention: exclude the player and environment).")]
    [SerializeField] private LayerMask enemyLayers = ~0;

    // Slows are re-applied on a coarse tick, not per frame — an OverlapSphere over the horde every
    // frame is the kind of cost the AIMovement repath stagger exists to avoid.
    private const float TickInterval = 0.25f;
    // Grace beyond the next tick so the slow doesn't flicker off between re-applications.
    private const float LingerSeconds = 0.2f;
    private const int MaxOverlapResults = 64;

    private readonly Collider[] overlapBuffer = new Collider[MaxOverlapResults];

    private float nextTickTime;
    private bool anyError = false;

    private void OnValidate()
    {
        if (bow == null)
        {
            bow = GetComponent<PlayerBow>();
        }
        if (stats == null)
        {
            stats = GetComponent<PlayerStats>();
        }
    }

    private void Start()
    {
        if (stats == null)
        {
            stats = Player.Instance != null ? Player.Instance.Stats : null;
        }

        if (bow == null)
        {
            Debug.LogError("FreezingDraw 'bow' (the PlayerBow to poll) is not assigned or found.");
            anyError = true;
        }
        if (stats == null)
        {
            Debug.LogError("FreezingDraw could not find PlayerStats (set it or ensure Player.Instance.Stats exists).");
            anyError = true;
        }
        if (config == null)
        {
            Debug.LogError("FreezingDraw 'config' (the BowSO) is not assigned in the inspector.");
            anyError = true;
        }
    }

    private void Update()
    {
        if (anyError || !bow.IsAiming || Time.time < nextTickTime)
        {
            return;
        }
        nextTickTime = Time.time + TickInterval;

        float fraction = stats.GetValue(StatType.FreezingDrawSlowPercent);
        if (fraction <= 0f)
        {
            return;
        }

        float duration = TickInterval + LingerSeconds + stats.GetValue(StatType.SlowDurationBonusSeconds);
        int count = Physics.OverlapSphereNonAlloc(transform.position, config.freezingDrawRadius, overlapBuffer, enemyLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            // GetOrAdd dedupes to the Health root, and re-applying the same slow twice in one tick
            // (an enemy's several colliders) is a harmless max().
            SlowStatus slow = SlowStatus.GetOrAdd(overlapBuffer[i]);
            if (slow != null)
            {
                slow.ApplySlow(fraction, duration);
            }
        }
    }
}
