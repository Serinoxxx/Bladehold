using MoreMountains.Tools;
using UnityEngine;

/// <summary>
///     Binds an <see cref="MMHealthBar" /> to a <see cref="Health" />: refreshes the bar whenever
///     health changes. It listens to <see cref="Health.OnHealthChanged" />; Health stays unaware of
///     the bar. Hiding at zero, lerping and bump-on-change are all handled by the MMHealthBar itself.
///     The bar follows the character's head bone so it stays aligned during animation and ragdolls.
///     Between hits the bar is hidden, so this and the MMHealthBar go to sleep (disabled) once it has
///     hidden itself, and wake on the next health change: 300 idle goblins otherwise cost ~1.4 ms a
///     frame re-evaluating colours and following a head nobody can see.
/// </summary>
public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private MMHealthBar healthBar;

    [SerializeField] private bool followHead;
    [SerializeField] private Transform headBone;
    [SerializeField, Min(0f)] private float heightAboveHead = 0.2f;

    private bool anyError = false;
    private float sleepAfter;

    // MMHealthBar draws its bar as a separate root object (billboard container) and never destroys it.
    private static readonly System.Reflection.FieldInfo ProgressBarField =
        typeof(MMHealthBar).GetField("_progressBar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    // Grace past MMHealthBar.DisplayDurationOnHit so its own Update gets to hide the bar first.
    private const float SleepGraceSeconds = 0.25f;

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponentInParent<Health>();
        }

        if (healthBar == null)
        {
            healthBar = GetComponent<MMHealthBar>();
        }

        if (headBone == null && followHead)
        {
            headBone = ResolveHeadBone();
        }
    }

    private void Start()
    {
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }

        if (healthBar == null)
        {
            Debug.LogError("MMHealthBar component is not assigned or found on the GameObject.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        if (headBone == null && followHead)
        {
            headBone = ResolveHeadBone();
            if (headBone == null)
            {
                Debug.LogWarning(
                    $"[{nameof(HealthBarUI)}] No head transform found for '{health.name}'. " +
                    "The health bar will retain its authored local position.",
                    this);
            }
        }

        health.OnHealthChanged += Refresh;
        health.OnDied += HandleDied;

        // Start-order safety: if Health.Start already ran, its initial OnHealthChanged fired before we
        // subscribed — refresh now; if it hasn't run yet, its event will overwrite this shortly.
        Refresh();
    }

    private void OnDestroy()
    {
        DestroyDetachedBar();
        if (health != null)
        {
            health.OnHealthChanged -= Refresh;
            health.OnDied -= HandleDied;
        }
    }

    /// <summary>
    ///     A destroyed enemy (corpse despawn, prewarm rehearsal) would otherwise leave its bar floating at the
    ///     last spot it followed. Only a bar outside this hierarchy is ours to remove; a nested one goes with us.
    /// </summary>
    private void DestroyDetachedBar()
    {
        if (ReferenceEquals(healthBar, null) || ProgressBarField == null || healthBar.HealthBarType == MMHealthBar.HealthBarTypes.Existing) return;
        MMProgressBar bar = ProgressBarField.GetValue(healthBar) as MMProgressBar;
        if (bar == null || bar.transform.IsChildOf(transform.root)) return;
        Destroy(bar.transform.root.gameObject);
    }

    private void LateUpdate()
    {
        FollowHead();

        if (DevConsole.Instance != null)
        {
            if (!DevConsole.Instance.enableHealthbars)
            {
                if (healthBar != null)
                {
                    healthBar.ShowBar(false);
                }
                return;
            }
        }

        if (!healthBar.AlwaysVisible && Time.unscaledTime >= sleepAfter && !healthBar.BarIsShown())
        {
            healthBar.enabled = false;
            enabled = false;
        }
    }

    private void FollowHead()
    {
        if (headBone != null && followHead)
        {
            transform.position = headBone.position + Vector3.up * heightAboveHead;
        }
    }

    private void Wake()
    {
        sleepAfter = Time.unscaledTime + healthBar.DisplayDurationOnHit + SleepGraceSeconds;
        if (!enabled)
        {
            enabled = true;
            // Enabling runs MMHealthBar.OnEnable (hides the bar); the UpdateBar that follows re-shows it.
            healthBar.enabled = true;
            FollowHead();
        }
    }

    private Transform ResolveHeadBone()
    {
        Transform searchRoot = health != null ? health.transform : transform.root;
        Animator animator = searchRoot.GetComponentInChildren<Animator>(true);
        if (animator != null && animator.isHuman)
        {
            Transform humanoidHead = animator.GetBoneTransform(HumanBodyBones.Head);
            if (humanoidHead != null)
            {
                return humanoidHead;
            }
        }

        foreach (Transform child in searchRoot.GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(child.name, "Head", System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    private void HandleDied()
    {
        if (healthBar != null)
        {
            healthBar.ShowBar(false);
        }
    }

    private void Refresh()
    {
        if (health != null && health.IsDead)
        {
            if (healthBar != null)
            {
                healthBar.ShowBar(false);
            }
            return;
        }

        Wake();
        healthBar.UpdateBar(health.CurrentHealth, 0f, health.MaxHealth, show: true);
    }
}
