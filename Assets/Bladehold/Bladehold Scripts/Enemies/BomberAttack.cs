using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     The Bomber's suicide charge. It chases its target (the player, or a gate via an optional
///     <see cref="AITargetSelector" />) carrying a flaming torch; once the target comes within
///     <see cref="BomberAttackSO.triggerRange" /> it stops for a short ignite pause to light the
///     dynamite in both hands (via <see cref="AIMovement.SetMovementPaused" />, the
///     <see cref="TrollSlamAttack" /> precedent), then sprints at the target at
///     <see cref="BomberAttackSO.fuseSpeedMultiplier" />× speed with the fuse sparking (via the new
///     <see cref="AIMovement.SetSpeedMultiplier" />, which composes with <see cref="SlowStatus" />).
///     <see cref="BomberAttackSO.fuseSeconds" /> after lighting, the dynamite explodes — damaging
///     <b>everything</b> in <see cref="BomberAttackSO.explosionRadius" /> (the player, gates, and
///     other enemies alike, never the bomber itself twice: he's force-killed through the normal
///     <see cref="Health" /> flow so wave/coin/kill accounting stays consistent, the
///     <see cref="ImpulseReceiver" /> precedent) and stamping impulse onto every hit so victims
///     with an <see cref="ImpulseReceiver" /> are ragdoll-flung. Once lit, the fuse can't be put
///     out — killing the bomber before it burns down is the only way to stop the explosion (a
///     dead bomber never explodes on its own).
///
///     <see cref="Detonate" /> triggers the explosion immediately from any state — the player's
///     Flaming Arrows skill line calls it on a winning roll (see <see cref="PlayerBow" />), turning
///     bombers into remote-detonated bombs against their own horde. Follows the
///     <see cref="TrollSlamAttack" /> skeleton (validate in Start, range check in Update, coroutine
///     to the payoff, stops on own/player death). All tunables live on <see cref="BomberAttackSO" />.
/// </summary>
public class BomberAttack : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;
    [SerializeField] private AIMovement movement;
    [SerializeField] private BomberAttackSO attackData;
    [Tooltip("Optional target-selection layer; without it the bomber only ever charges the player.")]
    [SerializeField] private AITargetSelector targetSelector;
    [SerializeField] private EnemyRagdoll ragdoll;

    [Header("Visuals & feedback (optional)")]
    [Tooltip("Torch prop held while chasing; hidden the moment the fuse is lit.")]
    [SerializeField] private GameObject torchVisual;
    [Tooltip("Dynamite props / sparking-fuse VFX enabled while the fuse burns (e.g. one per hand).")]
    [SerializeField] private GameObject[] fuseSparkVisuals;
    [Tooltip("Explosion VFX instantiated at the bomber on detonation (assumed to clean itself up).")]
    [SerializeField] private GameObject explosionVfxPrefab;
    [SerializeField] private MMF_Player igniteFeedback;
    [SerializeField] private MMF_Player explodeFeedback;

    // Animator trigger that plays the light-the-fuse animation. Wire a short ignite state driven by this.
    [SerializeField] private string lightFuseTrigger = "LightFuse";

    private const int MaxOverlapResults = 128;

    private readonly Collider[] overlapBuffer = new Collider[MaxOverlapResults];
    private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();

    private int lightFuseTriggerHash;
    private float? damageOverride;
    private IDamageable ownerDamageable;
    private Transform player;
    private Health playerHealth;
    private bool fuseLit = false;
    private bool hasExploded = false;
    private bool isDead = false;
    private bool playerDead = false;
    private bool anyError = false;

    /// <summary>
    ///     Per-instance damage override (e.g. <see cref="WaveSpawner" /> applying an enemy type's
    ///     roster CSV row). Call right after Instantiate; the shared <see cref="BomberAttackSO" />
    ///     is never mutated.
    /// </summary>
    public void SetDamage(float value)
    {
        damageOverride = value;
    }

    /// <summary>
    ///     Detonates the bomber on the spot, whatever its fuse state — chasing with the torch,
    ///     mid-ignite, or sprinting with the fuse burning. The player's Flaming Arrows skill calls
    ///     this on a winning roll. No-op for the dead (corpses never explode) and after the
    ///     explosion has already happened.
    /// </summary>
    public void Detonate()
    {
        if (anyError || isDead || hasExploded)
        {
            return;
        }
        Explode();
    }

    private void OnValidate()
    {
        if (animator == null)
        {
            // Synty rigs keep the Animator on a child model object.
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
        if (targetSelector == null)
        {
            targetSelector = GetComponent<AITargetSelector>();
        }
        if (ragdoll == null)
        {
            ragdoll = GetComponent<EnemyRagdoll>();
        }
    }

    private void Start()
    {
        if (animator == null)
        {
            Debug.LogError("Animator component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (movement == null)
        {
            Debug.LogError("AIMovement component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (attackData == null)
        {
            Debug.LogError("BomberAttackSO is not assigned in the inspector.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        lightFuseTriggerHash = Animator.StringToHash(lightFuseTrigger);

        // The explosion never damages the bomber directly (the DamageTrigger owner idiom) — he's
        // force-killed separately through the normal Health flow.
        ownerDamageable = GetComponentInParent<IDamageable>();

        Player playerInstance = Player.Instance;
        if (playerInstance == null)
        {
            Debug.LogError("Player.Instance is not set; the bomber has no one to charge.");
            anyError = true;
            return;
        }

        player = playerInstance.transform;

        // Stop charging once this bomber dies.
        health.OnDied += HandleDied;

        // Stop charging once the player dies (combat's over — time to celebrate).
        if (playerInstance.Health != null)
        {
            playerHealth = playerInstance.Health;
            playerHealth.OnDied += HandlePlayerDied;
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
        }
        if (playerHealth != null)
        {
            playerHealth.OnDied -= HandlePlayerDied;
        }
    }

    private void HandleDied()
    {
        isDead = true;
        // A bomber killed before the fuse burns down never explodes — the sparks just go out.
        SetFuseSparks(false);
        // Corpses have nothing left to tick. (Coroutines survive a disable; ExplodeAfterFuse
        // bails on isDead.)
        enabled = false;
    }

    private void HandlePlayerDied()
    {
        playerDead = true;
    }

    private void Update()
    {
        if (anyError || isDead || playerDead || fuseLit) return;

        // Do not detonate if there is no attackable target (e.g. flocking to an objective and waiting for player)
        if (targetSelector != null && targetSelector.TargetDamageable == null) return;

        if (IsTargetInRange())
        {
            LightFuse();
        }
    }

    private Vector3 TargetPosition()
    {
        return targetSelector != null ? targetSelector.TargetPosition : player.position;
    }

    private bool IsTargetInRange()
    {
        float sqrDistance = (TargetPosition() - transform.position).sqrMagnitude;
        return sqrDistance <= attackData.triggerRange * attackData.triggerRange;
    }

    private void LightFuse()
    {
        fuseLit = true;

        // Plant for the ignite pause — lighting dynamite mid-stride reads wrong (the
        // TrollSlamAttack wind-up precedent).
        movement.SetMovementPaused(true);

        if (igniteFeedback != null)
        {
            igniteFeedback.PlayFeedbacks();
        }
        animator.SetTrigger(lightFuseTriggerHash);

        if (torchVisual != null)
        {
            torchVisual.SetActive(false);
        }
        SetFuseSparks(true);

        StartCoroutine(ExplodeAfterFuse());
    }

    private IEnumerator ExplodeAfterFuse()
    {
        yield return new WaitForSeconds(attackData.igniteSeconds);

        // A detonation (Flaming Arrows) or death mid-ignite already resolved everything.
        if (isDead || hasExploded)
        {
            yield break;
        }
        if (playerDead)
        {
            FizzleFuse();
            yield break;
        }

        // The dynamite is lit — nothing can put it out now. Sprint at the target for what's left
        // of the fuse.
        movement.SetMovementPaused(false);
        movement.SetSpeedMultiplier(attackData.fuseSpeedMultiplier);

        yield return new WaitForSeconds(Mathf.Max(0f, attackData.fuseSeconds - attackData.igniteSeconds));

        if (isDead || hasExploded)
        {
            yield break;
        }
        if (playerDead)
        {
            FizzleFuse();
            yield break;
        }

        Explode();
    }

    /// <summary>The run ended mid-fuse: the sparks go out and the bomber joins the celebration alive.</summary>
    private void FizzleFuse()
    {
        SetFuseSparks(false);
        movement.SetSpeedMultiplier(1f);
        movement.SetMovementPaused(false);
    }

    private void Explode()
    {
        hasExploded = true;
        SetFuseSparks(false);

        Vector3 explosionPosition = transform.position;
        if (ragdoll != null && ragdoll.IsRagdolled && ragdoll.Pelvis != null)
        {
            explosionPosition = ragdoll.Pelvis.position;
        }

        if (explodeFeedback != null)
        {
            explodeFeedback.transform.position = explosionPosition;
            explodeFeedback.PlayFeedbacks();
        }
        if (explosionVfxPrefab != null)
        {
            Instantiate(explosionVfxPrefab, explosionPosition, Quaternion.identity);
        }

        ApplyExplosionDamage(explosionPosition);

        // The bomber never survives its own dynamite. Killed through the normal Health flow so
        // every death listener (wave accounting, coin drop, corpse pipeline) runs — the
        // ImpulseReceiver force-kill precedent.
        health.ReceiveDamage(new Damage
        {
            value = 999999f,
            type = attackData.damageType,
            sourcePosition = explosionPosition,
            source = ownerDamageable,
        });
    }

    /// <summary>
    ///     Damages every unique <see cref="IDamageable" /> in the radius except the bomber itself —
    ///     the player, gates, and other enemies alike. Victims with an <see cref="ImpulseReceiver" />
    ///     get flung by the stamped impulse (the <see cref="TrollSlamAttack" /> shape).
    /// </summary>
    private void ApplyExplosionDamage(Vector3 center)
    {
        hitTargets.Clear();
        int count = Physics.OverlapSphereNonAlloc(center, attackData.explosionRadius, overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider collider = overlapBuffer[i];
            if (!collider.TryGetComponent(out IDamageable damageable))
            {
                damageable = collider.GetComponentInParent<IDamageable>();
            }

            if (damageable == null) continue;
            if (damageable == ownerDamageable) continue;
            if (!hitTargets.Add(damageable)) continue;

            damageable.ReceiveDamage(new Damage
            {
                value = damageOverride ?? attackData.damage,
                type = attackData.damageType,
                sourcePosition = center,
                knockbackForce = attackData.knockbackForce,
                source = ownerDamageable,
                unparryable = true,
            });
        }
    }

    private void SetFuseSparks(bool active)
    {
        if (!active && igniteFeedback != null)
        {
            igniteFeedback.StopFeedbacks();
        }

        if (fuseSparkVisuals == null)
        {
            return;
        }
        foreach (GameObject spark in fuseSparkVisuals)
        {
            if (spark != null)
            {
                spark.SetActive(active);
            }
        }
    }
}
