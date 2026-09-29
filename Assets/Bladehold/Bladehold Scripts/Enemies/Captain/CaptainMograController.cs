using System;
using System.Collections;
using System.Collections.Generic;
using HighlightPlus;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
///     Captain Mogra Hexfang, the Bonecaller: a goblin shaman Clan Captain who fights at range. He has
///     three HP phases and a rotation of readable, telegraphed attacks:
///     <list type="bullet">
///         <item>Hex Bolts: a fan of slow, non-homing orbs. Strafe out of them.</item>
///         <item>Rune Eruption: green runes glow on the ground in a pattern (<see cref="HexRunePatterns" />), then erupt.</item>
///         <item>Hex Step: stand in his face too long and he blinks away, leaving a telegraphed blast behind.</item>
///         <item>Bone Totems (phase 2+): tethered totems pulse shockwave rings. Breaking one staggers him.</item>
///         <item>Bonefire Ritual (phase 3): a channel you break with damage (long stagger) or survive in a safe circle.</item>
///     </list>
///     Staggered, he takes <see cref="CaptainMograSO.staggerDamageTakenMultiplier" /> damage through
///     <see cref="Health.ScaleDamageTaken" />. Spawned via <see cref="GameLoopManager.SpawnCaptainForWave" />
///     (roster row <c>captain_mogra</c>) and can turn up in place of any node's captain
///     (<see cref="WaveChoiceConfigSO.wanderingCaptainChance" />).
/// </summary>
public class CaptainMograController : MonoBehaviour, ICaptain
{
    public enum MograAction { None, PhaseShift, Bolts, Runes, HexStep, Summon, Ritual }

    [Header("Captain Identity")]
    [SerializeField] private string captainName = CaptainRegistry.MograName;
    [SerializeField] private BannerDifficultyTier difficultyTier = BannerDifficultyTier.Enraged;

    [Header("Core Dependencies")]
    [SerializeField] private Health health;
    [SerializeField] private AIMovement movement;
    [SerializeField] private NavMeshAgent agent;
    [Tooltip("Optional: forced onto the player for good (he hunts you, not the gate).")]
    [SerializeField] private AITargetSelector targetSelector;
    [SerializeField] private Animator animator;
    [SerializeField] private HighlightEffect highlightEffect;
    [Tooltip("Where bolts leave from and totem tethers attach (his staff hand / chest).")]
    [SerializeField] private Transform castPoint;

    [Header("Config")]
    [SerializeField] private CaptainMograSO data;

    [Header("Ritual Cast Bar (instance of UI/BossCastBar.prefab)")]
    [SerializeField] private GameObject castBarRoot;
    [SerializeField] private Image castBarFill;
    [SerializeField] private TMP_Text castBarText;

    [Header("Feedbacks")]
    [Tooltip("Phase-change roar (sound + shake).")]
    [SerializeField] private MMF_Player phaseRoarFeedback;
    [Tooltip("Bolt cast (hiss, hand glow).")]
    [SerializeField] private MMF_Player boltCastFeedback;
    [Tooltip("Rune cast (staff slam, ground rumble).")]
    [SerializeField] private MMF_Player runeCastFeedback;
    [Tooltip("Blink puff, played at the spot he leaves and again where he lands.")]
    [SerializeField] private MMF_Player blinkFeedback;
    [Tooltip("Totem summon chant.")]
    [SerializeField] private MMF_Player summonFeedback;
    [Tooltip("Ritual channel loop: started with PlayFeedbacks, stopped with StopFeedbacks.")]
    [SerializeField] private MMF_Player ritualChannelFeedback;
    [Tooltip("The ritual is broken (glass-crack burst, big shake).")]
    [SerializeField] private MMF_Player ritualBrokenFeedback;
    [Tooltip("The ritual detonates.")]
    [SerializeField] private MMF_Player ritualDetonateFeedback;
    [Tooltip("He's staggered (daze stars, grunt).")]
    [SerializeField] private MMF_Player staggerFeedback;
    [Tooltip("The bolts leave his hand (release whoosh).")]
    [SerializeField] private MMF_Player boltReleaseFeedback;
    [Tooltip("The runes appear on the ground (arcane hum), played at the pattern centre.")]
    [SerializeField] private MMF_Player runeReleaseFeedback;

    private static readonly int CastBoltHash = Animator.StringToHash("CastBolt");
    private static readonly int CastRunesHash = Animator.StringToHash("CastRunes");
    private static readonly int SummonHash = Animator.StringToHash("Summon");
    private static readonly int BlinkHash = Animator.StringToHash("Blink");
    private static readonly int TauntHash = Animator.StringToHash("Taunt");
    private static readonly int ChannelingHash = Animator.StringToHash("Channeling");
    private static readonly int StaggeredHash = Animator.StringToHash("Staggered");
    // Triggers enter the stun and channel states; the bools above hold them and let them exit.
    private static readonly int StunHash = Animator.StringToHash("Stun");
    private static readonly int ChannelHash = Animator.StringToHash("Channel");

