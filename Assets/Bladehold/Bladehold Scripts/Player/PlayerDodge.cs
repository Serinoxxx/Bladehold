using System;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
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

    [Header("Juice & Effects")]
    [Tooltip("Particle VFX prefab spawned during the dodge dash.")]
    [SerializeField] private GameObject dashVfxPrefab;
    [Tooltip("Sound effect played on dodge initiation.")]
    [SerializeField] private AudioClip dodgeSfx;
    [Tooltip("Optional MMF_Player feedback triggered on dodge.")]
    [SerializeField] private MMF_Player dodgeFeedback;

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

        bool dashPressed = false;
        if (Keyboard.current != null)
        {
            dashPressed = Keyboard.current.leftCtrlKey.wasPressedThisFrame ||
                          Keyboard.current.rightCtrlKey.wasPressedThisFrame ||
                          Keyboard.current.spaceKey.wasPressedThisFrame;
        }

        if (!isDodging && !isCooldownActive && dashPressed)
        {
            StartCoroutine(PerformDodge());
        }
    }

    private IEnumerator PerformDodge()
    {
        isDodging = true;
        OnDodgeStarted?.Invoke();

        if (dodgeFeedback != null)
        {
            dodgeFeedback.PlayFeedbacks(transform.position);
        }
        else if (dodgeSfx != null)
        {
            AudioSource.PlayClipAtPoint(dodgeSfx, transform.position, 1.0f);
        }

        GameObject activeVfx = null;
        if (dashVfxPrefab != null)
        {
            activeVfx = Instantiate(dashVfxPrefab, transform.position, transform.rotation, transform);
            Destroy(activeVfx, dashDuration + 1f);
        }

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
                            source = player.Damageable,
                            isPlayerDamage = true,
                            elementId = RunSession.ElementalSlots.GetValueOrDefault("SLOT_MOBILITY", "")
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

