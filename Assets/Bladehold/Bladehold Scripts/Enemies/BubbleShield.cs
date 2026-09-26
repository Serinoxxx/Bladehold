using System;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Attached to an enemy while shielded by a Bubbler.
///     Intercepts incoming damage via Health.TryBlockDamage, plays the caster's block/break
///     feedbacks, and manages a visual 2m-radius sphere around the shielded enemy.
///     If an arrow projectile or melee sweep hits inside the bubble sphere, damage is negated.
/// </summary>
public class BubbleShield : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private BubbleShieldSO data;

    private GameObject bubbleVisualObj;
    private Transform caster;
    private MMF_Player blockFeedback;
    private MMF_Player breakFeedback;
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
    ///     Initializes and activates the bubble shield on this target. This component is added at
    ///     runtime, so its feedbacks live on the caster's prefab and are handed in here. With no
    ///     break feedback, a break plays the block feedback.
    /// </summary>
    public void Initialize(BubbleShieldSO shieldData, Transform casterTransform, Action onBrokenCallback,
        MMF_Player blockFeedbackPlayer = null, MMF_Player breakFeedbackPlayer = null)
    {
        data = shieldData;
        caster = casterTransform;
        blockFeedback = blockFeedbackPlayer;
        breakFeedback = breakFeedbackPlayer;
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

        if (data == null || data.bubbleVisualPrefab == null)
        {
            // The shield still blocks damage; it's just invisible.
            Debug.LogError("[BubbleShield] BubbleShieldSO.bubbleVisualPrefab is not assigned.", data);
            return;
        }

        float radius = data.radius;
        bubbleVisualObj = Instantiate(data.bubbleVisualPrefab, transform);
        bubbleVisualObj.transform.localPosition = new Vector3(0f, 1.0f, 0f);
        bubbleVisualObj.transform.localScale = Vector3.one * (radius * 2.0f);
    }

    private bool HandleTryBlockDamage(Damage damage)
    {
        // Block player attacks while absorbing damage into the bubble shield's health
        if (damage == null) return false;

        if (damage.IsPlayerOwned)
        {
            currentShieldHp -= damage.value;

            if (currentShieldHp <= 0f)
            {
                MMF_Player popFeedback = breakFeedback != null ? breakFeedback : blockFeedback;
                if (popFeedback != null)
                {
                    popFeedback.PlayFeedbacks(transform.position + Vector3.up * 1f);
                }

                // Shield broken! Destroy shield and notify caster
                CollapseShield();
                return true;
            }

            if (blockFeedback != null)
            {
                blockFeedback.PlayFeedbacks(transform.position + Vector3.up * 1f);
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
