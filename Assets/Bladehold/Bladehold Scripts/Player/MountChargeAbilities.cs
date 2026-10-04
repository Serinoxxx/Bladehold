using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     The draft-card effects that ride on the warhorse's charge, kept out of <see cref="HorseMotor" />
///     so the motor stays pure locomotion. Lives on the player root beside <see cref="PlayerMount" />
///     and polls the ridden horse's <see cref="HorseMotor.IsCharging" /> each frame:
///     <list type="bullet">
///         <item><b>Blazing Hooves</b> (<see cref="StatType.HorseChargeFireTrailDPS" />): drops a
///         <see cref="FireTrailSegment" /> every <see cref="HorseSO.fireTrailSpacing" /> metres galloped.</item>
///         <item><b>Frost Wake</b> (<see cref="StatType.HorseChargeFrostRadius" />): every
///         <see cref="HorseSO.frostPulseInterval" /> seconds, chills enemies within the radius.</item>
///     </list>
///     Battering Ram, Bloodlust and Iron Barding are plain stats read by <see cref="HorseMotor" />;
///     this component only adds Bloodlust's feedback, by listening to <see cref="HorseMotor.OnTrampleKill" />.
///     All stats start at 0 (locked), registered by <see cref="PlayerMount" />.
/// </summary>
public class MountChargeAbilities : MonoBehaviour
{
    [SerializeField] private PlayerMount mount;

    [Tooltip("Optional: parked VFX for each fire trail segment. Falls back to ElementalEffectsManager.fireStatusVfx.")]
    [SerializeField] private GameObject fireTrailVfxPrefab;

    [Tooltip("Optional: played at the horse on each Frost Wake pulse that chills at least one enemy (frost burst, crackle). Its particle feedback must use the Script position mode.")]
    [SerializeField] private MMF_Player frostPulseFeedback;

    [Tooltip("Optional: played at the victim when a trample kill refunds charge stamina (Bloodlust): blood burst + a short gulp/whinny. Leave empty for silence. Its particle feedback must use the Script position mode.")]
    [SerializeField] private MMF_Player trampleKillFeedback;

    [Tooltip("Layers Frost Wake scans for enemies.")]
    [SerializeField] private LayerMask enemyLayers = 1 << 7;

    private const int MaxFrostTargets = 32;

    private readonly Collider[] frostBuffer = new Collider[MaxFrostTargets];
    private readonly HashSet<Health> frostScratch = new HashSet<Health>();

    private PlayerStats stats;
    private Vector3 lastTrailPosition;
    private bool wasCharging;
    private float nextFrostPulseTime;
    private bool loggedMissingTrailVfx;
    private bool anyError = false;
    private HorseMotor subscribedHorse;

    private void OnValidate()
    {
        if (mount == null)
        {
            mount = GetComponent<PlayerMount>();
        }
    }

    private void Start()
    {
        if (mount == null)
        {
            Debug.LogError("[MountChargeAbilities] PlayerMount is not assigned or found on the GameObject.", this);
            anyError = true;
        }

        // Player (and its stats) sits on the Synty character child; resolve through the singleton.
        stats = Player.Instance != null ? Player.Instance.Stats : null;
        if (stats == null)
        {
            Debug.LogError("[MountChargeAbilities] No PlayerStats found via Player.Instance.", this);
            anyError = true;
        }
    }

    private void Update()
    {
        if (anyError) return;

        HorseMotor horse = mount.CurrentHorse;
        if (horse != subscribedHorse)
        {
            SubscribeHorse(horse);
        }

        bool charging = horse != null && horse.IsCharging && horse.Data != null;
        if (!charging)
        {
            wasCharging = false;
            return;
        }

        HorseSO data = horse.Data;
        Vector3 position = horse.transform.position;

        if (!wasCharging)
        {
            wasCharging = true;
            lastTrailPosition = position;
            nextFrostPulseTime = Time.time;
        }

        float fireDps = stats.GetValue(StatType.HorseChargeFireTrailDPS);
        if (fireDps > 0f && Vector3.Distance(lastTrailPosition, position) >= Mathf.Max(0.2f, data.fireTrailSpacing))
        {
            SpawnFireTrailSegment(position, fireDps, data.fireTrailLifetime);
            lastTrailPosition = position;
        }

        float frostRadius = stats.GetValue(StatType.HorseChargeFrostRadius);
        if (frostRadius > 0f && Time.time >= nextFrostPulseTime)
        {
            nextFrostPulseTime = Time.time + Mathf.Max(0.1f, data.frostPulseInterval);
            PulseFrost(horse, position, frostRadius, data.frostChillStacks);
        }
    }

    private void OnDestroy()
    {
        SubscribeHorse(null);
    }

    /// <summary>Follows the ridden horse (summons replace it) so Bloodlust's kill feedback hooks the live one.</summary>
    private void SubscribeHorse(HorseMotor horse)
    {
        if (subscribedHorse != null)
        {
            subscribedHorse.OnTrampleKill -= HandleTrampleKill;
        }
        subscribedHorse = horse;
        if (subscribedHorse != null)
        {
            subscribedHorse.OnTrampleKill += HandleTrampleKill;
        }
    }

    private void HandleTrampleKill(IDamageable victim)
    {
        if (anyError || trampleKillFeedback == null || stats.GetValue(StatType.HorseTrampleKillStamina) <= 0f)
        {
            return;
        }

        Vector3 position = victim is Component component ? component.transform.position
            : (subscribedHorse != null ? subscribedHorse.transform.position : transform.position);
        trampleKillFeedback.PlayFeedbacks(position);
    }

    private void SpawnFireTrailSegment(Vector3 position, float dps, float lifetime)
    {
        GameObject vfx = fireTrailVfxPrefab;
        if (vfx == null && ElementalEffectsManager.Instance != null)
        {
            vfx = ElementalEffectsManager.Instance.fireStatusVfx;
        }
        if (vfx == null && !loggedMissingTrailVfx)
        {
            loggedMissingTrailVfx = true;
            Debug.LogError("[MountChargeAbilities] Blazing Hooves has no VFX: assign fireTrailVfxPrefab (or ElementalEffectsManager.fireStatusVfx).", this);
        }

        GameObject segmentObject = new GameObject("HorseFireTrailSegment");
        segmentObject.transform.position = position;
        FireTrailSegment segment = segmentObject.AddComponent<FireTrailSegment>();
        segment.Init(dps, lifetime, vfx);
    }

    private void PulseFrost(HorseMotor horse, Vector3 center, float radius, float stacks)
    {
        int count = Physics.OverlapSphereNonAlloc(center, radius, frostBuffer, enemyLayers, QueryTriggerInteraction.Ignore);
        frostScratch.Clear();
        int chilled = 0;
        for (int i = 0; i < count; i++)
        {
            Health enemyHealth = frostBuffer[i].GetComponentInParent<Health>();
            if (enemyHealth == null || enemyHealth.IsDead || enemyHealth == horse.Health) continue;
            if (Player.Instance != null && enemyHealth == Player.Instance.Health) continue;
            if (!frostScratch.Add(enemyHealth)) continue;

            EnemyStatusManager status = EnemyStatusManager.GetOrAdd(enemyHealth);
            if (status != null)
            {
                status.ApplyStatus("Ice", stacks);
                chilled++;
            }
        }

        // Only when something was caught: an empty pulse every 0.4s would just be noise.
        if (chilled > 0 && frostPulseFeedback != null)
        {
            frostPulseFeedback.PlayFeedbacks(center);
        }
    }
}