    private float? damageOverride;
    private bool anyError;
    private bool stoppedForGood;
    private bool subscribed;
    private bool glowing;

    private int phase = 1;
    private Coroutine currentActionRoutine;
    private MograAction currentAction;
    private MograAction lastAttack;
    private int lastAttackRepeats;
    private HexRunePattern? lastPattern;
    private float nextActionTime;
    private float meleeDwell;
    private float nextHexStepTime;
    private float staggerUntil = -1f;
    private float pendingStaggerSeconds;
    private float nextTotemTime;
    private float nextRitualTime = float.MaxValue;
    private readonly List<BoneTotem> totems = new List<BoneTotem>();

    private bool ritualActive;
    private float ritualDamageTaken;
    private readonly List<GameObject> ritualMarkers = new List<GameObject>();
    private readonly List<Vector3> ritualSafeCircles = new List<Vector3>();

    private Transform playerTransform;
    private Health playerHealth;

    public string CaptainName => captainName;
    public BannerDifficultyTier DifficultyTier => difficultyTier;
    public int Phase => phase;
    public MograAction CurrentAction => currentAction;
    public bool IsStaggered => Time.time < staggerUntil;
    public bool IsRitualActive => ritualActive;
    public int AliveTotemCount => CountAliveTotems();
    public CaptainMograSO Data => data;

    public event Action<CaptainMograController> OnCaptainDied;
    public event Action<CaptainMograController, int> OnPhaseChanged;

    private float TierMultiplier => BannerDifficultyHelper.GetStatMultiplier(difficultyTier);

    /// <summary>The CSV damage column overrides the bolt damage; every other spell scales by the same ratio.</summary>
    private float SpellScale => damageOverride.HasValue && data != null && data.boltDamage > 0f ? damageOverride.Value / data.boltDamage : 1f;

    /// <summary>A base SO damage number after the CSV override ratio and the tier multiplier.</summary>
    public float ScaledDamage(float baseDamage) => baseDamage * SpellScale * TierMultiplier;

    private void OnValidate()
    {
        EnsureDependencies();
    }

    private void Awake()
    {
        EnsureDependencies();
    }

    private void EnsureDependencies()
    {
        if (health == null) health = GetComponent<Health>();
        if (movement == null) movement = GetComponent<AIMovement>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (targetSelector == null) targetSelector = GetComponent<AITargetSelector>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (highlightEffect == null) highlightEffect = GetComponentInChildren<HighlightEffect>();
    }

    private void Start()
    {
        EnsureDependencies();

        if (health == null) { Debug.LogError("[CaptainMograController] Health is missing.", this); anyError = true; }
        if (movement == null) { Debug.LogError("[CaptainMograController] AIMovement is missing.", this); anyError = true; }
        if (agent == null) { Debug.LogError("[CaptainMograController] NavMeshAgent is missing.", this); anyError = true; }
        if (animator == null) { Debug.LogError("[CaptainMograController] Animator is missing.", this); anyError = true; }
        if (data == null) { Debug.LogError("[CaptainMograController] CaptainMograSO (data) is not assigned.", this); anyError = true; }
        else
        {
            if (data.boltPrefab == null) { Debug.LogError("[CaptainMograController] CaptainMograSO.boltPrefab is not assigned.", this); anyError = true; }
            if (data.runeBlastPrefab == null) { Debug.LogError("[CaptainMograController] CaptainMograSO.runeBlastPrefab is not assigned.", this); anyError = true; }
            if (data.totemPrefab == null) { Debug.LogError("[CaptainMograController] CaptainMograSO.totemPrefab is not assigned.", this); anyError = true; }
            if (data.shockwaveRingPrefab == null) { Debug.LogError("[CaptainMograController] CaptainMograSO.shockwaveRingPrefab is not assigned.", this); anyError = true; }
            if (data.ritualSafeCirclePrefab == null) Debug.LogError("[CaptainMograController] CaptainMograSO.ritualSafeCirclePrefab is not assigned; the ritual's safe spots won't show.", this);
            if (data.ritualDangerPrefab == null) Debug.LogError("[CaptainMograController] CaptainMograSO.ritualDangerPrefab is not assigned; the ritual's reach won't show.", this);
        }
        if (castPoint == null) castPoint = transform;
        if (castBarRoot == null || castBarFill == null) Debug.LogError("[CaptainMograController] Ritual cast bar refs are not assigned (castBarRoot, castBarFill).", this);

        // Missing feedbacks only cost looks and sound.
        if (phaseRoarFeedback == null) Debug.LogError("[CaptainMograController] phaseRoarFeedback is not assigned.", this);
        if (boltCastFeedback == null) Debug.LogError("[CaptainMograController] boltCastFeedback is not assigned.", this);
        if (runeCastFeedback == null) Debug.LogError("[CaptainMograController] runeCastFeedback is not assigned.", this);
        if (blinkFeedback == null) Debug.LogError("[CaptainMograController] blinkFeedback is not assigned.", this);
        if (summonFeedback == null) Debug.LogError("[CaptainMograController] summonFeedback is not assigned.", this);
        if (ritualChannelFeedback == null) Debug.LogError("[CaptainMograController] ritualChannelFeedback is not assigned.", this);
        if (ritualBrokenFeedback == null) Debug.LogError("[CaptainMograController] ritualBrokenFeedback is not assigned.", this);
        if (ritualDetonateFeedback == null) Debug.LogError("[CaptainMograController] ritualDetonateFeedback is not assigned.", this);
        if (staggerFeedback == null) Debug.LogError("[CaptainMograController] staggerFeedback is not assigned.", this);
        if (boltReleaseFeedback == null) Debug.LogError("[CaptainMograController] boltReleaseFeedback is not assigned.", this);
        if (runeReleaseFeedback == null) Debug.LogError("[CaptainMograController] runeReleaseFeedback is not assigned.", this);

        if (anyError) return;

        health.OnDied += HandleDeath;
        health.OnDamaged += HandleDamaged;
        health.ScaleDamageTaken += ScaleDamageWhileStaggered;
        subscribed = true;

        if (targetSelector != null) targetSelector.SetPlayerTargetOverride(float.PositiveInfinity);
        if (castBarRoot != null) castBarRoot.SetActive(false);

        ResolvePlayer();
        ApplyDifficultyScaling();
        // A breather after the intro before the first cast.
        nextActionTime = Time.time + 2f;
        nextHexStepTime = Time.time + 3f;
    }

