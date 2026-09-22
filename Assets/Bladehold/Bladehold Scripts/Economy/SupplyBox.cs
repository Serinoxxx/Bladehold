using System;
using System.Collections;
using DamageNumbersPro;
using UnityEngine;

/// <summary>
///     Smashable supply crate, barrel, or metal lockbox found in the Supply Room and castle storerooms.
///     Implements the codebase convention where <see cref="Health"/> is the hub and remains unaware of loot.
///     Listens to <see cref="Health.OnDied"/>, rolls weighted resources (In-Run Gold, In-Run Supply, Goblin Blood,
///     Orcish Metal), spawns break VFX/splinters, plays audio, triggers DamageNumbersPro popups,
///     and safely sinks/destroys the container.
/// </summary>
[RequireComponent(typeof(Health))]
public class SupplyBox : MonoBehaviour
{
    public static event Action<SupplyBox> OnAnySupplyBoxSmashed;

    [Header("Dependencies")]
    [SerializeField] private Health health;

    [Header("Resource Quantities & Chances")]
    [Tooltip("Min and Max in-run gold awarded when broken.")]
    [SerializeField] private Vector2Int goldRange = new Vector2Int(15, 40);

    [Tooltip("Min and Max in-run fort defense supply awarded when broken.")]
    [SerializeField] private Vector2Int supplyRange = new Vector2Int(10, 25);

    [Tooltip("Chance (0-1) to drop Goblin Blood.")]
    [Range(0f, 1f)]
    [SerializeField] private float bloodChance = 0.40f;

    [Tooltip("Min and Max Goblin Blood dropped if rolled.")]
    [SerializeField] private Vector2Int bloodRange = new Vector2Int(1, 3);

    [Tooltip("Chance (0-1) to drop Orcish Metal.")]
    [Range(0f, 1f)]
    [SerializeField] private float metalChance = 0.20f;

    [Tooltip("Min and Max Orcish Metal dropped if rolled.")]
    [SerializeField] private Vector2Int metalRange = new Vector2Int(1, 2);

    [Header("Juice & Effects")]
    [Tooltip("Splinter or explosion particles spawned upon destruction.")]
    [SerializeField] private GameObject breakVfxPrefab;

    [Tooltip("Sound played when crate breaks. If null, loads from AssetDatabase or Resources.")]
    [SerializeField] private AudioClip breakSfx;

    [Tooltip("World offset for VFX, popups, and drops.")]
    [SerializeField] private Vector3 dropOffset = new Vector3(0f, 0.5f, 0f);

    [Tooltip("Seconds before broken box begins sinking or is destroyed.")]
    [SerializeField] private float destroyDelay = 1.2f;

    [Tooltip("Whether the crate sinks into the floor before being destroyed.")]
    [SerializeField] private bool sinkOnDestroy = true;

    [Header("DamageNumbersPro Popups (Optional / Auto-resolved)")]
    [SerializeField] private DamageNumber goldPopupPrefab;
    [SerializeField] private DamageNumber supplyPopupPrefab;
    [SerializeField] private DamageNumber bloodPopupPrefab;
    [SerializeField] private DamageNumber metalPopupPrefab;

