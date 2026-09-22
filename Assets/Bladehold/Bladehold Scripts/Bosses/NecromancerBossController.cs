using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Master boss controller for Malakor the Necromancer in the Crypt Arena (Part 4 of Castle Campaign Overhaul).
///     Coordinates the two-phase boss battle:
///     Phase 1: Invulnerable Bubble Shield, ritual channeling, and summoning 8-10 Crypt Skeletons.
///     Phase 2: Shield shatter on skeleton clear, Scythe draw, aggressive charges, and 180-degree telegraphed sweeping slashes.
/// </summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(NavMeshAgent))]
public class NecromancerBossController : MonoBehaviour
{
    [Header("Boss Identity")]
    [SerializeField] private string bossDisplayName = "Malakor, The Necromancer";
    [SerializeField] private string bossTitle = "Architect of the Siege";

    [Header("Phase 1: Bubble Shield & Summoning")]
    [SerializeField] private GameObject bubbleShieldVisual;
    [SerializeField] private Material bubbleShieldMaterial;
    [SerializeField] private float bubbleRadius = 2.4f;
    [SerializeField] private AudioClip bubbleDeflectSfx;
    [SerializeField] private AudioClip shieldShatterSfx;
    [SerializeField] private GameObject shieldShatterVfxPrefab;
    [SerializeField] private GameObject summoningCirclePrefab;
    [SerializeField] private Transform[] skeletonSpawnPoints;
    [SerializeField] private GameObject[] skeletonPrefabs;
    [SerializeField] private int targetSkeletonCount = 8;

    [Header("Phase 2: Scythe Sweeps & Direct Combat")]
    [SerializeField] private GameObject scytheWeaponObject;
    [SerializeField] private float normalMoveSpeed = 4.5f;
    [SerializeField] private float chargeMoveSpeed = 8.5f;
    [SerializeField] private float sweepAttackRange = 3.2f;
    [SerializeField] private float sweepAttackDamage = 38f;
    [SerializeField] private float sweepKnockback = 8.5f;
    [SerializeField] private float sweepWindup = 0.65f;
    [SerializeField] private float attackCooldown = 2.2f;

    [Header("Audio")]
    [SerializeField] private AudioClip laughVoiceSfx;
    [SerializeField] private AudioClip sweepWhooshSfx;
    [SerializeField] private AudioClip sweepImpactSfx;
    [SerializeField] private AudioClip victoryMusicSfx;

    [Header("Telegraph Arc")]
    [SerializeField] private GameObject sweepTelegraphArc;

    [Header("Defeat Rewards")]
    [SerializeField] private int goldReward = 150;
    [SerializeField] private int bloodReward = 20;
    [SerializeField] private int metalReward = 6;
    [SerializeField] private GameObject coinPrefab;

    private Health health;
    private NavMeshAgent agent;
    private Animator animator;
    private Collider bossCollider;

    private readonly List<CryptSkeletonAI> activeSkeletons = new List<CryptSkeletonAI>();
    private int totalSpawnedSkeletons = 0;
    private int aliveSkeletonCount = 0;

    private bool isFightActive = false;
    private bool isPhaseTwo = false;
    private bool isShieldActive = false;
    private bool isAttacking = false;
    private bool isDead = false;
    private float nextAttackTime = 0f;

    private static readonly int HashMoveSpeed = Animator.StringToHash("MoveSpeed");
    private static readonly int HashAttack = Animator.StringToHash("Attack");
    private static readonly int HashDeath = Animator.StringToHash("Death");
    private static readonly int HashCheer = Animator.StringToHash("Cheer");

    public int AliveSkeletonCount => aliveSkeletonCount;
    public int TotalSkeletonCount => targetSkeletonCount;
    public bool IsPhaseTwo => isPhaseTwo;
    public bool IsShieldActive => isShieldActive;

    public event Action<int, int> OnSkeletonCountChanged;
    public event Action OnShieldShattered;
    public event Action OnBossDefeated;

    private void Awake()
    {
        EnsureComponents();
    }

    public void EnsureComponents()
    {
        if (health == null) health = GetComponent<Health>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (bossCollider == null) bossCollider = GetComponent<Collider>();

        if (scytheWeaponObject == null)
        {
            // Auto locate scythe in children
            Transform scytheT = transform.Find("SM_Wep_Staff_DoubleBlade_01") ?? transform.Find("Scythe");
            if (scytheT != null) scytheWeaponObject = scytheT.gameObject;
        }

        if (sweepTelegraphArc == null)
        {
            CreateProceduralTelegraphArc();
        }
    }

