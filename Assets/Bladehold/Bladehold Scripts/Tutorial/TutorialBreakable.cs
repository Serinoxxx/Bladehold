using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     A tutorial obstacle broken by the player: hanging banners (any hit), the locked door (heavy
///     attack only) and the platform ropes (arrows, placed out of sword reach). Health comes from a
///     <see cref="Health" /> on the same object (tiny <c>HealthSO</c>s under Tutorial/). With
///     <see cref="requireChargedMelee" />, <see cref="Health.TryBlockDamage" /> refuses projectiles and
///     any swing whose latched <see cref="PlayerAttack.AttackDamageMultiplier" /> is under the config's
///     heavy threshold, and after a few refusals the hint nudges the player to hold the attack.
///     On death: <see cref="breakFeedback" />, intact visuals off, broken visuals on, colliders off.
/// </summary>
[RequireComponent(typeof(Health))]
public class TutorialBreakable : MonoBehaviour
{
    [Header("Rule")]
    [Tooltip("Only a charged melee swing counts (the locked door). Arrows and taps are refused.")]
    [SerializeField] private bool requireChargedMelee;
    [SerializeField] private TutorialHint heavyNudgeHint = TutorialHint.Of("tutorial.heavy_nudge", "Too weak! Hold the attack to charge it, then release", "Attack");

    [Header("Visuals")]
    [Tooltip("Shown until broken (the whole door, the rope, the banner).")]
    [SerializeField] private GameObject intactRoot;
    [Tooltip("Optional: shown once broken (planks, torn stub). Leave empty for none.")]
    [SerializeField] private GameObject brokenRoot;
    [Tooltip("Colliders turned off on break (blockers, the hit volume). Empty = every collider on this object and its children.")]
    [SerializeField] private Collider[] collidersToDisable;

    [Header("Feedback")]
    [Tooltip("Played on break: sound, particles, impulse.")]
    [SerializeField] private MMF_Player breakFeedback;
    [Tooltip("Optional: played when a hit is refused (dull thunk, small spark). Leave empty for none.")]
    [SerializeField] private MMF_Player blockedFeedback;

    private Health health;
    private PlayerAttack playerAttack;
    private TutorialConfigSO config;
    private int blockedHits;
    private bool anyError;

    private void OnValidate()
    {
        if (health == null) health = GetComponent<Health>();
    }

    private void Awake()
    {
        health = GetComponent<Health>();
        health.TryBlockDamage += BlockIfNotAllowed;
        health.OnDied += HandleDied;
    }

    private void Start()
    {
        config = TutorialConfigSO.Load();
        if (config == null && requireChargedMelee)
        {
            Debug.LogError($"[TutorialBreakable] {name}: Resources/TutorialConfig is missing.", this);
            anyError = true;
        }
        if (intactRoot == null) { Debug.LogError($"[TutorialBreakable] {name}: intactRoot is not assigned.", this); anyError = true; }
        if (breakFeedback == null) Debug.LogError($"[TutorialBreakable] {name}: breakFeedback is not assigned.", this);
        if (brokenRoot != null) brokenRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (health == null) return;
        health.TryBlockDamage -= BlockIfNotAllowed;
        health.OnDied -= HandleDied;
    }

    private bool BlockIfNotAllowed(Damage damage)
    {
        if (!requireChargedMelee || anyError) return false;

        bool heavyEnough = !damage.isProjectile && ChargeMultiplier() >= config.heavyMinChargeMultiplier;
        if (heavyEnough) return false;

        blockedHits++;
        if (blockedFeedback != null) blockedFeedback.PlayFeedbacks(damage.sourcePosition != Vector3.zero ? Vector3.Lerp(transform.position, damage.sourcePosition, 0.3f) : transform.position);
        if (blockedHits >= config.heavyNudgeAfterBlocks && TutorialDirector.Instance != null)
        {
            TutorialDirector.Instance.SetSecondaryHint(heavyNudgeHint);
        }
        return true;
    }

    private float ChargeMultiplier()
    {
        if (playerAttack == null && Player.Instance != null)
        {
            playerAttack = Player.Instance.GetComponentInChildren<PlayerAttack>();
        }
        return playerAttack != null ? playerAttack.AttackDamageMultiplier : 0f;
    }

    private void HandleDied()
    {
        if (breakFeedback != null) breakFeedback.PlayFeedbacks(transform.position);

        Collider[] cols = collidersToDisable != null && collidersToDisable.Length > 0
            ? collidersToDisable
            : GetComponentsInChildren<Collider>();
        foreach (Collider c in cols)
        {
            if (c != null) c.enabled = false;
        }

        if (intactRoot != null) intactRoot.SetActive(false);
        if (brokenRoot != null) brokenRoot.SetActive(true);
    }
}
