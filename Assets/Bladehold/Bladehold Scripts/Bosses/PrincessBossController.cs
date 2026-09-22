using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
///     State of Princess Katherine during the Sanctuary encounter.
/// </summary>
public enum PrincessState
{
    Idle,
    Fleeing,
    MovingToDownedKnight,
    ChannelingRevival,
    Defeated
}

/// <summary>
///     Master boss controller for Princess Katherine in the Royal Sanctuary (Part 5 of Castle Campaign Overhaul).
///     Combat Mechanics:
///     - While her Armored Knights fight, the Princess flees away from the player, staying behind her guards.
///     - When an Armored Knight is downed (0 HP), the Princess pathfinds to them.
///     - Once in range (~3.5m), she channels a 5.0-second golden revival spell.
///     - Hitting her with player attacks interrupts/delays the cast, adding +1.5 seconds per hit!
///     - When the cast timer reaches 0, the knight is restored to full health and rejoins the battle.
///     - Defeating the Princess (0 HP) grants Dark Campaign Victory, massive bounties, and ends the battle.
/// </summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(NavMeshAgent))]
public class PrincessBossController : MonoBehaviour
{
    [Header("Boss Identity")]
    [SerializeField] private string bossDisplayName = "Princess Katherine";
    [SerializeField] private string bossTitle = "Heir to the Throne";

    [Header("Revival Spell Mechanics")]
    [SerializeField] private float channelDuration = 5.0f;
    [SerializeField] private float hitDelayPenalty = 1.5f;
    [SerializeField] private float reviveRange = 3.5f;
    [SerializeField] private float fleeDistanceThreshold = 11.0f;
    [SerializeField] private float fleeSpeed = 6.2f;
    [SerializeField] private float walkToKnightSpeed = 5.0f;

    [Header("Knights Management")]
    [SerializeField] private List<ArmoredKnightAI> knights = new List<ArmoredKnightAI>();

    [Header("Visuals & Magic FX")]
    [SerializeField] private GameObject magicCircleVisual;
    [SerializeField] private GameObject floatingCastBarRoot;
    [SerializeField] private Image castBarFillImage;
    [SerializeField] private TMP_Text castBarTimeText;
    [SerializeField] private GameObject holyBurstVfxPrefab;
    [SerializeField] private DamageNumbersPro.DamageNumber delayPopupPrefab;

    [Header("Audio")]
    [SerializeField] private AudioClip spellChannelLoopSfx;
    [SerializeField] private AudioClip spellHitInterruptSfx;
    [SerializeField] private AudioClip spellCompleteSfx;
    [SerializeField] private AudioClip victoryMusicSfx;
    [SerializeField] private AudioClip fleeVoiceSfx;

    [Header("Campaign Rewards")]
    [SerializeField] private int goldReward = 500;
    [SerializeField] private int bloodReward = 30;
    [SerializeField] private int metalReward = 10;
    [SerializeField] private GameObject coinPrefab;

    private Health health;
    private NavMeshAgent agent;
    private Animator animator;
    private Collider princessCollider;
    private AudioSource audioSource;

    private PrincessState currentState = PrincessState.Idle;
    private ArmoredKnightAI targetKnight = null;
    private float currentChannelTimeRemaining = 0f;
    private float totalCastDurationThisChannel = 5.0f;
    private bool isDefeated = false;
    private float nextFleeCheckTime = 0f;

    private static readonly int HashMoveSpeed = Animator.StringToHash("MoveSpeed");
    private static readonly int HashCheer = Animator.StringToHash("Cheer");
    private static readonly int HashDeath = Animator.StringToHash("Death");

    public PrincessState CurrentState => currentState;
    public float CurrentChannelTimeRemaining => currentChannelTimeRemaining;
    public float ChannelDuration => channelDuration;
    public float HitDelayPenalty => hitDelayPenalty;
    public ArmoredKnightAI TargetKnight => targetKnight;
    public bool IsChanneling => currentState == PrincessState.ChannelingRevival;
    public bool IsDefeated => isDefeated;
    public IReadOnlyList<ArmoredKnightAI> Knights => knights;

    public event Action<ArmoredKnightAI> OnRevivalStarted;
    public event Action<float> OnRevivalDelayed;
    public event Action<ArmoredKnightAI> OnRevivalCompleted;
    public event Action OnPrincessDefeated;

    public void Initialize()
    {
        EnsureComponents();
        SubscribeEvents();
    }

