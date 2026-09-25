using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

/// <summary>
///     Master controller for a combat sector's 5-wave loop: war-banner pick + tower prep before each
///     wave, per-wave objectives and kill quotas, bounty rewards, captains on harder tiers, and
///     victory after wave 5 (towers dismantled for supply, then back to the Campaign Map).
/// </summary>
public class GameLoopManager : MonoBehaviour
{
    public static GameLoopManager Instance { get; private set; }

    [Header("Configurations")]
    [Tooltip("Round pacing config asset containing round rosters, 20 max concurrent limit, 3s indicators, and drop weights.")]
    [SerializeField] private RoundPacingConfigSO pacingConfig;
    [SerializeField] private WarBannerConfigSO bannerConfig;
    [SerializeField] private GameObject warBannerPrefab;
    [SerializeField] private Transform[] bannerSpawnPoints;

    [SerializeField] private bool enableGameLoopManageStateDebug = false;

    public BannerBuffType CurrentWaveBuff { get; private set; } = BannerBuffType.None;
    public BannerBountyType CurrentWaveBounty { get; private set; } = BannerBountyType.None;
    public BannerDifficultyTier CurrentWaveDifficultyTier { get; private set; } = BannerDifficultyTier.Standard;
    public WarBannerClanSO CurrentClanBuffSO { get; private set; }
    public WarBannerRewardSO CurrentBannerRewardSO { get; private set; }
    private List<WarBannerController> activeBanners = new List<WarBannerController>();

    [Header("Captain Settings")]
    [Tooltip("Captain Fraglob prefab. If null, an error is logged and Captain Kombusta spawns instead.")]
    [SerializeField] private GameObject captainPrefab;
    [Tooltip("Optional dedicated prefab for Captain Kombusta. If null, spawner will attempt to spawn 'captain_kombusta'.")]
    [SerializeField] private GameObject captainKombustaPrefab;
    [SerializeField] private Transform captainSpawnPoint;

    [Header("Cinematic Intermission")]
    [SerializeField] private CinemachineCamera intermissionVirtualCamera;
    [SerializeField] private GameObject intermissionStatsPanel;

    [Header("UI Dependencies")]
    [SerializeField] private SurvivorsSpawner spawner;
    [SerializeField] private SurvivorsObjectiveManager objectiveManager;
    [SerializeField] private Transform bossSpawnPoint;

    [Header("Upgrade Powerup (Between Waves)")]
    [Tooltip("World spawn point for the between-wave upgrade powerup. Defaults to arena center (0,0,0) if null.")]
    [SerializeField] private Transform upgradePowerupSpawnPoint;
    [Tooltip("Optional custom prefab for the WaveUpgradePowerup. If null, creates procedural visual.")]
    [SerializeField] private GameObject upgradePowerupPrefab;

    [Header("UI References (Optional / Fallback)")]
    [SerializeField] private TMP_Text waveAnnouncementText;
    [SerializeField] private TMP_Text intermissionTimerText;
    [SerializeField] private TMP_Text rewardNotificationText;
    [SerializeField] private GameObject intermissionBanner;
    [SerializeField] private GameObject victoryScreen;

    private int killsThisWave = 0;
    private int targetKillsThisWave = 15;
    private ISurvivorsObjective currentObjective;
    private bool isObjectiveComplete = false;
    private bool isWaveActive = false;
    private bool isIntermission = false;
    private float intermissionTimeRemaining = 0f;
    private WaveUpgradePowerup activePowerup;

    [Header("Resource Reward Feedback (Auto-Wired)")]
    public DamageNumbersPro.DamageNumber goldPopupPrefab;
    public DamageNumbersPro.DamageNumber metalPopupPrefab;
    public DamageNumbersPro.DamageNumber bloodPopupPrefab;
    public AudioClip rewardSfx;

    public IReadOnlyList<WarBannerController> ActiveBanners => activeBanners;

    public int CurrentWave => RunSession.CurrentWave;
    public int CurrentRound => RunSession.CurrentRound;
    public int KillsThisWave => killsThisWave;
    public int TargetKillsThisWave => targetKillsThisWave;
    public SurvivorsObjectiveManager ObjectiveManager => objectiveManager;
    public ISurvivorsObjective CurrentObjective => objectiveManager != null ? objectiveManager.CurrentObjective : currentObjective;
    public bool IsWaveActive => isWaveActive;
    public bool IsIntermission => isIntermission;
    public bool IsPrepPhase => isIntermission || !isWaveActive;
    public float IntermissionTimeRemaining => intermissionTimeRemaining;
    public Transform UpgradePowerupSpawnPoint => upgradePowerupSpawnPoint;
    public WaveUpgradePowerup ActivePowerup => activePowerup;

