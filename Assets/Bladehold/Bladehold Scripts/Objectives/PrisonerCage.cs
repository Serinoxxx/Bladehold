using System;
using System.Collections;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEngine;

/// <summary>
///     Destructible cage holding a captive prisoner NPC.
///     When broken, releases the prisoner, plays destruction feedbacks, and informs the objective.
/// </summary>
[RequireComponent(typeof(Health))]
public class PrisonerCage : MonoBehaviour
{
    [Header("Health & Hit Points")]
    [SerializeField] private float maxHealth = 150f;

    [Header("Prisoner Reference")]
    [Tooltip("The prisoner NPC inside the cage. If null, searched in children.")]
    [SerializeField] private RescuedPrisoner prisoner;

    [Header("Feedbacks & Juiciness")]
    [Tooltip("MMF_Player played when the cage takes a hit.")]
    [SerializeField] private MMF_Player hitFeedback;

    [Tooltip("MMF_Player played when the cage is destroyed.")]
    [SerializeField] private MMF_Player breakFeedback;

    [Header("Visual Effects & Audio")]
    [Tooltip("Wood splinter / debris VFX spawned when the cage breaks.")]
    [SerializeField] private GameObject cageBreakVfxPrefab;

    [Tooltip("Audio clip played when the cage is destroyed.")]
    [SerializeField] private AudioClip cageBreakSound;

    [Tooltip("Audio clip played when the cage takes damage.")]
    [SerializeField] private AudioClip cageHitSound;

    [Tooltip("Optional visual mesh roots to disable upon breaking so the open prisoner is clearly visible.")]
    [SerializeField] private GameObject[] cageVisualRoots;

    private Health health;
    private bool isBroken;

    public event Action<PrisonerCage> OnCageBroken;
    public bool IsBroken => isBroken;
    public Health Health => health;

    private void Awake()
    {
        health = GetComponent<Health>();
        if (health != null)
        {
            health.SetMaxHealth(maxHealth);
        }

        if (prisoner == null)
        {
            prisoner = GetComponentInChildren<RescuedPrisoner>();
        }
    }

    private void Start()
    {
        if (health != null)
        {
            health.OnDamaged += HandleDamaged;
            health.OnDied += HandleDied;
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnDied -= HandleDied;
        }
    }

    private void HandleDamaged(Damage damage)
    {
        if (isBroken) return;

        if (hitFeedback != null)
        {
            hitFeedback.PlayFeedbacks();
        }

        if (cageHitSound != null)
        {
            MMSoundManagerPlayOptions options = MMSoundManagerPlayOptions.Default;
            options.MmSoundManagerTrack = MMSoundManager.MMSoundManagerTracks.Sfx;
            options.Location = transform.position;
            options.Volume = 0.7f;
            MMSoundManagerSoundPlayEvent.Trigger(cageHitSound, options);
        }
    }

    private void HandleDied()
    {
        if (isBroken) return;
        isBroken = true;

        if (breakFeedback != null)
        {
            breakFeedback.PlayFeedbacks();
        }

        if (cageBreakVfxPrefab != null)
        {
            Instantiate(cageBreakVfxPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
        }

        if (cageBreakSound != null)
        {
            MMSoundManagerPlayOptions options = MMSoundManagerPlayOptions.Default;
            options.MmSoundManagerTrack = MMSoundManager.MMSoundManagerTracks.Sfx;
            options.Location = transform.position;
            options.Volume = 1.0f;
            MMSoundManagerSoundPlayEvent.Trigger(cageBreakSound, options);
        }

        // Disable cage colliders and visuals
        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }

        var healthBar = GetComponentInChildren<MoreMountains.Tools.MMHealthBar>();
        if (healthBar != null)
        {
            healthBar.ShowBar(false);
        }

        // Release the prisoner before modifying cage visuals
        if (prisoner != null)
        {
            // Unparent prisoner so cage destruction doesn't kill prisoner
            prisoner.transform.SetParent(null);
            prisoner.Release();
        }

        if (cageVisualRoots != null && cageVisualRoots.Length > 0)
        {
            foreach (GameObject visual in cageVisualRoots)
            {
                if (visual == null) continue;
                if (visual == gameObject)
                {
                    // If root object is passed as visual, disable cage renderers rather than deactivating the root GameObject
                    foreach (Renderer r in visual.GetComponentsInChildren<Renderer>())
                    {
                        if (prisoner != null && r.transform.IsChildOf(prisoner.transform)) continue;
                        r.enabled = false;
                    }
                }
                else
                {
                    visual.SetActive(false);
                }
            }
        }

        OnCageBroken?.Invoke(this);
        Destroy(gameObject, 1.0f);
    }
}
