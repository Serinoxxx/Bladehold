using DamageNumbersPro;
using UnityEngine;

/// <summary>
///     Spawns a DamageNumbersPro popup on this character whenever its <see cref="Health" /> takes
///     damage. It listens to <see cref="Health.OnDamaged" />, so the popups react to damage while
///     Health itself stays unaware of them — the dependency only points inward.
///
///     Mirrors the usage shown in <c>DamageNumbersPro/Demo C#/DNP_ExampleMesh</c>: spawn the prefab
///     at a world position with the damage value, then have it follow the target.
/// </summary>
public class DamageNumberSpawner : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private DamageNumber popupPrefab;

    // World-space offset from this transform where the popup appears (roughly head height by default).
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 2f, 0f);

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
        if (popupPrefab == null)
        {
            Debug.LogError("DamageNumber popup prefab is not assigned in the inspector.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        health.OnDamaged += HandleDamaged;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
        }
    }

    private void HandleDamaged(Damage damage)
    {
        // Spawn at the character and let the popup follow it as it rises/fades.
        DamageNumber popup = popupPrefab.Spawn(transform.position + spawnOffset, damage.value);
        popup.SetFollowedTarget(transform);
    }
}