    public event Action<int> OnWaveStarted;
    public event Action<int, string> OnWaveCleared;
    public event Action<float> OnIntermissionTick;
    public event Action OnVictory;
    public event Action<Health> OnEnemyKilledEvent;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
            return;
        }

        if (spawner == null) spawner = FindAnyObjectByType<SurvivorsSpawner>();
        if (objectiveManager == null) objectiveManager = SurvivorsObjectiveManager.Instance ?? FindAnyObjectByType<SurvivorsObjectiveManager>();
    }

    private void Start()
    {
        if (Player.Instance != null && Player.Instance.Health != null)
        {
            Player.Instance.Health.OnDied += HandlePlayerDied;
        }

        // The castle scenes' gates were authored with an Interactable from the old rest-gate flow;
        // nothing handles it now, so keep it from showing a dead [E] prompt.
        foreach (Gate gate in Gate.All)
        {
            Interactable gateInteractable = gate != null ? gate.GetComponent<Interactable>() : null;
            if (gateInteractable != null)
            {
                gateInteractable.CanInteract = false;
            }
        }

        // Auto-discover and bind objective manager if not wired
        if (objectiveManager == null) objectiveManager = SurvivorsObjectiveManager.Instance ?? FindAnyObjectByType<SurvivorsObjectiveManager>();
        if (objectiveManager != null)
        {
            objectiveManager.OnObjectiveCompleted += HandleObjectiveCompleted;
        }

        // Hook up spawner enemy death and wave wiped listeners
        HookupSpawnerEnemyDeaths();
        if (spawner != null)
        {
            spawner.OnWaveWiped -= HandleSpawnerWaveWiped;
            spawner.OnWaveWiped += HandleSpawnerWaveWiped;
        }

        // Initialize first wave
        int initialWave = RunSession.CurrentWave > 0 ? RunSession.CurrentWave : 1;
        RunSession.CurrentWave = initialWave;

        if (CampaignManager.Instance != null && CampaignManager.Instance.IsCampaignActive && CampaignManager.Instance.CurrentNode != null)
        {
            var node = CampaignManager.Instance.CurrentNode;
            CurrentWaveBounty = node.bountyType;
            CurrentWaveDifficultyTier = node.difficultyTier;
            Debug.Log($"[GameLoopManager] Campaign Active: Sector '{node.nodeTitle}' configured (Tier {CurrentWaveDifficultyTier}, Bounty {CurrentWaveBounty}).");
        }

        CheckAndSpawnBanners(initialWave);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (Player.Instance != null && Player.Instance.Health != null)
        {
            Player.Instance.Health.OnDied -= HandlePlayerDied;
        }

        if (objectiveManager != null)
        {
            objectiveManager.OnObjectiveCompleted -= HandleObjectiveCompleted;
            objectiveManager.OnObjectiveFailed -= HandleObjectiveFailed;
        }

        if (spawner != null)
        {
            spawner.OnWaveWiped -= HandleSpawnerWaveWiped;
        }
    }

    private void HandleSpawnerWaveWiped()
    {
        Debug.Log("[GameLoopManager] Spawner reported wave wiped!");
        CheckWaveCompletionConditions();
    }

    private void HookupSpawnerEnemyDeaths()
    {
        // Spawner notifies via enemy death or we can query active deaths
    }

    public void OnEnemyKilled(Health enemyHealth)
    {
        if (!isWaveActive) return;

        killsThisWave++;

        // Roll gold drop into in-run purse
        RunSession.AddInRunGold(UnityEngine.Random.Range(2, 6));

        // Defense Supply roll
        int supplyDrop = UnityEngine.Random.value < 0.35f ? UnityEngine.Random.Range(1, 4) : 0;
        if (enemyHealth != null && enemyHealth.MaxHealth >= 150f)
        {
            supplyDrop += UnityEngine.Random.Range(4, 8); // Brute / elite bonus supply
        }
        if (supplyDrop > 0)
        {
            RunSession.AddInRunSupply(supplyDrop);
        }

        cleanupTimer = 0f;
        OnEnemyKilledEvent?.Invoke(enemyHealth);
        CheckWaveCompletionConditions();
    }

    public void StartWave(int waveNumber)
    {
        RunSession.CurrentWave = waveNumber;
        int totalWaves = pacingConfig != null ? pacingConfig.wavesPerRound : 5;

        targetKillsThisWave = pacingConfig != null
            ? pacingConfig.GetKillQuota(waveNumber, SectorThreat.Current)
            : 15 + (waveNumber - 1) * 5;
        killsThisWave = 0;
        isObjectiveComplete = false;
        isWaveActive = true;
        isIntermission = false;

        if (intermissionBanner != null) intermissionBanner.SetActive(false);

        // Start wave objective via SurvivorsObjectiveManager
        StartWaveObjective(waveNumber);

        if (currentObjective is GoblinRushObjective)
        {
            targetKillsThisWave = 999999;
        }

        // Start Spawner with wave-specific settings & quota
        if (spawner != null)
        {
            spawner.StartWave(waveNumber, targetKillsThisWave);
        }

        // Wave 5 Climax: Spawn Clan Captain
        if (waveNumber >= totalWaves)
        {
            string preferredCaptain = null;
            if (CampaignManager.Instance != null && CampaignManager.Instance.IsCampaignActive && CampaignManager.Instance.CurrentNode != null)
            {
                preferredCaptain = CampaignManager.Instance.CurrentNode.captainName;
            }
            BannerDifficultyTier captainTier = CurrentWaveDifficultyTier >= BannerDifficultyTier.Enraged
                ? CurrentWaveDifficultyTier
                : BannerDifficultyTier.Enraged;
            SpawnCaptainForWave(captainTier, preferredCaptain);
        }
        else if (CurrentWaveDifficultyTier >= BannerDifficultyTier.Enraged)
        {
            string preferredCaptain = null;
            if (CampaignManager.Instance != null && CampaignManager.Instance.IsCampaignActive && CampaignManager.Instance.CurrentNode != null)
            {
                preferredCaptain = CampaignManager.Instance.CurrentNode.captainName;
            }
            SpawnCaptainForWave(CurrentWaveDifficultyTier, preferredCaptain);
        }

        if (waveAnnouncementText != null)
        {
            waveAnnouncementText.text = $"WAVE {waveNumber} / {totalWaves}";
        }

        OnWaveStarted?.Invoke(waveNumber);
        Debug.Log($"[GameLoopManager] Started Wave {waveNumber} / {totalWaves}. Target kills: {targetKillsThisWave}");
    }

    private void StartWaveObjective(int waveNumber)
    {
        if (objectiveManager == null)
        {
            objectiveManager = SurvivorsObjectiveManager.Instance ?? FindAnyObjectByType<SurvivorsObjectiveManager>();
        }

        if (objectiveManager != null)
        {
            objectiveManager.OnObjectiveCompleted -= HandleObjectiveCompleted;
            objectiveManager.OnObjectiveCompleted += HandleObjectiveCompleted;
            
            objectiveManager.OnObjectiveFailed -= HandleObjectiveFailed;
            objectiveManager.OnObjectiveFailed += HandleObjectiveFailed;

            isObjectiveComplete = false;
            objectiveManager.StartWaveObjective(waveNumber);
            currentObjective = objectiveManager.CurrentObjective;
            Debug.Log($"[GameLoopManager] Wave {waveNumber} Objective: {currentObjective?.Title ?? "None"}");
        }
        else
        {
            isObjectiveComplete = true;
            currentObjective = null;
        }
    }

    private void HandleObjectiveCompleted(ISurvivorsObjective obj)
    {
        if (obj is KillRemainingEnemiesObjective)
        {
            Debug.Log("[GameLoopManager] All remaining enemies eliminated!");
            ClearActiveWave();
            return;
        }

        isObjectiveComplete = true;
        Debug.Log($"[GameLoopManager] Objective Completed: {obj?.Title}");

        // Stop spawner from spawning any further enemies
        if (spawner != null && spawner.IsSpawningActive)
        {
            spawner.StopSpawning();
        }

        int remainingAlive = spawner != null ? spawner.AliveCount : 0;
        if (remainingAlive > 0)
        {
            Debug.Log($"[GameLoopManager] Objective '{obj?.Title}' completed. {remainingAlive} enemies remain. Starting cleanup phase.");
            if (objectiveManager != null)
            {
                objectiveManager.StartCleanupObjective();
            }
        }
        else
        {
            Debug.Log($"[GameLoopManager] Objective '{obj?.Title}' completed and no enemies remain. Clearing wave.");
            ClearActiveWave();
        }
    }

    private void HandleObjectiveFailed(ISurvivorsObjective obj)
    {
        isObjectiveComplete = true;
        Debug.Log($"[GameLoopManager] Objective Failed: {obj?.Title}. Proceeding with wave clear.");
        CheckWaveCompletionConditions();
    }

    /// <summary>Debug method: Manually completes the active objective to satisfy wave clear conditions.</summary>
    public void DebugCompleteObjective()
    {
        isObjectiveComplete = true;
        if (objectiveManager != null && objectiveManager.CurrentObjective != null)
        {
            objectiveManager.DebugCompleteCurrentObjective();
        }
        CheckWaveCompletionConditions();
    }

    private float cleanupTimer = 0f;

    private void CheckWaveCompletionConditions()
    {
        if (!isWaveActive) return;

        // If in cleanup objective, wave completes once all remaining enemies are dead
        if (objectiveManager != null && objectiveManager.CurrentObjective is KillRemainingEnemiesObjective)
        {
            if (spawner != null && spawner.AliveCount <= 0)
            {
                ClearActiveWave();
            }
            return;
        }

        bool killQuotaMet = killsThisWave >= targetKillsThisWave;

        // Special rule for time-based survival objectives: they end exactly when the time is up, regardless of kills
        if (currentObjective is GoblinRushObjective)
        {
            killQuotaMet = true;
        }

        // Wagon/ram: the wave can't end before the objective resolves (the spawner trickles enemies meanwhile).
        if (currentObjective is IRequiresContinuousSpawns && !isObjectiveComplete)
        {
            cleanupTimer = 0f;
            return;
        }

        if (killQuotaMet && isObjectiveComplete)
        {
            if (spawner != null && spawner.IsSpawningActive)
            {
                spawner.StopSpawning();
            }

            if (spawner != null && spawner.AliveCount > 0)
            {
                // Transition to cleanup objective if not already in cleanup
                if (objectiveManager != null && !(objectiveManager.CurrentObjective is KillRemainingEnemiesObjective))
                {
                    objectiveManager.StartCleanupObjective();
                }
                return;
            }

            ClearActiveWave();
        }
        else
        {
            cleanupTimer = 0f;
        }
    }

    private void Update()
    {
        if (isWaveActive && isObjectiveComplete)
        {
            cleanupTimer += Time.deltaTime;
            // Generous 45s fail-safe for unreachable/stuck enemies (reset by OnEnemyKilled)
            if (cleanupTimer >= 45f)
            {
                cleanupTimer = 0f;
                Debug.LogWarning("[GameLoopManager] Catch-all 45s cleanup timer expired! Striking stragglers with lightning.");
                if (spawner != null)
                {
                    spawner.StopSpawning();
                    spawner.StrikeAllAliveWithLightning(9999f);
                }
                if (isWaveActive)
                {
                    ClearActiveWave();
                }
            }
        }
        else
        {
            cleanupTimer = 0f;
        }
    }

    private void ClearActiveWave()
    {
        if (!isWaveActive) return;
        isWaveActive = false;
        cleanupTimer = 0f;
        int clearedWave = CurrentWave;
        int totalWaves = pacingConfig != null ? pacingConfig.wavesPerRound : 5;

        Debug.Log($"[GameLoopManager] Wave {clearedWave} / {totalWaves} Cleared!");

        // Decrement temporary buff durations from rest shop
        RunSession.OnWaveCompleted();

        // Award wave clear defense supply
        RunSession.AddInRunSupply(30);

        if (clearedWave >= totalWaves)
        {
            // All 5 waves cleared! Stop spawns, despawn lingering enemies, and trigger Victory Screen
            if (spawner != null)
            {
                spawner.StopSpawning();
                spawner.DespawnAllAliveEnemies();
            }

            OnWaveCleared?.Invoke(clearedWave, "Sector Defended");
            TriggerVictory();
        }
        else
        {
            // Intermediate wave (1 to 4): Stop enemy spawns, spawn powerup for the bounty
            if (spawner != null)
            {
                spawner.StopSpawning();
            }

            OnWaveCleared?.Invoke(clearedWave, "Wave Cleared");
            
            SpawnPowerupForCurrentBounty();
        }
    }

    private void SpawnPowerupForCurrentBounty()
    {
        if (activePowerup != null)
        {
            Debug.LogWarning("[GameLoopManager] activePowerup is already present; skipping duplicate powerup spawn.");
            return;
        }

        if (CurrentWaveBounty == BannerBountyType.None)
        {
            StartCoroutine(TransitionToNextBannersRoutine());
            return;
        }

        Vector3 spawnPos = upgradePowerupSpawnPoint != null ? upgradePowerupSpawnPoint.position : Vector3.zero;
        
        GameObject prefabToSpawn = null;
        if (CurrentBannerRewardSO != null && CurrentBannerRewardSO.rewardPrefab != null)
        {
            prefabToSpawn = CurrentBannerRewardSO.rewardPrefab;
        }
        else if (bannerConfig != null)
        {
            var activeRewards = bannerConfig.GetActiveRewards();
            var matchingReward = activeRewards.Find(r => r != null && r.bountyType == CurrentWaveBounty);
            if (matchingReward != null && matchingReward.rewardPrefab != null)
            {
                prefabToSpawn = matchingReward.rewardPrefab;
            }
        }

        if (prefabToSpawn == null)
        {
            prefabToSpawn = upgradePowerupPrefab;
        }

        activePowerup = WaveUpgradePowerup.Spawn(spawnPos, CurrentWaveBounty, prefabToSpawn);
        activePowerup.OnClaimed += HandlePowerupClaimed;
    }

    private void HandlePowerupClaimed(WaveUpgradePowerup powerup)
    {
        powerup.OnClaimed -= HandlePowerupClaimed;

        ApplyBounty(powerup.Bounty, () => 
        {
            powerup.DestroyPowerup();
            CurrentWaveBuff = BannerBuffType.None;
            CurrentWaveBounty = BannerBountyType.None;
            CurrentWaveDifficultyTier = BannerDifficultyTier.Standard;
            CurrentClanBuffSO = null;
            CurrentBannerRewardSO = null;
            StartCoroutine(TransitionToNextBannersRoutine());
        });
    }

    private void ApplyBounty(BannerBountyType bounty, Action onComplete)
    {
        string rewardDesc = "Bounty Claimed";
        bool isDraft = false;
        int multiplier = BannerDifficultyHelper.GetRewardMultiplier(CurrentWaveDifficultyTier);

        switch (bounty)
        {
            case BannerBountyType.WeaponDraft:
                rewardDesc = multiplier > 1 ? $"Weapon Upgrade Draft! ({multiplier}x)" : "Weapon Upgrade Draft!";
                if (multiplier > 1) RunSession.DraftRerollsRemaining += (multiplier - 1);
                if (SurvivorsCardSelectUI.Instance != null)
                {
                    isDraft = true;
                    SurvivorsCardSelectUI.Instance.OpenDraft(DraftCategory.Weapon, onComplete);
                }
                break;
            case BannerBountyType.FortressDraft:
                int supplyBonus = 50 * multiplier;
                rewardDesc = $"Supply Cache! (+{supplyBonus} Supply)";
                RunSession.AddInRunSupply(supplyBonus);
                onComplete?.Invoke();
                break;
            case BannerBountyType.ElementDraft:
                rewardDesc = multiplier > 1 ? $"Elemental Upgrade Draft! ({multiplier}x)" : "Elemental Upgrade Draft!";
                if (multiplier > 1) RunSession.DraftRerollsRemaining += (multiplier - 1);
                if (SurvivorsCardSelectUI.Instance != null)
                {
                    isDraft = true;
                    SurvivorsCardSelectUI.Instance.OpenDraft(DraftCategory.Elemental, onComplete);
                }
                break;
            case BannerBountyType.GoldCache:
                int gold = UnityEngine.Random.Range(75, 126) * multiplier;
                RunSession.AddInRunGold(gold);
                rewardDesc = multiplier > 1 ? $"+{gold} Gold ({multiplier}x)" : $"+{gold} Gold";
                break;
            case BannerBountyType.OrcishMetal:
                int metal = UnityEngine.Random.Range(2, 4) * multiplier;
                RunSession.AddOrcishMetal(metal);
                rewardDesc = multiplier > 1 ? $"+{metal} Orcish Metal ({multiplier}x)" : $"+{metal} Orcish Metal";
                break;
            case BannerBountyType.GoblinBlood:
                int blood = UnityEngine.Random.Range(4, 7) * multiplier;
                RunSession.AddGoblinBlood(blood);
                rewardDesc = multiplier > 1 ? $"+{blood} Goblin Blood ({multiplier}x)" : $"+{blood} Goblin Blood";
                break;
            case BannerBountyType.TrollHeart:
                float bonusHp = 25f * (multiplier > 1 ? 1.5f : 1.0f);
                RunSession.PlayerBonusMaxHealth += bonusHp;
                if (Player.Instance != null && Player.Instance.Health != null)
                {
                    float current = Player.Instance.Health.CurrentHealth;
                    Player.Instance.Health.SetMaxHealth(Player.Instance.Health.MaxHealth + bonusHp);
                    Player.Instance.Health.SetCurrentHealth(current + bonusHp);
                }
                rewardDesc = $"Troll Heart (+{bonusHp} Max HP)";
                break;
        }

        // Apply meta perks
        if (RunSession.HasMetaPerk("regeneration") && Player.Instance != null && Player.Instance.Health != null)
        {
            Player.Instance.Health.Heal(5f);
        }

        if (RunSession.SpecialHerbsWavesRemaining > 0 && Player.Instance != null && Player.Instance.Health != null)
        {
            Player.Instance.Health.Heal(5f);
        }

        if (rewardNotificationText != null)
        {
            rewardNotificationText.text = $"Wave Reward: {rewardDesc}";
        }

        PlayRewardFeedback(bounty, rewardDesc);

        if (!isDraft)
        {
            onComplete?.Invoke();
        }
    }

    private void PlayRewardFeedback(BannerBountyType bounty, string desc)
    {
        if (Player.Instance == null) return;
        Vector3 pos = Player.Instance.transform.position + new Vector3(0, 2f, 0);

        if (rewardSfx != null)
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                MoreMountains.Tools.MMSoundManagerPlayOptions options = MoreMountains.Tools.MMSoundManagerPlayOptions.Default;
                options.MmSoundManagerTrack = MoreMountains.Tools.MMSoundManager.MMSoundManagerTracks.UI;
                options.Location = pos;
                options.Volume = 1f;
                MoreMountains.Tools.MMSoundManagerSoundPlayEvent.Trigger(rewardSfx, options);
            }