    private void Awake()
    {
        EnsureComponents();
        SubscribeEvents();
    }

    private bool eventsSubscribed = false;

    public void SubscribeEvents()
    {
        if (eventsSubscribed) return;
        EnsureComponents();
        if (health != null)
        {
            health.OnDamaged -= HandlePrincessDamaged;
            health.OnDamaged += HandlePrincessDamaged;
            health.OnDied -= HandlePrincessDied;
            health.OnDied += HandlePrincessDied;
            eventsSubscribed = true;
        }
    }

    public void EnsureComponents()
    {
        if (health == null) health = GetComponent<Health>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (princessCollider == null) princessCollider = GetComponent<Collider>();

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        if (magicCircleVisual == null)
        {
            CreateProceduralMagicCircle();
        }

        if (floatingCastBarRoot == null)
        {
            CreateProceduralCastBar();
        }
    }

    private void Start()
    {
        EnsureComponents();
        SubscribeEvents();

        if (agent != null)
        {
            agent.speed = fleeSpeed;
            agent.stoppingDistance = 0.5f;
        }

        // Auto discover knights in scene if not manually wired
        if (knights == null || knights.Count == 0)
        {
            knights = new List<ArmoredKnightAI>(FindObjectsByType<ArmoredKnightAI>(FindObjectsSortMode.None));
        }

        foreach (var knight in knights)
        {
            if (knight != null)
            {
                knight.OnKnightDowned -= HandleKnightDowned;
                knight.OnKnightDowned += HandleKnightDowned;
            }
        }

        // Hide casting visuals initially
        if (magicCircleVisual != null) magicCircleVisual.SetActive(false);
        if (floatingCastBarRoot != null) floatingCastBarRoot.SetActive(false);

        // Show Boss Health Bar
        if (BossHealthBarUI.Instance != null && health != null)
        {
            BossHealthBarUI.Instance.Show(health, bossDisplayName);
        }

        currentState = PrincessState.Fleeing;
        Debug.Log($"[PrincessBossController] Princess Katherine initialized with {knights.Count} Armored Knights.");
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDamaged -= HandlePrincessDamaged;
            health.OnDied -= HandlePrincessDied;
        }

