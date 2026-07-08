using UnityEngine;

/// <summary>
///     Rolls the shared <see cref="PowerupDropSO" /> table when this enemy's <see cref="Health" />
///     dies and spawns any powerup pickups that win their chance roll (e.g. a <see cref="HealthPack" />).
///     Sits on every enemy prefab so any enemy can drop a powerup. Listens to
///     <see cref="Health.OnDied" />; Health stays unaware of loot.
/// </summary>
public class PowerupDropper : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private PowerupDropSO dropTable;
    [Tooltip("World-space offset from this transform where a powerup spawns.")]
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
        if (dropTable == null)
        {
            Debug.LogError("PowerupDropSO is not assigned in the inspector.");
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
        foreach (PowerupDropSO.Entry entry in dropTable.entries)
        {
            if (entry.prefab == null || entry.chance <= 0f || Random.value >= entry.chance)
            {
                continue;
            }

            // Small horizontal jitter so two winning rolls don't stack pickups in one spot.
            Vector2 jitter = Random.insideUnitCircle * 0.25f;
            Vector3 position = transform.position + dropOffset + new Vector3(jitter.x, 0f, jitter.y);
            Instantiate(entry.prefab, position, Quaternion.identity);
        }
    }
}