    private void OnDestroy()
    {
        if (subscribed && health != null)
        {
            health.OnDied -= HandleDeath;
            health.OnDamaged -= HandleDamaged;
            health.ScaleDamageTaken -= ScaleDamageWhileStaggered;
        }
        ClearRitualMarkers();
        CrumbleTotems();
    }

    public void SetDamage(float value)
    {
        damageOverride = value;
    }

    public void Initialize(BannerDifficultyTier tier, string customName = null)
    {
        EnsureDependencies();
        difficultyTier = tier;
        if (!string.IsNullOrEmpty(customName)) captainName = customName;
        ApplyDifficultyScaling();

        if (health != null && BossHealthBarUI.Instance != null)
        {
            BossHealthBarUI.Instance.Show(health, captainName);
        }
    }

    private void ApplyDifficultyScaling()
    {
        if (data == null) return;
        float multiplier = TierMultiplier;
        if (health != null) health.SetMaxHealth(data.baseMaxHealth * multiplier);
        if (movement != null) movement.SetSpeed(data.baseMoveSpeed * Mathf.Min(1.25f, 1f + (multiplier - 1f) * 0.35f));
        SetGlow(false);
    }

    /// <summary>
    ///     True while he's casting a spell or charged by a standing totem. The highlight uses the prefab's
    ///     own profile (Mogra Outline); this only toggles it.
    /// </summary>
    public bool ShouldGlow
    {
        get
        {
            if (stoppedForGood || health == null || health.IsDead) return false;
            bool casting = currentActionRoutine != null && currentAction != MograAction.None && currentAction != MograAction.PhaseShift;
            return casting || CountAliveTotems() > 0;
        }
    }

    private void SetGlow(bool on)
    {
        glowing = on;
        if (highlightEffect != null) highlightEffect.SetHighlighted(on);
    }

    private void ResolvePlayer()
    {
        if (playerTransform != null) return;
        Player p = Player.Instance;
        if (p == null) return;
        playerTransform = p.transform;
        playerHealth = p.Health;
    }

    // ------------------------------------------------------------------ brain

    private void Update()
    {
        if (anyError || stoppedForGood || health == null || health.IsDead) return;

        // The end-of-wave rout (EnemyRout) takes him over: stop casting for good.
        if (TryGetComponent(out EnemyRout _))
        {
            StopForGood();
            return;
        }

        ResolvePlayer();
        if (playerTransform == null) return;
        if (playerHealth != null && playerHealth.IsDead)
        {
            AbortAction();
            return;
        }

        if (staggerUntil > 0f)
        {
            if (Time.time < staggerUntil) return;
            EndStagger();
        }

        if (pendingStaggerSeconds > 0f && currentActionRoutine == null)
        {
            float seconds = pendingStaggerSeconds;
            pendingStaggerSeconds = 0f;
            Stagger(seconds);
            return;
        }

        float dist = FlatDistance(transform.position, playerTransform.position);
        UpdateMeleeDwell(dist);

        if (currentActionRoutine != null)
        {
            if (currentAction != MograAction.Ritual) FacePlayer(10f);
            return;
        }
        if (Time.time < nextActionTime) return;

        MograAction next = ChooseAction(dist);
        if (next != MograAction.None) StartAction(next);
    }

