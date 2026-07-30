using System;
using System.Collections.Generic;
using UnityEngine;

public class BerserkerUltimate : MonoBehaviour, IUltimateHandler
{
    private Player player;
    private float ultimateEndTime;
    private PlayerUltimateController controller;
    private bool isRunning;
    
    private Vector3 originalScale;
    private float addedDamageMultiplier = 0.5f; // +50% damage
    
    [Tooltip("Damage dealt to enemies touched while ultimate is active.")]
    [SerializeField] private float collisionDamage = 20f;
    [Tooltip("Knockback dealt to enemies touched while ultimate is active.")]
    [SerializeField] private float collisionKnockback = 15f;

    private readonly HashSet<IDamageable> hitThisFrame = new HashSet<IDamageable>();
    private Collider[] overlapBuffer = new Collider[32];

    private void Awake()
    {
        player = GetComponent<Player>();
    }

    public void Activate(PlayerUltimateController controller)
    {
        this.controller = controller;
        float duration = player.Stats.GetValue(StatType.UltimateDurationSeconds);
        ultimateEndTime = Time.time + duration;
        isRunning = true;

        originalScale = player.transform.localScale;
        
        float sizeMulti = player.Stats.GetValue(StatType.UltimateBerserkerSizeMultiplier);
        if (sizeMulti <= 0f) sizeMulti = 1.5f;

        player.transform.localScale = originalScale * sizeMulti;

        player.Stats.AddModifier(StatType.AllDamageMultiplier, ModifierKind.Percent, addedDamageMultiplier);
        
        if (player.Health != null)
        {
            player.Health.ScaleDamageTaken += HandleDamageReduction;
        }
    }

    private void Update()
    {
        if (!isRunning) return;

        if (Time.time >= ultimateEndTime)
        {
            End();
            return;
        }

        // Damage enemies we touch
        hitThisFrame.Clear();
        Vector3 center = player.transform.position + Vector3.up * (player.transform.localScale.y * 1f); // approximate center
        float radius = 1.5f * player.transform.localScale.x;
        
        int count = Physics.OverlapSphereNonAlloc(center, radius, overlapBuffer, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            IDamageable damageable = PlayerBow.ResolveDamageable(overlapBuffer[i]);
            if (damageable != null && damageable != player.Damageable && hitThisFrame.Add(damageable))
            {
                damageable.ReceiveDamage(new Damage
                {
                    value = collisionDamage,
                    type = DamageType.blunt,
                    sourcePosition = player.transform.position,
                    knockbackForce = collisionKnockback,
                    source = player.Damageable
                });
            }
        }
    }

    private float HandleDamageReduction(Damage damage)
    {
        float reduction = player.Stats.GetValue(StatType.UltimateBerserkerDamageReduction);
        return Mathf.Clamp01(1f - reduction);
    }

    private void End()
    {
        isRunning = false;

        player.transform.localScale = originalScale;
        player.Stats.AddModifier(StatType.AllDamageMultiplier, ModifierKind.Percent, -addedDamageMultiplier);

        if (player.Health != null)
        {
            player.Health.ScaleDamageTaken -= HandleDamageReduction;
        }

        controller?.EndUltimate();
    }
}
