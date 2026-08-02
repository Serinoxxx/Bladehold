using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerDodge : MonoBehaviour
{
    [SerializeField] private Player player;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Animator animator;
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
    }

    private void Start()
    {
        if (player == null) player = GetComponent<Player>();
        if (characterController == null) characterController = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
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
        
        // Find dash direction (use input direction if any, otherwise forward)
        Vector3 dashDir = transform.forward;
        // Check input vector from Synty's InputReader if possible, but fallback to forward is fine for now
        
        float damageMultiplier = player.Stats.GetValue(StatType.DodgeDamageMultiplier);
        HashSet<Health> hitEnemies = new HashSet<Health>();

        float timePassed = 0f;
        while (timePassed < dashDuration)
        {
            if (player.Health.IsDead) break;

            float moveStep = (distance / dashDuration) * Time.deltaTime;
            characterController.Move(dashDir * moveStep);
            
            if (damageMultiplier > 0f)
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
                            knockbackForce = 5f,
                            sourcePosition = transform.position,
                            source = player.Damageable
                        });
                    }
                }
            }

            timePassed += Time.deltaTime;
            yield return null;
        }

        isDodging = false;
    }
}