    private void LateUpdate()
    {
        if (!anyError)
        {
            bool glow = ShouldGlow;
            if (glow != glowing) SetGlow(glow);
        }

        if (castBarRoot == null || !castBarRoot.activeSelf) return;
        Camera cam = Camera.main;
        if (cam != null) castBarRoot.transform.rotation = cam.transform.rotation;
    }

    private void UpdateMeleeDwell(float dist)
    {
        if (dist <= data.hexStepTriggerRange) meleeDwell += Time.deltaTime;
        else meleeDwell = Mathf.Max(0f, meleeDwell - Time.deltaTime);
    }

    /// <summary>Picks the next action from the phase, cooldowns and the player's range. Public for the benchmark.</summary>
    public MograAction ChooseAction(float distToPlayer)
    {
        int targetPhase = PhaseForHealth();
        if (targetPhase > phase) return MograAction.PhaseShift;

        if (meleeDwell >= data.hexStepDwellSeconds && Time.time >= nextHexStepTime) return MograAction.HexStep;
        if (phase >= 3 && Time.time >= nextRitualTime) return MograAction.Ritual;
        if (phase >= 2 && CountAliveTotems() == 0 && Time.time >= nextTotemTime) return MograAction.Summon;

        // Out of reach: keep walking in.
        if (distToPlayer > data.preferredRange * 2.5f) return MograAction.None;

        MograAction pick = UnityEngine.Random.value < 0.55f ? MograAction.Runes : MograAction.Bolts;
        if (pick == lastAttack && lastAttackRepeats >= 1)
        {
            pick = pick == MograAction.Runes ? MograAction.Bolts : MograAction.Runes;
        }
        return pick;
    }

    /// <summary>The phase his current health fraction calls for (1-3).</summary>
    public int PhaseForHealth()
    {
        if (health == null || health.MaxHealth <= 0f) return phase;
        float fraction = health.CurrentHealth / health.MaxHealth;
        if (fraction <= data.phase3HealthFraction) return 3;
        if (fraction <= data.phase2HealthFraction) return 2;
        return 1;
    }

    private void StartAction(MograAction action)
    {
        IEnumerator routine = action switch
        {
            MograAction.PhaseShift => PhaseShiftRoutine(PhaseForHealth()),
            MograAction.Bolts => BoltsRoutine(),
            MograAction.Runes => RunesRoutine(),
            MograAction.HexStep => HexStepRoutine(),
            MograAction.Summon => SummonRoutine(),
            MograAction.Ritual => RitualRoutine(),
            _ => null
        };
        if (routine == null) return;

        if (action == MograAction.Bolts || action == MograAction.Runes)
        {
            lastAttackRepeats = action == lastAttack ? lastAttackRepeats + 1 : 0;
            lastAttack = action;
        }

        currentAction = action;
        movement.SetMovementPaused(true);
        currentActionRoutine = StartCoroutine(RunAction(routine));
    }

    private IEnumerator RunAction(IEnumerator routine)
    {
        yield return routine;
        currentActionRoutine = null;
        currentAction = MograAction.None;
        if (!health.IsDead) movement.SetMovementPaused(false);
        nextActionTime = Time.time + CaptainMograSO.ByPhase(data.recoveryByPhase, phase, 1.2f);
    }

    /// <summary>Stops the current cast. Runes already on the ground still go off (they were telegraphed).</summary>
    private void AbortAction()
    {
        if (currentActionRoutine != null)
        {
            StopCoroutine(currentActionRoutine);
            currentActionRoutine = null;
        }
        if (currentAction == MograAction.Ritual || ritualActive) EndRitual(detonated: false);
        currentAction = MograAction.None;
        if (animator != null) animator.SetBool(ChannelingHash, false);
        if (movement != null && health != null && !health.IsDead) movement.SetMovementPaused(false);
    }

    private void StopForGood()
    {
        stoppedForGood = true;
        AbortAction();
        EndStaggerVisuals();
        CrumbleTotems();
        if (BossHealthBarUI.Instance != null && BossHealthBarUI.Instance.IsVisible) BossHealthBarUI.Instance.Hide();
    }

    // ------------------------------------------------------------------ stagger

    /// <summary>Interrupts him for <paramref name="seconds" />; he takes extra damage meanwhile.</summary>
    public void Stagger(float seconds)
    {
        if (health == null || health.IsDead || stoppedForGood) return;
        AbortAction();
        staggerUntil = Mathf.Max(staggerUntil, Time.time + seconds);
        movement.SetMovementPaused(true);
        if (animator != null)
        {
            animator.SetBool(StaggeredHash, true);
            animator.SetTrigger(StunHash);
        }
        if (staggerFeedback != null) staggerFeedback.PlayFeedbacks(transform.position);
    }

