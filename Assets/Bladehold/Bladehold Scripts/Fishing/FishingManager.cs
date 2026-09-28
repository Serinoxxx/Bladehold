using System;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum FishingState
{
    WaitingToStart,
    Countdown,
    FrenzyActive,
    Finished
}

/// <summary>
///     Master state machine and spawner for the 60-second Fishing Minigame.
/// </summary>
public class FishingManager : MonoBehaviour
{
    public static FishingManager Instance { get; private set; }

    [Header("State")]
    [SerializeField] private FishingState currentState = FishingState.WaitingToStart;
    [SerializeField] private float totalFrenzyDuration = 60f;
    private float frenzyTimeRemaining;

    [Header("Pond Geometry")]
    [Tooltip("Optional: the fish orbit this transform (e.g. the pond water), so moving the pond moves the fish. Falls back to Pond Center.")]
    [SerializeField] private Transform pondCenterAnchor;
    [SerializeField] private Vector3 pondCenter = Vector3.zero;
    [SerializeField] private float minOrbitRadius = 4f;
    [SerializeField] private float maxOrbitRadius = 13f;
    [SerializeField] private float maxFishCount = 30;

    [Header("Fish Swimming")]
    [Tooltip("Orbit speed range in degrees per second, rolled per fish. On a 13 m orbit, 10°/s is about 2.3 m/s.")]
    [SerializeField] private Vector2 fishOrbitSpeedRange = new Vector2(8f, 16f);
    [Tooltip("Speed multiplier for the Speedy buff fish.")]
    [SerializeField] private float speedyFishSpeedMultiplier = 2f;

    [Header("Fish Type Highlight & Popups")]
    [Tooltip("Highlight Plus profile loaded into every fish (outline, plus glow for buff/Diamond fish). The colour comes from the type colours below.")]
    [SerializeField] private HighlightPlus.HighlightProfile fishHighlightProfile;
    [Tooltip("DamageNumbersPro popup spawned where a fish is caught (\"+12 Gold\"). Tinted with the fish's type colour.")]
    [SerializeField] private DamageNumbersPro.DamageNumber catchPopupPrefab;
    [SerializeField] private Vector3 catchPopupOffset = new Vector3(0f, 0.8f, 0f);
    [SerializeField] private Color goldFishColor = new Color(1f, 0.82f, 0.2f);
    [SerializeField] private Color orcMetalFishColor = new Color(0.55f, 0.75f, 1f);
    [SerializeField] private Color goblinBloodFishColor = new Color(0.95f, 0.2f, 0.2f);
    [SerializeField] private Color diamondFishColor = new Color(0.8f, 1f, 1f);
    [SerializeField] private Color speedyFishColor = new Color(0.4f, 1f, 0.45f);
    [SerializeField] private Color armoredFishColor = new Color(0.75f, 0.75f, 0.8f);
    [SerializeField] private Color fireFishColor = new Color(1f, 0.5f, 0.1f);
    [SerializeField] private Color frostFishColor = new Color(0.55f, 0.85f, 1f);
    [SerializeField] private Color sparkFishColor = new Color(0.7f, 0.4f, 1f);
    [SerializeField] private Color savageFishColor = new Color(0.6f, 0.05f, 0.1f);

    [Header("Prefabs & Materials")]
    [SerializeField] private GameObject fishBasePrefab;
    [SerializeField] private Material goldFishMat;
    [SerializeField] private Material orcMetalFishMat;
    [SerializeField] private Material goblinBloodFishMat;
    [SerializeField] private Material diamondFishMat;
    [SerializeField] private Material speedyFishMat;
    [SerializeField] private Material armoredFishMat;
    [SerializeField] private Material fireFishMat;
    [SerializeField] private Material frostFishMat;
    [SerializeField] private Material sparkFishMat;
    [SerializeField] private Material savageFishMat;

    [Header("Feedback (2D sounds)")]
    [Tooltip("Played on each 3-2-1 countdown beat (deep thump).")]
    [SerializeField] private MMF_Player countdownFeedback;
    [Tooltip("Played when the frenzy starts (horn).")]
    [SerializeField] private MMF_Player frenzyStartFeedback;
    [Tooltip("Optional: played when the timer runs out. Nothing is authored yet.")]
    [SerializeField] private MMF_Player timeUpFeedback;
    [Tooltip("Optional: played on every fish caught. Nothing is authored yet.")]
    [SerializeField] private MMF_Player catchFeedback;

