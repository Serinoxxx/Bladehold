using UnityEngine;

/// <summary>
///     Controls the damage direction UI indicator (<c>FX_FantasyWarrior_Damage_Direction_02</c>).
///     Subscribes to <see cref="Health.OnDamaged" /> on the player. When damage is received,
///     rotates the UI element on its Z axis towards the direction of the attacker relative to the camera/player
///     and triggers the <c>Hit</c> animation trigger on its Animator.
/// </summary>
public class DamageDirectionUI : MonoBehaviour
{
    [Tooltip("The player's Health component. Auto-wired from Player.Instance if left empty.")]
    [SerializeField] private Health health;

    [Tooltip("The Animator component driving the indicator. Auto-wired if left empty.")]
    [SerializeField] private Animator animator;

    [Tooltip("The RectTransform to rotate on the Z axis. Auto-wired if left empty.")]
    [SerializeField] private RectTransform rectTransform;

    [Tooltip("The Camera used for direction calculation. Defaults to Camera.main if left empty.")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("If true, damage direction is calculated relative to the camera's view; if false, relative to player facing.")]
    [SerializeField] private bool cameraRelative = true;

    [Tooltip("Name of the Animator trigger parameter to fire when damage is taken.")]
    [SerializeField] private string hitTrigger = "Hit";

    [SerializeField] private bool enableRotations = true;

    private int hitTriggerHash;
    private bool anyError;

    private void Awake()
    {
        hitTriggerHash = Animator.StringToHash(hitTrigger);

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }
    }

    private void Start()
    {
        if (health == null && Player.Instance != null)
        {
            health = Player.Instance.Health;
        }

        if (health == null)
        {
            Player p = FindObjectOfType<Player>();
            if (p != null)
            {
                health = p.Health ?? p.GetComponent<Health>();
            }
        }

        if (health == null)
        {
            Debug.LogError("[DamageDirectionUI] Player Health reference not found — assign it or ensure Player.Instance exists.");
            anyError = true;
        }

        if (animator == null)
        {
            Debug.LogError("[DamageDirectionUI] Animator component not found.");
            anyError = true;
        }

        if (rectTransform == null)
        {
            Debug.LogError("[DamageDirectionUI] RectTransform component not found.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        health.OnDamaged += HandleDamaged;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
        }
    }

    private void HandleDamaged(Damage damage)
    {
        if (anyError || damage == null)
        {
            return;
        }

        if (animator != null)
        {
            animator.SetTrigger(hitTriggerHash);
        }

        if (!enableRotations)
        {
            return;
        }

        Vector3 attackerPos = GetAttackerPosition(damage);
        Vector3 playerPos = GetPlayerPosition();
        Vector3 dirToAttacker = attackerPos - playerPos;
        dirToAttacker.y = 0f;

        if (dirToAttacker.sqrMagnitude > 0.0001f)
        {
            dirToAttacker.Normalize();

            Vector3 fwd = Vector3.forward;
            Vector3 right = Vector3.right;

            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cameraRelative && cam != null)
            {
                fwd = cam.transform.forward;
                fwd.y = 0f;
                fwd.Normalize();

                right = cam.transform.right;
                right.y = 0f;
                right.Normalize();
            }
            else
            {
                Transform pTransform = Player.Instance != null ? Player.Instance.transform : (health != null ? health.transform : null);
                if (pTransform != null)
                {
                    fwd = pTransform.forward;
                    fwd.y = 0f;
                    fwd.Normalize();

                    right = pTransform.right;
                    right.y = 0f;
                    right.Normalize();
                }
            }

            float fwdDot = Vector3.Dot(dirToAttacker, fwd);
            float rightDot = Vector3.Dot(dirToAttacker, right);

            // In UI space, 0 deg = UP (forward), -90 deg = RIGHT, +90 deg = LEFT, 180 deg = DOWN
            float zAngle = -Mathf.Atan2(rightDot, fwdDot) * Mathf.Rad2Deg;

            if (rectTransform != null)
            {
                rectTransform.localRotation = Quaternion.Euler(0f, 0f, zAngle);
            }
        }
    }

    private Vector3 GetAttackerPosition(Damage damage)
    {
        if (damage.sourcePosition != Vector3.zero)
        {
            return damage.sourcePosition;
        }

        if (damage.source is Component sourceComp && sourceComp != null)
        {
            return sourceComp.transform.position;
        }

        if (health != null && health.LastDamageSource is Component lastComp && lastComp != null)
        {
            return lastComp.transform.position;
        }

        // Fallback if no position found: 1 unit ahead of player
        Vector3 playerPos = GetPlayerPosition();
        Vector3 playerFwd = Player.Instance != null ? Player.Instance.transform.forward : Vector3.forward;
        return playerPos + playerFwd;
    }

    private Vector3 GetPlayerPosition()
    {
        if (Player.Instance != null)
        {
            return Player.Instance.transform.position;
        }

        if (health != null)
        {
            return health.transform.position;
        }

        return Vector3.zero;
    }
}
