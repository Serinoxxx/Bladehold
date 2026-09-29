using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     The Sapper: ignores the player and runs for the nearest built tower that still has supply,
///     then hacks supply out of it until it's empty or the sapper dies. It stands inside the
///     tower's blind spot, so the tower can't defend itself: a neighbouring tower or the hero has to.
///
///     Movement goes through <see cref="AITargetSelector.SetTowerTarget" />, so <see cref="AIMovement" />
///     paths to the tower and <see cref="AIAttack" /> stays idle while it's sapping. With no tower
///     in range it falls back to the base goblin melee.
/// </summary>
public class TowerSapper : MonoBehaviour
{
    [SerializeField] private TowerSapperSO data;
    [SerializeField] private Health health;
    [SerializeField] private AITargetSelector targetSelector;
    [SerializeField] private Animator animator;
    [Tooltip("Optional: played at the tower on each drain tick (hacking sound, wood chips).")]
    [SerializeField] private MMF_Player drainFeedback;

    private DefenseStructure currentTower;
    private float nextScanTime;
    private float nextDrainTime;
    private int drainTriggerHash;
    private Health playerHealth;
    private bool isDead;
    private bool playerDead;
    private bool anyError;

    public DefenseStructure CurrentTower => currentTower;

    private void OnValidate()
    {
        if (health == null) health = GetComponent<Health>();
        if (targetSelector == null) targetSelector = GetComponent<AITargetSelector>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        if (data == null)
        {
            Debug.LogError($"{name}: TowerSapper.data (TowerSapperSO) is not assigned.", this);
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError($"{name}: TowerSapper.health is not assigned or found.", this);
            anyError = true;
        }
        if (targetSelector == null)
        {
            Debug.LogError($"{name}: TowerSapper needs an AITargetSelector to walk to towers.", this);
            anyError = true;
        }
        if (animator == null)
        {
            Debug.LogError($"{name}: TowerSapper.animator is not assigned or found.", this);
            anyError = true;
        }
        if (anyError) return;

        drainTriggerHash = Animator.StringToHash(data.drainTrigger);
        health.OnDied += HandleDied;

        Player player = Player.Instance;
        if (player != null && player.Health != null)
        {
            playerHealth = player.Health;
            playerHealth.OnDied += HandlePlayerDied;
        }
    }

    private void OnDestroy()
    {
        if (health != null) health.OnDied -= HandleDied;
        if (playerHealth != null) playerHealth.OnDied -= HandlePlayerDied;
    }

    private void Update()
    {
        if (anyError || isDead || playerDead) return;

        if (!IsValidTarget(currentTower))
        {
            currentTower = null;
            targetSelector.SetTowerTarget(null);

            if (Time.time < nextScanTime) return;
            nextScanTime = Time.time + data.rescanInterval;

            currentTower = FindNearestTower(transform.position, data.searchRadius);
            if (currentTower == null) return;
            targetSelector.SetTowerTarget(currentTower);
        }

        if (Time.time < nextDrainTime) return;
        if (!IsWithinFlatDistance(transform.position, currentTower.transform.position, data.drainRange)) return;

        nextDrainTime = Time.time + data.drainInterval;
        animator.SetTrigger(drainTriggerHash);
        if (drainFeedback != null) drainFeedback.PlayFeedbacks(currentTower.transform.position);
        currentTower.ConsumeSupply(data.drainPerTick);
    }

    private void HandleDied()
    {
        isDead = true;
        currentTower = null;
        if (targetSelector != null) targetSelector.SetTowerTarget(null);
    }

    private void HandlePlayerDied()
    {
        playerDead = true;
        currentTower = null;
        if (targetSelector != null) targetSelector.SetTowerTarget(null);
    }

    private static bool IsValidTarget(DefenseStructure tower)
    {
        return tower != null && tower.isActiveAndEnabled && !tower.IsDepleted;
    }

    /// <summary>Nearest built tower with supply left, by flat distance, within <paramref name="radius" />.</summary>
    public static DefenseStructure FindNearestTower(Vector3 from, float radius)
    {
        DefenseStructure best = null;
        float bestSqr = radius * radius;
        foreach (DefenseStructure tower in DefenseStructure.AllActive)
        {
            if (!IsValidTarget(tower)) continue;
            Vector3 offset = tower.transform.position - from;
            offset.y = 0f;
            float sqr = offset.sqrMagnitude;
            if (sqr <= bestSqr)
            {
                bestSqr = sqr;
                best = tower;
            }
        }
        return best;
    }

    private static bool IsWithinFlatDistance(Vector3 a, Vector3 b, float distance)
    {
        Vector3 offset = b - a;
        offset.y = 0f;
        return offset.sqrMagnitude <= distance * distance;
    }
}
