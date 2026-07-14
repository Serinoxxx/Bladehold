using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     The Dark Elf's lateral dodge: on a cooldown, while close to the player and inside the
///     player's facing cone (the <see cref="Parry" /> dot-product test, reversed — "am I being
///     aimed at?"), it burst-strafes sideways via <see cref="NavMeshAgent.Move" /> over
///     ~<see cref="DodgeDashSO.dashSeconds" />. The dodge lane is pre-checked with
///     <see cref="NavMesh.Raycast" /> (the <see cref="MountedKnightBrain" /> lane-clamp precedent):
///     a blocked side falls back to the other, and if both are walls the dodge is skipped.
///     <see cref="AIMovement" /> is paused for the burst so a repath doesn't fight the strafe.
///     A bow-aim-ray trigger can layer on later (the plan's open design question) — this timer
///     version is v1.
/// </summary>
public class DodgeDash : MonoBehaviour
{
    [SerializeField] private DodgeDashSO data;
    [SerializeField] private Health health;
    [SerializeField] private AIMovement movement;
    [SerializeField] private NavMeshAgent agent;

    private Transform player;
    private Health playerHealth;
    private float lastDodgeTime = Mathf.NegativeInfinity;
    private bool dashing;
    private bool isDead = false;
    private bool playerDead = false;
    private bool anyError = false;

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (movement == null)
        {
            movement = GetComponent<AIMovement>();
        }
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }
    }

    private void Start()
    {
        if (data == null)
        {
            Debug.LogError("DodgeDashSO is not assigned in the inspector.");
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (movement == null)
        {
            Debug.LogError("AIMovement component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (agent == null)
        {
            Debug.LogError("NavMeshAgent component is not assigned or found on the GameObject.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        Player playerInstance = Player.Instance;
        if (playerInstance == null)
        {
            Debug.LogError("Player.Instance is not set; the dark elf has no one to dodge.");
            anyError = true;
            return;
        }

        player = playerInstance.transform;

        health.OnDied += HandleDied;

        if (playerInstance.Health != null)
        {
            playerHealth = playerInstance.Health;
            playerHealth.OnDied += HandlePlayerDied;
        }

        // De-phase a pack of elves so they don't all dodge on the same frame.
        lastDodgeTime = Time.time - Random.value * data.dodgeCooldown;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
        }
        if (playerHealth != null)
        {
            playerHealth.OnDied -= HandlePlayerDied;
        }
    }

    private void HandleDied()
    {
        isDead = true;
        enabled = false;
    }

    private void HandlePlayerDied()
    {
        playerDead = true;
    }

    private void Update()
    {
        if (anyError || isDead || playerDead || dashing) return;

        if (Time.time - lastDodgeTime < data.dodgeCooldown) return;

        if (IsTargeted())
        {
            TryDodge();
        }
    }

    /// <summary>Close to the player and inside their facing cone — "the player is lining me up".</summary>
    private bool IsTargeted()
    {
        Vector3 toMe = transform.position - player.position;
        toMe.y = 0f;

        float sqrDistance = toMe.sqrMagnitude;
        if (sqrDistance > data.triggerDistance * data.triggerDistance || sqrDistance < 0.0001f)
        {
            return false;
        }

        Vector3 playerForward = player.forward;
        playerForward.y = 0f;
        return Vector3.Dot(playerForward.normalized, toMe.normalized) >= data.targetedDotThreshold;
    }

    private void TryDodge()
    {
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f)
        {
            return;
        }

        // Lateral to the player's line of sight; random side first, the other as fallback.
        Vector3 lateral = Vector3.Cross(Vector3.up, toPlayer.normalized);
        float side = Random.value < 0.5f ? 1f : -1f;

        Vector3 direction = lateral * side;
        if (IsDashBlocked(direction))
        {
            direction = -direction;
            if (IsDashBlocked(direction))
            {
                // Boxed in — no honest dodge lane. Re-check again next cooldown.
                lastDodgeTime = Time.time;
                return;
            }
        }

        lastDodgeTime = Time.time;
        StartCoroutine(RunDash(direction));
    }

    private bool IsDashBlocked(Vector3 direction)
    {
        // Blocked when a wall cuts the lane down to less than half the intended distance.
        if (NavMesh.Raycast(transform.position, transform.position + direction * data.dashDistance, out NavMeshHit hit, NavMesh.AllAreas))
        {
            return hit.distance < data.dashDistance * 0.5f;
        }
        return false;
    }

    private IEnumerator RunDash(Vector3 direction)
    {
        dashing = true;
        movement.SetMovementPaused(true);

        float speed = data.dashDistance / Mathf.Max(0.01f, data.dashSeconds);
        float elapsed = 0f;
        while (elapsed < data.dashSeconds && !isDead)
        {
            agent.Move(direction * speed * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!isDead)
        {
            movement.SetMovementPaused(false);
        }
        dashing = false;
    }
}
