using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using UnityEngine;

/// <summary>
///     Keeps the sword swinging from horseback. On foot, the vendored Synty controller fires the
///     <c>StartAttack</c> trigger / <c>IsHoldingAttack</c> bool on attack input — but that
///     controller is disabled while mounted (see <see cref="PlayerMount" />), so this component
///     re-fires the same animator params from the same <see cref="InputReader" /> events while in
///     the saddle. Everything downstream is untouched: <see cref="PlayerAttack" /> still times the
///     hold for charge levels, and the damage still comes from the attack clip's animation event
///     (<see cref="AnimationEvents.OneHandedSwordAttack" /> → <see cref="DamageTrigger.Activate" />).
///     Skipped while the bow aims, mirroring <see cref="PlayerBow" />'s swing suppression.
/// </summary>
public class MountedCombat : MonoBehaviour
{
    [SerializeField] private PlayerMount mount;
    [SerializeField] private InputReader inputReader;
    [SerializeField] private PlayerBow bow;
    [SerializeField] private PlayerThrownAxe thrownAxe;
    [SerializeField] private PlayerWand wand;
    [Tooltip("The player rig's Animator. Synty rigs keep it on a child.")]
    [SerializeField] private Animator animator;

    private int startAttackHash;
    private int isHoldingAttackHash;
    private bool subscribed;
    private bool anyError = false;

    private void OnValidate()
    {
        if (mount == null)
        {
            mount = GetComponent<PlayerMount>();
        }
        if (inputReader == null)
        {
            inputReader = GetComponentInChildren<InputReader>();
        }
        if (bow == null)
        {
            bow = GetComponent<PlayerBow>();
        }
        if (thrownAxe == null)
        {
            thrownAxe = GetComponent<PlayerThrownAxe>();
        }
        if (wand == null)
        {
            wand = GetComponent<PlayerWand>();
        }
        if (animator == null)
        {
            // Synty rigs keep the Animator on a child model object.
            animator = GetComponentInChildren<Animator>();
        }
    }

    private void Start()
    {
        if (mount == null)
        {
            Debug.LogError("PlayerMount component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (inputReader == null)
        {
            Debug.LogError("InputReader is not assigned or found; mounted attacks can't read input.");
            anyError = true;
        }
        if (animator == null)
        {
            Debug.LogError("Player Animator is not assigned or found on a child.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        startAttackHash = Animator.StringToHash("StartAttack");
        isHoldingAttackHash = Animator.StringToHash("IsHoldingAttack");

        Subscribe();
    }

    private void OnEnable()
    {
        if (!anyError && inputReader != null)
        {
            Subscribe();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (subscribed || inputReader == null)
        {
            return;
        }
        inputReader.onAttackActivated += HandleAttackPressed;
        inputReader.onAttackDeactivated += HandleAttackReleased;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || inputReader == null)
        {
            return;
        }
        inputReader.onAttackActivated -= HandleAttackPressed;
        inputReader.onAttackDeactivated -= HandleAttackReleased;
        subscribed = false;
    }

    private void HandleAttackPressed()
    {
        if (anyError || !mount.IsMounted) return;
        if ((bow != null && bow.IsAiming) || (thrownAxe != null && thrownAxe.IsAiming) || (wand != null && wand.IsAiming)) return;

        animator.SetTrigger(startAttackHash);
        animator.SetBool(isHoldingAttackHash, true);
    }

    private void HandleAttackReleased()
    {
        if (anyError || !mount.IsMounted) return;

        animator.SetBool(isHoldingAttackHash, false);
    }
}
