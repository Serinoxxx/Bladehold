using System;
using MoreMountains.Feedbacks;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using UnityEngine;

/// <summary>
///     The Summon Mount ability's input and HUD face. The summon itself (cast, the horse, ride duration,
///     cooldown) belongs to <see cref="PlayerMount" /> and its equipped <see cref="MountDefinitionSO" />;
///     this component turns the SummonMount press into <see cref="PlayerMount.TryStartMountCast" /> (a
///     second press mid-cast cancels) and re-raises PlayerMount's timers as the events the HUD
///     (<see cref="SummonCastBarUI" />, <see cref="SummonMountUI" />) listens to.
///     Keyboard X is bound to both SummonMount and Dismount: PlayerMount only dismounts on Dismount, and
///     this ignores the press while riding, so one press never both dismounts and re-summons.
/// </summary>
public class PlayerSummonMount : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PlayerMount playerMount;
    [SerializeField] private Player player;
    [SerializeField] private InputReader inputReader;

    [Header("Feedback")]
    [SerializeField] private MMF_Player spawnFeedback;
    [SerializeField] private MMF_Player despawnFeedback;
    [Tooltip("Played when a summon press is refused (on cooldown, blocked by the scene).")]
    [SerializeField] private MMF_Player errorFeedback;

    // UI Events
    public event Action<float, float> OnDurationUpdated; // current, max
    public event Action<float, float> OnCooldownUpdated; // current, max
    public event Action OnAbilityReady;
    public event Action OnAbilityTriggered;
    public event Action<float> OnCastStarted; // max cast time
    public event Action<float, float> OnCastUpdated; // current, max
    public event Action OnCastFinished;
    public event Action OnCastCancelled;

    private float castDuration;
    private int lastDismountFrame = -1;
    private bool anyError;

    private void OnValidate()
    {
        if (playerMount == null) playerMount = GetComponent<PlayerMount>();
        if (player == null) player = GetComponent<Player>();
        if (inputReader == null) inputReader = GetComponentInChildren<InputReader>(true);
    }

    private void Start()
    {
        if (playerMount == null) playerMount = GetComponent<PlayerMount>();
        if (player == null) player = GetComponent<Player>();
        if (inputReader == null) inputReader = GetComponentInChildren<InputReader>(true);

        if (playerMount == null || player == null || inputReader == null)
        {
            Debug.LogError($"PlayerSummonMount: Missing core dependencies. PlayerMount: {playerMount != null}, Player: {player != null}, InputReader: {inputReader != null}");
            anyError = true;
        }

        if (anyError) return;

        // Unlocked from the start of every run (1); kept as a stat so something could still lock it.
        player.Stats.SetBase(StatType.SummonMountUnlocked, 1f);

        inputReader.onSummonMountPerformed += HandleSummonAction;
        playerMount.OnMountCastStarted += HandleCastStarted;
        playerMount.OnMountCastCancelled += HandleCastCancelled;
        playerMount.OnMountCastCompleted += HandleCastCompleted;
        playerMount.OnMountDurationChanged += HandleDurationChanged;
        playerMount.OnMountCooldownChanged += HandleCooldownChanged;
        playerMount.OnMountedChanged += HandleMountedChanged;
    }

    private void OnDestroy()
    {
        if (inputReader != null) inputReader.onSummonMountPerformed -= HandleSummonAction;
        if (playerMount != null)
        {
            playerMount.OnMountCastStarted -= HandleCastStarted;
            playerMount.OnMountCastCancelled -= HandleCastCancelled;
            playerMount.OnMountCastCompleted -= HandleCastCompleted;
            playerMount.OnMountDurationChanged -= HandleDurationChanged;
            playerMount.OnMountCooldownChanged -= HandleCooldownChanged;
            playerMount.OnMountedChanged -= HandleMountedChanged;
        }
    }

    private void Update()
    {
        if (anyError || !playerMount.IsCastingMount) return;
        OnCastUpdated?.Invoke(playerMount.CastProgress * castDuration, castDuration);
    }

    private void HandleSummonAction()
    {
        if (anyError || player.Health.IsDead) return;

        // Riding: the same X press is the Dismount action, handled by PlayerMount. The Dismount handler
        // may run first and have just put us on the ground, so ignore the press that frame too.
        if (playerMount.IsMounted || Time.frameCount == lastDismountFrame) return;

        if (playerMount.IsCastingMount)
        {
            playerMount.CancelMountCast();
            return;
        }

        if (!IsAbilityUnlocked || !playerMount.TryStartMountCast())
        {
            if (errorFeedback != null) errorFeedback.PlayFeedbacks();
        }
    }

    private void HandleCastStarted(float duration)
    {
        castDuration = duration;
        Animator anim = player.GetComponentInChildren<Animator>();
        if (anim != null) anim.SetTrigger("Cheer"); // Placeholder for casting
        OnCastStarted?.Invoke(duration);
    }

    private void HandleCastCancelled()
    {
        if (errorFeedback != null) errorFeedback.PlayFeedbacks();
        OnCastCancelled?.Invoke();
    }

    private void HandleCastCompleted()
    {
        OnCastFinished?.Invoke();
        if (spawnFeedback != null)
        {
            spawnFeedback.transform.position = transform.position;
            spawnFeedback.PlayFeedbacks();
        }
        OnAbilityTriggered?.Invoke();
        OnDurationUpdated?.Invoke(playerMount.MountRemainingDuration, playerMount.MaxMountDuration);
    }

    private void HandleDurationChanged(float current, float max) => OnDurationUpdated?.Invoke(current, max);

    private void HandleCooldownChanged(float current, float max)
    {
        OnCooldownUpdated?.Invoke(current, max);
        if (current <= 0f) OnAbilityReady?.Invoke();
    }

    private void HandleMountedChanged(bool mounted)
    {
        if (mounted) return;
        lastDismountFrame = Time.frameCount;
        if (despawnFeedback != null)
        {
            despawnFeedback.transform.position = transform.position;
            despawnFeedback.PlayFeedbacks();
        }
        OnCooldownUpdated?.Invoke(playerMount.MountRemainingCooldown, playerMount.MaxMountCooldown);
    }

    /// <summary>True when the mount can be summoned here: unlocked and allowed by the scene's <see cref="SceneAbilityRules" />. SummonMountUI hides the slot when false.</summary>
    public bool IsAbilityUnlocked => SceneAbilityRules.MountAllowed && player != null && player.Stats != null && player.Stats.GetValue(StatType.SummonMountUnlocked) > 0f;
    public bool IsHorseActive => playerMount != null && playerMount.IsMounted;
    /// <summary>True once the run's warhorse has died (summoning locked until a replacement is bought).</summary>
    public bool IsMountLost => playerMount != null && playerMount.IsMountLost;
    public bool IsCooldownActive => playerMount != null && !playerMount.IsMounted && playerMount.MountRemainingCooldown > 0f;
    public float RemainingDuration => playerMount != null ? playerMount.MountRemainingDuration : 0f;
    public float MaxDuration => playerMount != null ? playerMount.MaxMountDuration : 0f;
    public float RemainingCooldown => playerMount != null ? playerMount.MountRemainingCooldown : 0f;
    public float MaxCooldown => playerMount != null ? playerMount.MaxMountCooldown : 0f;
}