    private bool isBroken = false;
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
            health = GetComponent<Health>();
        }

        if (health == null)
        {
            Debug.LogError($"[SupplyBox] Health component missing on {name}!");
            anyError = true;
            return;
        }

        health.OnDied += HandleBroken;

        // Auto-resolve popups and VFX/SFX if not explicitly assigned
        ResolveFallbacks();
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleBroken;
        }
    }

    private void ResolveFallbacks()
    {
        if (goldPopupPrefab == null || bloodPopupPrefab == null || metalPopupPrefab == null)
        {
            if (GameLoopManager.Instance != null)
            {
                if (goldPopupPrefab == null) goldPopupPrefab = GameLoopManager.Instance.goldPopupPrefab;
                if (bloodPopupPrefab == null) bloodPopupPrefab = GameLoopManager.Instance.bloodPopupPrefab;
                if (metalPopupPrefab == null) metalPopupPrefab = GameLoopManager.Instance.metalPopupPrefab;
            }
        }

#if UNITY_EDITOR
        if (goldPopupPrefab == null)
            goldPopupPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<DamageNumber>("Assets/Third Party/DamageNumbersPro/Demo/Prefabs/3D/Gold.prefab");
        if (bloodPopupPrefab == null)
            bloodPopupPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<DamageNumber>("Assets/Third Party/DamageNumbersPro/Demo/Prefabs/3D/Blood Text.prefab");
        if (metalPopupPrefab == null)
            metalPopupPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<DamageNumber>("Assets/Third Party/DamageNumbersPro/Demo/Prefabs/3D/Clear.prefab");
        if (supplyPopupPrefab == null)
            supplyPopupPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<DamageNumber>("Assets/Third Party/DamageNumbersPro/Demo/Prefabs/3D/Outline.prefab");

        if (breakVfxPrefab == null)
        {
            breakVfxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticleFX/Prefabs/FX_Impact_Wood_01.prefab");
        }

        if (breakSfx == null)
        {
            breakSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/SFX/Impacts/Generic Wood Item Break A.wav");
            if (breakSfx == null)
            {
                breakSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/SFX/Impacts/Wood Break Large A .wav");
            }
        }
#endif
    }

    private void HandleBroken()
    {
        if (anyError || isBroken) return;
        isBroken = true;

        Vector3 spawnPos = transform.position + dropOffset;

        // 1. Play Break VFX
        if (breakVfxPrefab != null)
        {
            Instantiate(breakVfxPrefab, spawnPos, Quaternion.identity);
        }

        // 2. Play Break SFX
        if (breakSfx != null)
        {
            MoreMountains.Tools.MMSoundManagerPlayOptions options = MoreMountains.Tools.MMSoundManagerPlayOptions.Default;
            options.MmSoundManagerTrack = MoreMountains.Tools.MMSoundManager.MMSoundManagerTracks.Sfx;
            options.Location = spawnPos;
            options.Volume = 0.9f;
            options.Pitch = UnityEngine.Random.Range(0.95f, 1.15f);
            MoreMountains.Tools.MMSoundManagerSoundPlayEvent.Trigger(breakSfx, options);
        }

        // 3. Roll & Award Currencies
        AwardCurrencies(spawnPos);

        // 4. Disable Colliders immediately so player and attacks pass through
        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }

        // 5. Notify global listeners (e.g. SupplyRoomController tracking smashed count)
        OnAnySupplyBoxSmashed?.Invoke(this);

        // 6. Sink and Destroy
        if (sinkOnDestroy && gameObject.activeInHierarchy)
        {
            StartCoroutine(SinkAndDestroyRoutine());
        }
        else
        {
            Destroy(gameObject, destroyDelay);
        }
    }

    private void AwardCurrencies(Vector3 spawnPos)
    {
        // A. In-Run Gold
        int gold = UnityEngine.Random.Range(goldRange.x, goldRange.y + 1);
        if (gold > 0)
        {
            RunSession.AddInRunGold(gold);
            if (GameStats.Instance != null)
            {
                GameStats.Instance.AddGold(gold);
            }
            if (goldPopupPrefab != null)
            {
                goldPopupPrefab.Spawn(spawnPos + Vector3.up * 0.2f, gold);
            }
        }

        // B. In-Run Supply
        int supply = UnityEngine.Random.Range(supplyRange.x, supplyRange.y + 1);
        if (supply > 0)
        {
            RunSession.AddInRunSupply(supply);
            if (supplyPopupPrefab != null)
            {
                supplyPopupPrefab.Spawn(spawnPos + Vector3.up * 0.5f, supply);
            }
        }

        // C. Goblin Blood (Metaprogression)
        if (UnityEngine.Random.value <= bloodChance)
        {
            int blood = UnityEngine.Random.Range(bloodRange.x, bloodRange.y + 1);
            if (blood > 0)
            {
                RunSession.AddGoblinBlood(blood);
                if (bloodPopupPrefab != null)
                {
                    bloodPopupPrefab.Spawn(spawnPos + Vector3.up * 0.8f, blood);
                }
            }
        }

        // D. Orcish Metal (Rare Metaprogression)
        if (UnityEngine.Random.value <= metalChance)
        {
            int metal = UnityEngine.Random.Range(metalRange.x, metalRange.y + 1);
            if (metal > 0)
            {
                RunSession.AddOrcishMetal(metal);
                if (metalPopupPrefab != null)
                {
                    metalPopupPrefab.Spawn(spawnPos + Vector3.up * 1.1f, metal);
                }
            }
        }
    }

    private IEnumerator SinkAndDestroyRoutine()
    {
        yield return new WaitForSeconds(destroyDelay);

        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + Vector3.down * 1.5f;
        float duration = 0.8f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, endPos, elapsed / duration);
            yield return null;
        }

        Destroy(gameObject);
    }
}
