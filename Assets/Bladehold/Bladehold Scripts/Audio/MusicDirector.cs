using System.Collections.Generic;
using Bladehold.UI;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
///     Persistent music brain. Feel's <see cref="MMSoundManager" /> plays the audio (Music track, so the Music
///     slider works); this decides what plays. It derives the wanted <see cref="MusicCueSO" /> from a little
///     state (scene, wave phase, captains alive, boss fight, run over) and crossfades whenever that changes.
///     It owns every music source's volume (<c>cue volume x envelope x duck</c>, on unscaled time), so
///     pause ducking and sting ducking never fight a tween. Spawned before the first scene from
///     <c>Resources/Audio/MusicSystem.prefab</c>.
/// </summary>
public class MusicDirector : MonoBehaviour
{
    private const string PrefabResourcePath = "Audio/MusicSystem";

    /// <summary>A source that is not playing is only treated as finished once it has had this long to start.</summary>
    private const float StartGraceSeconds = 1f;

    private enum Phase { None, Prep, Battle }

    private sealed class Voice
    {
        public AudioSource source;
        public AudioClip clip;
        public MusicCueSO cue;
        public bool sting;
        public bool rotated;
        public float startTime;
        public float envelope;
        public float target;
        public float rate;
    }

    [Tooltip("Default cues and fade timings.")]
    [SerializeField] private MusicSettingsSO settings;

    private static MusicDirector instance;

    private readonly List<Voice> voices = new List<Voice>();
    private readonly Dictionary<MusicCueSO, AudioClip> lastPick = new Dictionary<MusicCueSO, AudioClip>();
    private readonly List<Health> captainHealths = new List<Health>();

    private Voice current;
    private Voice dipped;
    private bool transitionActive;
    private bool needsRefresh;
    private bool pendingSceneRefresh;
    private bool anyError;

    private Phase phase;
    private int captainsAlive;
    private bool bossFight;
    private bool silenced;
    private MusicCueSO terminalCue;

    private float pauseDuck = 1f;
    private float pauseTarget = 1f;
    private float stingDuck = 1f;
    private float stingHoldUntil;

