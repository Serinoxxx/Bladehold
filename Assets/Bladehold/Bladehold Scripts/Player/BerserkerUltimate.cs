using System;
using UnityEngine;

/// <summary>
///     The Berserker's Whirlwind Ultimate:
///     - Unleashes a continuous spinning whirlwind attack using the equipped weapon (2H Axe).
///     - Weapon remains active for the full duration, dealing fully-charged melee damage and knockback.
///     - Drives an override animation layer via start and stop triggers.
///     - Spawns the whirlwind particle VFX (same as the Assassin enemy variant).
///     - Retains damage reduction from the Thick Skin skill tree node.
/// </summary>
public class BerserkerUltimate : MonoBehaviour, IUltimateHandler
{
    [Header("Animation")]
    [Tooltip("Animator trigger sent to start the spinning whirlwind animation on the override layer.")]
    [SerializeField] private string startTrigger = "StartWhirlwind";
    [Tooltip("Animator trigger sent to stop the spinning whirlwind animation.")]
    [SerializeField] private string stopTrigger = "StopWhirlwind";

    [Header("Visual Effects")]
    [Tooltip("Whirlwind particle VFX spawned during the ultimate (Assassin enemy variant swirl effect).")]
    [SerializeField] private GameObject whirlwindVfxPrefab;
    [Tooltip("Offset relative to the player where the whirlwind VFX is anchored.")]
    [SerializeField] private Vector3 vfxOffset = new Vector3(0f, 0.2f, 0f);

    [Header("Whirlwind Mechanics")]
    [Tooltip("Minimum cooldown between hits on the same enemy while spinning.")]
    [SerializeField] private float hitInterval = 0.3f;

    [Header("Spin Rotation")]
    [Tooltip("If true, programmatically rotates the character model while the whirlwind is active (ideal for static poses).")]
    [SerializeField] private bool rotateCharacter = true;
    [Tooltip("Spin speed in degrees per second.")]
    [SerializeField] private float spinDegreesPerSecond = 1080f;
    [Tooltip("Transform to spin. If null, defaults to the rig root bone under the player.")]
    [SerializeField] private Transform spinTransform;

    private Player player;
    private PlayerClassController classController;
    private Animator animator;
    private DamageTrigger activeTrigger;
    private GameObject activeWhirlwindVfx;
    private PlayerUltimateController controller;

    private float ultimateEndTime;
    private bool isRunning;
    private bool anyError = false;
    private float currentSpinAngle;
    private Quaternion originalSpinLocalRotation = Quaternion.identity;

    private int startTriggerHash;
    private int stopTriggerHash;

    private void Awake()
    {
        player = GetComponentInChildren<Player>();
        classController = GetComponentInParent<PlayerClassController>() ?? GetComponentInChildren<PlayerClassController>();
    }

    private void Start()
    {
        if (player == null)
        {
            player = GetComponentInChildren<Player>();
        }

        if (player == null)
        {
            Debug.LogError("[BerserkerUltimate] Player component not found in children.", this);
            anyError = true;
        }
        else if (player.Stats == null)
        {
            Debug.LogError("[BerserkerUltimate] PlayerStats component missing on Player.", this);
            anyError = true;
        }

        if (classController == null)
        {
            classController = GetComponentInParent<PlayerClassController>() ?? GetComponentInChildren<PlayerClassController>();
        }

        if (player != null)
        {
            animator = player.GetComponentInChildren<Animator>();
        }

        if (!string.IsNullOrEmpty(startTrigger))
        {
            startTriggerHash = Animator.StringToHash(startTrigger);
        }
        if (!string.IsNullOrEmpty(stopTrigger))
        {
            stopTriggerHash = Animator.StringToHash(stopTrigger);
        }

        if (spinTransform == null && player != null)
        {
            spinTransform = player.transform.Find("root");
            if (spinTransform == null && animator != null)
            {
                spinTransform = animator.transform.Find("root");
            }
        }
    }