    private void EndStagger()
    {
        staggerUntil = -1f;
        EndStaggerVisuals();
        if (!health.IsDead) movement.SetMovementPaused(false);
        nextActionTime = Time.time + 0.5f;
    }

    private void EndStaggerVisuals()
    {
        if (animator != null) animator.SetBool(StaggeredHash, false);
    }

    private float ScaleDamageWhileStaggered(Damage damage) => IsStaggered ? data.staggerDamageTakenMultiplier : 1f;

    private void HandleDamaged(Damage damage)
    {
        if (ritualActive && damage != null) ritualDamageTaken += damage.value;
    }

    // ------------------------------------------------------------------ actions

    private IEnumerator PhaseShiftRoutine(int newPhase)
    {
        phase = Mathf.Clamp(newPhase, 1, 3);
        OnPhaseChanged?.Invoke(this, phase);
        Debug.Log($"[CaptainMograController] {captainName} enters phase {phase}.");

        FacePlayer(1000f);
        animator.SetTrigger(TauntHash);
        if (phaseRoarFeedback != null) phaseRoarFeedback.PlayFeedbacks(transform.position);
        yield return new WaitForSeconds(data.phaseRoarSeconds);

        if (phase >= 2) nextTotemTime = Time.time;
        if (phase >= 3) nextRitualTime = Mathf.Min(nextRitualTime, Time.time + 4f);
    }

    private IEnumerator BoltsRoutine()
    {
        int volleys = Mathf.Max(1, CaptainMograSO.ByPhase(data.boltVolleysByPhase, phase, 1));
        int count = Mathf.Max(1, CaptainMograSO.ByPhase(data.boltCountByPhase, phase, 3));
        float damage = ScaledDamage(data.boltDamage);

        for (int v = 0; v < volleys; v++)
        {
            animator.SetTrigger(CastBoltHash);
            if (boltCastFeedback != null) boltCastFeedback.PlayFeedbacks(castPoint.position);

            float t = 0f;
            while (t < data.boltWindupSeconds)
            {
                FacePlayer(12f);
                t += Time.deltaTime;
                yield return null;
            }

            Vector3 aim = playerTransform.position - castPoint.position;
            aim.y = 0f;
            if (aim.sqrMagnitude < 0.01f) aim = transform.forward;
            FireBoltFan(aim.normalized, count, damage);
            if (boltReleaseFeedback != null) boltReleaseFeedback.PlayFeedbacks(castPoint.position);

            if (v < volleys - 1) yield return new WaitForSeconds(0.35f);
        }
        yield return new WaitForSeconds(0.25f);
    }

    private void FireBoltFan(Vector3 aim, int count, float damage)
    {
        float fan = count > 1 ? data.boltFanDegrees : 0f;
        HexHitGroup volley = new HexHitGroup();
        for (int i = 0; i < count; i++)
        {
            float angle = count > 1 ? -fan * 0.5f + fan * i / (count - 1) : 0f;
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * aim;
            GameObject boltObj = Instantiate(data.boltPrefab, castPoint.position, Quaternion.LookRotation(dir));
            HexBolt bolt = boltObj.GetComponent<HexBolt>();
            if (bolt == null)
            {
                Debug.LogError($"[CaptainMograController] boltPrefab '{data.boltPrefab.name}' has no HexBolt.", this);
                Destroy(boltObj);
                return;
            }
            bolt.Launch(dir, data.boltSpeed, damage, data.boltLifetime, health, volley);
        }
    }

    private IEnumerator RunesRoutine()
    {
        animator.SetTrigger(CastRunesHash);
        if (runeCastFeedback != null) runeCastFeedback.PlayFeedbacks(transform.position);

        float t = 0f;
        while (t < data.runeWindupSeconds)
        {
            FacePlayer(12f);
            t += Time.deltaTime;
            yield return null;
        }

        HexRunePattern pattern = PickPattern();
        float telegraph = data.RuneTelegraphSeconds(phase);
        Vector3 centre = playerTransform.position;
        Vector3 forward = playerTransform.position - transform.position;
        List<HexRuneSpot> spots = HexRunePatterns.Build(pattern, forward, data.runeRadius, data.runeStepSeconds, telegraph, UnityEngine.Random.Range(0, 2));
        if (runeReleaseFeedback != null) runeReleaseFeedback.PlayFeedbacks(centre);
        spots.Sort((a, b) => a.appearAt.CompareTo(b.appearAt));

        float damage = ScaledDamage(data.runeDamage);
        // One hit per step: runes that go off together overlap, and mustn't stack.
        Dictionary<float, HexHitGroup> stepGroups = new Dictionary<float, HexHitGroup>();
        float elapsed = 0f;
        int next = 0;
        while (next < spots.Count)
        {
            while (next < spots.Count && spots[next].appearAt <= elapsed)
            {
                HexHitGroup group;
                if (!stepGroups.TryGetValue(spots[next].appearAt, out group))
                {
                    group = new HexHitGroup();
                    stepGroups[spots[next].appearAt] = group;
                }
                SpawnRune(centre + spots[next].offset, data.runeRadius, telegraph, damage, group);
                next++;
            }
            if (next >= spots.Count) break;
            yield return null;
            elapsed += Time.deltaTime;
        }
        // Hold the cast pose until the first runes erupt, so the recovery doesn't start mid-pattern.
        yield return new WaitForSeconds(Mathf.Min(telegraph, 0.6f));
    }