#else
            MoreMountains.Tools.MMSoundManagerPlayOptions options = MoreMountains.Tools.MMSoundManagerPlayOptions.Default;
            options.MmSoundManagerTrack = MoreMountains.Tools.MMSoundManager.MMSoundManagerTracks.UI;
            options.Location = pos;
            options.Volume = 1f;
            MoreMountains.Tools.MMSoundManagerSoundPlayEvent.Trigger(rewardSfx, options);
#endif
        }

        DamageNumbersPro.DamageNumber prefab = null;
        if (bounty == BannerBountyType.GoldCache) prefab = goldPopupPrefab;
        else if (bounty == BannerBountyType.OrcishMetal) prefab = metalPopupPrefab;
        else if (bounty == BannerBountyType.GoblinBlood) prefab = bloodPopupPrefab;

        if (prefab != null)
        {
            // Extract the number from desc (e.g. "+75 Gold")
            string[] parts = desc.Split(' ');
            if (parts.Length > 0 && parts[0].StartsWith("+"))
            {
                if (int.TryParse(parts[0].Substring(1), out int amount))
                {
                    prefab.Spawn(pos, amount);
                }
            }
        }
    }

    private IEnumerator TransitionToNextBannersRoutine()
    {
        yield return new WaitForSeconds(3.0f);
        CheckAndSpawnBanners(CurrentWave + 1);
    }

    private int upcomingWave = 1;

    private void CheckAndSpawnBanners(int nextWave)
    {
        upcomingWave = nextWave;
        int totalWaves = pacingConfig != null ? pacingConfig.wavesPerRound : 5;
        if (nextWave > totalWaves) return;

        SpawnWarBanners();
    }

    private void SpawnWarBanners()
    {
        StartCoroutine(SpawnWarBannersRoutine());
    }

    private IEnumerator SpawnWarBannersRoutine()
    {
        isIntermission = true;
        if (intermissionBanner != null) intermissionBanner.SetActive(true);
        if (waveAnnouncementText != null) waveAnnouncementText.text = "SELECT A WAR BANNER TO START NEXT WAVE";
        if (intermissionTimerText != null) intermissionTimerText.text = "[E] Tear Down Banner";

        if (objectiveManager != null) objectiveManager.SetPhase(SurvivorsObjectivePhase.Intermission, 0f);

        // Clear old banners
        foreach (var b in activeBanners) { if (b != null) Destroy(b.gameObject); }
        activeBanners.Clear();

        if (bannerConfig == null || warBannerPrefab == null || bannerSpawnPoints == null || bannerSpawnPoints.Length < 3)
        {
            Debug.LogWarning("[GameLoopManager] Banner config/prefab/spawn points missing. Skipping banners.");
            StartCoroutine(StartNextWaveAfterDelay());
            yield break;
        }
        
        // Pick 3 random unique clans and rewards
        List<WarBannerClanSO> availableClans = new List<WarBannerClanSO>(bannerConfig.GetActiveClans());
        List<WarBannerRewardSO> availableRewards = new List<WarBannerRewardSO>(bannerConfig.GetActiveRewards());

        // Shuffle
        for (int i = 0; i < availableClans.Count; i++) { WarBannerClanSO temp = availableClans[i]; int randomIndex = UnityEngine.Random.Range(i, availableClans.Count); availableClans[i] = availableClans[randomIndex]; availableClans[randomIndex] = temp; }
        for (int i = 0; i < availableRewards.Count; i++) { WarBannerRewardSO temp = availableRewards[i]; int randomIndex = UnityEngine.Random.Range(i, availableRewards.Count); availableRewards[i] = availableRewards[randomIndex]; availableRewards[randomIndex] = temp; }

        SaveData saveData = SaveSystem.Load();
        int runs = (saveData != null) ? saveData.runsAttempted : 1;

        for (int i = 0; i < 3; i++)
        {
            GameObject bannerGo = Instantiate(warBannerPrefab, bannerSpawnPoints[i].position, bannerSpawnPoints[i].rotation);
            WarBannerController controller = bannerGo.GetComponent<WarBannerController>();
            if (controller != null)
            {
                BannerDifficultyTier tier = BannerDifficultyHelper.RollTierForBanner(runs, CurrentRound, i);
                controller.Initialize(availableClans[i % availableClans.Count], availableRewards[i % availableRewards.Count], tier);
                controller.OnBannerInteracted += HandleBannerInteracted;
                controller.StageHighUp();
                activeBanners.Add(controller);
                
                if (i == 0 && intermissionVirtualCamera != null)
                {
                    intermissionVirtualCamera.LookAt = bannerGo.transform;
                }
            }
        }
        
        // --- Cinematic Sequence Start ---
        
        // 1. Brief slow motion
        Time.timeScale = 0.3f;
        
        // 2. Camera transition
        if (intermissionVirtualCamera != null)
        {
            intermissionVirtualCamera.gameObject.SetActive(true);
        }
        
        yield return new WaitForSecondsRealtime(0.6f);
        Time.timeScale = 1.0f;
        
        // Show side stats panel
        if (intermissionStatsPanel != null)
        {
            intermissionStatsPanel.SetActive(true);
        }

        // 3. Drop banners
        for (int i = 0; i < 3; i++)
        {
            if (activeBanners.Count > i && activeBanners[i] != null)
            {
                activeBanners[i].SlamDown();
            }
            yield return new WaitForSeconds(0.4f);
        }
        
        // Wait a moment to admire the banners, then cut back to player
        yield return new WaitForSeconds(1.0f);
        if (intermissionVirtualCamera != null)
        {
            intermissionVirtualCamera.gameObject.SetActive(false);
        }
    }

    private void HandleBannerInteracted(WarBannerController selectedBanner)
    {
        CurrentClanBuffSO = selectedBanner.Clan;
        CurrentBannerRewardSO = selectedBanner.Reward;
        CurrentWaveBuff = selectedBanner.Clan != null ? selectedBanner.Clan.buffType : selectedBanner.Buff.buffType;
        CurrentWaveBounty = selectedBanner.Reward != null ? selectedBanner.Reward.bountyType : selectedBanner.Bounty.bountyType;
        CurrentWaveDifficultyTier = selectedBanner.DifficultyTier;

        StartCoroutine(BannerTeardownRoutine(selectedBanner));
    }
    
    private IEnumerator BannerTeardownRoutine(WarBannerController selectedBanner)
    {
        if (intermissionVirtualCamera != null)
        {
            intermissionVirtualCamera.LookAt = selectedBanner.transform;
            intermissionVirtualCamera.gameObject.SetActive(true);
        }

        // 1. Lock interaction on all banners
        foreach (var banner in activeBanners)
        {
            if (banner != null)
            {
                banner.SetInteractable(false);
            }
        }
        
        // 2. Hide Stats Panel
        if (intermissionStatsPanel != null)
        {
            intermissionStatsPanel.SetActive(false);
        }
        
        // 3. Trigger animations
        foreach (var banner in activeBanners)
        {
            if (banner != null)
            {
                if (banner == selectedBanner)
                {
                    banner.TearDown();
                }
                else
                {
                    banner.ShrinkOut();
                }
            }
        }
        
        activeBanners.Clear();
        
        Debug.Log("[GameLoopManager] Them's tearin down ours bannah! Get 'em!");
        
        // 4. Wait for burning dissolve to finish
        yield return new WaitForSeconds(3.0f);
        
        // 5. Restore camera
        if (intermissionVirtualCamera != null)
        {
            intermissionVirtualCamera.gameObject.SetActive(false);
        }

        StartCoroutine(StartNextWaveAfterDelay());
    }

    private IEnumerator StartNextWaveAfterDelay()
    {
        // Brief countdown
        for (int sec = 3; sec > 0; sec--)
        {
            if (intermissionTimerText != null) intermissionTimerText.text = $"Next Wave in: {sec}s";
            if (waveAnnouncementText != null) waveAnnouncementText.text = $"NEXT WAVE IN {sec}...";
            yield return new WaitForSeconds(1.0f);
        }

        isIntermission = false;
        if (intermissionBanner != null) intermissionBanner.SetActive(false);

        StartWave(upcomingWave);
    }

    /// <summary>
    ///     Spawns a Clan Captain (e.g. Captain Kombusta or Captain Fraglob) for Enraged, Nightmare, or Omega difficulty tiers,
    ///     plays the cinematic EnemyIntroUI with difficulty skulls, and initializes the captain controller.
    /// </summary>
    public GameObject SpawnCaptainForWave(BannerDifficultyTier tier, string preferredCaptainName = null)
    {
        Vector3 spawnPos = captainSpawnPoint != null ? captainSpawnPoint.position : 
                           (bossSpawnPoint != null ? bossSpawnPoint.position : (transform.position + new Vector3(0f, 0f, 25f)));
        Quaternion spawnRot = captainSpawnPoint != null ? captainSpawnPoint.rotation : 
                             (bossSpawnPoint != null ? bossSpawnPoint.rotation : Quaternion.identity);

        bool pickKombusta;
        if (!string.IsNullOrEmpty(preferredCaptainName))
        {
            pickKombusta = preferredCaptainName.IndexOf("Kombusta", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        else
        {
            pickKombusta = UnityEngine.Random.value < 0.5f;
        }

        if (!pickKombusta && captainPrefab == null)
        {
            Debug.LogError("[GameLoopManager] Captain Fraglob has no prefab assigned (captainPrefab). Spawning Captain Kombusta instead. Needs Lance in the Editor.");
            pickKombusta = true;
        }

        GameObject captainGo;
        string captainName;
        if (pickKombusta)
        {
            captainName = "Captain Kombusta";
            // The roster's captain_kombusta row is the same authored prefab when the slot is empty.
            captainGo = captainKombustaPrefab != null
                ? Instantiate(captainKombustaPrefab, spawnPos, spawnRot)
                : spawner != null && spawner.Roster != null && spawner.Roster.Find("captain_kombusta") != null
                    ? spawner.DebugSpawnEnemyType("captain_kombusta")
                    : null;
            if (captainGo == null)
            {
                Debug.LogError("[GameLoopManager] Captain Kombusta has no prefab (captainKombustaPrefab empty and no captain_kombusta roster row). No captain spawned.");
                return null;
            }

            CaptainKombustaController kombusta = captainGo.GetComponent<CaptainKombustaController>();
            if (kombusta == null)
            {
                Debug.LogError($"[GameLoopManager] Captain Kombusta prefab '{captainGo.name}' has no CaptainKombustaController.");
                return captainGo;
            }
            kombusta.Initialize(tier, captainName);
        }
        else
        {
            captainName = "Captain Fraglob";
            captainGo = Instantiate(captainPrefab, spawnPos, spawnRot);

            CaptainEnemyController fraglob = captainGo.GetComponent<CaptainEnemyController>();
            if (fraglob == null)
            {
                Debug.LogError($"[GameLoopManager] Captain Fraglob prefab '{captainGo.name}' has no CaptainEnemyController.");
                return captainGo;
            }
            fraglob.Initialize(tier, captainName);
        }

        // Play the enemy intro announcement with difficulty skulls
        if (EnemyIntroUI.Instance != null)
        {
            string subtitle = $"{BannerDifficultyHelper.GetTierName(tier).ToUpper()} - {BannerDifficultyHelper.GetRewardMultiplier(tier)}X REWARDS";
            EnemyIntroUI.Instance.ShowIntro($"{captainName} has arrived!", (int)tier, subtitle, 3.5f);
        }

        Debug.Log($"[GameLoopManager] Spawned {captainName} at Tier {tier} ({BannerDifficultyHelper.GetRewardMultiplier(tier)}x rewards)!");
        return captainGo;
    }

    private void TriggerVictory()
    {
        int totalWaves = pacingConfig != null ? pacingConfig.wavesPerRound : 5;
        Debug.Log($"[GameLoopManager] DEFENSE VICTORY! All {totalWaves} waves cleared!");
        isWaveActive = false;
        isIntermission = false;

        // Preserve player health ratio so it carries over to the next node
        if (Player.Instance != null && Player.Instance.Health != null)
        {
            RunSession.PlayerHealthRatio = Mathf.Clamp01(Player.Instance.Health.CurrentHealth / Player.Instance.Health.MaxHealth);
        }

        // Towers don't follow the player to the next sector: dismantle them for supply.
        int towerRefund = TowerPlotManager.Instance != null ? TowerPlotManager.Instance.DismantleAllForRefund() : 0;

        // Trigger DeathScreen in victory mode (No procedural fallback UI)
        DeathScreen endScreen = DeathScreen.Instance != null ? DeathScreen.Instance : FindAnyObjectByType<DeathScreen>();
        if (endScreen != null)
        {
            endScreen.ShowVictory("VICTORY!", towerRefund);
        }
        else if (victoryScreen != null)
        {
            victoryScreen.SetActive(true);
            CursorLockManager.SetUnlock("VictoryScreen", true);
        }

        OnVictory?.Invoke();
    }

    private void HandlePlayerDied()
    {
        // Second Wind is handled by RunSession's Health.TryPreventDeath hook, before death is final.
        Debug.Log("[GameLoopManager] Player died. DeathScreen handles run conclusion and transition to Meta Area.");
    }

    private void OnGUI()
    {
        if (!Application.isPlaying) return;
        
        if (!enableGameLoopManageStateDebug) return;

        GUILayout.BeginArea(new Rect(10, 10, 350, 250), GUI.skin.box);
        GUILayout.Label("<b>GameLoopManager State</b>");
        GUILayout.Label($"Round: {CurrentRound} | Wave: {CurrentWave}");
        GUILayout.Label($"Wave Active: {isWaveActive} | Intermission: {isIntermission}");
        GUILayout.Label($"Kills: {killsThisWave} / {targetKillsThisWave}");
        
        if (currentObjective != null)
        {
            GUILayout.Label($"Objective: {currentObjective.Title}");
            GUILayout.Label($"Objective Complete: {isObjectiveComplete}");
        }
        else
        {
            GUILayout.Label("Objective: None");
        }

        if (spawner != null)
        {
            GUILayout.Label($"Spawner Active: {spawner.IsSpawningActive}");
            GUILayout.Label($"Enemies Alive: {spawner.AliveCount} / {spawner.MaxConcurrentEnemies}");
            GUILayout.Label($"Remaining to Spawn: {spawner.RemainingToSpawn}");
            GUILayout.Label($"Remaining to Kill: {Mathf.Max(0, targetKillsThisWave - killsThisWave)}");
        }
        
        GUILayout.EndArea();
    }
}
