using System;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Destructible banner carried above the Bannerman's head.
///     Can be targeted and shot independently of the bannerman. When destroyed,
///     it disables the buff aura immediately.
/// </summary>
public class DestructibleBanner : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 25f;
    [SerializeField] private GameObject bannerVisual;
    [Tooltip("Optional: played at the banner when it breaks. Nothing is authored yet.")]
    [SerializeField] private MMF_Player breakFeedback;

    public event Action OnBannerDestroyed;

    private float currentHealth;
    private bool isDestroyed = false;
    private Collider bannerCollider;

    public bool IsDestroyed => isDestroyed;

    private void Awake()
    {
        currentHealth = maxHealth;
        bannerCollider = GetComponent<Collider>();
    }

    public void Initialize(float health)
    {
        maxHealth = health;
        currentHealth = health;
        isDestroyed = false;
        if (bannerCollider != null)
        {
            bannerCollider.enabled = true;
        }
        if (bannerVisual != null)
        {
            bannerVisual.SetActive(true);
        }
    }

    public void ReceiveDamage(Damage damage)
    {
        if (isDestroyed || damage == null) return;

        currentHealth -= damage.value;
        if (currentHealth <= 0f)
        {
            BreakBanner();
        }
    }

    private void BreakBanner()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        if (bannerCollider != null)
        {
            bannerCollider.enabled = false;
        }

        if (bannerVisual != null)
        {
            bannerVisual.SetActive(false);
        }

        if (breakFeedback != null)
        {
            breakFeedback.PlayFeedbacks(transform.position);
        }

        OnBannerDestroyed?.Invoke();
    }
}
