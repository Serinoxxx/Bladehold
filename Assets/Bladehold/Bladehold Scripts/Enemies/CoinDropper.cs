using UnityEngine;

/// <summary>
///     Drops a coin pickup when this enemy's <see cref="Health" /> dies. The coin amount is rolled
///     from the enemy's <see cref="EnemySO" />, so tougher enemies can drop more later. Listens to
///     <see cref="Health.OnDied" />; Health stays unaware of loot.
/// </summary>
public class CoinDropper : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private EnemySO enemyData;
    [SerializeField] private Coin coinPrefab;
    [Tooltip("World-space offset from this transform where the coin spawns.")]
    [SerializeField] private Vector3 dropOffset = new Vector3(0f, 0.5f, 0f);

    private bool anyError = false;

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
        int amount = enemyData.RollCoinDrop();
        Coin coin = Instantiate(coinPrefab, transform.position + dropOffset, Quaternion.identity);
        coin.SetAmount(amount);
    }
}
