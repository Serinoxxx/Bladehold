using UnityEngine;

/// <summary>
///     The Mutant's parting gift: on its own <see cref="Health.OnDied" /> (the
///     <see cref="CoinDropper" /> idiom), spawns a <see cref="ToxicPoolZone" /> at the corpse that
///     keeps damaging anything standing in it for a few seconds. The dead mutant itself is passed as
///     the zone's owner so the corpse is never re-hit, whatever the collider-disable ordering.
/// </summary>
public class ToxicPoolOnDeath : MonoBehaviour
{
    [SerializeField] private ToxicPoolOnDeathSO data;
    [SerializeField] private Health health;
    [SerializeField] private ToxicPoolZone poolPrefab;

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
        if (data == null)
        {
            Debug.LogError("ToxicPoolOnDeathSO is not assigned in the inspector.");
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (poolPrefab == null)
        {
            Debug.LogError("ToxicPoolZone prefab is not assigned in the inspector.");
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
        ToxicPoolZone pool = Instantiate(poolPrefab, transform.position, Quaternion.identity);
        pool.Initialize(data.poolRadius, data.poolDuration, data.tickInterval, data.tickDamage, data.damageType, health);
    }
}
