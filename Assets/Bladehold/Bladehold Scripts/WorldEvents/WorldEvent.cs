using System;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
///     One biome world event living on the scene's WorldEvents prefab (a child per event). The base class owns
///     everything every event shares: the run window (<see cref="WorldEventSO.duration" />), the post-processing
///     <see cref="Volume" /> faded in and out, the weather particles that follow the player, the player stat buff
///     (added on start, removed exactly on end), and the start/ambience/end <see cref="MMF_Player" />s.
///     Subclasses only spawn their hazards in <see cref="TickHazards" /> and clean up in <see cref="OnEventEnded" />.
///     Started and stopped by <see cref="WorldEventDirector" />; runs on scaled time so pauses freeze it.
/// </summary>
public abstract class WorldEvent : MonoBehaviour
{
    [Header("Presentation")]
    [Tooltip("Global Volume (high priority, weight 0 at rest) carrying this event's colour grade. Faded in/out.")]
    [SerializeField] private Volume postVolume;
    [Tooltip("Optional: weather particles (snow, rain, ash) shown while active. Kept centred on the player.")]
    [SerializeField] private GameObject weatherRoot;
    [Tooltip("Offset from the player for the weather root (rain/snow emitters sit above the camera).")]
    [SerializeField] private Vector3 weatherOffset = new Vector3(0f, 0f, 0f);

    [Header("Feedback")]
    [Tooltip("Played when the event starts: the sting, a screen flash and a rumble.")]
    [SerializeField] private MMF_Player startFeedback;
    [Tooltip("Optional: looping ambience (wind howl, rain, rumble). Started on begin, stopped on end.")]
    [SerializeField] private MMF_Player ambienceFeedback;
    [Tooltip("Optional: played when the event ends (wind dies down). Leave empty for silence.")]
    [SerializeField] private MMF_Player endFeedback;

    private ParticleSystem[] weatherSystems = Array.Empty<ParticleSystem>();
    private float elapsed;
    private float fadeOutElapsed = -1f;
    private float appliedBuff;
    private bool hazardsStarted;

    /// <summary>Raised once when the event finishes (timer, wave end or forced stop).</summary>
    public event Action<WorldEvent> Ended;

    public abstract WorldEventSO Config { get; }

    public bool IsRunning { get; private set; }
    public float Remaining => Config != null ? Mathf.Max(0f, Config.duration - elapsed) : 0f;
    public float RemainingNormalized => Config != null && Config.duration > 0f ? Remaining / Config.duration : 0f;

    /// <summary>
    ///     Optional one-liner shown on the timer chip when the event ends ("Caravan looted!"). Null = the chip just
    ///     fades. Read right after <see cref="Ended" />.
    /// </summary>
    public virtual string OutcomeText => null;

    /// <summary>Whether <see cref="OutcomeText" /> is good news (gold) or bad (red).</summary>
    public virtual bool OutcomeIsGood => true;

    /// <summary>False when required refs are missing; the director skips such events.</summary>
    public bool IsValid { get; private set; } = true;

    protected virtual void Awake()
    {
        if (weatherRoot != null)
        {
            weatherSystems = weatherRoot.GetComponentsInChildren<ParticleSystem>(true);
            weatherRoot.SetActive(false);
        }
        if (postVolume != null) postVolume.weight = 0f;
    }

    protected virtual void Start()
    {
        if (Config == null)
        {
            Debug.LogError($"[WorldEvent] {name} has no config asset.", this);
            IsValid = false;
        }
        if (postVolume == null)
        {
            Debug.LogError($"[WorldEvent] {name} has no post-processing Volume.", this);
            IsValid = false;
        }
        if (startFeedback == null) Debug.LogError($"[WorldEvent] {name}: startFeedback is not assigned.", this);
        ValidateEvent();
    }

    /// <summary>Subclasses null-check their own refs here and call <see cref="MarkInvalid" />.</summary>
    protected virtual void ValidateEvent() { }

