using MoreMountains.Tools;
using UnityEngine;

/// <summary>
///     A white health bar for a <see cref="BulwarkShield" />, drawn just above the enemy's own health bar.
///     Listens to the shield's <see cref="BulwarkShield.OnBlocked" /> / <see cref="BulwarkShield.OnBroken" />;
///     the shield knows nothing about it. Shield hits never touch <see cref="Health" />, so without this the
///     player gets no read on how close the shield is to breaking.
///     Builds its own <see cref="MMHealthBar" /> at runtime from the enemy's existing one (same size, padding
///     and display-on-hit timing, white fill), so the prefab needs only this component. Like the HP bar it
///     shows for a moment on each hit and hides at zero.
/// </summary>
public class BulwarkShieldBar : MonoBehaviour
{
    [SerializeField] private BulwarkShield shield;
    [Tooltip("The enemy's health bar, copied for size/timing. Auto-found in the enemy's hierarchy.")]
    [SerializeField] private MMHealthBar sourceBar;
    [SerializeField] private Color fillColor = Color.white;
    [SerializeField] private Color delayedColor = new Color(0.65f, 0.65f, 0.65f, 1f);
    [Tooltip("Height (m) above the head; keep it above the HP bar's own height so the two bars stack.")]
    [SerializeField, Min(0f)] private float heightAboveHead = 0.38f;

    private static readonly System.Reflection.FieldInfo ProgressBarField =
        typeof(MMHealthBar).GetField("_progressBar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    private MMHealthBar bar;
    private Transform anchor;
    private Transform headBone;
    private Health health;
    private float maxHp;
    private bool anyError;

    private void OnValidate()
    {
        if (shield == null) shield = GetComponentInChildren<BulwarkShield>(true);
        if (sourceBar == null) sourceBar = GetComponentInChildren<MMHealthBar>(true);
    }

    private void Start()
    {
        if (shield == null) shield = GetComponentInChildren<BulwarkShield>(true);
        if (sourceBar == null) sourceBar = GetComponentInChildren<MMHealthBar>(true);
        if (shield == null)
        {
            Debug.LogError($"[BulwarkShieldBar] {name}: no BulwarkShield in this hierarchy.", this);
            anyError = true;
        }
        if (sourceBar == null)
        {
            Debug.LogError($"[BulwarkShieldBar] {name}: no MMHealthBar in this hierarchy to copy.", this);
            anyError = true;
        }
        if (anyError) return;

        health = GetComponentInParent<Health>();
        headBone = ResolveHeadBone();
        maxHp = Mathf.Max(shield.MaxHp, shield.CurrentHp, 1f);
        BuildBar();

        shield.OnBlocked += HandleBlocked;
        shield.OnBroken += HandleBroken;
        if (health != null) health.OnDied += HandleBroken;
    }

    private void OnDestroy()
    {
        if (shield != null)
        {
            shield.OnBlocked -= HandleBlocked;
            shield.OnBroken -= HandleBroken;
        }
        if (health != null) health.OnDied -= HandleBroken;

        // MMHealthBar draws its bar as a separate root object and never destroys it.
        if (bar != null && ProgressBarField != null && ProgressBarField.GetValue(bar) is MMProgressBar drawn && drawn != null)
        {
            Destroy(drawn.gameObject);
        }
    }

    private void LateUpdate()
    {
        if (anyError || anchor == null) return;
        Transform follow = headBone != null ? headBone : transform;
        anchor.position = follow.position + Vector3.up * heightAboveHead;
    }

    private void BuildBar()
    {
        var holder = new GameObject("ShieldHealthBar");
        holder.SetActive(false);
        anchor = holder.transform;
        anchor.SetParent(transform, false);
        LateUpdate();

        bar = holder.AddComponent<MMHealthBar>();
        // Inactive, so Awake (which draws the bar) waits until the copied settings are in place.
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(sourceBar), bar);
        bar.HealthBarType = MMHealthBar.HealthBarTypes.Drawn;
        bar.HealthBarOffset = Vector3.zero;
        bar.AlwaysVisible = false;
        bar.HideBarAtZero = true;
        bar.ForegroundColor = Solid(fillColor);
        bar.DelayedColor = Solid(delayedColor);
        holder.SetActive(true);
    }

    private void HandleBlocked(Damage damage, Vector3 hitPos)
    {
        if (bar == null) return;
        bar.UpdateBar(Mathf.Max(shield.CurrentHp, 0f), 0f, maxHp, show: true);
    }

    private void HandleBroken()
    {
        if (bar == null || !bar.isActiveAndEnabled) return;
        bar.UpdateBar(0f, 0f, maxHp, show: false);
        bar.ShowBar(false);
    }

    private static Gradient Solid(Color c)
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
            new[] { new GradientAlphaKey(c.a, 0f), new GradientAlphaKey(c.a, 1f) });
        return g;
    }

    private Transform ResolveHeadBone()
    {
        Animator animator = GetComponentInChildren<Animator>(true);
        if (animator != null && animator.isHuman)
        {
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null) return head;
        }
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(child.name, "Head", System.StringComparison.OrdinalIgnoreCase)) return child;
        }
        return null;
    }
}
