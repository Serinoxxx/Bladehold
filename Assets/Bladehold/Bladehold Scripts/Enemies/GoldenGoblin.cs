using UnityEngine;

/// <summary>
///     Marks a spawned goblin as a Golden Goblin: a shinier, more valuable variant rolled by
///     <see cref="WaveSpawner" /> per-spawn against <see cref="StatType.GoldenGoblinChance" />.
///     <see cref="MarkGolden" /> is called right after <c>Instantiate</c>, before <c>Start</c> runs (the same
///     ordering <see cref="WaveSpawner" /> already relies on elsewhere), so the visual swap in <c>Start</c>
///     sees the flag.
///
///     Reacts to this goblin's own <see cref="Health.OnDied" /> independently of <see cref="CoinDropper" /> —
///     it drops its own bonus coin worth <see cref="StatType.GoldenGoblinGoldBonusPercent" /> of a fresh roll,
///     rather than reaching into CoinDropper's roll. VFX/SFX are optional and purely cosmetic: a missing
///     material/prefab/clip never blocks the gold bonus from applying.
/// </summary>
public class GoldenGoblin : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private EnemySO enemyData;
    [SerializeField] private Coin coinPrefab;
    [Tooltip("World-space offset from this transform where the bonus coin spawns.")]
    [SerializeField] private Vector3 dropOffset = new Vector3(0f, 0.5f, 0f);

    [Header("Golden visual (cosmetic, optional)")]
    [Tooltip("Renderer(s) swapped to goldenMaterial when this goblin is marked golden.")]
    [SerializeField] private Renderer[] bodyRenderers;
    [SerializeField] private Material goldenMaterial;
    [Tooltip("Instantiated at this goblin's position on death.")]
    [SerializeField] private GameObject deathVfxPrefab;
    [SerializeField] private AudioClip deathSfx;

    private PlayerStats stats;
    private bool isGolden;
    private bool anyError = false;

    /// <summary>True once this instance has been marked golden by the spawner.</summary>
    public bool IsGolden => isGolden;

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
    }

    /// <summary>Marks this goblin as golden. Call right after Instantiate, before Start runs.</summary>
    public void MarkGolden()
    {
        isGolden = true;
    }

    private void Start()
    {
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }

        stats = Player.Instance != null ? Player.Instance.Stats : null;

        if (anyError)
        {
            return;
        }

        health.OnDied += HandleDied;

        if (isGolden)
        {
            ApplyGoldenVisual();
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
        }
    }

    private void ApplyGoldenVisual()
    {
        if (goldenMaterial == null || bodyRenderers == null)
        {
            return;
        }

        foreach (Renderer renderer in bodyRenderers)
        {
            if (renderer != null)
            {
                renderer.material = goldenMaterial;
            }
        }
    }

    private void HandleDied()
    {
        if (!isGolden)
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

        float bonusPercent = stats != null ? stats.GetValue(StatType.GoldenGoblinGoldBonusPercent) : 0f;
        if (bonusPercent <= 0f || enemyData == null || coinPrefab == null)
        {
            return;
        }

        int bonus = Mathf.RoundToInt(enemyData.RollCoinDrop() * bonusPercent);
        if (bonus > 0)
        {
            Coin coin = Instantiate(coinPrefab, transform.position + dropOffset, Quaternion.identity);
            coin.SetAmount(bonus);
        }
    }
}
