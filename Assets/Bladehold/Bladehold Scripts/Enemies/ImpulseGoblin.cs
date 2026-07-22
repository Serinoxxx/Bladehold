using UnityEngine;

/// <summary>
///     Marks a spawned goblin as an Impulse Goblin: a crackling variant rolled by
///     <see cref="WaveSpawner" /> per-spawn against <see cref="StatType.ImpulseGoblinChance" />, the
///     same pattern as <see cref="GoldenGoblin" />. <see cref="MarkImpulse" /> is called right after
///     <c>Instantiate</c>, before <c>Start</c> runs, so the visual swap in <c>Start</c> sees the flag.
///
///     A marked goblin telegraphs its drop with an aura material and, on its own
///     <see cref="Health.OnDied" />, always drops one <see cref="ImpulseOrb" /> (independent of
///     <see cref="CoinDropper" />). The two marks are rolled independently — a goblin can be both
///     golden and impulse, in which case this aura wins the body swap (it telegraphs combat
///     behaviour; gold is economy-only — see the guard in <see cref="GoldenGoblin" />) and both drops
///     still occur. VFX/SFX are optional and purely cosmetic: a missing material/prefab/clip never
///     blocks the orb drop.
/// </summary>
public class ImpulseGoblin : MonoBehaviour
{
    [SerializeField] private Health health;

    [Header("Impulse visual (cosmetic, optional)")]
    [Tooltip("Renderer(s) swapped to impulseAuraMaterial when this goblin is marked. Mirror GoldenGoblin's list.")]
    [SerializeField] private Renderer[] bodyRenderers;
    [SerializeField] private Material impulseAuraMaterial;
    [Tooltip("Instantiated at this goblin's position on death.")]
    [SerializeField] private GameObject deathVfxPrefab;
    [SerializeField] private AudioClip deathSfx;

    private bool isImpulse;
    private bool anyError = false;

    /// <summary>True once this instance has been marked by the spawner.</summary>
    public bool IsImpulse => isImpulse;

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
    }

    /// <summary>Marks this goblin as an Impulse Goblin. Call right after Instantiate, before Start runs.</summary>
    public void MarkImpulse()
    {
        isImpulse = true;
    }

    private void Start()
    {
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        health.OnDied += HandleDied;

        if (isImpulse)
        {
            ApplyImpulseVisual();
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
        }
    }

    private void ApplyImpulseVisual()
    {
        if (impulseAuraMaterial == null || bodyRenderers == null)
        {
            return;
        }

        foreach (Renderer renderer in bodyRenderers)
        {
            if (renderer != null)
            {
                renderer.material = impulseAuraMaterial;
            }
        }
    }

    private void HandleDied()
    {
        if (!isImpulse)
        {
            return;
        }

        if (deathVfxPrefab != null)
        {
            Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);
        }
        if (deathSfx != null)
        {
            AudioSource.PlayClipAtPoint(deathSfx, transform.position);
        }

        if (Player.Instance != null)
        {
            Player.Instance.GetComponentInChildren<ImpulseBuff>()?.CollectOrb();
        }
    }
}
