using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Station 1 in the Rest Area: The Well.
///     Restores up to 20 HP to the player (1 use per visit).
/// </summary>
[RequireComponent(typeof(Interactable))]
public class WellStation : MonoBehaviour
{
    [SerializeField] private float healAmount = 20f;
    [Tooltip("Played at the well when the player drinks (healing sound + heal burst).")]
    [SerializeField] private MMF_Player drinkFeedback;

    private Interactable interactable;
    private bool usedThisVisit = false;

    public void Initialize()
    {
        if (interactable == null)
        {
            interactable = GetComponent<Interactable>();
            if (interactable != null)
            {
                interactable.PromptText = $"Drink from Well (+{Mathf.RoundToInt(healAmount)} HP)";
                interactable.OnInteractedEvent += HandleDrink;
            }
        }
    }

    private void Start()
    {
        if (drinkFeedback == null) Debug.LogError("[WellStation] drinkFeedback is not assigned.", this);
    }

    private void Awake()
    {
        Initialize();
    }

    public void Drink(Player player)
    {
        HandleDrink(player);
    }

    private void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.OnInteractedEvent -= HandleDrink;
        }
    }

    private void HandleDrink(Player player)
    {
        if (usedThisVisit) return;
        usedThisVisit = true;

        Health h = player != null ? (player.Health != null ? player.Health : player.GetComponent<Health>()) : null;
        if (h != null)
        {
            h.Heal(healAmount);
        }

        if (drinkFeedback != null)
        {
            drinkFeedback.PlayFeedbacks(transform.position);
        }

        interactable.PromptText = "Well is Dry (Depleted)";
        interactable.CanInteract = false;

        Debug.Log($"[WellStation] Player drank from the well and restored {healAmount} HP!");
    }
}
