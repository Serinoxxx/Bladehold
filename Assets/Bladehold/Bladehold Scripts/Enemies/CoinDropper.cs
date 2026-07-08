using DamageNumbersPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
///     Grants the player gold instantly when this enemy's <see cref="Health" /> dies. The amount is
///     rolled from the enemy's <see cref="EnemySO" /> and scaled by <see cref="StatType.GoldDropMultiplier" />
///     (a base-1.0 multiplier the gold tree raises), so tougher enemies can drop more later. Rarely
///     (<see cref="goldBagChance" />) the kill also drops a <b>gold bag</b> — a <see cref="Coin" />
///     pickup worth <see cref="goldBagMultiplier" />× the enemy's rolled gold value — the only way
///     loose gold reaches the ground from a normal kill. Listens to <see cref="Health.OnDied" />;
///     Health stays unaware of loot.
/// </summary>
public class CoinDropper : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private EnemySO enemyData;
    [Tooltip("Pickup spawned on the rare gold-bag drop. Regular kills grant gold instantly and drop nothing.")]
    [FormerlySerializedAs("coinPrefab")]
    [SerializeField] private Coin goldBagPrefab;
    [Tooltip("World-space offset from this transform where the gold bag spawns.")]
    [SerializeField] private Vector3 dropOffset = new Vector3(0f, 0.5f, 0f);
    [Tooltip("Optional DamageNumbersPro popup showing the gold granted instantly on this enemy's death.")]
    [SerializeField] private DamageNumber goldPopup;
    [Tooltip("Per-kill chance (0-1) this enemy also drops a gold bag pickup on top of the instant gold.")]
    [Range(0f, 1f)]
    [SerializeField] private float goldBagChance = 0.05f;
    [Tooltip("The gold bag is worth this multiple of the enemy's rolled gold value.")]
    [SerializeField] private float goldBagMultiplier = 5f;
    [Tooltip("Visual scale applied to the spawned gold bag so it reads as special next to a plain coin.")]
    [SerializeField] private float goldBagScale = 1.5f;

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
        if (goldBagPrefab == null)
        {
            Debug.LogError("Gold bag (Coin) prefab is not assigned in the inspector.");
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

        Wallet wallet = Player.Instance != null ? Player.Instance.Wallet : null;
        if (wallet != null)
        {
            wallet.Add(amount);
            if (GameStats.Instance != null)
            {
                GameStats.Instance.AddGold(amount);
            }
            if (goldPopup != null)
            {
                goldPopup.Spawn(transform.position + dropOffset, amount);
            }
        }
        else
        {
            // No wallet to pay into (player object gone mid-teardown) — fall back to a ground coin
            // so the gold isn't silently lost.
            SpawnPickup(amount, 1f);
        }

        if (goldBagChance > 0f && Random.value < goldBagChance)
        {
            int bagAmount = Mathf.Max(1, Mathf.RoundToInt(rolled * goldBagMultiplier * multiplier));
            SpawnPickup(bagAmount, goldBagScale);
        }
    }

    private void SpawnPickup(int amount, float scale)
    {
        Coin coin = Instantiate(goldBagPrefab, transform.position + dropOffset, Quaternion.identity);
        coin.SetAmount(amount);
        if (!Mathf.Approximately(scale, 1f))
        {
            coin.transform.localScale *= scale;
        }
    }
}