    private void Start()
    {
        EnsureComponents();

        if (health != null)
        {
            health.OnDied += HandleBossDied;
        }

        if (agent != null)
        {
            agent.speed = normalMoveSpeed;
            agent.stoppingDistance = sweepAttackRange * 0.75f;
            agent.isStopped = true;
        }

        // Initialize Bubble Shield Visual
        CreateOrConfigureBubbleShield();

        // Hide telegraph arc initially
        if (sweepTelegraphArc != null)
        {
            sweepTelegraphArc.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleBossDied;
            health.TryBlockDamage -= HandleTryBlockDamage;
        }
    }

    private void CreateOrConfigureBubbleShield()
    {
        if (bubbleShieldVisual == null)
        {
            bubbleShieldVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bubbleShieldVisual.name = "Necromancer_BubbleShield";
            bubbleShieldVisual.transform.SetParent(transform, false);
            bubbleShieldVisual.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            bubbleShieldVisual.transform.localScale = Vector3.one * (bubbleRadius * 2f);

            Collider col = bubbleShieldVisual.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            Renderer rend = bubbleShieldVisual.GetComponent<Renderer>();
            if (rend != null)
            {
                if (bubbleShieldMaterial != null)
                {
                    rend.sharedMaterial = bubbleShieldMaterial;
                }
                else
                {
                    Shader urpShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    Material mat = new Material(urpShader);
                    mat.name = "BubbleShield_Purple_Mat";
                    mat.color = new Color(0.6f, 0.1f, 0.9f, 0.45f);
                    rend.sharedMaterial = mat;
                }
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        bubbleShieldVisual.SetActive(true);
    }

    private void CreateProceduralTelegraphArc()
    {
        sweepTelegraphArc = new GameObject("SweepTelegraphArc");
        sweepTelegraphArc.transform.SetParent(transform, false);
        sweepTelegraphArc.transform.localPosition = new Vector3(0f, 0.05f, 0f);

        LineRenderer lr = sweepTelegraphArc.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.startWidth = 0.15f;
        lr.endWidth = 0.15f;

        int segments = 24;
        lr.positionCount = segments + 2;

        float radius = sweepAttackRange;
        Vector3[] pts = new Vector3[segments + 2];
        pts[0] = Vector3.zero;

        for (int i = 0; i <= segments; i++)
        {
            // 180 degree fan (-90 to +90)
            float angle = -90f + (180f / segments) * i;
            float rad = angle * Mathf.Deg2Rad;
            pts[i + 1] = new Vector3(Mathf.Sin(rad) * radius, 0f, Mathf.Cos(rad) * radius);
        }

        lr.SetPositions(pts);

        Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        Material arcMat = new Material(s);
        arcMat.color = new Color(0.9f, 0.15f, 0.35f, 0.85f);
        lr.sharedMaterial = arcMat;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>
    ///     Initiates the boss encounter when dialogue ends / Defy choice is selected.
    /// </summary>
    public void StartBossFight()
    {
        if (isFightActive) return;
        isFightActive = true;

        EnsureComponents();

        Debug.Log("[NecromancerBossController] Boss Fight Started! Phase 1: Bubble Shield & Skeleton Army.");

        // Hook invincible damage block
        isShieldActive = true;
        if (health != null)
        {
            health.TryBlockDamage -= HandleTryBlockDamage;
            health.TryBlockDamage += HandleTryBlockDamage;
        }

        // Show Boss Health Bar
        if (BossHealthBarUI.Instance != null)
        {
            BossHealthBarUI.Instance.Show(health, bossDisplayName);
        }

        // Spawn Skeletons across crypt summoning points
        StartCoroutine(SummonSkeletonArmyRoutine());
    }

    private bool HandleTryBlockDamage(Damage damage)
    {
        if (!isShieldActive) return false;

        // Block all player attacks while skeletons live
        if (damage != null && damage.IsPlayerOwned)
        {
            if (bubbleDeflectSfx != null)
            {
                AudioSource.PlayClipAtPoint(bubbleDeflectSfx, transform.position, 0.8f);
            }

            // Pulse bubble visual scale
            if (bubbleShieldVisual != null)
            {
                LeanTween.cancel(bubbleShieldVisual);
                float baseScale = bubbleRadius * 2f;
                bubbleShieldVisual.transform.localScale = Vector3.one * (baseScale * 1.15f);
                LeanTween.scale(bubbleShieldVisual, Vector3.one * baseScale, 0.2f).setEaseOutQuad();
            }

            return true;
        }

        return false;
    }

    private IEnumerator SummonSkeletonArmyRoutine()
    {
        activeSkeletons.Clear();
        totalSpawnedSkeletons = 0;
        aliveSkeletonCount = 0;

        int count = Mathf.Max(6, targetSkeletonCount);

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = transform.position + Quaternion.Euler(0f, (360f / count) * i, 0f) * Vector3.forward * 8f;
            if (skeletonSpawnPoints != null && skeletonSpawnPoints.Length > 0)
            {
                Transform pt = skeletonSpawnPoints[i % skeletonSpawnPoints.Length];
                if (pt != null) spawnPos = pt.position;
            }

            if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 6f, NavMesh.AllAreas))
            {
                spawnPos = hit.position;
            }

            // Summoning FX
            if (summoningCirclePrefab != null)
            {
                Instantiate(summoningCirclePrefab, spawnPos, Quaternion.identity);
            }

            yield return new WaitForSeconds(0.15f);

            GameObject chosenPrefab = (skeletonPrefabs != null && skeletonPrefabs.Length > 0)
                ? skeletonPrefabs[i % skeletonPrefabs.Length]
                : null;

            GameObject skelObj = null;
            if (chosenPrefab != null)
            {
                skelObj = Instantiate(chosenPrefab, spawnPos, Quaternion.LookRotation(transform.position - spawnPos));
            }
            else
            {
                skelObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                skelObj.name = $"Crypt_Skeleton_{i + 1}";
                skelObj.transform.position = spawnPos;
            }

            CryptSkeletonAI ai = skelObj.GetComponent<CryptSkeletonAI>() ?? skelObj.AddComponent<CryptSkeletonAI>();
            ai.Initialize(this);

            activeSkeletons.Add(ai);
            totalSpawnedSkeletons++;
            aliveSkeletonCount++;

            OnSkeletonCountChanged?.Invoke(aliveSkeletonCount, targetSkeletonCount);
        }

        Debug.Log($"[NecromancerBossController] Summoned {totalSpawnedSkeletons} Crypt Skeletons! Alive: {aliveSkeletonCount}");
    }

