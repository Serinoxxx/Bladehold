using System;
using HighlightPlus;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     A bone totem raised by Captain Mogra (phase 2+). It sends out a <see cref="HexShockwaveRing" />
///     every pulse interval and is tethered to Mogra by a visible beam. Breaking it backlashes onto him:
///     <see cref="OnBroken" /> tells the controller, which staggers him. Totems are an opportunity, not a
///     gate: he never becomes immune while they stand. Death is signalled via <see cref="Health.OnDied" />
///     and the totem crumbles itself; no kill credit or gold.
/// </summary>
[RequireComponent(typeof(Health))]
public class BoneTotem : MonoBehaviour
{
    [SerializeField] private Health health;
    [Tooltip("Where rings start and the tether attaches (near the base / the skull).")]
    [SerializeField] private Transform ringOrigin;
    [SerializeField] private Transform tetherAnchor;
    [Tooltip("Beam from this totem to Mogra. Its two positions are set every frame.")]
    [SerializeField] private LineRenderer tether;
    [Tooltip("Visuals hidden when the totem breaks.")]
    [SerializeField] private GameObject visualRoot;
    [Tooltip("Played when the totem rises.")]
    [SerializeField] private MMF_Player riseFeedback;
    [Tooltip("Charge-up before each pulse (rising hum). Plays as the glow starts.")]
    [SerializeField] private MMF_Player chargeFeedback;
    [Tooltip("Discharge as the ring leaves (the release crack).")]
    [SerializeField] private MMF_Player pulseFeedback;
    [Tooltip("Glow switched on while the totem charges a pulse (HighlightPlus, Mogra Outline profile).")]
    [SerializeField] private HighlightEffect chargeGlow;
    [Tooltip("Optional: a light ramped up over the charge. Leave empty for none.")]
    [SerializeField] private Light chargeLight;
    [Tooltip("Peak intensity of chargeLight at the moment of discharge.")]
    [SerializeField] private float chargeLightIntensity = 4f;
    [Tooltip("Played when the totem is broken.")]
    [SerializeField] private MMF_Player breakFeedback;

    private GameObject ringPrefab;
    private Transform ownerTether;
    private IDamageable ownerDamageable;
    private float pulseInterval;
    private float chargeSeconds;
    private bool charging;
    private float nextPulseTime;
    private float ringSpeed;
    private float ringMaxRadius;
    private float ringWidth;
    private float ringDamage;
    private bool initialized;
    private bool broken;

    /// <summary>Raised once when the player breaks the totem (not when it crumbles with Mogra).</summary>
    public event Action<BoneTotem> OnBroken;

    public Health Health => health;
    public bool IsBroken => broken;

    private void OnValidate()
    {
        if (health == null) health = GetComponent<Health>();
    }

    private void Awake()
    {
        if (health == null) health = GetComponent<Health>();
    }

    private void Start()
    {
        if (health == null) Debug.LogError($"{name}: BoneTotem needs a Health.", this);
        if (tether == null) Debug.LogError($"{name}: tether LineRenderer is not assigned.", this);
        if (breakFeedback == null) Debug.LogError($"{name}: breakFeedback is not assigned.", this);
        if (chargeFeedback == null) Debug.LogError($"{name}: chargeFeedback is not assigned.", this);
        if (chargeGlow == null) Debug.LogError($"{name}: chargeGlow (HighlightEffect) is not assigned; the charge-up won't glow.", this);
        SetCharging(false, 0f);
        if (health != null) health.OnDied += HandleDied;
    }

    private void OnDestroy()
    {
        if (health != null) health.OnDied -= HandleDied;
    }

