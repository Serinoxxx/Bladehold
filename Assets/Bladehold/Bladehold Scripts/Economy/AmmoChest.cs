using System.Collections.Generic;
using DamageNumbersPro;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Ammo chest near the gate: each [E] press spends <see cref="goldCost" /> in-run gold for
///     <see cref="arrowsPerPurchase" /> arrows via <see cref="PlayerAmmo.AddAmmo" />. Usable any time,
///     prep or mid-wave, so a player who has emptied the quiver can run back and restock. The prompt
///     stays visible when the quiver is full or gold is short so the price is always readable; the press
///     is then refused with <see cref="deniedFeedback" />.
///     While the quiver is below <see cref="lowAmmoFraction" />, the chest nearest the player shows a HUD
///     waypoint (as an <see cref="IWaypointSource" />) so a player running dry knows where to restock.
/// </summary>
public class AmmoChest : MonoBehaviour, IInteractable, IAffordableInteractable, IWaypointSource
{
    private static readonly List<AmmoChest> Active = new List<AmmoChest>();

    [Header("Price")]
    [Min(0)] [SerializeField] private int goldCost = 50;
    [Min(1)] [SerializeField] private int arrowsPerPurchase = 5;

    [Header("Interaction")]
    [Tooltip("Where the player stands to buy. Defaults to this transform.")]
    [SerializeField] private Transform interactionAnchor;
    [SerializeField] private float interactionRadius = 3f;

    [Header("Feedback")]
    [Tooltip("Popup showing the arrows gained, e.g. \"+5 arrows\".")]
    [SerializeField] private DamageNumber purchasePopup;
    [SerializeField] private Vector3 popupOffset = new Vector3(0f, 1.5f, 0f);
    [Tooltip("Played on a successful purchase (coin clink, lid rattle).")]
    [SerializeField] private MMF_Player purchaseFeedback;
    [Tooltip("Played when the press is refused: not enough gold or quiver already full.")]
    [SerializeField] private MMF_Player deniedFeedback;

    [Header("Low-ammo waypoint")]
    [Tooltip("The nearest chest shows a HUD waypoint while ammo is below this fraction of max.")]
    [Range(0f, 1f)] [SerializeField] private float lowAmmoFraction = 0.25f;
    [SerializeField] private Sprite lowAmmoIcon;
    [SerializeField] private Color lowAmmoTint = new Color(1f, 0.75f, 0.3f, 1f);
    [SerializeField] private Vector3 waypointOffset = new Vector3(0f, 2f, 0f);

    private bool anyError;

    public Vector3 InteractionPosition => interactionAnchor != null ? interactionAnchor.position : transform.position;
    public float InteractionRadius => interactionRadius > 0f ? interactionRadius : 3f;

    private static PlayerAmmo Ammo => Player.Instance != null && Player.Instance.Ammo != null ? Player.Instance.Ammo : PlayerAmmo.Instance;

    public bool CanInteract => !anyError && Ammo != null && !Ammo.InfiniteAmmo;

    public bool CanAfford => goldCost <= 0 || RunSession.InRunGold >= goldCost;

    public string PromptText
    {
        get
        {
            PlayerAmmo ammo = Ammo;
            if (ammo != null && ammo.CurrentAmmo >= ammo.MaxAmmo)
            {
                return string.Format(Loc.Get("ammo_chest.prompt_full", "Quiver full · {0}/{1}"), ammo.CurrentAmmo, ammo.MaxAmmo);
            }
            if (goldCost == 0)
            {
                return string.Format(Loc.Get("ammo_chest.prompt_free", "Take {0} arrows"), arrowsPerPurchase);
            }
            if (RunSession.InRunGold < goldCost)
            {
                return string.Format(Loc.Get("ammo_chest.prompt_no_gold", "Buy {0} arrows ({1}g) · not enough gold"), arrowsPerPurchase, goldCost);
            }
            return string.Format(Loc.Get("ammo_chest.prompt", "Buy {0} arrows ({1}g)"), arrowsPerPurchase, goldCost);
        }
    }

    private void OnEnable()
    {
        InteractableRegistry.Register(this);
        Active.Add(this);
        ObjectiveWaypointTrackerUI.RegisterSource(this);
    }

    private void OnDisable()
    {
        InteractableRegistry.Unregister(this);
        Active.Remove(this);
        ObjectiveWaypointTrackerUI.UnregisterSource(this);
    }

    public void GetWaypointTargets(List<ObjectiveWaypointTarget> results)
    {
        PlayerAmmo ammo = Ammo;
        if (!CanInteract || ammo.MaxAmmo <= 0 || ammo.CurrentAmmo >= ammo.MaxAmmo * lowAmmoFraction) return;
        if (Player.Instance != null && NearestTo(Player.Instance.transform.position) != this) return;

        ObjectiveWaypointTrackerUI tracker = ObjectiveWaypointTrackerUI.Instance;
        Sprite icon = lowAmmoIcon != null ? lowAmmoIcon : tracker != null ? tracker.DefaultObjectiveIcon : null;
        results.Add(new ObjectiveWaypointTarget(transform, waypointOffset, icon, lowAmmoTint,
            Loc.Get("ammo_chest.waypoint_low", "LOW AMMO")));
    }

    // Only one chest marks itself, so a scene with several doesn't scatter markers.
    private static AmmoChest NearestTo(Vector3 position)
    {
        AmmoChest nearest = null;
        float bestSq = float.MaxValue;
        foreach (AmmoChest chest in Active)
        {
            if (chest == null || !chest.CanInteract) continue;
            float d = (chest.transform.position - position).sqrMagnitude;
            if (d < bestSq)
            {
                bestSq = d;
                nearest = chest;
            }
        }
        return nearest;
    }

    private void Start()
    {
        // Feedback is required but never gameplay-breaking: log, don't set anyError.
        if (purchasePopup == null) Debug.LogError($"[AmmoChest] {name}: purchasePopup is not assigned.", this);
        if (purchaseFeedback == null) Debug.LogError($"[AmmoChest] {name}: purchaseFeedback is not assigned.", this);
        if (deniedFeedback == null) Debug.LogError($"[AmmoChest] {name}: deniedFeedback is not assigned.", this);
    }

    public void Interact(Player player)
    {
        if (!CanInteract) return;

        PlayerAmmo ammo = player != null && player.Ammo != null ? player.Ammo : Ammo;
        if (ammo == null || ammo.CurrentAmmo >= ammo.MaxAmmo || !RunSession.TrySpendInRunGold(goldCost))
        {
            if (deniedFeedback != null) deniedFeedback.PlayFeedbacks(transform.position);
            return;
        }

        int added = ammo.AddAmmo(arrowsPerPurchase);
        if (purchasePopup != null && added > 0)
        {
            purchasePopup.Spawn(transform.position + popupOffset, string.Format(Loc.Get("ammo.popup_gained", "+{0} arrows"), added));
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
