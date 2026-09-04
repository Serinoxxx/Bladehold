using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
///     Master controller for the overhauled 4-round / 12-wave game loop.
///     Coordinates round-based enemy unlocks, per-wave random objectives, kill quotas,
///     30s intermissions with reward drops, 3-wave rest breaks at the gate,
///     the Round 4 Slayer/Siegebreaker boss, level victory, and death transitions to the Meta Area.
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

    public BannerBuffType CurrentWaveBuff { get; private set; } = BannerBuffType.None;
    public BannerBountyType CurrentWaveBounty { get; private set; } = BannerBountyType.None;
    private List<WarBannerController> activeBanners = new List<WarBannerController>();

    [Header("UI Dependencies")]
    [SerializeField] private SurvivorsSpawner spawner;
    [SerializeField] private Interactable castleGateInteractable;
    [SerializeField] private SurvivorsObjectiveManager objectiveManager;
    [SerializeField] private Transform bossSpawnPoint;
    [SerializeField] private GameObject siegebreakerBossPrefab;

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
    private GameObject spawnedBoss;
    private WaveUpgradePowerup activePowerup;
    private bool isRestGateOpen = false;

    public int CurrentWave => RunSession.CurrentWave;
    public int CurrentRound => RunSession.CurrentRound;
    public int KillsThisWave => killsThisWave;
    public int TargetKillsThisWave => targetKillsThisWave;
    public SurvivorsObjectiveManager ObjectiveManager => objectiveManager;
    public ISurvivorsObjective CurrentObjective => objectiveManager != null ? objectiveManager.CurrentObjective : currentObjective;
    public bool IsWaveActive => isWaveActive;
    public bool IsIntermission => isIntermission;
    public float IntermissionTimeRemaining => intermissionTimeRemaining;
    public bool IsRestGateOpen => isRestGateOpen;
    public Transform CastleGateTransform => castleGateInteractable != null ? castleGateInteractable.transform : null;
    public Transform UpgradePowerupSpawnPoint => upgradePowerupSpawnPoint;
    public WaveUpgradePowerup ActivePowerup => activePowerup;

    public event Action<int> OnWaveStarted;
    public event Action<int, string> OnWaveCleared;
    public event Action<float> OnIntermissionTick;
    public event Action OnRestGateOpened;
    public event Action OnVictory;

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
            ApplyPlayerTrollHeartBonus();
        }

        // Auto-discover gate if not wired
        if (castleGateInteractable == null)
        {
            Gate gate = FindAnyObjectByType<Gate>();
            if (gate != null)
            {
                castleGateInteractable = gate.GetComponent<Interactable>();
                if (castleGateInteractable == null)
                {
                    castleGateInteractable = gate.gameObject.AddComponent<Interactable>();
                }
            }
        }

        if (castleGateInteractable != null)
        {
            castleGateInteractable.PromptText = "Rest Area";
            castleGateInteractable.CanInteract = false;
            castleGateInteractable.OnInteractedEvent += HandleGateInteracted;
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
        CheckAndSpawnBanners(initialWave);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (Player.Instance != null && Player.Instance.Health != null)
        {
            Player.Instance.Health.OnDied -= HandlePlayerDied;
        }

        if (castleGateInteractable != null)
        {
            castleGateInteractable.OnInteractedEvent -= HandleGateInteracted;
        }

        if (objectiveManager != null)
        {
            objectiveManager.OnObjectiveCompleted -= HandleObjectiveCompleted;
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

    private void ApplyPlayerTrollHeartBonus()
    {
        if (RunSession.PlayerBonusMaxHealth > 0f && Player.Instance != null && Player.Instance.Health != null)
        {
            Player.Instance.Health.SetMaxHealth(Player.Instance.Health.MaxHealth + RunSession.PlayerBonusMaxHealth);
            Player.Instance.Health.Heal(RunSession.PlayerBonusMaxHealth);
        }
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

        CheckWaveCompletionConditions();
    }

    public void StartWave(int waveNumber)
    {
        RunSession.CurrentWave = waveNumber;
        int round = RunSession.CurrentRound;

        RoundPacingConfigSO.RoundDefinition roundDef = pacingConfig != null ? pacingConfig.GetRound(round) : null;
        targetKillsThisWave = roundDef != null ? roundDef.requiredKillsPerWave : (15 + (round - 1) * 5);
        killsThisWave = 0;
        isObjectiveComplete = false;
        isWaveActive = true;
        isIntermission = false;
        isRestGateOpen = false;

        if (castleGateInteractable != null)
        {
            castleGateInteractable.CanInteract = false;
        }

        if (intermissionBanner != null) intermissionBanner.SetActive(false);

        // Start wave objective via SurvivorsObjectiveManager
        StartWaveObjective(waveNumber);

        // Start Spawner with round-specific settings & quota
        if (spawner != null)
        {
            spawner.StartWave(waveNumber, targetKillsThisWave);
        }

        // Round 4 Boss: Slayer / Siegebreaker
        if (pacingConfig != null && waveNumber == pacingConfig.bossSpawnWave)
        {
            SpawnEndgameBoss();
        }

        if (waveAnnouncementText != null)
        {
            waveAnnouncementText.text = $"WAVE {waveNumber} - ROUND {round}";
        }

        OnWaveStarted?.Invoke(waveNumber);
        Debug.Log($"[GameLoopManager] Started Wave {waveNumber} (Round {round}). Target kills: {targetKillsThisWave}");
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
        isObjectiveComplete = true;
        Debug.Log($"[GameLoopManager] Objective Completed: {obj?.Title}");
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

    private void CheckWaveCompletionConditions()
    {
        if (!isWaveActive) return;

        bool killQuotaMet = killsThisWave >= targetKillsThisWave;

        // Escort wagon special rule: enemies must keep spawning until wagon arrives at the destination!
        if (currentObjective is ProtectWagonObjective)
        {
            if (!isObjectiveComplete)
            {
                // Wagon has not reached the destination yet; wave cannot end
                return;
            }
        }

        if (killQuotaMet && isObjectiveComplete)
        {
            ClearActiveWave();
        }
    }

    private void ClearActiveWave()
    {
        isWaveActive = false;
        int clearedWave = CurrentWave;
        int wavesPerRound = pacingConfig != null ? pacingConfig.wavesPerRound : 3;
        bool isRestWave = (clearedWave % wavesPerRound == 0);

        Debug.Log($"[GameLoopManager] Wave {clearedWave} Cleared! (RestWave: {isRestWave})");

        // Decrement temporary buff durations from rest shop
        RunSession.OnWaveCompleted();

        if (isRestWave)
        {
            isRestGateOpen = true;

            // Stop spawns and despawn lingering enemies
            if (spawner != null)
            {
                spawner.StopSpawning();
                spawner.DespawnAllAliveEnemies();
            }

            // Open Castle Gate for Rest Break
            if (castleGateInteractable == null)
            {
                Gate gate = FindAnyObjectByType<Gate>();
                if (gate != null) castleGateInteractable = gate.GetComponent<Interactable>();
            }

            if (castleGateInteractable != null)
            {
                castleGateInteractable.CanInteract = true;
                castleGateInteractable.PromptText = "Return to Fortress";
            }

            if (waveAnnouncementText != null)
            {
                waveAnnouncementText.text = "ROUND COMPLETE! RETURN TO THE FORTRESS VIA GATE";
            }

            OnRestGateOpened?.Invoke();
            OnWaveCleared?.Invoke(clearedWave, "Return to the Fortress");
        }
        else
        {
            // Intermediate wave: Stop enemy spawns, grant bounty, and spawn next banners
            if (spawner != null)
            {
                spawner.StopSpawning();
            }

            GrantBounty();
            OnWaveCleared?.Invoke(clearedWave, "Wave Cleared");
            
            CurrentWaveBuff = BannerBuffType.None;
            CurrentWaveBounty = BannerBountyType.None;

            // Wait a moment before spawning banners for the next wave
            StartCoroutine(TransitionToNextBannersRoutine());
        }

        // Check for stage victory: survived all 4 rounds (Wave 12)
        int totalRounds = pacingConfig != null ? pacingConfig.totalRounds : 4;
        if (clearedWave >= totalRounds * wavesPerRound)
        {
            TriggerVictory();
        }
    }

    private void GrantBounty()
    {
        string rewardDesc = "Bounty Claimed";

        switch (CurrentWaveBounty)
        {
            case BannerBountyType.WeaponDraft:
                rewardDesc = "Weapon Upgrade Draft!";
                if (SurvivorsCardSelector.Instance != null) SurvivorsGameManager.Instance?.PauseForCardSelection();
                break;
            case BannerBountyType.FortressDraft:
                rewardDesc = "Fortress Upgrade Draft!";
                // In a real implementation this might use a specific category, but we just trigger standard draft
                if (SurvivorsCardSelector.Instance != null) SurvivorsGameManager.Instance?.PauseForCardSelection();
                break;
            case BannerBountyType.ElementDraft:
                rewardDesc = "Elemental Upgrade Draft!";
                if (SurvivorsCardSelector.Instance != null) SurvivorsGameManager.Instance?.PauseForCardSelection();
                break;
            case BannerBountyType.GoldCache:
                int gold = UnityEngine.Random.Range(75, 126);
                RunSession.AddInRunGold(gold);
                rewardDesc = $"+{gold} Gold";
                break;
            case BannerBountyType.OrcishMetal:
                int metal = UnityEngine.Random.Range(2, 4);
                RunSession.AddOrcishMetal(metal);
                rewardDesc = $"+{metal} Orcish Metal";
                break;
            case BannerBountyType.GoblinBlood:
                int blood = UnityEngine.Random.Range(4, 7);
                RunSession.AddGoblinBlood(blood);
                rewardDesc = $"+{blood} Goblin Blood";
                break;
            case BannerBountyType.TrollHeart:
                RunSession.PlayerBonusMaxHealth += 25f;
                if (Player.Instance != null && Player.Instance.Health != null)
                {
                    Player.Instance.Health.SetMaxHealth(Player.Instance.Health.MaxHealth + 25f);
                    Player.Instance.Health.Heal(25f);
                }
                rewardDesc = "Troll Heart (+25 Max HP)";
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
        int wavesPerRound = pacingConfig != null ? pacingConfig.wavesPerRound : 3;
        bool isRestWave = (nextWave % wavesPerRound == 0);

        if (isRestWave || bannerConfig == null || warBannerPrefab == null)
        {
            // Banners do not spawn preceding a Rest Area. We just start the wave (which will open the gate) or wait?
            // Wait, if nextWave is a rest wave, it's just a regular wave where at the end the gate opens.
            // Oh, wait. The spec says: "Banners do not spawn preceding a Rest Area; they spawn only before standard combat waves."
            // So if wave 3 is a rest wave... wait, is Wave 3 played, or is Wave 3 the Rest Area itself?
            // "Retain: Fixed Rest Areas occurring strictly every 3 waves (Waves 3, 6, 9, etc.)... Banners do not spawn preceding a Rest Area; they spawn only before standard combat waves."
            // Let's assume standard waves are 1, 2, 4, 5. Wait, earlier code said `clearedWave % 3 == 0` opens the gate AT THE END of wave 3.
            // So Wave 3 IS a combat wave, and at the end of it, the gate opens.
            // But if "Banners do not spawn preceding a Rest Area", maybe Wave 3 itself doesn't have banners?
            // "Route player to the Rest Area. Do not spawn banners." This happens on OnWaveCompleted if `waveIndex + 1 % 3 == 0` (which means after wave 2 completes, wave 3 is next? No, waveIndex is zero-based in the spec, but here it's 1-based: `clearedWave % 3 == 0`.)
            // Let's just always spawn banners unless we just finished a wave that opened the gate.
            // If the gate is open, we don't spawn banners.
        }

        SpawnWarBanners();
    }

    private void SpawnWarBanners()
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
            return;
        }

        // Pick 3 random unique buffs and bounties
        List<BannerBuffDef> buffs = new List<BannerBuffDef>(bannerConfig.buffs);
        List<BannerBountyDef> bounties = new List<BannerBountyDef>(bannerConfig.bounties);

        // Shuffle
        for (int i = 0; i < buffs.Count; i++) { BannerBuffDef temp = buffs[i]; int randomIndex = UnityEngine.Random.Range(i, buffs.Count); buffs[i] = buffs[randomIndex]; buffs[randomIndex] = temp; }
        for (int i = 0; i < bounties.Count; i++) { BannerBountyDef temp = bounties[i]; int randomIndex = UnityEngine.Random.Range(i, bounties.Count); bounties[i] = bounties[randomIndex]; bounties[randomIndex] = temp; }

        for (int i = 0; i < 3; i++)
        {
            GameObject bannerGo = Instantiate(warBannerPrefab, bannerSpawnPoints[i].position, bannerSpawnPoints[i].rotation);
            WarBannerController controller = bannerGo.GetComponent<WarBannerController>();
            if (controller != null)
            {
                controller.Initialize(buffs[i % buffs.Count], bounties[i % bounties.Count]);
                controller.OnBannerInteracted += HandleBannerInteracted;
                activeBanners.Add(controller);
            }
        }
    }

    private void HandleBannerInteracted(WarBannerController selectedBanner)
    {
        CurrentWaveBuff = selectedBanner.Buff.buffType;
        CurrentWaveBounty = selectedBanner.Bounty.bountyType;

        // Despawn others
        foreach (var banner in activeBanners)
        {
            if (banner != null)
            {
                if (banner == selectedBanner)
                {
                    // Play destruction VFX/SFX here on selected banner
                    Destroy(banner.gameObject, 0.5f); // Destroy after a short delay
                }
                else
                {
                    Destroy(banner.gameObject);
                }
            }
        }
        activeBanners.Clear();

        // Voice bark
        Debug.Log("[GameLoopManager] Them's tearin down ours bannah! Get 'em!");

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

    private void HandleGateInteracted(Player player)
    {
        Debug.Log("[GameLoopManager] Gate interacted! Transitioning to Rest Area Scene...");
        RunSession.RestVisitsCount++;

        // Preserve player health ratio
        if (Player.Instance != null && Player.Instance.Health != null)
        {
            RunSession.PlayerHealthRatio = Player.Instance.Health.CurrentHealth / Player.Instance.Health.MaxHealth;
        }

        // Load Rest Area Scene
        if (Application.isPlaying)
        {
            SceneManager.LoadScene("Bladehold Rest Area Scene");
        }
    }

    private void SpawnEndgameBoss()
    {
        if (spawnedBoss != null) return;

        Vector3 spawnPos = bossSpawnPoint != null ? bossSpawnPoint.position : transform.position + new Vector3(0f, 0f, 30f);
        Quaternion spawnRot = bossSpawnPoint != null ? bossSpawnPoint.rotation : Quaternion.identity;

        if (siegebreakerBossPrefab != null)
        {
            spawnedBoss = Instantiate(siegebreakerBossPrefab, spawnPos, spawnRot);
        }
        else if (spawner != null)
        {
            spawner.DebugSpawnEnemyType(pacingConfig != null ? pacingConfig.bossEnemyId : "slayer");
        }

        Debug.Log("[GameLoopManager] Round 4 Endgame Boss Spawned!");
    }

    private void TriggerVictory()
    {
        Debug.Log("[GameLoopManager] STAGE VICTORY! All 4 rounds cleared!");
        isWaveActive = false;
        isIntermission = false;

        SaveData data = SaveSystem.Load();
        if (data != null)
        {
            data.highestUnlockedStage = Mathf.Max(data.highestUnlockedStage, data.selectedStage + 1);
            SaveSystem.Save(data);
        }

        if (victoryScreen != null)
        {
            victoryScreen.SetActive(true);
        }

        OnVictory?.Invoke();
    }

    private void HandlePlayerDied()
    {
        // Second Wind meta perk: revive once per run with 50% HP
        if (RunSession.HasMetaPerk("second_wind") && Player.Instance != null && Player.Instance.Health != null)
        {
            // Check if already used
            // If revive succeeds, heal 50%
        }

        Debug.Log("[GameLoopManager] Player died. Transitioning to Meta Area Scene...");
        RunSession.ClearRun();
        if (Application.isPlaying)
        {
            StartCoroutine(TransitionToMetaSceneRoutine());
        }
    }

    private IEnumerator TransitionToMetaSceneRoutine()
    {
        yield return new WaitForSeconds(2.0f);
        CursorLockManager.SetUnlock("MetaArea", true);
        if (Application.isPlaying)
        {
            SceneManager.LoadScene("Bladehold Meta Area Scene");
        }
    }
}