    /// <summary>Configures the totem. Call right after Instantiate (before Start).</summary>
    /// <param name="chargeUpSeconds">How long the totem glows and hums before each pulse (the tell).</param>
    public void Init(float maxHealth, GameObject shockwaveRingPrefab, float firstPulseDelay, float interval, float chargeUpSeconds,
        float speed, float maxRadius, float width, float damage, Transform ownerTetherPoint, IDamageable owner)
    {
        if (health == null) health = GetComponent<Health>();
        if (health != null) health.SetMaxHealth(maxHealth);
        ringPrefab = shockwaveRingPrefab;
        pulseInterval = Mathf.Max(0.5f, interval);
        chargeSeconds = Mathf.Clamp(chargeUpSeconds, 0f, pulseInterval);
        // The first pulse still gets its full charge-up.
        nextPulseTime = Time.time + Mathf.Max(chargeSeconds, firstPulseDelay);
        ringSpeed = speed;
        ringMaxRadius = maxRadius;
        ringWidth = width;
        ringDamage = damage;
        ownerTether = ownerTetherPoint;
        ownerDamageable = owner;
        initialized = true;
        if (riseFeedback != null) riseFeedback.PlayFeedbacks(transform.position);
    }

    private void Update()
    {
        if (!initialized || broken) return;

        UpdateTether();

        float untilPulse = nextPulseTime - Time.time;
        if (!charging && untilPulse <= chargeSeconds && chargeSeconds > 0f)
        {
            charging = true;
            if (chargeFeedback != null) chargeFeedback.PlayFeedbacks(transform.position);
        }
        if (charging) SetCharging(true, chargeSeconds > 0f ? 1f - Mathf.Clamp01(untilPulse / chargeSeconds) : 1f);

        if (Time.time >= nextPulseTime)
        {
            nextPulseTime = Time.time + pulseInterval;
            charging = false;
            SetCharging(false, 0f);
            Pulse();
        }
    }

    /// <summary>Glow on (and the light ramping to its peak by <paramref name="progress" /> 0-1) while charging.</summary>
    private void SetCharging(bool on, float progress)
    {
        if (chargeGlow != null && chargeGlow.highlighted != on) chargeGlow.SetHighlighted(on);
        if (chargeLight != null)
        {
            chargeLight.enabled = on;
            chargeLight.intensity = on ? chargeLightIntensity * progress : 0f;
        }
    }

    private void UpdateTether()
    {
        if (tether == null) return;
        bool show = ownerTether != null;
        tether.enabled = show;
        if (!show) return;
        tether.positionCount = 2;
        tether.SetPosition(0, tetherAnchor != null ? tetherAnchor.position : transform.position + Vector3.up * 1.5f);
        tether.SetPosition(1, ownerTether.position);
    }

    private void Pulse()
    {
        if (ringPrefab == null)
        {
            Debug.LogError($"{name}: no shockwave ring prefab (CaptainMograSO.shockwaveRingPrefab).", this);
            return;
        }
        Vector3 origin = ringOrigin != null ? ringOrigin.position : transform.position;
        GameObject ringObj = Instantiate(ringPrefab, origin, ringPrefab.transform.rotation);
        HexShockwaveRing ring = ringObj.GetComponent<HexShockwaveRing>();
        if (ring == null)
        {
            Debug.LogError($"{name}: shockwave ring prefab '{ringPrefab.name}' has no HexShockwaveRing.", this);
            Destroy(ringObj);
            return;
        }
        ring.Launch(ringSpeed, ringMaxRadius, ringWidth, ringDamage, ownerDamageable);
        if (pulseFeedback != null) pulseFeedback.PlayFeedbacks(origin);
    }

    private void HandleDied()
    {
        if (broken) return;
        Break();
        OnBroken?.Invoke(this);
    }

    /// <summary>Mogra died or left: the totem falls apart without a backlash.</summary>
    public void Crumble()
    {
        if (broken) return;
        Break();
    }

    private void Break()
    {
        broken = true;
        charging = false;
        SetCharging(false, 0f);
        if (tether != null) tether.enabled = false;
        if (breakFeedback != null) breakFeedback.PlayFeedbacks(transform.position);
        if (visualRoot != null) visualRoot.SetActive(false);
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        Destroy(gameObject, 2f);
    }
}
