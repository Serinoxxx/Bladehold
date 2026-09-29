using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     The Hexer: walks to within ~10m of the nearest built tower (preferring one nobody is hexing
///     yet) and channels a beam into it. While the channel holds, the tower can't fire at all
///     (<see cref="DefenseStructure.IsHexed" />), and the HUD marks it HEXED. A hit from the hero
///     breaks the channel for a moment; killing the Hexer ends it. Tower hits don't interrupt it,
///     so a neighbouring tower can kill it but not stop it.
///
///     Movement goes through <see cref="AITargetSelector.SetTowerTarget" /> like the Sapper; the
///     prefab's NavMeshAgent stopping distance keeps it standing off. With no tower in range it falls
///     back to the base goblin melee.
/// </summary>
public class TowerHexer : MonoBehaviour
{
    [SerializeField] private TowerHexerSO data;
    [SerializeField] private Health health;
    [SerializeField] private AITargetSelector targetSelector;
    [Tooltip("Paused while channelling so the Hexer plants itself instead of walking on into the tower.")]
    [SerializeField] private AIMovement movement;
    [SerializeField] private Animator animator;
    [Tooltip("Optional: played at the tower when a hex lands.")]
    [SerializeField] private MMF_Player hexStartFeedback;
    [Tooltip("Optional: played at the Hexer when the hero breaks its channel.")]
    [SerializeField] private MMF_Player interruptFeedback;

    private DefenseStructure currentTower;
    private bool isChannelling;
    private float nextScanTime;
    private float resumeTime;
    private float nextPulseTime;
    private int castTriggerHash;
    private Health playerHealth;
    private LightningSystemChain beam;
    private Transform beamOrigin;
    private Transform beamTarget;
    private bool isDead;
    private bool playerDead;
    private bool anyError;

    public DefenseStructure CurrentTower => currentTower;
    public bool IsChannelling => isChannelling;

    private void OnValidate()
    {
        if (health == null) health = GetComponent<Health>();
        if (targetSelector == null) targetSelector = GetComponent<AITargetSelector>();
        if (movement == null) movement = GetComponent<AIMovement>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        if (data == null)
        {
            Debug.LogError($"{name}: TowerHexer.data (TowerHexerSO) is not assigned.", this);
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError($"{name}: TowerHexer.health is not assigned or found.", this);
            anyError = true;
        }
        if (targetSelector == null)
        {
            Debug.LogError($"{name}: TowerHexer needs an AITargetSelector to walk to towers.", this);
            anyError = true;
        }
        if (animator == null)
        {
            Debug.LogError($"{name}: TowerHexer.animator is not assigned or found.", this);
            anyError = true;
        }
        if (movement == null)
        {
            Debug.LogError($"{name}: TowerHexer.movement (AIMovement) is not assigned or found.", this);
            anyError = true;
        }
        if (anyError) return;

        castTriggerHash = Animator.StringToHash(data.castTrigger);
        health.OnDied += HandleDied;
        health.OnDamaged += HandleDamaged;

        Player player = Player.Instance;
        if (player != null && player.Health != null)
        {
            playerHealth = player.Health;
            playerHealth.OnDied += HandlePlayerDied;
        }

        SetUpBeam();
    }

    private void SetUpBeam()
    {
        if (data.beamPrefab == null)
        {
            // Still hexes; players just can't see who's doing it.
            Debug.LogError("[TowerHexer] TowerHexerSO.beamPrefab is not assigned.", data);
            return;
        }

        beamOrigin = new GameObject("HexBeamOrigin").transform;
        beamOrigin.SetParent(transform, false);
        beamOrigin.localPosition = Vector3.up * data.beamOriginHeight;
        beamTarget = new GameObject("HexBeamTarget").transform;
        beamTarget.SetParent(transform, false);

        GameObject beamObj = Instantiate(data.beamPrefab, transform);
        beam = beamObj.GetComponentInChildren<LightningSystemChain>(true);
        if (beam == null)
        {
            Debug.LogError("[TowerHexer] beamPrefab has no LightningSystemChain.", data.beamPrefab);
            Destroy(beamObj);
            return;
        }
        beam.autoScaleEnabled = false;
        beam.masterScale = 1f;
        beam.chainPoints = new[] { beamOrigin, beamTarget };
        beam.vfxEnabled = false; // its own toggle; SetActive before its Start throws in its OnDisable
    }

    private void OnDestroy()
    {
        StopChannel();
        if (health != null)
        {
            health.OnDied -= HandleDied;
            health.OnDamaged -= HandleDamaged;
        }
        if (playerHealth != null) playerHealth.OnDied -= HandlePlayerDied;
    }

    private void Update()
    {
        if (anyError || isDead || playerDead) return;

        if (!IsValidTarget(currentTower))
        {
            ReleaseTower();
            if (Time.time < nextScanTime) return;
            nextScanTime = Time.time + data.rescanInterval;

            currentTower = FindTower(transform.position, data.searchRadius);
            if (currentTower == null) return;
            targetSelector.SetTowerTarget(currentTower);
        }

        float flatDistance = FlatDistance(transform.position, currentTower.transform.position);
        if (isChannelling)
        {
            if (flatDistance > data.channelRange + data.breakRangeMargin) StopChannel();
        }
        else if (flatDistance <= data.channelRange && Time.time >= resumeTime)
        {
            StartChannel();
        }

        if (!isChannelling) return;

        if (beamTarget != null) beamTarget.position = currentTower.transform.position + Vector3.up * data.beamTargetHeight;
        FaceTower();
        if (Time.time >= nextPulseTime)
        {
            nextPulseTime = Time.time + data.castPulseInterval;
            animator.SetTrigger(castTriggerHash);
        }
    }

    private void StartChannel()
    {
        isChannelling = true;
        currentTower.AddHexer(this);
        movement.SetMovementPaused(true);
        nextPulseTime = 0f;
        if (beam != null) beam.vfxEnabled = true;
        if (hexStartFeedback != null) hexStartFeedback.PlayFeedbacks(currentTower.transform.position);
    }

    private void StopChannel()
    {
        if (!isChannelling) return;
        isChannelling = false;
        if (currentTower != null) currentTower.RemoveHexer(this);
        if (movement != null) movement.SetMovementPaused(false);
        if (beam != null) beam.vfxEnabled = false;
    }

    private void ReleaseTower()
    {
        StopChannel();
        currentTower = null;
        if (targetSelector != null) targetSelector.SetTowerTarget(null);
    }

    private void FaceTower()
    {
        Vector3 toTower = currentTower.transform.position - transform.position;
        toTower.y = 0f;
        if (toTower.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toTower), 360f * Time.deltaTime);
    }

    private void HandleDamaged(Damage damage)
    {
        if (damage == null || !damage.IsPlayerOwned || damage.isDefenseDamage || damage.isStatusEffect) return;
        resumeTime = Time.time + data.interruptDuration;
        if (!isChannelling) return;
        StopChannel();
        if (interruptFeedback != null) interruptFeedback.PlayFeedbacks(transform.position);
    }

    private void HandleDied()
    {
        isDead = true;
        ReleaseTower();
    }

    private void HandlePlayerDied()
    {
        playerDead = true;
        ReleaseTower();
    }

    private static bool IsValidTarget(DefenseStructure tower)
    {
        return tower != null && tower.isActiveAndEnabled && !tower.IsDepleted;
    }

    /// <summary>
    ///     Nearest tower with supply within <paramref name="radius" />, preferring one nobody is hexing
    ///     yet, so two Hexers split up instead of doubling up.
    /// </summary>
    public static DefenseStructure FindTower(Vector3 from, float radius)
    {
        DefenseStructure bestFree = null, bestAny = null;
        float bestFreeSqr = radius * radius, bestAnySqr = radius * radius;
        foreach (DefenseStructure tower in DefenseStructure.AllActive)
        {
            if (!IsValidTarget(tower)) continue;
            Vector3 offset = tower.transform.position - from;
            offset.y = 0f;
            float sqr = offset.sqrMagnitude;
            if (sqr <= bestAnySqr) { bestAnySqr = sqr; bestAny = tower; }
            if (!tower.IsHexed && sqr <= bestFreeSqr) { bestFreeSqr = sqr; bestFree = tower; }
        }
        return bestFree != null ? bestFree : bestAny;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        Vector3 offset = b - a;
        offset.y = 0f;
        return offset.magnitude;
    }
}