    public void Activate(PlayerUltimateController controller)
    {
        if (anyError || player == null || player.Stats == null)
        {
            controller?.EndUltimate();
            return;
        }

        this.controller = controller;
        float duration = player.Stats.GetValue(StatType.UltimateDurationSeconds);
        if (duration <= 0f) duration = 6f;

        ultimateEndTime = Time.time + duration;
        isRunning = true;
        currentSpinAngle = 0f;

        if (spinTransform != null)
        {
            originalSpinLocalRotation = spinTransform.localRotation;
        }

        // Damage reduction hook (Thick Skin skill node)
        if (player.Health != null)
        {
            player.Health.ScaleDamageTaken += HandleDamageReduction;
            player.Health.OnDied += HandlePlayerDied;
        }

        // Start spinning animation via override layer trigger
        if (animator == null && player != null)
        {
            animator = player.GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            if (stopTriggerHash != 0) animator.ResetTrigger(stopTriggerHash);
            if (startTriggerHash != 0) animator.SetTrigger(startTriggerHash);
        }

        // Spawn whirlwind VFX
        if (whirlwindVfxPrefab != null)
        {
            activeWhirlwindVfx = Instantiate(whirlwindVfxPrefab, player.transform.position + vfxOffset, Quaternion.identity, player.transform);
        }

        // Activate equipped melee weapon in whirlwind mode
        activeTrigger = classController != null ? classController.ActiveMeleeTrigger : null;
        if (activeTrigger == null && player != null)
        {
            activeTrigger = player.GetComponentInChildren<DamageTrigger>();
        }

        if (activeTrigger != null)
        {
            activeTrigger.StartWhirlwind(hitInterval);
        }
        else
        {
            Debug.LogWarning("[BerserkerUltimate] No active melee DamageTrigger found to activate for whirlwind.", this);
        }
    }

    private void Update()
    {
        if (anyError || !isRunning) return;

        if (Time.time >= ultimateEndTime)
        {
            End();
            return;
        }

        if (rotateCharacter && spinTransform != null)
        {
            currentSpinAngle = (currentSpinAngle + spinDegreesPerSecond * Time.deltaTime) % 360f;
            spinTransform.localRotation = Quaternion.Euler(0f, currentSpinAngle, 0f);
        }
    }

    private void LateUpdate()
    {
        if (anyError || !isRunning) return;

        // Maintain spin rotation after animator passes
        if (rotateCharacter && spinTransform != null)
        {
            spinTransform.localRotation = Quaternion.Euler(0f, currentSpinAngle, 0f);
        }
    }

    private float HandleDamageReduction(Damage damage)
    {
        if (player == null || player.Stats == null) return 1f;
        float reduction = player.Stats.GetValue(StatType.UltimateBerserkerDamageReduction);
        return Mathf.Clamp01(1f - reduction);
    }

    private void HandlePlayerDied()
    {
        End();
    }

    private void End()
    {
        if (!isRunning) return;
        isRunning = false;

        if (player != null && player.Health != null)
        {
            player.Health.ScaleDamageTaken -= HandleDamageReduction;
            player.Health.OnDied -= HandlePlayerDied;
        }

        // Stop weapon whirlwind
        if (activeTrigger != null)
        {
            activeTrigger.StopWhirlwind();
            activeTrigger = null;
        }

        // Stop spinning animation
        if (animator != null)
        {
            if (startTriggerHash != 0) animator.ResetTrigger(startTriggerHash);
            if (stopTriggerHash != 0) animator.SetTrigger(stopTriggerHash);
        }

        // Clean up whirlwind VFX
        if (activeWhirlwindVfx != null)
        {
            Destroy(activeWhirlwindVfx);
            activeWhirlwindVfx = null;
        }

        // Restore model rotation
        if (spinTransform != null)
        {
            spinTransform.localRotation = originalSpinLocalRotation;
        }

        controller?.EndUltimate();
    }

    private void OnDisable()
    {
        if (isRunning)
        {
            End();
        }
    }

    private void OnDestroy()
    {
        if (isRunning)
        {
            End();
        }
    }
}
