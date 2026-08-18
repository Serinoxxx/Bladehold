using System;
using System.Collections;
using System.Collections.Generic;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerDodge : MonoBehaviour
{
    [SerializeField] private Player player;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Animator animator;
    [SerializeField] private InputReader inputReader;
    [SerializeField] private Camera facingCamera;
    [SerializeField] private float dashDuration = 0.2f;

    public event Action<float, float> OnCooldownUpdated;
    public event Action OnAbilityReady;
    public event Action OnDodgeStarted;

    private float remainingCooldown;
    private float maxCooldown;
    private bool isCooldownActive;
    private bool isDodging;

    private void OnValidate()
    {
        if (player == null) player = GetComponent<Player>();
        if (characterController == null) characterController = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (inputReader == null) inputReader = GetComponentInChildren<InputReader>();
    }

    private void Start()
    {
        if (player == null) player = GetComponent<Player>();
        if (characterController == null) characterController = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (inputReader == null) inputReader = GetComponentInChildren<InputReader>();
        if (facingCamera == null) facingCamera = Camera.main;
    }

    private void Update()
    {
        if (player == null || player.Health.IsDead) return;

        if (isCooldownActive)
        {
            remainingCooldown -= Time.deltaTime;
            OnCooldownUpdated?.Invoke(remainingCooldown, maxCooldown);

            if (remainingCooldown <= 0f)
            {
                isCooldownActive = false;
                OnAbilityReady?.Invoke();
            }
        }

        if (player.Stats.GetValue(StatType.DodgeUnlocked) <= 0f) return;

        if (!isDodging && !isCooldownActive && Keyboard.current != null && Keyboard.current.leftCtrlKey.wasPressedThisFrame)
        {
            StartCoroutine(PerformDodge());
        }
    }

    private IEnumerator PerformDodge()
    {
        isDodging = true;
        OnDodgeStarted?.Invoke();

        maxCooldown = player.Stats.GetValue(StatType.DodgeCooldown);
        remainingCooldown = maxCooldown;
        isCooldownActive = true;
        OnCooldownUpdated?.Invoke(remainingCooldown, maxCooldown);

        float distance = player.Stats.GetValue(StatType.DodgeDistance);
        
        // Find dash direction (use camera-relative movement input if active, otherwise transform.forward)
        Vector3 dashDir = transform.forward;
        if (inputReader == null) inputReader = GetComponentInChildren<InputReader>();
        Camera cam = facingCamera != null ? facingCamera : Camera.main;

        if (inputReader != null && inputReader._moveComposite.sqrMagnitude > 0.01f && cam != null)
        {
            Vector3 camForward = cam.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();

            Vector3 camRight = cam.transform.right;
            camRight.y = 0f;
            camRight.Normalize();

            Vector2 moveInput = inputReader._moveComposite;
            Vector3 calculatedDir = (camForward * moveInput.y + camRight * moveInput.x).normalized;
            if (calculatedDir.sqrMagnitude > 0.001f)
            {
                dashDir = calculatedDir;
            }
        }

        if (dashDir.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(dashDir);
        }
        
        float damageMultiplier = player.Stats.GetValue(StatType.DodgeDamageMultiplier);
        float knockback = player.Stats.GetValue(StatType.DodgeKnockbackForce);
        float chainReduction = player.Stats.GetValue(StatType.DodgeChainCooldownReduction);
        HashSet<Health> hitEnemies = new HashSet<Health>();

        float timePassed = 0f;
        while (timePassed < dashDuration)
        {
            if (player.Health.IsDead) break;

            float moveStep = (distance / dashDuration) * Time.deltaTime;
            characterController.Move(dashDir * moveStep);
            
            if (damageMultiplier > 0f || knockback > 0f)
            {
                Collider[] hits = Physics.OverlapSphere(transform.position, 1.5f);
                foreach (var hit in hits)
                {
                    Health enemyHealth = hit.GetComponentInParent<Health>();
                    if (enemyHealth != null && enemyHealth != player.Health && !enemyHealth.IsDead && hitEnemies.Add(enemyHealth))
                    {
                        float baseDamage = player.Stats.GetValue(StatType.SwordDamage);
                        float finalDamage = baseDamage * damageMultiplier * player.Stats.GetValue(StatType.AllDamageMultiplier);
                        enemyHealth.ReceiveDamage(new Damage { 
                            value = finalDamage, 
                            isCritical = false, 
                            knockbackForce = knockback,
                            sourcePosition = transform.position,
                            source = player.Damageable
                        });

                        if (enemyHealth.IsDead && chainReduction > 0f)
                        {
                            remainingCooldown -= chainReduction;
                            if (remainingCooldown < 0f) remainingCooldown = 0f;
                            OnCooldownUpdated?.Invoke(remainingCooldown, maxCooldown);
                        }
                    }
                }
            }

            timePassed += Time.deltaTime;
            yield return null;
        }

        isDodging = false;
    }
}