    [Header("UI Controllers")]
    [SerializeField] private FishingHUDUI hudUI;
    [SerializeField] private FishingDraftUI draftUI;
    [SerializeField] private FishingTallyUI tallyUI;

    // Tracking
    private readonly List<FishController> activeFish = new List<FishController>();
    private readonly HashSet<BuffFishType> killedBuffFishThisSession = new HashSet<BuffFishType>();
    private bool diamondFishSpawned = false;
    private bool rewardsCommitted = false;
    private bool anyError = false;
    private PlayerBow playerBow;
    private PlayerStats playerStats;
    private int appliedPierceBonus = 0;

    // Progression / Stats this session
    private int totalFishCaught = 0;
    private int sessionGold = 0;
    private int sessionBlood = 0;
    private int sessionMetal = 0;
    private int sessionDiamondBones = 0;

    // Fishing Leveling
    private int currentFishingXp = 0;
    private int currentFishingLevel = 1;
    private int xpToNextLevel = 40;

    public FishingState CurrentState => currentState;
    public bool IsFrenzyActive => currentState == FishingState.FrenzyActive;
    public float FrenzyTimeRemaining => frenzyTimeRemaining;
    public float TotalFrenzyDuration => totalFrenzyDuration;
    public int ActiveFishCount => activeFish.Count;
    public int MaxFishCount => (int)maxFishCount;
    public int TotalFishCaught => totalFishCaught;
    public int CurrentFishingLevel => currentFishingLevel;
    public int CurrentFishingXp => currentFishingXp;
    public int XpToNextLevel => xpToNextLevel;
    public int SessionGold => sessionGold;
    public int SessionBlood => sessionBlood;
    public int SessionMetal => sessionMetal;
    public int SessionDiamondBones => sessionDiamondBones;
    public IReadOnlyCollection<BuffFishType> KilledBuffFish => killedBuffFishThisSession;