    private HexRunePattern PickPattern()
    {
        HexRunePattern[] pool = data.PatternsForPhase(phase);
        if (pool == null || pool.Length == 0) return HexRunePattern.Rings;
        HexRunePattern pick = pool[UnityEngine.Random.Range(0, pool.Length)];
        if (pool.Length > 1 && lastPattern.HasValue && pick == lastPattern.Value)
        {
            pick = pool[(Array.IndexOf(pool, pick) + 1 + UnityEngine.Random.Range(0, pool.Length - 1)) % pool.Length];
        }
        lastPattern = pick;
        return pick;
    }

    /// <summary>Places one armed rune on the ground at (or under) <paramref name="position" />. Skipped off the map.</summary>
    private HexRuneBlast SpawnRune(Vector3 position, float radius, float delay, float damage, HexHitGroup group = null)
    {
        if (!TryGroundPoint(position, out Vector3 ground)) return null;
        GameObject obj = Instantiate(data.runeBlastPrefab, ground + Vector3.up * 0.05f, Quaternion.identity);
        HexRuneBlast rune = obj.GetComponent<HexRuneBlast>();
        if (rune == null)
        {
            Debug.LogError($"[CaptainMograController] runeBlastPrefab '{data.runeBlastPrefab.name}' has no HexRuneBlast.", this);
            Destroy(obj);
            return null;
        }
        rune.Arm(radius, delay, damage, health, group);
        return rune;
    }

    private IEnumerator HexStepRoutine()
    {
        meleeDwell = 0f;
        nextHexStepTime = Time.time + data.hexStepCooldown;

        Vector3 origin = transform.position;
        SpawnRune(origin, data.hexStepBlastRadius, data.hexStepBlastDelay, ScaledDamage(data.hexStepBlastDamage));

        animator.SetTrigger(BlinkHash);
        if (blinkFeedback != null) blinkFeedback.PlayFeedbacks(origin);
        yield return new WaitForSeconds(0.2f);

        if (TryFindBlinkDestination(out Vector3 destination))
        {
            agent.Warp(destination);
            FacePlayer(1000f);
            if (blinkFeedback != null) blinkFeedback.PlayFeedbacks(destination);
        }
        yield return new WaitForSeconds(0.3f);
    }

