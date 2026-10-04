using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Rolls this scene's biome world events (Eruption, Blizzard, Thunderstorm, Stampede, Blood Moon, Goblin
///     Caravan). Lives on the WorldEvents prefab dropped into a battle scene; the events it may pick are the
///     enabled <see cref="WorldEvent" /> children listed in <see cref="events" />. No director in a scene means
///     no events (the <see cref="SceneAbilityRules" /> "absent = default" convention), so the tutorial, hub
///     scenes and the Fishing Pond never get one.
///
///     At most one event per wave: when a wave starts it rolls <see cref="WorldEventScheduleSO.chancePerWave" />,
///     waits a random delay, then starts a weighted pick. The event ends on its own timer or when the wave
///     resolves, whichever comes first, so nothing carries into prep or the draft.
/// </summary>
public class WorldEventDirector : MonoBehaviour
{
    public static WorldEventDirector Instance { get; private set; }

    [SerializeField] private WorldEventScheduleSO schedule;
    [Tooltip("The events this scene may roll. Disable a child (or remove it here) to keep it out of this biome.")]
    [SerializeField] private List<WorldEvent> events = new List<WorldEvent>();

    private GameLoopManager gameLoop;
    private Health playerHealth;
    private WorldEvent lastEvent;
    private float pendingDelay = -1f;
    private int pendingWave;
    private bool anyError;

    /// <summary>The event currently running, or null.</summary>
    public WorldEvent ActiveEvent { get; private set; }

    public IReadOnlyList<WorldEvent> Events => events;

    /// <summary>Raised when an event starts (the banner listens).</summary>
    public event Action<WorldEvent> OnEventStarted;

    /// <summary>Raised when the running event ends for any reason.</summary>
    public event Action<WorldEvent> OnEventEnded;

    private void OnValidate()
    {
        if (events == null || events.Count == 0)
        {
            events = new List<WorldEvent>(GetComponentsInChildren<WorldEvent>(true));
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (schedule == null)
        {
            Debug.LogError("[WorldEventDirector] schedule is not assigned.", this);
            anyError = true;
        }
        events.RemoveAll(e => e == null);
        if (events.Count == 0)
        {
            Debug.LogError("[WorldEventDirector] no events listed; this scene will never roll one.", this);
            anyError = true;
        }

        gameLoop = GameLoopManager.Instance;
        if (gameLoop == null)
        {
            // Hub or test scene without a wave loop: events can still be forced from the DevConsole.
            Debug.LogWarning("[WorldEventDirector] no GameLoopManager in this scene; events only start from the DevConsole.", this);
        }
        else
        {
            gameLoop.OnWaveStarted += HandleWaveStarted;
            gameLoop.OnWaveResolved += HandleWaveResolved;
            gameLoop.OnVictory += HandleVictory;
        }

        foreach (WorldEvent e in events)
        {
            e.Ended += HandleEventEnded;
        }

        // A dead player ends the event: no meteors on the corpse, no storm over the death screen.
        playerHealth = Player.Instance != null ? Player.Instance.Health : null;
        if (playerHealth != null) playerHealth.OnDied += StopActive;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (gameLoop != null)
        {
            gameLoop.OnWaveStarted -= HandleWaveStarted;
            gameLoop.OnWaveResolved -= HandleWaveResolved;
            gameLoop.OnVictory -= HandleVictory;
        }
        foreach (WorldEvent e in events)
        {
            if (e != null) e.Ended -= HandleEventEnded;
        }
        if (playerHealth != null) playerHealth.OnDied -= StopActive;
    }

    private void HandleWaveStarted(int wave)
    {
        pendingDelay = -1f;
        if (anyError || ActiveEvent != null) return;
        if (wave < schedule.firstEligibleWave) return;
        if (UnityEngine.Random.value > schedule.chancePerWave) return;
        pendingWave = wave;
        pendingDelay = UnityEngine.Random.Range(schedule.startDelayRange.x, schedule.startDelayRange.y);
    }

    private void HandleWaveResolved(int wave, bool success, WaveCard card) => StopActive();

    private void HandleVictory() => StopActive();

    private void Update()
    {
        if (anyError || pendingDelay < 0f) return;
        if (gameLoop != null && !gameLoop.IsWaveActive)
        {
            pendingDelay = -1f;
            return;
        }

        pendingDelay -= Time.deltaTime;
        if (pendingDelay > 0f) return;
        pendingDelay = -1f;

        WorldEvent pick = Pick(pendingWave);
        if (pick != null) StartEvent(pick);
    }

    private WorldEvent Pick(int wave)
    {
        float total = 0f;
        List<WorldEvent> candidates = new List<WorldEvent>();
        foreach (WorldEvent e in events)
        {
            if (!IsEligible(e, wave)) continue;
            candidates.Add(e);
        }
        if (schedule.avoidRepeat && candidates.Count > 1) candidates.Remove(lastEvent);
        foreach (WorldEvent e in candidates) total += e.Config.weight;
        if (candidates.Count == 0 || total <= 0f) return null;

        float roll = UnityEngine.Random.value * total;
        foreach (WorldEvent e in candidates)
        {
            roll -= e.Config.weight;
            if (roll <= 0f) return e;
        }
        return candidates[candidates.Count - 1];
    }

    private static bool IsEligible(WorldEvent e, int wave) =>
        e != null && e.isActiveAndEnabled && e.IsValid && e.Config != null && e.Config.weight > 0f && wave >= e.Config.minWave;

    /// <summary>Starts <paramref name="worldEvent" /> now, ending any running one first. DevConsole uses this too.</summary>
    public void StartEvent(WorldEvent worldEvent)
    {
        if (worldEvent == null || !worldEvent.IsValid) return;
        StopActive();
        pendingDelay = -1f;
        ActiveEvent = worldEvent;
        lastEvent = worldEvent;
        worldEvent.Begin();
        if (!worldEvent.IsRunning)
        {
            ActiveEvent = null;
            return;
        }
        OnEventStarted?.Invoke(worldEvent);
    }

    /// <summary>Ends the running event, if any.</summary>
    public void StopActive()
    {
        pendingDelay = -1f;
        if (ActiveEvent != null) ActiveEvent.Stop();
    }

    private void HandleEventEnded(WorldEvent worldEvent)
    {
        if (worldEvent != ActiveEvent) return;
        ActiveEvent = null;
        OnEventEnded?.Invoke(worldEvent);
    }
}
