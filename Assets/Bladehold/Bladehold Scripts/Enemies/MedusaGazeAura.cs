using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     The Medusa's petrifying gaze: while the player stands inside a cone in front of her
///     (range + dot-product test — the <see cref="Parry" /> facing-cone shape, pointed the other
///     way), their <see cref="StatType.MoveSpeed" /> is slowed by a Percent modifier. There is no
///     <c>RemoveModifier</c> on <see cref="PlayerStats" />, so leaving the gaze adds the exact
///     negative back (the <c>HoldTheLineBonus</c> idiom); <see cref="PlayerMoveSpeedBinder" /> picks
///     the change up live via <c>OnStatChanged</c>.
///
///     A <b>static refcount</b> guards the modifier so two Medusas can't stack the player to a
///     standstill: only the first gaze to land applies the slow, and only the last to release
///     removes it. Own death, <c>OnDestroy</c>, and player death all release this instance's hold.
/// </summary>
public class MedusaGazeAura : MonoBehaviour
{
    [SerializeField] private MedusaGazeAuraSO data;
    [SerializeField] private Health health;
    [Tooltip("Optional feedback played each time this medusa's gaze catches the player (a stony chord).")]
    [SerializeField] private MMF_Player gazeCaughtFeedback;
    [Tooltip("The lightning effect component to enable when gazing at the player.")]
    [SerializeField] private LightningSystemChain lightningEffect;
    [Tooltip("Transform on the Medusa to use as the origin for the lightning chain (e.g., an eye or head bone).")]
    [SerializeField] private Transform lightningOriginPoint;
    [Tooltip("Offset from the player's origin for the lightning chain.")]
    [SerializeField] private Vector3 lightningTargetOffset = new Vector3(0, 1.5f, 0);

    // How many medusas currently hold the player in their gaze, and the exact percent the first
    // one applied (so the last release cancels precisely even if the SO is retuned mid-run).
    private static int activeGazeCount;
    private static float appliedSlowPercent;

    private Health playerHealth;
    private Transform player;
    private PlayerStats stats;
    private float lastTickTime;
    private bool gazing;
    private bool isDead = false;
    private bool playerDead = false;
    private bool anyError = false;

    private Transform lightningTargetTransform;

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
    }

    private void Start()
    {
        if (data == null)
        {
            Debug.LogError("MedusaGazeAuraSO is not assigned in the inspector.");
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }

        Player playerInstance = Player.Instance;
        if (playerInstance == null)
        {
            Debug.LogError("Player.Instance is not set; the medusa has no one to gaze at.");
            anyError = true;
        }
        else
        {
            player = playerInstance.transform;
            stats = playerInstance.Stats;
            if (stats == null)
            {
                Debug.LogError("MedusaGazeAura could not find PlayerStats via Player.Instance.");
                anyError = true;
            }
        }

        if (anyError)
        {
            return;
        }

        health.OnDied += HandleDied;

        if (Player.Instance.Health != null)
        {
            playerHealth = Player.Instance.Health;
            playerHealth.OnDied += HandlePlayerDied;
        }

        if (!anyError)
        {
            lightningTargetTransform = new GameObject("LightningTarget").transform;
            lightningTargetTransform.SetParent(player, false);
            lightningTargetTransform.localPosition = lightningTargetOffset;
        }

        lastTickTime = Time.time - Random.value * data.tickInterval;
    }

    private void OnDestroy()
    {
        // Scene teardown races the player's destruction — only unwind the modifier if the stats
        // object is still alive to receive it.
        ReleaseGaze();

        if (health != null)
        {
            health.OnDied -= HandleDied;
        }
        if (playerHealth != null)
        {
            playerHealth.OnDied -= HandlePlayerDied;
        }

        if (lightningTargetTransform != null) Destroy(lightningTargetTransform.gameObject);
    }

    private void HandleDied()
    {
        isDead = true;
        ReleaseGaze();
        enabled = false;
    }

    private void HandlePlayerDied()
    {
        playerDead = true;
        ReleaseGaze();
    }

    private void Update()
    {
        if (anyError || isDead || playerDead) return;

        if (Time.time - lastTickTime < data.tickInterval) return;
        lastTickTime = Time.time;

        if (IsPlayerInGazeCone())
        {
            BeginGaze();
        }
        else
        {
            ReleaseGaze();
        }
    }

    private bool IsPlayerInGazeCone()
    {
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;

        float sqrDistance = toPlayer.sqrMagnitude;
        if (sqrDistance > data.range * data.range || sqrDistance < 0.0001f)
        {
            return false;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        return Vector3.Dot(forward.normalized, toPlayer.normalized) >= Mathf.Cos(data.halfAngleDegrees * Mathf.Deg2Rad);
    }

    private void BeginGaze()
    {
        if (gazing)
        {
            return;
        }
        gazing = true;

        // Only the first gaze applies the slow — later medusas just hold a reference on it.
        activeGazeCount++;
        if (activeGazeCount == 1)
        {
            appliedSlowPercent = data.slowFraction;
            stats.AddModifier(StatType.MoveSpeed, ModifierKind.Percent, -appliedSlowPercent);
        }

        if (gazeCaughtFeedback != null)
        {
            gazeCaughtFeedback.PlayFeedbacks();
        }

        if (lightningEffect != null)
        {
            Transform origin = lightningOriginPoint != null ? lightningOriginPoint : transform;
            lightningEffect.chainPoints = new Transform[] { origin, lightningTargetTransform };
            lightningEffect.gameObject.SetActive(true);
        }
    }

    private void ReleaseGaze()
    {
        if (!gazing)
        {
            return;
        }
        gazing = false;

        activeGazeCount--;
        if (activeGazeCount == 0 && stats != null)
        {
            // Add the exact negative back — the HoldTheLineBonus cancellation idiom.
            stats.AddModifier(StatType.MoveSpeed, ModifierKind.Percent, appliedSlowPercent);
        }

        if (lightningEffect != null)
        {
            lightningEffect.gameObject.SetActive(false);
        }
    }
}