    /// <summary>A NavMesh point about blinkDistance from the player, preferring directly away from them.</summary>
    private bool TryFindBlinkDestination(out Vector3 destination)
    {
        Vector3 away = transform.position - playerTransform.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = -transform.forward;
        away.Normalize();

        float[] angles = { 0f, 45f, -45f, 90f, -90f, 135f, -135f };
        foreach (float angle in angles)
        {
            Vector3 candidate = playerTransform.position + Quaternion.AngleAxis(angle, Vector3.up) * away * data.hexStepBlinkDistance;
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2.5f, NavMesh.AllAreas) &&
                FlatDistance(hit.position, playerTransform.position) >= data.hexStepBlinkDistance * 0.6f)
            {
                destination = hit.position;
                return true;
            }
        }
        destination = transform.position;
        return false;
    }

    private IEnumerator SummonRoutine()
    {
        int count = CaptainMograSO.ByPhase(data.totemCountByPhase, phase, 0);
        if (count <= 0) yield break;

        animator.SetTrigger(SummonHash);
        if (summonFeedback != null) summonFeedback.PlayFeedbacks(transform.position);
        yield return new WaitForSeconds(data.totemSummonSeconds * 0.6f);

        float startAngle = UnityEngine.Random.Range(0f, 360f);
        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + i * 360f / count;
            Vector3 candidate = transform.position + Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward * data.totemPlacementRadius;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;
            SpawnTotem(hit.position, i);
        }
        yield return new WaitForSeconds(data.totemSummonSeconds * 0.4f);
    }

    private void SpawnTotem(Vector3 position, int index)
    {
        GameObject obj = Instantiate(data.totemPrefab, position, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
        BoneTotem totem = obj.GetComponent<BoneTotem>();
        if (totem == null)
        {
            Debug.LogError($"[CaptainMograController] totemPrefab '{data.totemPrefab.name}' has no BoneTotem.", this);
            Destroy(obj);
            return;
        }
        // Stagger first pulses so the rings don't all arrive at once.
        float firstPulse = data.totemFirstPulseDelay + index * (data.totemPulseInterval / Mathf.Max(1, CaptainMograSO.ByPhase(data.totemCountByPhase, phase, 1)));
        totem.Init(data.totemBaseHealth * TierMultiplier, data.shockwaveRingPrefab, firstPulse, data.totemPulseInterval, data.totemChargeSeconds,
            data.ringSpeed, data.ringMaxRadius, data.ringWidth, ScaledDamage(data.ringDamage), castPoint, health);
        totem.OnBroken += HandleTotemBroken;
        totems.Add(totem);
    }

    private void HandleTotemBroken(BoneTotem totem)
    {
        totem.OnBroken -= HandleTotemBroken;
        totems.Remove(totem);
        if (CountAliveTotems() == 0) nextTotemTime = Time.time + data.totemResummonCooldown;
        if (health.IsDead || stoppedForGood) return;

        Debug.Log($"[CaptainMograController] A bone totem broke: {captainName} is staggered by the backlash.");
        if (currentActionRoutine != null && currentAction != MograAction.PhaseShift)
        {
            // Breaking a totem mid-ritual breaks the ritual too.
            Stagger(ritualActive ? data.ritualBrokenStaggerSeconds : data.totemBacklashStaggerSeconds);
        }
        else if (currentActionRoutine != null)
        {
            pendingStaggerSeconds = Mathf.Max(pendingStaggerSeconds, data.totemBacklashStaggerSeconds);
        }
        else
        {
            Stagger(data.totemBacklashStaggerSeconds);
        }
    }

    private int CountAliveTotems()
    {
        int alive = 0;
        for (int i = totems.Count - 1; i >= 0; i--)
        {
            if (totems[i] == null || totems[i].IsBroken) { totems.RemoveAt(i); continue; }
            alive++;
        }
        return alive;
    }

    private void CrumbleTotems()
    {
        foreach (BoneTotem totem in totems)
        {
            if (totem == null) continue;
            totem.OnBroken -= HandleTotemBroken;
            totem.Crumble();
        }
        totems.Clear();
    }

    private IEnumerator RitualRoutine()
    {
        ritualActive = true;
        ritualDamageTaken = 0f;
        float breakThreshold = Mathf.Max(1f, health.MaxHealth * data.ritualBreakHealthFraction);

        animator.SetBool(ChannelingHash, true);
        animator.SetTrigger(ChannelHash);
        if (ritualChannelFeedback != null) ritualChannelFeedback.PlayFeedbacks(transform.position);
        PlaceRitualMarkers();
        if (castBarRoot != null) castBarRoot.SetActive(true);

        float t = 0f;
        while (t < data.ritualChannelSeconds)
        {
            float ward = Mathf.Clamp01(1f - ritualDamageTaken / breakThreshold);
            if (castBarFill != null) castBarFill.fillAmount = ward;
            if (castBarText != null) castBarText.text = Loc.Get("captain.mogra.ritual_bar", "BREAK THE RITUAL {0}s").Replace("{0}", Mathf.CeilToInt(data.ritualChannelSeconds - t).ToString());

            if (ritualDamageTaken >= breakThreshold)
            {
                Debug.Log($"[CaptainMograController] {captainName}'s Bonefire Ritual is broken!");
                if (ritualBrokenFeedback != null) ritualBrokenFeedback.PlayFeedbacks(transform.position);
                EndRitual(detonated: false);
                pendingStaggerSeconds = data.ritualBrokenStaggerSeconds;
                yield break;
            }

            t += Time.deltaTime;
            yield return null;
        }

        DetonateRitual();
        EndRitual(detonated: true);
        yield return new WaitForSeconds(0.6f);
    }

    private void PlaceRitualMarkers()
    {
        ClearRitualMarkers();
        ritualSafeCircles.Clear();

        if (data.ritualDangerPrefab != null)
        {
            GameObject danger = Instantiate(data.ritualDangerPrefab, transform.position + Vector3.up * 0.04f, Quaternion.identity);
            danger.transform.localScale = new Vector3(data.ritualBlastRadius * 2f, danger.transform.localScale.y, data.ritualBlastRadius * 2f);
            ritualMarkers.Add(danger);
        }

        float startAngle = UnityEngine.Random.Range(0f, 360f);
        int wanted = Mathf.Max(1, data.ritualSafeCircleCount);
        for (int attempt = 0; attempt < wanted * 6 && ritualSafeCircles.Count < wanted; attempt++)
        {
            float angle = startAngle + attempt * (360f / wanted) + UnityEngine.Random.Range(-20f, 20f);
            float distance = UnityEngine.Random.Range(data.ritualSafeCircleMinDistance, data.ritualSafeCircleMaxDistance);
            Vector3 candidate = transform.position + Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward * distance;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas)) continue;
            bool overlaps = false;
            foreach (Vector3 existing in ritualSafeCircles)
            {
                if (FlatDistance(existing, hit.position) < data.ritualSafeCircleRadius * 2f + 1f) { overlaps = true; break; }
            }
            if (overlaps) continue;

            ritualSafeCircles.Add(hit.position);
            if (data.ritualSafeCirclePrefab != null)
            {
                GameObject safe = Instantiate(data.ritualSafeCirclePrefab, hit.position + Vector3.up * 0.08f, Quaternion.identity);
                safe.transform.localScale = new Vector3(data.ritualSafeCircleRadius * 2f, safe.transform.localScale.y, data.ritualSafeCircleRadius * 2f);
                ritualMarkers.Add(safe);
            }
        }
    }

    /// <summary>True if the point is inside one of the current ritual's safe circles.</summary>
    public bool IsInRitualSafeCircle(Vector3 position)
    {
        foreach (Vector3 safe in ritualSafeCircles)
        {
            if (FlatDistance(safe, position) <= data.ritualSafeCircleRadius) return true;
        }
        return false;
    }

    private void DetonateRitual()
    {
        if (ritualDetonateFeedback != null) ritualDetonateFeedback.PlayFeedbacks(transform.position);
        if (playerHealth == null || playerHealth.IsDead || playerTransform == null) return;
        if (FlatDistance(transform.position, playerTransform.position) > data.ritualBlastRadius) return;
        if (IsInRitualSafeCircle(playerTransform.position)) return;

        playerHealth.ReceiveDamage(new Damage
        {
            value = ScaledDamage(data.ritualBlastDamage),
            type = DamageType.elemental,
            unparryable = true,
            source = health,
            sourcePosition = transform.position,
            knockbackForce = 10f,
        });
    }

    private void EndRitual(bool detonated)
    {
        bool wasActive = ritualActive;
        ritualActive = false;
        if (animator != null) animator.SetBool(ChannelingHash, false);
        if (ritualChannelFeedback != null) ritualChannelFeedback.StopFeedbacks();
        if (castBarRoot != null) castBarRoot.SetActive(false);
        ClearRitualMarkers();
        ritualSafeCircles.Clear();
        if (wasActive) nextRitualTime = Time.time + data.ritualCooldown;
    }

    private void ClearRitualMarkers()
    {
        foreach (GameObject marker in ritualMarkers)
        {
            if (marker != null) Destroy(marker);
        }
        ritualMarkers.Clear();
    }

    // ------------------------------------------------------------------ death

    private void HandleDeath()
    {
        Debug.Log($"[CaptainMograController] {captainName} DEFEATED!");
        stoppedForGood = true;
        AbortAction();
        EndStaggerVisuals();
        CrumbleTotems();

        // Morale Break: nearby minions falter (the Clan Captain hierarchy, as Kombusta).
        Collider[] colliders = Physics.OverlapSphere(transform.position, 15f);
        HashSet<AIMovement> paused = new HashSet<AIMovement>();
        foreach (Collider col in colliders)
        {
            if (col.gameObject == gameObject || col.CompareTag("Player")) continue;
            AIMovement m = col.GetComponentInParent<AIMovement>();
            if (m != null && m != movement && paused.Add(m)) StartCoroutine(StaggerMinionRoutine(m, 2f));
        }

        int reward = BannerDifficultyHelper.GetRewardMultiplier(difficultyTier);
        RunSession.AddInRunGold(data.bonusGold * reward);
        RunSession.AddGoblinBlood(data.bonusGoblinBlood * reward);

        OnCaptainDied?.Invoke(this);
    }

    private IEnumerator StaggerMinionRoutine(AIMovement m, float duration)
    {
        if (m == null) yield break;
        m.SetMovementPaused(true);
        yield return new WaitForSeconds(duration);
        if (m != null) m.SetMovementPaused(false);
    }

    // ------------------------------------------------------------------ helpers

    private void FacePlayer(float degreesPerSecondFactor)
    {
        if (playerTransform == null) return;
        Vector3 dir = playerTransform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;
        Quaternion target = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Mathf.Clamp01(degreesPerSecondFactor * Time.deltaTime));
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private static bool TryGroundPoint(Vector3 position, out Vector3 ground)
    {
        int groundMask = ~LayerMask.GetMask("Player", "Enemy", "Ignore Raycast");
        if (Physics.Raycast(position + Vector3.up * 4f, Vector3.down, out RaycastHit hit, 12f, groundMask, QueryTriggerInteraction.Ignore))
        {
            ground = hit.point;
            return true;
        }
        if (NavMesh.SamplePosition(position, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
        {
            ground = navHit.position;
            return true;
        }
        ground = position;
        return false;
    }
}
