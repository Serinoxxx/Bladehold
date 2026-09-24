using System;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Combat behavior for the Bulwark enemy.
///     When its shield blocks an incoming attack, it plays a block-hit reaction.
///     Normal melee attacks when in range of the player are handled via AIAttack.
///     Handles body stagger reactions when struck from behind or when shield is broken.
/// </summary>
public class BulwarkAttack : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;
    [SerializeField] private AIMovement movement;
    [SerializeField] private BulwarkAttackSO attackData;
    [SerializeField] private BulwarkShield shield;
    [SerializeField] private AIAttack aiAttack;

    [SerializeField] private string blockHitTrigger = "BlockHit";
    [SerializeField] private string staggerTrigger = "Stagger";
    [SerializeField] private string shieldBrokenTrigger = "ShieldBroken";
    [SerializeField] private float staggerDuration = 0.8f;

    private int blockHitHash;
    private int staggerTriggerHash;
    private int shieldBrokenHash;
    private float? damageOverride;
    private bool isDead = false;
    private bool playerDead = false;
    private bool anyError = false;

    public bool IsSlamming => false;
    public BulwarkShield Shield => shield;

    /// <summary>
    ///     Per-instance damage override applied by WaveSpawner.
    /// </summary>
    public void SetDamage(float value)
    {
        damageOverride = value;
        if (aiAttack != null)
        {
            aiAttack.SetDamage(value);
        }
        else
        {
            GetComponent<AIAttack>()?.SetDamage(value);
        }
    }

    private void OnValidate()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (movement == null)
        {
            movement = GetComponent<AIMovement>();
        }
        if (shield == null)
        {
            shield = GetComponentInChildren<BulwarkShield>(true);
        }
        if (aiAttack == null)
        {
            aiAttack = GetComponent<AIAttack>();
        }
    }

    private void Start()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogError("[BulwarkAttack] Animator component is missing.");
                anyError = true;
            }
        }
        if (health == null)
        {
            health = GetComponent<Health>();
            if (health == null)
            {
                Debug.LogError("[BulwarkAttack] Health component is missing.");
                anyError = true;
            }
        }
        if (movement == null)
        {
            movement = GetComponent<AIMovement>();
            if (movement == null)
            {
                Debug.LogError("[BulwarkAttack] AIMovement component is missing.");
                anyError = true;
            }
        }
        if (shield == null)
        {
            shield = GetComponentInChildren<BulwarkShield>(true);
        }
        if (aiAttack == null)
        {
            aiAttack = GetComponent<AIAttack>();
        }

        if (anyError)
        {
            return;
        }

        blockHitHash = Animator.StringToHash(blockHitTrigger);
        staggerTriggerHash = Animator.StringToHash(staggerTrigger);
        shieldBrokenHash = Animator.StringToHash(shieldBrokenTrigger);

        Player playerInstance = Player.Instance;
        if (playerInstance != null && playerInstance.Health != null)
        {
            playerInstance.Health.OnDied += HandlePlayerDied;
        }

        health.OnDied += HandleDied;
        health.OnDamaged += HandleDamaged;
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
            health.OnDied += HandleDied;
            health.OnDamaged -= HandleDamaged;
            health.OnDamaged += HandleDamaged;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
            health.OnDamaged -= HandleDamaged;
        }

        StopAllCoroutines();
        if (movement != null)
        {
            movement.SetMovementPaused(false);
            movement.SetTurningPaused(false);
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
            health.OnDamaged -= HandleDamaged;
        }
        if (Player.Instance != null && Player.Instance.Health != null)
        {
            Player.Instance.Health.OnDied -= HandlePlayerDied;
        }
    }

    private void HandleDied()
    {
        isDead = true;
        if (animator != null && animator.layerCount > 1)
        {
            animator.SetLayerWeight(1, 0f);
        }
        enabled = false;
    }

    private void HandleDamaged(Damage damage)
    {
        if (anyError || isDead || playerDead) return;

        // If damaged by player (e.g. hit in the back or shield broken), stagger normally!
        if (damage != null && damage.IsPlayerOwned)
        {
            TriggerBodyStagger();
        }
    }

    public void TriggerBodyStagger()
    {
        if (isDead || playerDead) return;

        if (animator != null)
        {
            animator.SetTrigger(staggerTriggerHash);
        }

        if (movement != null)
        {
            movement.SetMovementPaused(true);
            movement.SetTurningPaused(true);
            StartCoroutine(UnpauseAfterDelay(staggerDuration));
        }
    }

    private IEnumerator UnpauseAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!isDead && !playerDead && movement != null)
        {
            movement.SetMovementPaused(false);
            movement.SetTurningPaused(false);
        }
    }

    private void HandlePlayerDied()
    {
        playerDead = true;
    }

    /// <summary>
    ///     Called by BulwarkShield when an attack is successfully blocked.
    ///     Triggers block hit animation.
    /// </summary>
    public void HandleShieldBlocked(Damage damage)
    {
        if (anyError || isDead || playerDead)
        {
            return;
        }

        if (animator != null)
        {
            animator.SetTrigger(blockHitHash);
        }
    }

    public void HandleShieldBroken()
    {
        // Shield has broken; Bulwark can no longer block.
        if (animator != null)
        {
            animator.SetTrigger(shieldBrokenHash);
            if (animator.layerCount > 1)
            {
                animator.SetLayerWeight(1, 0f);
            }
        }
    }
}
