using System;
using UnityEngine;

/// <summary>
///     Attached to an enemy while shielded by a Bubbler.
///     Intercepts incoming damage via Health.TryBlockDamage, plays deflection SFX,
///     and manages a visual 2m-radius sphere around the shielded enemy.
///     If an arrow projectile or melee sweep hits inside the bubble sphere, damage is negated.
/// </summary>
public class BubbleShield : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private BubbleShieldSO data;

    private GameObject bubbleVisualObj;
    private Transform caster;
    private Action onShieldBroken;
    private bool isAttached;

    public Health TargetHealth => health;
    public Transform Caster => caster;

    private void Awake()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
    }

    private float currentShieldHp;

    /// <summary>
    ///     Initializes and activates the bubble shield on this target.
    /// </summary>
    public void Initialize(BubbleShieldSO shieldData, Transform casterTransform, Action onBrokenCallback)
    {
        data = shieldData;
        caster = casterTransform;
        onShieldBroken = onBrokenCallback;
        currentShieldHp = data != null ? data.shieldHealth : 40f;

        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (health != null && !isAttached)
        {
            health.TryBlockDamage += HandleTryBlockDamage;
            health.OnDied += HandleTargetDied;
            isAttached = true;
        }

        CreateBubbleVisual();
    }

    private void CreateBubbleVisual()
    {
        if (bubbleVisualObj != null) return;

        float radius = data != null ? data.radius : 2.0f;

        bubbleVisualObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bubbleVisualObj.name = "BubbleShield_Sphere";
        bubbleVisualObj.transform.SetParent(transform, false);
        bubbleVisualObj.transform.localPosition = new Vector3(0f, 1.0f, 0f);
        bubbleVisualObj.transform.localScale = Vector3.one * (radius * 2.0f);

        // Configure collider: trigger so it does not interfere with NavMeshAgent physics
        Collider col = bubbleVisualObj.GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        // Apply material
        if (data != null && data.bubbleMaterial != null)
        {
            Renderer rend = bubbleVisualObj.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = data.bubbleMaterial;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
    }

    private bool HandleTryBlockDamage(Damage damage)
    {
        // Block player attacks while absorbing damage into the bubble shield's health
        if (damage == null) return false;

        if (damage.IsPlayerOwned)
        {
            currentShieldHp -= damage.value;

            // Deflection sound or break sound
            if (currentShieldHp <= 0f)
            {
                if (data != null && data.shieldBreakSfx != null)
                {
                    AudioSource.PlayClipAtPoint(data.shieldBreakSfx, transform.position, data.blockSfxVolume);
                }
                else if (data != null && data.blockSfx != null)
                {
                    AudioSource.PlayClipAtPoint(data.blockSfx, transform.position, data.blockSfxVolume);
                }

                if (data != null && data.shieldBreakVfxPrefab != null)
                {
                    Instantiate(data.shieldBreakVfxPrefab, transform.position + Vector3.up * 1f, Quaternion.identity);
                }

                // Shield broken! Destroy shield and notify caster
                CollapseShield();
                return true;
            }

            // Play deflection sound
            if (data != null && data.blockSfx != null)
            {
                AudioSource.PlayClipAtPoint(data.blockSfx, transform.position, data.blockSfxVolume);
            }

            // Punch scale animation on the bubble visual to give juicy impact feel
            if (bubbleVisualObj != null)
            {
                LeanTween.cancel(bubbleVisualObj);
                float baseScale = (data != null ? data.radius : 2.0f) * 2.0f;
                bubbleVisualObj.transform.localScale = Vector3.one * (baseScale * 1.15f);
                LeanTween.scale(bubbleVisualObj, Vector3.one * baseScale, 0.25f).setEaseOutQuad();
            }

            // Return true so the shielded enemy's health is protected
            return true;
        }

        return false;
    }

    private void HandleTargetDied()
    {
        CollapseShield();
    }

    /// <summary>
    ///     Collapses the bubble shield, unsubscribes hooks, and removes this component.
    /// </summary>
    public void CollapseShield()
    {
        if (isAttached && health != null)
        {
            health.TryBlockDamage -= HandleTryBlockDamage;
            health.OnDied -= HandleTargetDied;
            isAttached = false;
        }

        if (bubbleVisualObj != null)
        {
            if (Application.isPlaying)
            {
                Destroy(bubbleVisualObj);
            }
            else
            {
                DestroyImmediate(bubbleVisualObj);
            }
            bubbleVisualObj = null;
        }

        onShieldBroken?.Invoke();
        onShieldBroken = null;

        if (Application.isPlaying)
        {
            Destroy(this);
        }
        else
        {
            DestroyImmediate(this);
        }
    }

    private void OnDestroy()
    {
        if (isAttached && health != null)
        {
            health.TryBlockDamage -= HandleTryBlockDamage;
            health.OnDied -= HandleTargetDied;
            isAttached = false;
        }

        if (bubbleVisualObj != null)
        {
            if (Application.isPlaying)
            {
                Destroy(bubbleVisualObj);
            }
            else
            {
                DestroyImmediate(bubbleVisualObj);
            }
        }
    }
}