    /// <summary>
    ///     Invoked by a CryptSkeletonAI when it dies.
    /// </summary>
    public void OnSkeletonDied(CryptSkeletonAI skeleton)
    {
        if (activeSkeletons.Contains(skeleton))
        {
            activeSkeletons.Remove(skeleton);
        }

        aliveSkeletonCount = Mathf.Max(0, aliveSkeletonCount - 1);
        Debug.Log($"[NecromancerBossController] Skeleton died! Remaining: {aliveSkeletonCount}");

        OnSkeletonCountChanged?.Invoke(aliveSkeletonCount, targetSkeletonCount);

        if (aliveSkeletonCount <= 0 && !isPhaseTwo)
        {
            TransitionToPhaseTwo();
        }
    }

    private void TransitionToPhaseTwo()
    {
        EnsureComponents();

        isPhaseTwo = true;
        isShieldActive = false;

        Debug.Log("[NecromancerBossController] ALL SKELETONS SLAIN! Bubble Shield shattered! Entering Phase 2: Direct Scythe Combat!");

        // Unhook invulnerability
        if (health != null)
        {
            health.TryBlockDamage -= HandleTryBlockDamage;
        }

        // Shield Shatter Effects
        if (shieldShatterSfx != null)
        {
            AudioSource.PlayClipAtPoint(shieldShatterSfx, transform.position, 1.0f);
        }

        if (shieldShatterVfxPrefab != null)
        {
            Instantiate(shieldShatterVfxPrefab, transform.position + Vector3.up * 1.2f, Quaternion.identity);
        }

        if (bubbleShieldVisual != null)
        {
            LeanTween.scale(bubbleShieldVisual, Vector3.zero, 0.3f).setEaseInBack().setOnComplete(() =>
            {
                bubbleShieldVisual.SetActive(false);
            });
        }

        // Laugh / Scream
        if (laughVoiceSfx != null)
        {
            AudioSource.PlayClipAtPoint(laughVoiceSfx, transform.position, 1.0f);
        }

        // Camera Shake
        StartCoroutine(CameraShakeRoutine(0.4f, 0.35f));

        // Draw Scythe
        if (scytheWeaponObject != null)
        {
            scytheWeaponObject.SetActive(true);
        }

        // Activate NavMeshAgent
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = normalMoveSpeed;
        }

