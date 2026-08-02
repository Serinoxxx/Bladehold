using System;
using MoreMountains.Feedbacks;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerSummonMount : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PlayerMount playerMount;
    [SerializeField] private Player player;
    [SerializeField] private InputReader inputReader;

    [Header("Settings")]
    [SerializeField] private HorseMotor horsePrefab;
    [SerializeField] private MMF_Player spawnFeedback;
    [SerializeField] private MMF_Player despawnFeedback;
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

    private HorseMotor spawnedHorse;
    private float remainingDuration;
    private float maxDuration;
    private float remainingCooldown;
    private float maxCooldown;
    private bool isCooldownActive;

    private bool isCasting;
    private float castTimer;
    private float maxCastTime = 2f;
    private Vector2 lastMoveInput;

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

        player.Stats.SetBase(StatType.SummonMountUnlocked, 0f);
        player.Stats.SetBase(StatType.SummonMountDuration, 20f);
        player.Stats.SetBase(StatType.SummonMountCooldown, 45f);

        inputReader.onDismountPerformed += HandleDismountAction;
        player.Health.OnDamaged += HandleDamaged;
    }

    private void OnDestroy()
    {
        if (inputReader != null)
        {
            inputReader.onDismountPerformed -= HandleDismountAction;
        }
        if (player != null && player.Health != null)
        {
            player.Health.OnDamaged -= HandleDamaged;
        }
    }

    private void HandleDamaged(Damage damage)
    {
        if (isCasting)
        {
            CancelCast();
        }
    }

    private void HandleDismountAction()
    {
        if (anyError || player.Health.IsDead) return;

        if (playerMount.IsMounted) return;

        if (player.Stats.GetValue(StatType.SummonMountUnlocked) <= 0f) return;

        if (spawnedHorse != null)
        {
            if (errorFeedback != null) errorFeedback.PlayFeedbacks();
            return;
        }

        if (isCasting)
        {
            return;
        }

        StartCast();
    }

    private void StartCast()
    {
        isCasting = true;
        castTimer = 0f;
        lastMoveInput = inputReader._moveComposite;
        
        Animator anim = player.GetComponentInChildren<Animator>();
        if (anim != null) anim.SetTrigger("Cheer"); // Placeholder for casting

        OnCastStarted?.Invoke(maxCastTime);
    }

    private void CancelCast()
    {
        isCasting = false;
        if (errorFeedback != null) errorFeedback.PlayFeedbacks();
        OnCastCancelled?.Invoke();
    }

    private void FinishCast()
    {
        isCasting = false;
        OnCastFinished?.Invoke();
        SummonHorse();
    }

    private void SummonHorse()
    {
        if (horsePrefab == null) return;

        spawnedHorse = Instantiate(horsePrefab, transform.position, transform.rotation);

        playerMount.TryMount(spawnedHorse);

        maxDuration = player.Stats.GetValue(StatType.SummonMountDuration);
        remainingDuration = maxDuration;
        isCooldownActive = false;
        remainingCooldown = 0f;

        if (spawnFeedback != null)
        {
            spawnFeedback.transform.position = transform.position;
            spawnFeedback.PlayFeedbacks();
        }

        OnAbilityTriggered?.Invoke();
    }

    private void DespawnHorse()
    {
        if (spawnedHorse == null) return;

        if (playerMount.CurrentHorse == spawnedHorse)
        {
            playerMount.Dismount();
        }

        if (despawnFeedback != null)
        {
            despawnFeedback.transform.position = spawnedHorse.transform.position;
            despawnFeedback.PlayFeedbacks();
        }

        Destroy(spawnedHorse.gameObject);
        spawnedHorse = null;

        maxCooldown = player.Stats.GetValue(StatType.SummonMountCooldown);
        remainingCooldown = maxCooldown;
        isCooldownActive = true;
    }

    private void Update()
    {
        if (anyError) return;

        if (isCasting)
        {
            if (inputReader._moveComposite != lastMoveInput && inputReader._moveComposite.magnitude > 0.1f)
            {
                CancelCast();
                return;
            }

            castTimer += Time.deltaTime;
            OnCastUpdated?.Invoke(castTimer, maxCastTime);

            if (castTimer >= maxCastTime)
            {
                FinishCast();
            }
            return;
        }

        if (spawnedHorse != null)
        {
            if (spawnedHorse.Health != null && spawnedHorse.Health.IsDead)
            {
                DespawnHorse();
            }
            else
            {
                remainingDuration -= Time.deltaTime;
                OnDurationUpdated?.Invoke(remainingDuration, maxDuration);

                if (remainingDuration <= 0f)
                {
                    DespawnHorse();
                }
            }
        }
        else if (isCooldownActive)
        {
            remainingCooldown -= Time.deltaTime;
            OnCooldownUpdated?.Invoke(remainingCooldown, maxCooldown);

            if (remainingCooldown <= 0f)
            {
                isCooldownActive = false;
                OnAbilityReady?.Invoke();
            }
        }
    }

    public bool IsAbilityUnlocked => player != null && player.Stats.GetValue(StatType.SummonMountUnlocked) > 0f;
    public bool IsHorseActive => spawnedHorse != null;
    public bool IsCooldownActive => isCooldownActive;
}