    public event Action OnStateChanged;
    public event Action<int> OnCountdownTick;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); return; }

        // Before any Start: the Player and its weapon/armour managers read it to load the bare base kit.
        RunSession.RunUpgradesSuspended = true;
    }

    private void Start()
    {
        // Missing HUD/draft UI degrades the pond; missing tally or CampaignManager would strand the player, so block.
        if (hudUI == null) Debug.LogError("[FishingManager] hudUI is not assigned.", this);
        if (draftUI == null) Debug.LogError("[FishingManager] draftUI is not assigned: level-ups give no cards.", this);
        if (countdownFeedback == null) Debug.LogError("[FishingManager] countdownFeedback is not assigned.", this);
        if (frenzyStartFeedback == null) Debug.LogError("[FishingManager] frenzyStartFeedback is not assigned.", this);
        if (fishHighlightProfile == null) Debug.LogError("[FishingManager] fishHighlightProfile is not assigned: fish types have no outline.", this);
        if (catchPopupPrefab == null) Debug.LogError("[FishingManager] catchPopupPrefab is not assigned: catches show no \"+N\" popup.", this);
        if (tallyUI == null) { Debug.LogError("[FishingManager] tallyUI is not assigned: the pond can't be left without it.", this); anyError = true; }
        if (CampaignManager.Instance == null) { Debug.LogError("[FishingManager] No CampaignManager: can't return to the map.", this); anyError = true; }

        frenzyTimeRemaining = totalFrenzyDuration;
        SetupPlayerBow();
        SetState(FishingState.WaitingToStart);
    }

    /// <summary>
    ///     The pond uses the player's real bow and arrows: shots are free here, and the fishing draft's
    ///     Fish Skewer and Bounce Shot cards are layered onto that bow (a pierce modifier, and a fixed
    ///     bounce on every fish hit) rather than a separate fishing bow.
    /// </summary>
    private void SetupPlayerBow()
    {
        Player player = Player.Instance ?? FindAnyObjectByType<Player>();
        if (player == null)
        {
            Debug.LogError("[FishingManager] No Player in the pond scene.", this);
            return;
        }

        if (player.Ammo != null) player.Ammo.InfiniteAmmo = true;
        else Debug.LogError("[FishingManager] The Player has no PlayerAmmo: pond shots will spend nothing but can't be made free either.", this);

        playerStats = player.Stats;
        playerBow = player.GetComponentInChildren<PlayerBow>(true);
        if (playerBow == null)
        {
            Debug.LogError("[FishingManager] The Player has no PlayerBow: Bounce Shot does nothing.", this);
        }
        else
        {
            playerBow.OnArrowImpact += HandleArrowImpact;
        }

        if (FishingUpgradeManager.Instance != null)
        {
            FishingUpgradeManager.Instance.OnUpgradeApplied += HandleUpgradeApplied;
        }
    }

    private void OnDestroy()
    {
        if (playerBow != null) playerBow.OnArrowImpact -= HandleArrowImpact;
        if (FishingUpgradeManager.Instance != null) FishingUpgradeManager.Instance.OnUpgradeApplied -= HandleUpgradeApplied;
        if (Instance == this)
        {
            Instance = null;
            RunSession.RunUpgradesSuspended = false;
        }
    }

    private void HandleUpgradeApplied(FishingUpgradeType type, int level)
    {
        if (type != FishingUpgradeType.FishSkewer || playerStats == null) return;

        // Fish Skewer rides the bow's own pierce stat; swap this pond's bonus for the new level's.
        int pierce = FishingUpgradeManager.Instance.PierceCount;
        playerStats.RemoveModifier(StatType.BowPierceCount, ModifierKind.Flat, appliedPierceBonus);
        playerStats.AddModifier(StatType.BowPierceCount, ModifierKind.Flat, pierce);
        appliedPierceBonus = pierce;
    }

    private void HandleArrowImpact(ArrowImpact impact)
    {
        if (FishingUpgradeManager.Instance == null || !(impact.target is FishController)) return;

        int bounces = FishingUpgradeManager.Instance.BounceCount;
        if (bounces > 0)
        {
            // The bow's own bounce mask is Enemy only; pond bounces hop between fish.
            playerBow.BounceFrom(impact.target, impact.damage, impact.point, bounces, 1 << ((FishController)impact.target).gameObject.layer);
        }
    }

    private void Update()
    {
        if (anyError) return;

        if (currentState == FishingState.WaitingToStart)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tKey.wasPressedThisFrame)
            {
                StartCountdown();
            }
        }
        else if (currentState == FishingState.FrenzyActive)
        {
            frenzyTimeRemaining -= Time.deltaTime;

            // Clean dead fish references
            activeFish.RemoveAll(f => f == null || f.IsDead);

            // Maintain fish population up to maxFishCount
            if (activeFish.Count < maxFishCount)
            {
                SpawnRandomFish();
            }

            // Check Diamond fish spawn condition (after 30s elapsed, 1 attempt)
            if (!diamondFishSpawned && (totalFrenzyDuration - frenzyTimeRemaining >= 30f))
            {
                diamondFishSpawned = true;
                SpawnDiamondFish();
            }

            if (frenzyTimeRemaining <= 0f)
            {
                frenzyTimeRemaining = 0f;
                FinishFrenzy();
            }
        }
    }

    public void StartCountdown()
    {
        if (currentState != FishingState.WaitingToStart) return;
        StartCoroutine(CountdownRoutine());
    }

    private IEnumerator CountdownRoutine()
    {
        SetState(FishingState.Countdown);

        for (int i = 3; i >= 1; i--)
        {
            OnCountdownTick?.Invoke(i);
            Play(countdownFeedback);
            yield return new WaitForSeconds(1f);
        }

        OnCountdownTick?.Invoke(0); // 0 = "FISHING FRENZY!"
        Play(frenzyStartFeedback);
        yield return new WaitForSeconds(0.6f);

        // Pre-populate pond
        while (activeFish.Count < maxFishCount)
        {
            SpawnRandomFish();
        }

        SetState(FishingState.FrenzyActive);
    }

    private void FinishFrenzy()
    {
        SetState(FishingState.Finished);
        Play(timeUpFeedback);

        if (draftUI != null) draftUI.CancelPendingDrafts();

        if (tallyUI != null)
        {
            tallyUI.OpenTally(sessionGold, sessionBlood, sessionMetal, sessionDiamondBones, totalFishCaught, killedBuffFishThisSession);
        }
    }

    public void OnFishKilled(FishController fish)
    {
        // Bleed ticks and Fishsploshion chains can kill fish after time's up: those don't count.
        if (fish == null || currentState != FishingState.FrenzyActive) return;
        totalFishCaught++;

        float fatMultiplier = 1f + (FishingUpgradeManager.Instance != null ? FishingUpgradeManager.Instance.FatFishBonusPercent : 0f);

        string popupText;
        if (fish.isBuffFish)
        {
            killedBuffFishThisSession.Add(fish.buffType);
            AddXp(25);
            int buffGold = Mathf.RoundToInt(15 * fatMultiplier);
            sessionGold += buffGold;
            popupText = $"{fish.buffType} Fish! +{buffGold} Gold";
        }
        else
        {
            switch (fish.resourceType)
            {
                case ResourceFishType.Gold:
                    int goldEarned = Mathf.RoundToInt(UnityEngine.Random.Range(8, 16) * fatMultiplier);
                    sessionGold += goldEarned;
                    AddXp(10);
                    popupText = $"+{goldEarned} Gold";
                    break;
                case ResourceFishType.OrcMetal:
                    int metalEarned = Mathf.Max(1, Mathf.RoundToInt(1 * fatMultiplier));
                    sessionMetal += metalEarned;
                    AddXp(15);
                    popupText = $"+{metalEarned} Orcish Metal";
                    break;
                case ResourceFishType.GoblinBlood:
                    int bloodEarned = Mathf.Max(1, Mathf.RoundToInt(UnityEngine.Random.Range(1, 3) * fatMultiplier));
                    sessionBlood += bloodEarned;
                    AddXp(15);
                    popupText = $"+{bloodEarned} Goblin Blood";
                    break;
                case ResourceFishType.Diamond:
                    sessionDiamondBones += 1;
                    int diamondGold = Mathf.RoundToInt(100 * fatMultiplier);
                    sessionGold += diamondGold;
                    AddXp(100);
                    popupText = $"DIAMOND FISH! +1 Bone, +{diamondGold} Gold";
                    break;
                default:
                    popupText = null;
                    break;
            }
        }

        Vector3 catchPosition = fish.transform.position;
        if (catchPopupPrefab != null && !string.IsNullOrEmpty(popupText))
        {
            DamageNumbersPro.DamageNumber popup = catchPopupPrefab.Spawn(catchPosition + catchPopupOffset, popupText);
            // The popup text carries its own unit ("+12 Gold", "+1 Orcish Metal"); drop any suffix baked into the prefab.
            popup.enableRightText = false;
            popup.SetColor(GetFishTypeColor(fish.isBuffFish, fish.resourceType, fish.buffType));
        }

        if (catchFeedback != null)
        {
            catchFeedback.PlayFeedbacks(catchPosition);
        }
    }

    private Color GetFishTypeColor(bool isBuff, ResourceFishType resType, BuffFishType bType)
    {
        if (isBuff)
        {
            return bType switch
            {
                BuffFishType.Speedy => speedyFishColor,
                BuffFishType.Armored => armoredFishColor,
                BuffFishType.Fire => fireFishColor,
                BuffFishType.Frost => frostFishColor,
                BuffFishType.Spark => sparkFishColor,
                BuffFishType.Savage => savageFishColor,
                _ => speedyFishColor
            };
        }

        return resType switch
        {
            ResourceFishType.Gold => goldFishColor,
            ResourceFishType.OrcMetal => orcMetalFishColor,
            ResourceFishType.GoblinBlood => goblinBloodFishColor,
            ResourceFishType.Diamond => diamondFishColor,
            _ => goldFishColor
        };
    }

    private void AddXp(int amount)
    {
        currentFishingXp += amount;
        while (currentFishingXp >= xpToNextLevel)
        {
            currentFishingXp -= xpToNextLevel;
            currentFishingLevel++;
            xpToNextLevel = Mathf.RoundToInt(xpToNextLevel * 1.5f);
            // Several level-ups in one frame (a Fishsploshion chain) each earn their own pick.
            if (draftUI != null) draftUI.QueueDraft();
        }
    }

    private void SpawnRandomFish()
    {
        float roll = UnityEngine.Random.value;
        if (roll < 0.55f)
        {
            SpawnFish(false, ResourceFishType.Gold, BuffFishType.Speedy, goldFishMat, 1f);
        }
        else if (roll < 0.73f)
        {
            SpawnFish(false, ResourceFishType.OrcMetal, BuffFishType.Speedy, orcMetalFishMat, 1f);
        }
        else if (roll < 0.88f)
        {
            SpawnFish(false, ResourceFishType.GoblinBlood, BuffFishType.Speedy, goblinBloodFishMat, 1f);
        }
        else
        {
            // Buff fish roll
            BuffFishType[] buffs = (BuffFishType[])Enum.GetValues(typeof(BuffFishType));
            BuffFishType chosenBuff = buffs[UnityEngine.Random.Range(0, buffs.Length)];
            Material mat = chosenBuff switch
            {
                BuffFishType.Speedy => speedyFishMat,
                BuffFishType.Armored => armoredFishMat,
                BuffFishType.Fire => fireFishMat,
                BuffFishType.Frost => frostFishMat,
                BuffFishType.Spark => sparkFishMat,
                BuffFishType.Savage => savageFishMat,
                _ => speedyFishMat
            };
            float hpMult = (chosenBuff == BuffFishType.Armored) ? 5f : 1f;
            SpawnFish(true, ResourceFishType.Gold, chosenBuff, mat, hpMult);
        }
    }

    private void SpawnDiamondFish()
    {
        SpawnFish(false, ResourceFishType.Diamond, BuffFishType.Speedy, diamondFishMat, 20f);
        Debug.Log("[FishingManager] The legendary Diamond Fish has emerged!");
    }

    private void SpawnFish(bool isBuff, ResourceFishType resType, BuffFishType bType, Material mat, float hpMult)
    {
        if (fishBasePrefab == null)
        {
            Debug.LogError("[FishingManager] fishBasePrefab is not assigned.", this);
            return;
        }

        GameObject fishObj = Instantiate(fishBasePrefab);
        FishController controller = fishObj.GetComponent<FishController>();
        if (controller == null)
        {
            Debug.LogError($"[FishingManager] fishBasePrefab '{fishBasePrefab.name}' has no FishController.", this);
            Destroy(fishObj);
            return;
        }

        Renderer rend = fishObj.GetComponentInChildren<Renderer>();
        if (rend != null && mat != null)
        {
            rend.material = mat;
        }

        float radius = UnityEngine.Random.Range(minOrbitRadius, maxOrbitRadius);
        float speed = UnityEngine.Random.Range(fishOrbitSpeedRange.x, fishOrbitSpeedRange.y);
        if (isBuff && bType == BuffFishType.Speedy) speed *= speedyFishSpeedMultiplier;
        float angle = UnityEngine.Random.Range(0f, 360f);
        float depth = UnityEngine.Random.Range(-0.4f, 0.2f);
        bool cw = UnityEngine.Random.value > 0.5f;

        // The anchor supplies the pond's x/z; the swim height stays Pond Center's y (the bow aims at the y = 0 water plane).
        Vector3 center = pondCenterAnchor != null
            ? new Vector3(pondCenterAnchor.position.x, pondCenter.y, pondCenterAnchor.position.z)
            : pondCenter;
        controller.Setup(center, radius, speed, angle, depth, cw, isBuff, resType, bType, hpMult);
        bool special = isBuff || resType == ResourceFishType.Diamond;
        controller.ApplyTypeHighlight(fishHighlightProfile, GetFishTypeColor(isBuff, resType, bType), special);
        activeFish.Add(controller);
    }

    public void CommitRewardsAndReturnToCampaign()
    {
        // Continue can be pressed more than once before the scene unloads: grant exactly once.
        if (rewardsCommitted || currentState != FishingState.Finished) return;
        rewardsCommitted = true;

        // 1. Commit in-run gold
        if (sessionGold > 0)
        {
            RunSession.AddInRunGold(sessionGold);
        }

        // 2. Commit permanent currencies
        if (sessionBlood > 0) RunSession.AddGoblinBlood(sessionBlood);
        if (sessionMetal > 0) RunSession.AddOrcishMetal(sessionMetal);
        if (sessionDiamondBones > 0) RunSession.AddDiamondFishBones(sessionDiamondBones);

        // 3. Complete the campaign node and head back to the map (through the loading screen)
        if (CampaignManager.Instance.IsCampaignActive)
        {
            CampaignManager.Instance.CompleteCurrentNodeAndContinue();
        }
        else
        {
            CampaignManager.Instance.OpenOverviewMap();
        }
    }

    private void SetState(FishingState state)
    {
        currentState = state;
        OnStateChanged?.Invoke();
    }

    private void Play(MMF_Player feedback)
    {
        if (feedback != null)
        {
            feedback.PlayFeedbacks();
        }
    }
}