    protected void MarkInvalid(string reason)
    {
        Debug.LogError($"[WorldEvent] {name}: {reason}", this);
        IsValid = false;
    }

    public void Begin()
    {
        if (IsRunning || !IsValid) return;
        IsRunning = true;
        elapsed = 0f;
        fadeOutElapsed = -1f;
        hazardsStarted = false;

        if (weatherRoot != null)
        {
            FollowPlayer();
            weatherRoot.SetActive(true);
            foreach (ParticleSystem ps in weatherSystems)
            {
                if (ps != null) ps.Play(true);
            }
        }

        ApplyBuff();
        if (startFeedback != null) startFeedback.PlayFeedbacks();
        if (ambienceFeedback != null) ambienceFeedback.PlayFeedbacks();
        OnEventBegan();
    }

    /// <summary>Ends the event now (the wave ended, or a DevConsole stop). Safe to call when not running.</summary>
    public void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;
        fadeOutElapsed = 0f;

        OnEventEnded();
        RemoveBuff();
        if (ambienceFeedback != null) ambienceFeedback.StopFeedbacks();
        if (endFeedback != null) endFeedback.PlayFeedbacks();
        foreach (ParticleSystem ps in weatherSystems)
        {
            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        Ended?.Invoke(this);
    }

    protected virtual void Update()
    {
        if (Config == null) return;
        float dt = Time.deltaTime;

        if (IsRunning)
        {
            elapsed += dt;
            FollowPlayer();
            if (postVolume != null) postVolume.weight = Mathf.Clamp01(elapsed / Config.fadeSeconds);

            if (elapsed >= Config.duration)
            {
                Stop();
                return;
            }

            if (elapsed >= Config.hazardGraceSeconds)
            {
                if (!hazardsStarted)
                {
                    hazardsStarted = true;
                    OnHazardsStarted();
                }
                TickHazards(dt);
            }
            return;
        }

        if (fadeOutElapsed >= 0f)
        {
            fadeOutElapsed += dt;
            FollowPlayer();
            float t = Mathf.Clamp01(fadeOutElapsed / Config.fadeSeconds);
            if (postVolume != null) postVolume.weight = 1f - t;
            // Let the last flakes/drops land before switching the weather off.
            if (t >= 1f && fadeOutElapsed >= Config.fadeSeconds + 2f)
            {
                fadeOutElapsed = -1f;
                if (weatherRoot != null) weatherRoot.SetActive(false);
            }
        }
    }

    private void FollowPlayer()
    {
        if (weatherRoot == null || Player.Instance == null) return;
        weatherRoot.transform.position = Player.Instance.transform.position + weatherOffset;
    }

    private void ApplyBuff()
    {
        appliedBuff = 0f;
        if (!Config.hasBuff || Player.Instance == null || Player.Instance.Stats == null) return;
        Player.Instance.Stats.AddModifier(Config.buffStat, Config.buffKind, Config.buffAmount);
        appliedBuff = Config.buffAmount;
    }

    private void RemoveBuff()
    {
        if (Mathf.Approximately(appliedBuff, 0f)) return;
        // Remove exactly what was added; modifiers carry no source id (the RageBuff pattern).
        if (Player.Instance != null && Player.Instance.Stats != null)
        {
            Player.Instance.Stats.RemoveModifier(Config.buffStat, Config.buffKind, appliedBuff);
        }
        appliedBuff = 0f;
    }

    protected virtual void OnDestroy()
    {
        RemoveBuff();
    }

    /// <summary>The event just started (banner is up). Hazards wait for <see cref="WorldEventSO.hazardGraceSeconds" />.</summary>
    protected virtual void OnEventBegan() { }

    /// <summary>The grace period is over; the first hazard tick follows this frame.</summary>
    protected virtual void OnHazardsStarted() { }

    /// <summary>Called every frame while active, after the grace period.</summary>
    protected abstract void TickHazards(float deltaTime);

    /// <summary>Clean up anything still live (pending strikes, the caravan). Already-landed effects may finish.</summary>
    protected virtual void OnEventEnded() { }
}
