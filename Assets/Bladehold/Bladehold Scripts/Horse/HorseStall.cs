using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Horse stall beside the gate: during the prep phase, if the run's warhorse has died
///     (<see cref="RunSession.MountLost" />), [E] buys a new one for <see cref="goldCost" /> in-run gold
///     (<see cref="RunSession.ReplaceMount" />: full health and charge, summonable again). The same
///     replacement the Rest Area shop sells, available at the front line. The prompt stays up through
///     prep so the price is always readable; while the horse is alive or gold is short the press is
///     refused with the interact prompt's denied pulse. Hidden mid-wave and where the scene's
///     <see cref="SceneAbilityRules" /> forbid the mount.
/// </summary>
public class HorseStall : MonoBehaviour, IInteractable, IAffordableInteractable
{
    [Header("Price")]
    [Min(0)] [SerializeField] private int goldCost = 500;

    [Header("Interaction")]
    [Tooltip("Where the player stands to buy. Defaults to this transform.")]
    [SerializeField] private Transform interactionAnchor;
    [SerializeField] private float interactionRadius = 3.5f;

    [Header("Feedback")]
    [Tooltip("Played on a successful purchase (coins, a horse whinny).")]
    [SerializeField] private MMF_Player purchaseFeedback;

    private static bool IsPrep => GameLoopManager.Instance == null || GameLoopManager.Instance.IsPrepPhase;

    public Vector3 InteractionPosition => interactionAnchor != null ? interactionAnchor.position : transform.position;
    public float InteractionRadius => interactionRadius > 0f ? interactionRadius : 3.5f;

    public bool CanInteract => IsPrep && SceneAbilityRules.MountAllowed;

    public bool CanAfford => RunSession.MountLost && (goldCost <= 0 || RunSession.InRunGold >= goldCost);

    public string PromptText
    {
        get
        {
            if (!RunSession.MountLost)
            {
                return Loc.Get("horse_stall.prompt_alive", "Warhorse · yours still lives");
            }
            if (RunSession.InRunGold < goldCost)
            {
                return string.Format(Loc.Get("horse_stall.prompt_no_gold", "Buy a new warhorse ({0}g) · not enough gold"), goldCost);
            }
            return string.Format(Loc.Get("horse_stall.prompt", "Buy a new warhorse ({0}g)"), goldCost);
        }
    }

    private void OnEnable()
    {
        InteractableRegistry.Register(this);
    }

    private void OnDisable()
    {
        InteractableRegistry.Unregister(this);
    }

    private void Start()
    {
        // Feedback is required but never gameplay-breaking: log, don't disable the stall.
        if (purchaseFeedback == null) Debug.LogError($"[HorseStall] {name}: purchaseFeedback is not assigned.", this);
    }

    public void Interact(Player player)
    {
        // PlayerInteraction already refuses (with the denied pulse) while CanAfford is false; re-check the spend.
        if (!CanInteract || !RunSession.MountLost || !RunSession.TrySpendInRunGold(goldCost))
        {
            return;
        }

        RunSession.ReplaceMount();
        if (purchaseFeedback != null) purchaseFeedback.PlayFeedbacks(transform.position);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(InteractionPosition, InteractionRadius);
    }
#endif
}
