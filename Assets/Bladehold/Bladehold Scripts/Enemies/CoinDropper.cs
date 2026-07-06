using UnityEngine;

/// <summary>
///     Drops a coin pickup when this enemy's <see cref="Health" /> dies. The coin amount is rolled
///     from the enemy's <see cref="EnemySO" /> and scaled by <see cref="StatType.GoldDropMultiplier" />
///     (a base-1.0 multiplier the gold tree raises), so tougher enemies can drop more later. Listens to
///     <see cref="Health.OnDied" />; Health stays unaware of loot.
/// </summary>
public class CoinDropper : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private EnemySO enemyData;
    [SerializeField] private Coin coinPrefab;
    [Tooltip("World-space offset from this transform where the coin spawns.")]
    [SerializeField] private Vector3 dropOffset = new Vector3(0f, 0.5f, 0f);

    private PlayerStats stats;
    private int? minCoinOverride;
    private int? maxCoinOverride;
    private bool anyError = false;

    /// <summary>
    ///     Per-instance coin-drop override (e.g. <see cref="WaveSpawner" /> applying an enemy type's
    ///     roster CSV row). Call right after Instantiate; the shared <see cref="EnemySO" /> is never
    ///     mutated.
    /// </summary>
    public void SetCoinDrop(int min, int max)
    {
        minCoinOverride = min;
        maxCoinOverride = max;
    }

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
    }

    private void Start()
    {
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (enemyData == null)
        {
            Debug.LogError("EnemySO is not assigned in the inspector.");
            anyError = true;
        }
        if (coinPrefab == null)
        {
            Debug.LogError("Coin prefab is not assigned in the inspector.");
            anyError = true;
        }

        // Optional: with no PlayerStats the drop just stays unmultiplied.
        stats = Player.Instance != null ? Player.Instance.Stats : null;

        if (anyError)
        {
            return;
        }

        health.OnDied += HandleDied;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
        }
    }

    private void HandleDied()
    {
        // GetValue returns 0 while the base is unregistered, so anything <= 0 means "no multiplier yet".
        float multiplier = stats != null ? stats.GetValue(StatType.GoldDropMultiplier) : 1f;
        if (multiplier <= 0f)
        {
            multiplier = 1f;
        }

        int rolled = minCoinOverride.HasValue
            ? Random.Range(minCoinOverride.Value, maxCoinOverride.Value + 1)
            : enemyData.RollCoinDrop();
        int amount = Mathf.Max(1, Mathf.RoundToInt(rolled * multiplier));
        Coin coin = Instantiate(coinPrefab, transform.position + dropOffset, Quaternion.identity);
        coin.SetAmount(amount);
    }
}