    private GameLoopManager gameLoop;
    private SurvivorsObjectiveManager objectives;
    private PauseMenuController pauseMenu;
    private Health playerHealth;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        MusicDirector prefab = Resources.Load<MusicDirector>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogError("[MusicDirector] Resources/" + PrefabResourcePath + ".prefab is missing; the game has no music.");
            return;
        }
        Instantiate(prefab).name = prefab.name;
    }

    /// <summary>Boss fights call this when the fight starts and ends. No-op when the director is not running.</summary>
    public static void SetBossFight(bool active)
    {
        if (instance != null)
        {
            instance.SetBossFightInternal(active);
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (instance != this)
        {
            return;
        }

        if (settings == null)
        {
            Debug.LogError("[MusicDirector] settings is not assigned; music is disabled.", this);
            anyError = true;
        }

        // The first scene's sceneLoaded can fire before this subscribed (the Editor loads the open scene first),
        // so always pick music once on the first frame.
        pendingSceneRefresh = true;
    }

    private void OnEnable()
    {
        if (instance != this)
        {
            return;
        }

        SceneManager.sceneLoaded += HandleSceneLoaded;
        LoadingScreenManager.OnTransitionStarted += HandleTransitionStarted;
        LoadingScreenManager.OnSceneRevealed += HandleSceneRevealed;
        Gate.OnAnyGateDestroyed += HandleGateDestroyed;
        DeathScreen.OnRunOver += HandleRunOver;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        LoadingScreenManager.OnTransitionStarted -= HandleTransitionStarted;
        LoadingScreenManager.OnSceneRevealed -= HandleSceneRevealed;
        Gate.OnAnyGateDestroyed -= HandleGateDestroyed;
        DeathScreen.OnRunOver -= HandleRunOver;
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (instance == this)
        {
            instance = null;
        }
    }

    // ---------------------------------------------------------------- per-frame

    private void Update()
    {
        if (anyError || instance != this)
        {
            return;
        }

        float dt = Time.unscaledDeltaTime;
        UpdateDucks(dt);

        for (int i = voices.Count - 1; i >= 0; i--)
        {
            Voice v = voices[i];
            if (!IsAlive(v))
            {
                voices.RemoveAt(i);
                if (v == current)
                {
                    current = null;
                    needsRefresh = true;
                }
                if (v == dipped)
                {
                    dipped = null;
                }
                continue;
            }

            v.envelope = Mathf.MoveTowards(v.envelope, v.target, v.rate * dt);
            float duck = v.sting ? 1f : pauseDuck * stingDuck;
            v.source.volume = Mathf.Clamp01(v.cue.volume * v.envelope * duck);

            if (v.envelope <= 0f && v.target <= 0f && v != dipped)
            {
                FreeVoice(v);
                voices.RemoveAt(i);
                if (v == current)
                {
                    current = null;
                }
            }
        }

        RotateCurrent();

        if (pendingSceneRefresh && !transitionActive)
        {
            pendingSceneRefresh = false;
            needsRefresh = false;
            Subscribe();
            if (gameLoop != null)
            {
                phase = gameLoop.IsWaveActive ? Phase.Battle : Phase.Prep;
            }
            Refresh(settings.sceneFadeIn);
        }
        else if (needsRefresh && !transitionActive)
        {
            needsRefresh = false;
            Refresh(settings.toPrep);
        }

        if (playerHealth == null && Player.Instance != null && Player.Instance.Health != null)
        {
            playerHealth = Player.Instance.Health;
            playerHealth.OnDied += HandlePlayerDied;
        }
    }

    private void UpdateDucks(float dt)
    {
        float pauseSpeed = (1f - settings.pauseDuckTo) / Mathf.Max(0.01f, settings.pauseDuckFade);
        pauseDuck = Mathf.MoveTowards(pauseDuck, pauseTarget, pauseSpeed * dt);

        bool stingHeld = Time.unscaledTime < stingHoldUntil;
        float stingTarget = stingHeld ? settings.stingDuckTo : 1f;
        float stingSeconds = stingHeld ? settings.stingDuckIn : settings.stingDuckOut;
        float stingSpeed = (1f - settings.stingDuckTo) / Mathf.Max(0.01f, stingSeconds);
        stingDuck = Mathf.MoveTowards(stingDuck, stingTarget, stingSpeed * dt);
    }

    private void RotateCurrent()
    {
        Voice v = current;
        if (v == null || v.cue.loopClip || v.rotated || transitionActive)
        {
            return;
        }

        float remaining = v.clip.length - v.source.time;
        if (remaining > v.cue.rotateCrossfade)
        {
            return;
        }

        v.rotated = true;
        MusicCueSO cue = v.cue;
        Fade(v, 0f, Mathf.Max(remaining, 0.1f));
        current = StartVoice(cue, cue.rotateCrossfade, false);
        if (current == null)
        {
            needsRefresh = true;
        }
    }

    // ---------------------------------------------------------------- choosing music

    /// <summary>False when nothing has an opinion (a scene without <see cref="SceneMusic" /> and no wave phase): keep what plays.</summary>
    private bool TryGetWanted(out MusicCueSO cue)
    {
        cue = null;
        if (silenced)
        {
            return true;
        }
        if (terminalCue != null)
        {
            cue = terminalCue;
            return true;
        }
        if (bossFight)
        {
            cue = settings.boss;
            return true;
        }

        SceneMusic scene = SceneMusic.Current;
        if (phase == Phase.Battle)
        {
            if (captainsAlive > 0)
            {
                cue = settings.captain;
            }
            else
            {
                cue = scene != null && scene.BattleOverride != null ? scene.BattleOverride : settings.battle;
            }
            return true;
        }
        if (phase == Phase.Prep)
        {
            cue = scene != null && scene.PrepOverride != null ? scene.PrepOverride : settings.prep;
            return true;
        }

        if (scene == null)
        {
            return false;
        }
        cue = scene.SceneCue;
        return true;
    }

    private void Refresh(float seconds)
    {
        if (anyError || transitionActive || !TryGetWanted(out MusicCueSO wanted))
        {
            return;
        }

        if (current == null && wanted == null)
        {
            return;
        }
        if (current != null && current.cue == wanted)
        {
            return;
        }

        Crossfade(wanted, seconds);
    }

    private void Crossfade(MusicCueSO wanted, float seconds)
    {
        if (current != null)
        {
            Fade(current, 0f, seconds);
            current = null;
        }
        if (wanted != null)
        {
            current = StartVoice(wanted, seconds, false);
        }
    }

    // ---------------------------------------------------------------- voices

    private Voice StartVoice(MusicCueSO cue, float fadeInSeconds, bool sting)
    {
        AudioClip clip = PickClip(cue);
        if (clip == null)
        {
            Debug.LogWarning($"[MusicDirector] Cue '{cue.name}' has no clips.", cue);
            return null;
        }

        MMSoundManagerPlayOptions options = MMSoundManagerPlayOptions.Default;
        options.MmSoundManagerTrack = MMSoundManager.MMSoundManagerTracks.Music;
        options.Loop = !sting && cue.loopClip;
        options.Volume = sting ? Mathf.Clamp01(cue.volume) : 0f;
        options.Persistent = true;

        AudioSource source = MMSoundManager.Instance.PlaySound(clip, options);
        if (source == null)
        {
            return null;
        }

        Voice v = new Voice
        {
            source = source,
            clip = clip,
            cue = cue,
            sting = sting,
            startTime = Time.unscaledTime,
            envelope = sting ? 1f : 0f,
        };
        Fade(v, 1f, fadeInSeconds);
        voices.Add(v);
        return v;
    }

    private AudioClip PickClip(MusicCueSO cue)
    {
        AudioClip[] pool = cue.clips;
        if (pool == null || pool.Length == 0)
        {
            return null;
        }
        if (pool.Length == 1)
        {
            return pool[0];
        }

        lastPick.TryGetValue(cue, out AudioClip last);
        AudioClip pick;
        do
        {
            pick = pool[Random.Range(0, pool.Length)];
        }
        while (pick == last || pick == null);

        lastPick[cue] = pick;
        return pick;
    }

    private static void Fade(Voice v, float target, float seconds)
    {
        v.target = target;
        v.rate = seconds <= 0.001f ? 1000f : Mathf.Abs(target - v.envelope) / seconds;
    }

    /// <summary>False once the pool recycled or disabled the source, or the clip finished on its own.</summary>
    private static bool IsAlive(Voice v)
    {
        if (v.source == null || !v.source.gameObject.activeInHierarchy || v.source.clip != v.clip)
        {
            return false;
        }
        return v.source.isPlaying || Time.unscaledTime - v.startTime < StartGraceSeconds;
    }

    private void FreeVoice(Voice v)
    {
        if (v.source != null && MMSoundManager.HasInstance)
        {
            MMSoundManager.Instance.FreeSound(v.source);
        }
    }

    private void PlaySting(MusicCueSO cue)
    {
        if (anyError || cue == null || transitionActive)
        {
            return;
        }

        Voice v = StartVoice(cue, 0f, true);
        if (v != null)
        {
            stingHoldUntil = Mathf.Max(stingHoldUntil, Time.unscaledTime + v.clip.length);
        }
    }

    // ---------------------------------------------------------------- scene transitions

    private void HandleTransitionStarted()
    {
        if (anyError)
        {
            return;
        }

        transitionActive = true;
        stingHoldUntil = 0f;

        dipped = current;
        current = null;
        foreach (Voice v in voices)
        {
            Fade(v, 0f, settings.sceneFadeOut);
        }
    }

    private void HandleSceneRevealed()
    {
        if (anyError || !transitionActive)
        {
            return;
        }

        transitionActive = false;

        bool hasOpinion = TryGetWanted(out MusicCueSO wanted);
        Voice resume = dipped;
        dipped = null;

        if (resume != null && IsAlive(resume) && (!hasOpinion || resume.cue == wanted))
        {
            current = resume;
            current.rotated = false;
            Fade(current, 1f, settings.sceneFadeIn);
            return;
        }
        if (resume != null)
        {
            FreeVoice(resume);
            voices.Remove(resume);
        }

        Refresh(settings.sceneFadeIn);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (anyError || mode != LoadSceneMode.Single)
        {
            return;
        }

        phase = Phase.None;
        captainsAlive = 0;
        bossFight = false;
        silenced = false;
        terminalCue = null;
        pauseTarget = 1f;

        Unsubscribe();

        // Deferred to Update: on newly loaded scenes, singletons set their Instance in Awake/Start.
        pendingSceneRefresh = true;
    }

    // ---------------------------------------------------------------- combat hooks

    private void Subscribe()
    {
        Unsubscribe();

        gameLoop = GameLoopManager.Instance;
        if (gameLoop != null)
        {
            gameLoop.OnWaveStarted += HandleWaveStarted;
            gameLoop.OnWaveResolved += HandleWaveResolved;
            gameLoop.OnCaptainSpawned += HandleCaptainSpawned;
        }

        objectives = SurvivorsObjectiveManager.Instance;
        if (objectives != null)
        {
            objectives.OnPhaseChanged += HandleObjectivePhaseChanged;
            objectives.OnObjectiveCompleted += HandleObjectiveCompleted;
        }

        pauseMenu = PauseMenuController.Instance;
        if (pauseMenu != null)
        {
            pauseMenu.OnPauseChanged += HandlePauseChanged;
        }

        playerHealth = Player.Instance != null ? Player.Instance.Health : null;
        if (playerHealth != null)
        {
            playerHealth.OnDied += HandlePlayerDied;
        }
    }

    private void Unsubscribe()
    {
        if (gameLoop != null)
        {
            gameLoop.OnWaveStarted -= HandleWaveStarted;
            gameLoop.OnWaveResolved -= HandleWaveResolved;
            gameLoop.OnCaptainSpawned -= HandleCaptainSpawned;
        }
        gameLoop = null;

        if (objectives != null)
        {
            objectives.OnPhaseChanged -= HandleObjectivePhaseChanged;
            objectives.OnObjectiveCompleted -= HandleObjectiveCompleted;
        }
        objectives = null;

        if (pauseMenu != null)
        {
            pauseMenu.OnPauseChanged -= HandlePauseChanged;
        }
        pauseMenu = null;

        if (playerHealth != null)
        {
            playerHealth.OnDied -= HandlePlayerDied;
        }
        playerHealth = null;

        foreach (Health captain in captainHealths)
        {
            if (captain != null)
            {
                captain.OnDied -= HandleCaptainDied;
            }
        }
        captainHealths.Clear();
    }

    private void HandleWaveStarted(int wave)
    {
        phase = Phase.Battle;
        Refresh(settings.toBattle);
    }

    private void HandleWaveResolved(int wave, bool success, WaveCard card) => EnterPrep();

    private void HandleObjectivePhaseChanged(SurvivorsObjectivePhase objectivePhase)
    {
        if (objectivePhase == SurvivorsObjectivePhase.Intermission)
        {
            EnterPrep();
        }
    }

    private void EnterPrep()
    {
        phase = Phase.Prep;
        captainsAlive = 0;
        Refresh(settings.toPrep);
    }

    private void HandleCaptainSpawned(Health captain)
    {
        if (captain == null)
        {
            return;
        }

        captainsAlive++;
        captainHealths.Add(captain);
        captain.OnDied += HandleCaptainDied;
        Refresh(settings.toCaptain);
    }

    private void HandleCaptainDied()
    {
        captainsAlive = Mathf.Max(0, captainsAlive - 1);
        Refresh(settings.toBattle);
    }

    private void HandleObjectiveCompleted(ISurvivorsObjective objective)
    {
        // The post-wave "kill the stragglers" cleanup is not an achievement worth a fanfare.
        if (objectives != null && objectives.Phase != SurvivorsObjectivePhase.Active)
        {
            return;
        }
        if (objective is KillRemainingEnemiesObjective)
        {
            return;
        }
        PlaySting(settings.objectiveSting);
    }

    private void HandlePauseChanged(bool paused)
    {
        pauseTarget = paused ? settings.pauseDuckTo : 1f;
    }

    private void HandlePlayerDied()
    {
        // The tutorial arena reloads the room on death; the reload brings the music back.
        if (TutorialDirector.Instance != null && TutorialDirector.Instance.ReloadOnDeath)
        {
            return;
        }
        Silence();
    }

    private void HandleGateDestroyed(Gate gate) => Silence();

    private void Silence()
    {
        if (terminalCue != null)
        {
            return;
        }
        silenced = true;
        Refresh(settings.defeatFadeOut);
    }

    private void HandleRunOver(bool isVictory)
    {
        if (anyError)
        {
            return;
        }

        if (isVictory)
        {
            bool endsCampaign = CampaignManager.HasInstance && CampaignManager.Instance.CurrentNodeEndsCampaign;
            PlaySting(endsCampaign ? settings.campaignCompleteSting : settings.victorySting);
            silenced = false;
            terminalCue = settings.victoryBed;
            Refresh(settings.toPrep);
        }
        else
        {
            PlaySting(settings.defeatSting);
            silenced = true;
            Refresh(settings.defeatFadeOut);
        }
    }

    private void SetBossFightInternal(bool active)
    {
        if (anyError || bossFight == active)
        {
            return;
        }

        bossFight = active;
        Refresh(active ? settings.toBoss : settings.toBattle);
    }
}
