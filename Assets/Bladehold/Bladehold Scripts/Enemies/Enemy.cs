using UnityEngine;

/// <summary>
///     Marks a GameObject as an enemy and reports its death to the run's <see cref="GameStats" />
///     scoreboard. Listens to <see cref="Health.OnDied" />; Health stays unaware of scoring.
/// </summary>
public class Enemy : MonoBehaviour
{
    [SerializeField] private Health health;

    private bool anyError = false;

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }

        EnsureVulnerableSpot();
    }

    private void EnsureVulnerableSpot()
    {
        if (GetComponentInChildren<VulnerableSpot>() != null)
        {
            return;
        }

        Animator animator = GetComponentInChildren<Animator>();
        if (animator == null)
        {
            return;
        }

        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (head == null)
        {
            return;
        }

        SphereCollider collider = head.gameObject.AddComponent<SphereCollider>();
        collider.radius = 0.4f;
        collider.isTrigger = true;
        head.gameObject.AddComponent<VulnerableSpot>();
    }

    private void Start()
    {
        EnsureVulnerableSpot();

        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        health.OnDied += HandleDied;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
        }
    }

    private void HandleDied()
    {
        if (health.LastDamageSource is Component sourceComp && sourceComp.GetComponentInParent<Enemy>() != null)
        {
            return;
        }

        if (GameStats.Instance != null)
        {
            GameStats.Instance.RegisterGoblinKilled();
        }
    }
}