        foreach (var knight in knights)
        {
            if (knight != null)
            {
                knight.OnKnightDowned -= HandleKnightDowned;
            }
        }
    }

    private void CreateProceduralMagicCircle()
    {
        Transform existing = transform.Find("Procedural_MagicCircle");
        if (existing != null)
        {
            magicCircleVisual = existing.gameObject;
            return;
        }

        magicCircleVisual = new GameObject("Procedural_MagicCircle");
        magicCircleVisual.transform.SetParent(transform, false);
        magicCircleVisual.transform.localPosition = new Vector3(0f, 0.08f, 0f);

        LineRenderer lr = magicCircleVisual.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.startWidth = 0.18f;
        lr.endWidth = 0.18f;

        int segments = 36;
        lr.positionCount = segments;
        float radius = 2.4f;
        Vector3[] pts = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float rad = (360f / segments) * i * Mathf.Deg2Rad;
            pts[i] = new Vector3(Mathf.Sin(rad) * radius, 0f, Mathf.Cos(rad) * radius);
        }
        lr.SetPositions(pts);

        Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        Material circleMat = new Material(s);
        circleMat.color = new Color(1.0f, 0.85f, 0.2f, 0.9f);
        lr.sharedMaterial = circleMat;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        magicCircleVisual.SetActive(false);
    }

    private void CreateProceduralCastBar()
    {
        Transform existing = transform.Find("Overhead_CastBar_Canvas");
        if (existing != null)
        {
            floatingCastBarRoot = existing.gameObject;
            return;
        }

        floatingCastBarRoot = new GameObject("Overhead_CastBar_Canvas", typeof(Canvas), typeof(CanvasScaler));
        floatingCastBarRoot.transform.SetParent(transform, false);
        floatingCastBarRoot.transform.localPosition = new Vector3(0f, 2.5f, 0f);

        Canvas canvas = floatingCastBarRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform canvasRect = floatingCastBarRoot.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(240f, 40f);
        canvasRect.localScale = Vector3.one * 0.01f;

        // Background Bar
        GameObject bgObj = new GameObject("CastBar_BG", typeof(RectTransform), typeof(Image));
        bgObj.transform.SetParent(floatingCastBarRoot.transform, false);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgObj.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.85f);

        // Fill Bar
        GameObject fillObj = new GameObject("CastBar_Fill", typeof(RectTransform), typeof(Image));
        fillObj.transform.SetParent(floatingCastBarRoot.transform, false);
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.sizeDelta = new Vector2(-4f, -4f);
        fillRect.anchoredPosition = Vector2.zero;
        castBarFillImage = fillObj.GetComponent<Image>();
        castBarFillImage.type = Image.Type.Filled;
        castBarFillImage.fillMethod = Image.FillMethod.Horizontal;
        castBarFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        castBarFillImage.color = new Color(1.0f, 0.85f, 0.2f, 0.95f);

        // Text Label
        GameObject textObj = new GameObject("CastBar_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(floatingCastBarRoot.transform, false);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        castBarTimeText = textObj.GetComponent<TextMeshProUGUI>();
        castBarTimeText.alignment = TextAlignmentOptions.Center;
        castBarTimeText.fontSize = 18;
        castBarTimeText.fontStyle = FontStyles.Bold;
        castBarTimeText.color = Color.white;
        castBarTimeText.text = "REVIVING KNIGHT";

        floatingCastBarRoot.SetActive(false);
    }

    private void Update()
    {
        if (isDefeated) return;

        UpdateOverheadBillboard();

        switch (currentState)
        {
            case PrincessState.Fleeing:
                UpdateFleeingState();
                break;

            case PrincessState.MovingToDownedKnight:
                UpdateMovingToKnightState();
                break;

            case PrincessState.ChannelingRevival:
                UpdateChannelingState();
                break;
        }

        UpdateAnimatorSpeed();
    }

    private void UpdateOverheadBillboard()
    {
        if (floatingCastBarRoot != null && floatingCastBarRoot.activeSelf)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                floatingCastBarRoot.transform.rotation = cam.transform.rotation;
            }
        }
    }

    private void UpdateFleeingState()
    {
        // Priority check: Is there a downed knight that needs revival?
        ArmoredKnightAI downedKnight = GetNearestDownedKnight();
        if (downedKnight != null)
        {
            targetKnight = downedKnight;
            currentState = PrincessState.MovingToDownedKnight;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.speed = walkToKnightSpeed;
                agent.SetDestination(targetKnight.transform.position);
            }
            return;
        }

        // Standard Fleeing: Keep distance from player
        Player player = Player.Instance;
        if (player == null) return;

        if (Time.time < nextFleeCheckTime) return;
        nextFleeCheckTime = Time.time + 0.35f;

        Vector3 playerPos = player.transform.position;
        float distToPlayer = Vector3.Distance(transform.position, playerPos);

        if (distToPlayer < fleeDistanceThreshold)
        {
            // Vector pointing away from player
            Vector3 fleeDir = (transform.position - playerPos).normalized;
            Vector3 desiredPos = transform.position + fleeDir * 8f;

            if (NavMesh.SamplePosition(desiredPos, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            {
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.speed = fleeSpeed;
                    agent.SetDestination(hit.position);
                }
            }
        }
        else
        {
            // In safe range: stop or wander slightly
            if (agent != null && agent.isOnNavMesh && agent.hasPath && agent.remainingDistance < 1f)
            {
                agent.isStopped = true;
            }
        }
    }

    private void UpdateMovingToKnightState()
    {
        if (targetKnight == null || !targetKnight.IsDowned)
        {
            // Target was already revived or destroyed; look for another or return to fleeing
            ArmoredKnightAI nextDowned = GetNearestDownedKnight();
            if (nextDowned != null)
            {
                targetKnight = nextDowned;
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.speed = walkToKnightSpeed;
                    agent.SetDestination(targetKnight.transform.position);
                }
            }
            else
            {
                targetKnight = null;
                currentState = PrincessState.Fleeing;
            }
            return;
        }

        float distToKnight = Vector3.Distance(transform.position, targetKnight.transform.position);
        if (distToKnight <= reviveRange)
        {
            StartRevivalChannel(targetKnight);
        }
        else
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.speed = walkToKnightSpeed;
                agent.SetDestination(targetKnight.transform.position);
            }
        }
    }

    private void UpdateChannelingState()
    {
        if (targetKnight == null || !targetKnight.IsDowned)
        {
            CancelRevivalChannel();
            return;
        }

        // Face the downed knight while channeling
        Vector3 toKnight = targetKnight.transform.position - transform.position;
        toKnight.y = 0f;
        if (toKnight.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toKnight), Time.deltaTime * 8f);
        }

        // Rotate magic circle
        if (magicCircleVisual != null)
        {
            magicCircleVisual.transform.Rotate(0f, 45f * Time.deltaTime, 0f);
        }

        TickChannel(Time.deltaTime);
    }

    /// <summary>
    ///     Begins channeling the 5.0-second revival spell targeting the specified downed knight.
    /// </summary>
    public void StartRevivalChannel(ArmoredKnightAI knight)
    {
        SubscribeEvents();
        if (knight == null || !knight.IsDowned) return;

        targetKnight = knight;
        currentState = PrincessState.ChannelingRevival;
        currentChannelTimeRemaining = channelDuration;
        totalCastDurationThisChannel = channelDuration;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        if (animator != null)
        {
            animator.SetTrigger(HashCheer);
        }

        if (magicCircleVisual != null)
        {
            magicCircleVisual.SetActive(true);
        }

        if (floatingCastBarRoot != null)
        {
            floatingCastBarRoot.SetActive(true);
            UpdateCastBarVisual();
        }

        if (audioSource != null && spellChannelLoopSfx != null)
        {
            audioSource.clip = spellChannelLoopSfx;
            audioSource.loop = true;
            audioSource.Play();
        }

        Debug.Log($"[PrincessBossController] Princess Katherine started channeling revival on {knight.name} (5.0s)!");
        OnRevivalStarted?.Invoke(knight);
    }

    /// <summary>
    ///     Advances the revival spell timer by deltaTime seconds.
    ///     When timer reaches 0, completes revival.
    /// </summary>
    public void TickChannel(float deltaTime)
    {
        if (currentState != PrincessState.ChannelingRevival) return;

        currentChannelTimeRemaining -= deltaTime;
        UpdateCastBarVisual();

        if (currentChannelTimeRemaining <= 0f)
        {
            CompleteRevivalSpell();
        }
    }

    private void UpdateCastBarVisual()
    {
        if (castBarFillImage != null)
        {
            // Fill ratio goes from 0 (start) to 1 (complete)
            float progress = Mathf.Clamp01(1f - (currentChannelTimeRemaining / Mathf.Max(0.1f, totalCastDurationThisChannel)));
            castBarFillImage.fillAmount = progress;
        }

        if (castBarTimeText != null)
        {
            float displayTime = Mathf.Max(0f, currentChannelTimeRemaining);
            castBarTimeText.text = $"REVIVING: {displayTime:F1}s";
        }
    }

    /// <summary>
    ///     Reacts to incoming damage: when struck by player attacks during channeling,
    ///     adds +1.5 seconds to the cast timer and triggers interrupt feedback.
    /// </summary>
    private void HandlePrincessDamaged(Damage damage)
    {
        if (isDefeated) return;

        if (currentState == PrincessState.ChannelingRevival)
        {
            if (damage != null && damage.IsPlayerOwned)
            {
                currentChannelTimeRemaining += hitDelayPenalty;
                totalCastDurationThisChannel += hitDelayPenalty;

                Debug.Log($"[PrincessBossController] Princess struck while channeling! +{hitDelayPenalty}s delay added (Remaining: {currentChannelTimeRemaining:F1}s)");

                if (spellHitInterruptSfx != null)
                {
                    AudioSource.PlayClipAtPoint(spellHitInterruptSfx, transform.position, 1.0f);
                }

                // Show popup or flash cast bar
                if (Application.isPlaying)
                {
                    StartCoroutine(FlashCastBarPenaltyRoutine());
                }

                if (delayPopupPrefab != null)
                {
                    delayPopupPrefab.Spawn(transform.position + Vector3.up * 2.3f, $"+{hitDelayPenalty:F1}s Delay!");
                }

                OnRevivalDelayed?.Invoke(currentChannelTimeRemaining);
            }
        }
    }

    private IEnumerator FlashCastBarPenaltyRoutine()
    {
        if (castBarFillImage != null)
        {
            Color origColor = castBarFillImage.color;
            castBarFillImage.color = Color.red;

            if (castBarTimeText != null)
            {
                castBarTimeText.text = $"+{hitDelayPenalty:F1}s INTERRUPT!";
            }

            yield return new WaitForSeconds(0.25f);
            castBarFillImage.color = origColor;
            UpdateCastBarVisual();
        }
    }

    /// <summary>
    ///     Successfully completes the 5-second revival spell, restoring the target knight to full health.
    /// </summary>
    public void CompleteRevivalSpell()
    {
        if (targetKnight != null && targetKnight.IsDowned)
        {
            targetKnight.Revive(1.0f);
        }

        if (holyBurstVfxPrefab != null)
        {
            Vector3 burstPos = targetKnight != null ? targetKnight.transform.position : transform.position;
            Instantiate(holyBurstVfxPrefab, burstPos, Quaternion.identity);
        }

        if (spellCompleteSfx != null)
        {
            AudioSource.PlayClipAtPoint(spellCompleteSfx, transform.position, 1.0f);
        }

        StopChannelingVisuals();

        Debug.Log($"[PrincessBossController] Revival Spell Completed! Knight restored.");
        OnRevivalCompleted?.Invoke(targetKnight);

        targetKnight = null;

        // Check if there are other downed knights waiting
        ArmoredKnightAI nextDowned = GetNearestDownedKnight();
        if (nextDowned != null)
        {
            targetKnight = nextDowned;
            currentState = PrincessState.MovingToDownedKnight;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.speed = walkToKnightSpeed;
                agent.SetDestination(targetKnight.transform.position);
            }
        }
        else
        {
            currentState = PrincessState.Fleeing;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.speed = fleeSpeed;
            }
        }
    }

    private void CancelRevivalChannel()
    {
        StopChannelingVisuals();
        targetKnight = null;
        currentState = PrincessState.Fleeing;
    }

    private void StopChannelingVisuals()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }

        if (magicCircleVisual != null)
        {
            magicCircleVisual.SetActive(false);
        }

        if (floatingCastBarRoot != null)
        {
            floatingCastBarRoot.SetActive(false);
        }
    }

    private void HandleKnightDowned(ArmoredKnightAI knight)
    {
        if (isDefeated) return;

        // If currently fleeing, switch to moving to this knight
        if (currentState == PrincessState.Fleeing)
        {
            targetKnight = knight;
            currentState = PrincessState.MovingToDownedKnight;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.speed = walkToKnightSpeed;
                agent.SetDestination(knight.transform.position);
            }
        }
    }

    private ArmoredKnightAI GetNearestDownedKnight()
    {
        ArmoredKnightAI nearest = null;
        float minDist = float.MaxValue;

        foreach (var k in knights)
        {
            if (k != null && k.IsDowned)
            {
                float dist = Vector3.Distance(transform.position, k.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = k;
                }
            }
        }

        return nearest;
    }

    /// <summary>
    ///     Princess Defeat logic (0 HP).
    ///     Declares campaign Dark Victory, disables remaining knights, drops bounties, and completes the node.
    /// </summary>
    private void HandlePrincessDied()
    {
        if (isDefeated) return;
        isDefeated = true;
        currentState = PrincessState.Defeated;

        Debug.Log("[PrincessBossController] The Princess has fallen! The throne of Bladehold belongs to darkness.");

        StopChannelingVisuals();

        if (princessCollider != null) princessCollider.enabled = false;
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        if (animator != null)
        {
            animator.SetTrigger(HashDeath);
        }

        // Defeat / surrender all knights
        foreach (var k in knights)
        {
            if (k != null)
            {
                k.DefeatOrSurrender();
            }
        }

        if (victoryMusicSfx != null)
        {
            AudioSource.PlayClipAtPoint(victoryMusicSfx, transform.position, 1.0f);
        }

        // Grant massive campaign rewards
        RunSession.AddInRunGold(goldReward);

        SaveData data = SaveSystem.Load();
        if (data != null)
        {
            data.goblinBlood += bloodReward;
            data.orcishMetal += metalReward;
            SaveSystem.Save(data);
        }

        // Spawn physical coins
        for (int i = 0; i < 20; i++)
        {
            Vector3 coinPos = transform.position + UnityEngine.Random.insideUnitSphere * 3.0f;
            coinPos.y = transform.position.y + 0.5f;
            if (coinPrefab != null)
            {
                Instantiate(coinPrefab, coinPos, Quaternion.identity);
            }
        }

        // Complete campaign node
        if (CampaignManager.Instance != null)
        {
            CampaignManager.Instance.CompleteCurrentNode();
        }

        OnPrincessDefeated?.Invoke();
    }

    private void UpdateAnimatorSpeed()
    {
        if (animator != null && agent != null)
        {
            float speed = (agent.isOnNavMesh && !agent.isStopped) ? agent.velocity.magnitude : 0f;
            animator.SetFloat(HashMoveSpeed, speed);
        }
    }
}
