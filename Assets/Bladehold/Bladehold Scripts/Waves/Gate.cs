using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEngine;

/// <summary>
///     A castle gate — the second loss condition alongside player death. A gate is just another
///     <see cref="Health" />/<see cref="IDamageable" /> scene object: enemies path to and attack it
///     through <see cref="AITargetSelector" />/<see cref="AIAttack" /> exactly as they do the player,
///     and when any gate's <see cref="Health.OnDied" /> fires the run is over
///     (<see cref="OnAnyGateDestroyed" /> — <see cref="DeathScreen" /> and <see cref="WaveSpawner" />
///     listen, the same routing as the player's death).
///
///     Gates self-register in a static list in <c>Awake</c> (so enemy <c>Start</c>s can already see
///     them) and unregister in <c>OnDestroy</c>, which keeps the list correct across scene reloads.
///     Scenes without gates work exactly as before — every consumer treats "no gates" as
///     "target the player".
/// </summary>
public class Gate : MonoBehaviour
{
    private static readonly List<Gate> all = new List<Gate>();

    /// <summary>Every gate currently in the scene, destroyed or not.</summary>
    public static IReadOnlyList<Gate> All => all;

    /// <summary>Raised once per gate whose health reaches zero — the "run over" signal for gate defense.</summary>
    public static event Action<Gate> OnAnyGateDestroyed;

    [SerializeField] private Health health;
    [Tooltip("Where attackers path to and measure attack range from (e.g. the doors). Defaults to this transform.")]
    [SerializeField] private Transform attackPoint;

    [Header("Destruction Effects & Feedbacks")]
    [Tooltip("Prefab spawned at gate position when destroyed (e.g. large fire/debris explosion).")]
    [SerializeField] private GameObject explosionVfxPrefab;

    [Tooltip("Optional SFX played on destruction.")]
    [SerializeField] private AudioClip deathSound;

    [Tooltip("Optional MMF_Player feedback played on destruction (e.g. camera shake).")]
    [SerializeField] private MMF_Player deathFeedback;

    [Tooltip("Optional specific visual GameObjects to deactivate on destruction. If empty, all Renderers and Colliders in children are disabled.")]
    [SerializeField] private GameObject[] visualsToHide;

    private bool anyError = false;

    /// <summary>True once this gate has fallen.</summary>
    public bool IsDestroyed => health == null || health.IsDead;

    /// <summary>The point enemies path to / attack.</summary>
    public Vector3 TargetPosition => (attackPoint != null ? attackPoint : transform).position;

    /// <summary>The gate's damage sink, so <see cref="AIAttack" /> can hurt it.</summary>
    public IDamageable Damageable => health;

    /// <summary>The nearest still-standing gate to a position, or null when none is left (or none exists).</summary>
    public static Gate NearestAlive(Vector3 position)
    {
        Gate best = null;
        float bestSqrDistance = float.MaxValue;
        foreach (Gate gate in all)
        {
            if (gate == null || gate.IsDestroyed)
            {
                continue;
            }
            float sqrDistance = (gate.TargetPosition - position).sqrMagnitude;
            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                best = gate;
            }
        }
        return best;
    }

    /// <summary>True while at least one gate in the scene still stands.</summary>
    public static bool AnyAlive
    {
        get
        {
            foreach (Gate gate in all)
            {
                if (gate != null && !gate.IsDestroyed)
                {
                    return true;
                }
            }
            return false;
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (explosionVfxPrefab == null)
        {
            explosionVfxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticleFX/Prefabs/FX_Fire_Explosion_01.prefab");
        }
        if (deathSound == null)
        {
            deathSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Audio/Enemies/Bomber/explosion_large_01.wav");
        }
    }
#endif

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (health != null)
        {
            health.ImmuneToPlayerDamage = true;
        }
#if UNITY_EDITOR
        if (explosionVfxPrefab == null)
        {
            explosionVfxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticleFX/Prefabs/FX_Fire_Explosion_01.prefab");
        }
        if (deathSound == null)
        {
            deathSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Audio/Enemies/Bomber/explosion_large_01.wav");
        }
#endif
    }

    private void Awake()
    {
        // Register before any enemy's Start so target selection can see every gate from frame one.
        all.Add(this);
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (health != null)
        {
            health.ImmuneToPlayerDamage = true;
        }
    }

    private void Start()
    {
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the Gate.");
            anyError = true;
            return;
        }

        health.OnDied += HandleDied;
    }

    private void OnDestroy()
    {
        all.Remove(this);
        if (health != null)
        {
            health.OnDied -= HandleDied;
        }
    }

    private void HandleDied()
    {
        if (anyError)
        {
            return;
        }

        if (deathFeedback != null)
        {
            deathFeedback.PlayFeedbacks();
        }

        Vector3 spawnPos = attackPoint != null ? attackPoint.position : transform.position + Vector3.up * 1.5f;

        if (explosionVfxPrefab != null)
        {
            GameObject vfx = Instantiate(explosionVfxPrefab, spawnPos, Quaternion.identity);
            foreach (ParticleSystem ps in vfx.GetComponentsInChildren<ParticleSystem>())
            {
                var main = ps.main;
                main.useUnscaledTime = true;
            }
        }

        if (deathSound != null)
        {
            MMSoundManagerPlayOptions options = MMSoundManagerPlayOptions.Default;
            options.MmSoundManagerTrack = MMSoundManager.MMSoundManagerTracks.Sfx;
            options.Location = spawnPos;
            options.Volume = 1.0f;
            MMSoundManagerSoundPlayEvent.Trigger(deathSound, options);
        }

        // Disable all colliders and renderers on the gate immediately
        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }
        foreach (Renderer rend in GetComponentsInChildren<Renderer>())
        {
            rend.enabled = false;
        }

        if (visualsToHide != null)
        {
            foreach (GameObject go in visualsToHide)
            {
                if (go != null)
                {
                    go.SetActive(false);
                }
            }
        }

        OnAnyGateDestroyed?.Invoke(this);
    }
}
