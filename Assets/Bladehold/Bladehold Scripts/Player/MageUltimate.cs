using System;
using UnityEngine;

public class MageUltimate : MonoBehaviour, IUltimateHandler
{
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
        player = GetComponentInChildren<Player>();
        wand = GetComponentInChildren<PlayerWand>();
        characterController = GetComponent<CharacterController>();
        imbuement = GetComponent<MageImbuement>();
        animator = GetComponentInChildren<Animator>();
    }

    public void Activate(PlayerUltimateController controller)
    {
        this.controller = controller;
        float duration = player.Stats.GetValue(StatType.UltimateDurationSeconds);
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
                source = player.Damageable
            };

            damageable.ReceiveDamage(damage);
        }
    }
}
