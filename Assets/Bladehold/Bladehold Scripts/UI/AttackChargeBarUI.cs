using TMPro;
using UnityEngine;
using MoreMountains.Tools;

/// <summary>
///     HUD bar that displays the charge level and progress of the player's melee attack.
///     Polls <see cref="PlayerAttack" /> and updates an <see cref="MMProgressBar" /> while the attack is held.
///     Remains fully charged when max charge level is reached and stays visible until the attack button is released.
/// </summary>
public class AttackChargeBarUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("MMProgressBar component that visually displays attack charge.")]
    [SerializeField] private MMProgressBar progressBar;

    [Tooltip("CanvasGroup used to smoothly show or hide the attack charge bar.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("Optional TextMeshProUGUI label displaying charge status.")]
    [SerializeField] private TextMeshProUGUI chargeLabel;

    [Header("Target")]
    [Tooltip("PlayerAttack script to monitor. Automatically bound if left unassigned.")]
    [SerializeField] private PlayerAttack playerAttack;

    private bool anyError;

    private void OnValidate()
    {
        if (progressBar == null)
        {
            progressBar = GetComponentInChildren<MMProgressBar>();
        }
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }

    private void Start()
    {
        if (progressBar == null || canvasGroup == null)
        {
            Debug.LogError("AttackChargeBarUI: Missing UI references (MMProgressBar or CanvasGroup).", this);
            anyError = true;
        }

        if (playerAttack == null)
        {
            TryBindPlayerAttack();
        }

        if (anyError) return;

        // Hide bar by default
        canvasGroup.alpha = 0f;
        if (chargeLabel != null)
        {
            chargeLabel.text = "CHARGING...";
        }
    }

    private void TryBindPlayerAttack()
    {
        if (playerAttack != null) return;

        if (Player.Instance != null)
        {
            playerAttack = Player.Instance.GetComponent<PlayerAttack>();
        }
        if (playerAttack == null)
        {
            playerAttack = FindFirstObjectByType<PlayerAttack>();
        }
    }

    private void Update()
    {
        if (anyError) return;

        if (playerAttack == null)
        {
            TryBindPlayerAttack();
        }

        bool isMeleeCharging = playerAttack != null && playerAttack.IsCharging && playerAttack.MaxChargeLevels > 0;
        IChargedAimWeapon activeRangedWeapon = AimWeaponResolver.Resolve(null);
        bool isRangedCharging = activeRangedWeapon != null && activeRangedWeapon.IsCharging;

        // Hide bar if neither melee nor ranged weapon is charging
        if (!isMeleeCharging && !isRangedCharging)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
            return;
        }

        float currentCharge = 0f;
        float maxCharge = 0f;

        if (isMeleeCharging)
        {
            currentCharge = playerAttack.CurrentChargeTime;
            maxCharge = playerAttack.MaxChargeTime;
        }
        else if (isRangedCharging)
        {
            currentCharge = activeRangedWeapon.CurrentChargeTime;
            maxCharge = activeRangedWeapon.MaxChargeTime;
        }

        // Show bar while charging
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }

        if (progressBar != null)
        {
            progressBar.UpdateBar(currentCharge, 0f, maxCharge);
        }

        if (chargeLabel != null)
        {
            if (currentCharge >= maxCharge && maxCharge > 0f)
            {
                chargeLabel.text = "MAX CHARGE";
            }
            else
            {
                chargeLabel.text = "CHARGING...";
            }
        }
    }
}
