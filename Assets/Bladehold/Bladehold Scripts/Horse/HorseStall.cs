using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Horse stall beside the gate: during the prep phase, if the run's warhorse has died
///     (<see cref="RunSession.MountLost" />), [E] buys a new one for <see cref="HorseReplacement.GoldCost" /> in-run gold
///     (<see cref="RunSession.ReplaceMount" />: full health and charge, summonable again). The same
///     replacement the Rest Area shop sells, available at the front line. The prompt stays up through
///     prep so the price is always readable; while the horse is alive or gold is short the press is
///     refused with the interact prompt's denied pulse. Hidden mid-wave (unless the Field Stables meta perk
///     is owned: <see cref="HorseReplacement.CanBuyNow" />) and where the scene's
///     <see cref="SceneAbilityRules" /> forbid the mount.
///     While the warhorse is dead and the stall is usable, it shows a yellow horse-head HUD waypoint
///     (as an <see cref="IWaypointSource" />) so the player knows where to buy a replacement.
/// </summary>
public class HorseStall : MonoBehaviour, IInteractable, IAffordableInteractable, IWaypointSource
{
    // The price is shared with the Summon Mount button's buy prompt: Resources/HorseReplacementConfig.
    private static int GoldCost => HorseReplacement.GoldCost;

    [Header("Interaction")]
    [Tooltip("Where the player stands to buy. Defaults to this transform.")]
    [SerializeField] private Transform interactionAnchor;
    [SerializeField] private float interactionRadius = 3.5f;

    [Header("Feedback")]
    [Tooltip("Played on a successful purchase (coins, a horse whinny).")]
    [SerializeField] private MMF_Player purchaseFeedback;

    [Header("Mount-lost waypoint")]
    [SerializeField] private Sprite mountLostIcon;
    [SerializeField] private Color mountLostTint = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Vector3 waypointOffset = new Vector3(0f, 2.5f, 0f);

    public Vector3 InteractionPosition => interactionAnchor != null ? interactionAnchor.position : transform.position;
    public float InteractionRadius => interactionRadius > 0f ? interactionRadius : 3.5f;

    // Between waves, or mid-wave too with the Field Stables meta perk: the shared rule in HorseReplacement.
    public bool CanInteract => SceneAbilityRules.MountAllowed && HorseReplacement.IsBuyWindowOpen;

    public bool CanAfford => HorseReplacement.CanAfford;

    public string PromptText
    {
        get
        {
            if (!RunSession.MountLost)
            {
                return Loc.Get("horse_stall.prompt_alive", "Warhorse · yours still lives");
            }
            if (RunSession.InRunGold < GoldCost)
            {
                return string.Format(Loc.Get("horse_stall.prompt_no_gold", "Buy a new warhorse ({0}g) · not enough gold"), GoldCost);
            }
            return string.Format(Loc.Get("horse_stall.prompt", "Buy a new warhorse ({0}g)"), GoldCost);
        }
    }

    private void OnEnable()
    {
        InteractableRegistry.Register(this);
        ObjectiveWaypointTrackerUI.RegisterSource(this);
    }

    private void OnDisable()
    {
        InteractableRegistry.Unregister(this);
        ObjectiveWaypointTrackerUI.UnregisterSource(this);
    }

    public void GetWaypointTargets(List<ObjectiveWaypointTarget> results)
    {
        // Only while a purchase is possible: the stall refuses mid-wave (without Field Stables), so a marker then would mislead.
        if (!RunSession.MountLost || !CanInteract) return;

        ObjectiveWaypointTrackerUI tracker = ObjectiveWaypointTrackerUI.Instance;
        Sprite icon = mountLostIcon != null ? mountLostIcon : tracker != null ? tracker.DefaultObjectiveIcon : null;
        results.Add(new ObjectiveWaypointTarget(transform, waypointOffset, icon, mountLostTint,
            Loc.Get("horse_stall.waypoint", "WARHORSE")));
    }

    private void Start()
    {
        // Feedback is required but never gameplay-breaking: log, don't disable the stall.
        if (purchaseFeedback == null) Debug.LogError($"[HorseStall] {name}: purchaseFeedback is not assigned.", this);
    }

    public void Interact(Player player)
    {
        // PlayerInteraction already refuses (with the denied pulse) while CanAfford is false; re-check the spend.
        if (!CanInteract || !HorseReplacement.TryBuy())
        {
            return;
        }

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
