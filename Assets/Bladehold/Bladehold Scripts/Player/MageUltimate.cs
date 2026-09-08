using System;
using UnityEngine;

public class MageUltimate : MonoBehaviour, IUltimateHandler
{
    [Header("Config")]
    [Tooltip("Configuration ScriptableObject defining base duration and metadata.")]
    [SerializeField] private UltimateConfigSO config;

    public float BaseDuration => config != null && config.baseDuration > 0f ? config.baseDuration : 6f;

    private Player player;
    private PlayerWand wand;
    private CharacterController characterController;
    private MageImbuement imbuement;
    private Animator animator;

    private float ultimateEndTime;
    private PlayerUltimateController controller;
    private bool isRunning;

    private Vector3 startPosition;
    private float hoverHeight = 10f;

    [Tooltip("Radius of the slam explosion.")]
    [SerializeField] private float slamRadius = 5f;
    [Tooltip("Base damage of the slam explosion.")]
    [SerializeField] private float slamDamage = 100f;
    [Tooltip("Knockback force of the slam explosion.")]
    [SerializeField] private float slamKnockback = 30f;

    private void Awake()
    {
        FindDependencies();
    }

    private void Start()
    {
        FindDependencies();
    }

    private void FindDependencies()
    {
        Transform rootTr = transform.root;
        if (player == null) player = rootTr.GetComponentInChildren<Player>(true) ?? GetComponentInParent<Player>() ?? GetComponentInChildren<Player>(true) ?? Player.Instance;
        if (wand == null) wand = rootTr.GetComponentInChildren<PlayerWand>(true) ?? GetComponentInParent<PlayerWand>() ?? GetComponentInChildren<PlayerWand>(true);
        if (characterController == null) characterController = rootTr.GetComponentInChildren<CharacterController>(true) ?? GetComponentInParent<CharacterController>() ?? GetComponent<CharacterController>();
        if (imbuement == null) imbuement = rootTr.GetComponentInChildren<MageImbuement>(true) ?? GetComponentInParent<MageImbuement>() ?? GetComponent<MageImbuement>();
        if (animator == null && player != null) animator = player.GetComponentInChildren<Animator>();
    }

    public void Activate(PlayerUltimateController controller)
    {
        this.controller = controller;
        FindDependencies();
        float duration = player != null && player.Stats != null ? player.Stats.GetValue(StatType.UltimateDurationSeconds) : BaseDuration;
        if (duration <= 0f) duration = BaseDuration;
        ultimateEndTime = Time.time + duration;
        isRunning = true;

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        startPosition = transform.position;
        transform.position = startPosition + Vector3.up * hoverHeight;

        if (imbuement != null)
        {
            // Force fire imbuement during ultimate
            imbuement.CollectNode(ElementType.Fire);
        }

        if (wand != null)
        {
            wand.IsUltimateLocked = true;
            wand.ForceStartAim();
        }

        if (animator != null)
        {
            animator.CrossFade("UltimateHover", 0.1f);
        }
    }

    private void Update()
    {
        if (!isRunning) return;

        if (Time.time >= ultimateEndTime)
        {
            End();
        }
    }

    private void End()
    {
        isRunning = false;

        // Slam down
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, hoverHeight + 5f, ~0, QueryTriggerInteraction.Ignore))
        {
            transform.position = hit.point;
        }
        else
        {
            transform.position = startPosition;
        }

        if (characterController != null)
        {
            characterController.enabled = true;
        }

        if (wand != null)
        {
            wand.IsUltimateLocked = false;
            wand.ForceEndAim();
        }

        if (animator != null)
        {
            animator.CrossFade("UltimateSlam", 0.1f);
        }

        SlamExplosion();

        controller?.EndUltimate();
    }

    private void SlamExplosion()
    {
        float radius = player.Stats.GetValue(StatType.UltimateMageLandingExplosionRadius);
        if (radius <= 0f) radius = slamRadius;

        Vector3 center = transform.position;
        Collider[] overlapBuffer = new Collider[32];
        int count = Physics.OverlapSphereNonAlloc(center, radius, overlapBuffer, ~0, QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            IDamageable damageable = PlayerBow.ResolveDamageable(overlapBuffer[i]);
            if (damageable == null || damageable == player.Damageable) continue;

            Damage damage = new Damage
            {
                value = slamDamage,
                type = DamageType.elemental,
                sourcePosition = center,
                knockbackForce = slamKnockback,
                source = player.Damageable,
                isPlayerDamage = true,
            };

            damageable.ReceiveDamage(damage);
        }
    }
}