        OnShieldShattered?.Invoke();
    }

    private void Update()
    {
        if (!isFightActive || isDead) return;

        Player player = Player.Instance;
        if (player == null || player.Health == null || player.Health.IsDead)
        {
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            UpdateAnimatorSpeed(0f);
            return;
        }

        if (!isPhaseTwo)
        {
            // Phase 1: Channeling at altar, stationary, watching player
            Vector3 lookDir = player.transform.position - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 3f);
            }
            return;
        }

        // Phase 2: Direct combat with player
        if (isAttacking)
        {
            return;
        }

        float distToPlayer = Vector3.Distance(transform.position, player.transform.position);

        if (distToPlayer <= sweepAttackRange && Time.time >= nextAttackTime)
        {
            StartCoroutine(ExecuteSweepingSlash(player));
        }
        else
        {
            // Chase player
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = false;

                // Lunge sprint if far away
                if (distToPlayer > sweepAttackRange * 2.5f)
                {
                    agent.speed = chargeMoveSpeed;
                }
                else
                {
                    agent.speed = normalMoveSpeed;
                }

                agent.SetDestination(player.transform.position);
            }

            UpdateAnimatorSpeed(agent != null ? agent.velocity.magnitude : 0f);
        }
    }

    private void UpdateAnimatorSpeed(float speed)
    {
        if (animator != null)
        {
            animator.SetFloat(HashMoveSpeed, speed);
        }
    }

    private IEnumerator ExecuteSweepingSlash(Player player)
    {
        isAttacking = true;
        nextAttackTime = Time.time + attackCooldown;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }
        UpdateAnimatorSpeed(0f);

        // Turn toward player
        Vector3 faceDir = (player.transform.position - transform.position);
        faceDir.y = 0f;
        if (faceDir.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(faceDir);
        }

        // Show telegraph arc
        if (sweepTelegraphArc != null)
        {
            sweepTelegraphArc.SetActive(true);
        }

        yield return new WaitForSeconds(sweepWindup);

        // Hide telegraph arc
        if (sweepTelegraphArc != null)
        {
            sweepTelegraphArc.SetActive(false);
        }

        if (animator != null)
        {
            animator.SetTrigger(HashAttack);
        }

        if (sweepWhooshSfx != null)
        {
            AudioSource.PlayClipAtPoint(sweepWhooshSfx, transform.position, 0.9f);
        }

        // Perform 180 degree sweep check
        if (player != null && player.Damageable != null)
        {
            Vector3 toPlayer = player.transform.position - transform.position;
            float dist = toPlayer.magnitude;
            toPlayer.y = 0f;

            if (dist <= sweepAttackRange + 0.8f)
            {
                float dot = Vector3.Dot(transform.forward, toPlayer.normalized);
                // 180 degree cone => dot >= 0
                if (dot >= -0.05f)
                {
                    Damage dmg = new Damage
                    {
                        value = sweepAttackDamage,
                        type = DamageType.slash,
                        knockbackForce = sweepKnockback,
                        sourcePosition = transform.position,
                        source = health,
                        direction = transform.forward,
                        unparryable = true
                    };

                    player.Damageable.ReceiveDamage(dmg);

                    if (sweepImpactSfx != null)
                    {
                        AudioSource.PlayClipAtPoint(sweepImpactSfx, player.transform.position, 1.0f);
                    }

                    StartCoroutine(CameraShakeRoutine(0.2f, 0.25f));
                }
            }
        }

        yield return new WaitForSeconds(0.4f);
        isAttacking = false;
    }

    private void HandleBossDied()
    {
        if (isDead) return;
        isDead = true;
        isFightActive = false;

        Debug.Log("[NecromancerBossController] Malakor Defeated! Granting campaign victory and massive rewards!");

        if (bossCollider != null) bossCollider.enabled = false;
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        if (animator != null)
        {
            animator.SetTrigger(HashDeath);
        }

        if (bubbleShieldVisual != null)
        {
            bubbleShieldVisual.SetActive(false);
        }

        if (victoryMusicSfx != null)
        {
            AudioSource.PlayClipAtPoint(victoryMusicSfx, transform.position, 1.0f);
        }

        // Drop massive rewards
        RunSession.AddInRunGold(goldReward);

        SaveData data = SaveSystem.Load();
        if (data != null)
        {
            data.goblinBlood += bloodReward;
            data.orcishMetal += metalReward;
            SaveSystem.Save(data);
        }

        // Spawn physical coins
        for (int i = 0; i < 15; i++)
        {
            Vector3 coinPos = transform.position + UnityEngine.Random.insideUnitSphere * 2.5f;
            coinPos.y = transform.position.y + 0.5f;
            if (coinPrefab != null)
            {
                Instantiate(coinPrefab, coinPos, Quaternion.identity);
            }
        }

        // Unlock campaign victory node
        if (CampaignManager.Instance != null)
        {
            CampaignManager.Instance.CompleteCurrentNode();
        }

        OnBossDefeated?.Invoke();
    }

    private IEnumerator CameraShakeRoutine(float duration, float magnitude)
    {
        Camera cam = Camera.main;
        if (cam == null) yield break;

        Vector3 originalPos = cam.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float x = UnityEngine.Random.Range(-1f, 1f) * magnitude;
            float y = UnityEngine.Random.Range(-1f, 1f) * magnitude;
            cam.transform.localPosition = originalPos + new Vector3(x, y, 0f);
            yield return null;
        }

        cam.transform.localPosition = originalPos;
    }
}
