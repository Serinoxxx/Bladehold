using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Storm Cloud spawned overhead by Tempest Catapults upon impact.
///     Hovers above the battlefield and periodically calls down lightning strikes on enemies in radius.
/// </summary>
public class CatapultStormCloud : MonoBehaviour
{
    [SerializeField] private float radius = 6.0f;
    [SerializeField] private float duration = 8.0f;
    [SerializeField] private float strikeInterval = 0.75f;
    [SerializeField] private float strikeDamage = 35f;
    [SerializeField] private GameObject cloudVisualPrefab;
    [SerializeField] private GameObject strikeVfxPrefab;
    [SerializeField] private AudioClip strikeSfx;

    private readonly Collider[] hitBuffer = new Collider[32];
    private float aliveTime = 0f;
    private float nextStrikeTime = 0f;
    private GameObject visualInstance;

    public float Radius => radius;
    public float Duration => duration;
    public float StrikeDamage => strikeDamage;

    public static CatapultStormCloud Spawn(Vector3 impactPosition, float rad = 6.0f, float dur = 8.0f, float damage = 35f, GameObject cloudPrefab = null, GameObject strikeVfx = null, AudioClip strikeAudio = null)
    {
        Vector3 spawnPos = impactPosition + Vector3.up * 3.5f;
        GameObject cloudObj = new GameObject("CatapultStormCloud");
        cloudObj.transform.position = spawnPos;

        CatapultStormCloud cloud = cloudObj.AddComponent<CatapultStormCloud>();
        cloud.Init(rad, dur, damage, cloudPrefab, strikeVfx, strikeAudio);
        return cloud;
    }

    public void Init(float rad, float dur, float damage, GameObject cloudPrefab = null, GameObject strikeVfx = null, AudioClip strikeAudio = null)
    {
        radius = rad;
        duration = dur;
        strikeDamage = damage;
        cloudVisualPrefab = cloudPrefab;
        strikeVfxPrefab = strikeVfx;
        strikeSfx = strikeAudio;

        CreateCloudVisual();
    }

    private void Start()
    {
        if (visualInstance == null)
        {
            CreateCloudVisual();
        }
        nextStrikeTime = Time.time + 0.3f; // quick initial strike
    }

    private void CreateCloudVisual()
    {
        if (visualInstance != null) return;

        if (cloudVisualPrefab != null)
        {
            visualInstance = Instantiate(cloudVisualPrefab, transform.position, Quaternion.identity, transform);
        }
        else
        {
            // Try load generic cloud or create procedural puff
            GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            puff.name = "StormCloudSphere";
            puff.transform.SetParent(transform, false);
            puff.transform.localScale = new Vector3(radius * 1.5f, 1.2f, radius * 1.5f);

            Collider col = puff.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            if (puff.TryGetComponent(out Renderer rend))
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.2f, 0.25f, 0.35f, 0.75f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
                rend.material = mat;
            }

            visualInstance = puff;
        }
    }

    private void Update()
    {
        aliveTime += Time.deltaTime;
        if (aliveTime >= duration)
        {
            Destroy(gameObject);
            return;
        }

        // Slight drift / wobble
        transform.position += new Vector3(Mathf.Sin(Time.time * 1.5f) * 0.15f, Mathf.Cos(Time.time * 2f) * 0.08f, 0f) * Time.deltaTime;

        if (Time.time >= nextStrikeTime)
        {
            nextStrikeTime = Time.time + strikeInterval;
            PerformLightningStrike();
        }
    }

    private void PerformLightningStrike()
    {
        Vector3 groundPos = transform.position - Vector3.up * 3.5f;
        int count = Physics.OverlapSphereNonAlloc(groundPos, radius, hitBuffer);
        List<Health> eligibleTargets = new List<Health>();

        for (int i = 0; i < count; i++)
        {
            Collider col = hitBuffer[i];
            if (col == null) continue;

            if (Player.Instance != null && (col.transform.root == Player.Instance.transform.root || col.gameObject == Player.Instance.gameObject))
            {
                continue;
            }

            Health h = col.GetComponentInParent<Health>();
            if (h != null && !h.IsDead && !eligibleTargets.Contains(h))
            {
                eligibleTargets.Add(h);
            }
        }

        if (eligibleTargets.Count == 0) return;

        // Pick 1 or 2 targets
        int strikeCount = Mathf.Min(eligibleTargets.Count, Random.Range(1, 3));
        for (int s = 0; s < strikeCount; s++)
        {
            int idx = Random.Range(0, eligibleTargets.Count);
            Health target = eligibleTargets[idx];
            eligibleTargets.RemoveAt(idx);

            StrikeTarget(target);
        }
    }

    private void StrikeTarget(Health target)
    {
        if (target == null || target.IsDead) return;

        Vector3 strikePoint = target.transform.position + Vector3.up * 0.8f;
        float actualDamage = strikeDamage;
        if (Player.Instance != null && Player.Instance.Stats != null)
        {
            actualDamage *= Player.Instance.Stats.GetValue(StatType.AllDamageMultiplier);
        }

        Damage damage = new Damage
        {
            value = actualDamage,
            type = DamageType.elemental,
            elementId = "Lightning",
            sourcePosition = transform.position,
            source = Player.Instance != null ? Player.Instance.Damageable : null,
            isPlayerDamage = true
        };

        target.ReceiveDamage(damage);
        EnemyStatusManager.GetOrAdd(target)?.ApplyStatus("Lightning");

        // Visual lightning bolt
        if (strikeVfxPrefab != null)
        {
            Instantiate(strikeVfxPrefab, strikePoint, Quaternion.identity);
        }
        else if (ElementalEffectsManager.Instance != null && ElementalEffectsManager.Instance.superconductorVfx != null)
        {
            Instantiate(ElementalEffectsManager.Instance.superconductorVfx, strikePoint, Quaternion.identity);
        }

        // SFX
        if (strikeSfx != null)
        {
            AudioSource.PlayClipAtPoint(strikeSfx, strikePoint, 0.8f);
        }
    }

    private void OnDestroy()
    {
        if (visualInstance != null)
        {
            Destroy(visualInstance);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.8f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(transform.position - Vector3.up * 3.5f, radius);
    }
#endif
}
