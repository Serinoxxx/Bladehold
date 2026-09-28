using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     One of Captain Mogra's ground runes: a green telegraph that glows for its delay, with a fill disc
///     growing to the edge as the countdown, then erupts and hits the player if they're still inside.
///     Spawned already placed by <see cref="CaptainMograController" />; it runs on its own once armed, so a
///     rune that's been shown always goes off even if Mogra is staggered or killed mid-cast (an honest
///     telegraph). Only the player is hurt: his spells don't hit his own clan.
/// </summary>
public class HexRuneBlast : MonoBehaviour
{
    [Tooltip("Ground marker scaled to the blast diameter on x/z.")]
    [SerializeField] private Transform telegraphVisual;
    [Tooltip("Optional: a disc scaled from 0 to the blast diameter over the delay (the countdown). Leave empty for none.")]
    [SerializeField] private Transform fillVisual;
    [Tooltip("Eruption burst + sound at the rune.")]
    [SerializeField] private MMF_Player eruptFeedback;
    [Tooltip("Seconds the object lingers after erupting so the burst can finish.")]
    [SerializeField] private float lingerSeconds = 2f;
    [Tooltip("Vertical tolerance: a player more than this far above or below the rune isn't hit.")]
    [SerializeField] private float heightTolerance = 2.5f;

    private float radius;
    private float delay;
    private float damage;
    private IDamageable source;
    private HexHitGroup hitGroup;
    private float timer;
    private bool armed;
    private bool erupted;

    public bool Erupted => erupted;
    public float Radius => radius;

    private void Start()
    {
        if (telegraphVisual == null) Debug.LogError($"{name}: telegraphVisual is not assigned.", this);
        if (eruptFeedback == null) Debug.LogError($"{name}: eruptFeedback is not assigned.", this);
    }

    /// <summary>Starts the countdown. Call right after Instantiate at the rune's ground position.</summary>
    /// <param name="group">Optional: runes sharing a group hit the player at most once between them.</param>
    public void Arm(float blastRadius, float delaySeconds, float damageValue, IDamageable owner, HexHitGroup group = null)
    {
        hitGroup = group;
        radius = Mathf.Max(0.1f, blastRadius);
        delay = Mathf.Max(0f, delaySeconds);
        damage = damageValue;
        source = owner;
        timer = 0f;
        armed = true;

        if (telegraphVisual != null)
        {
            telegraphVisual.localScale = new Vector3(radius * 2f, telegraphVisual.localScale.y, radius * 2f);
        }
        SetFill(0f);
    }

    private void Update()
    {
        if (!armed || erupted) return;

        timer += Time.deltaTime;
        SetFill(delay > 0f ? Mathf.Clamp01(timer / delay) : 1f);
        if (timer >= delay) Erupt();
    }

    private void SetFill(float t)
    {
        if (fillVisual == null) return;
        float d = radius * 2f * t;
        fillVisual.localScale = new Vector3(d, fillVisual.localScale.y, d);
    }

    private void Erupt()
    {
        erupted = true;

        Player player = Player.Instance;
        if (player != null && player.Health != null && !player.Health.IsDead && IsInside(player.transform.position) &&
            (hitGroup == null || hitGroup.TryConsume()))
        {
            player.Health.ReceiveDamage(new Damage
            {
                value = damage,
                type = DamageType.elemental,
                unparryable = true,
                source = source,
                sourcePosition = transform.position,
            });
        }

        if (eruptFeedback != null) eruptFeedback.PlayFeedbacks(transform.position);
        if (telegraphVisual != null) telegraphVisual.gameObject.SetActive(false);
        if (fillVisual != null) fillVisual.gameObject.SetActive(false);
        Destroy(gameObject, lingerSeconds);
    }

    /// <summary>XZ distance within the radius, and roughly level with the rune.</summary>
    public bool IsInside(Vector3 worldPosition)
    {
        Vector3 delta = worldPosition - transform.position;
        if (Mathf.Abs(delta.y) > heightTolerance) return false;
        delta.y = 0f;
        return delta.sqrMagnitude <= radius * radius;
    }
}
