using UnityEngine;

/// <summary>
///     Station 1 in the Rest Area: The Well.
///     Restores up to 20 HP to the player (1 use per visit).
/// </summary>
[RequireComponent(typeof(Interactable))]
public class WellStation : MonoBehaviour
{
    [SerializeField] private float healAmount = 20f;
    [SerializeField] private AudioClip drinkSfx;
    [SerializeField] private GameObject splashVfxPrefab;

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

        if (drinkSfx != null)
        {
            AudioSource.PlayClipAtPoint(drinkSfx, transform.position, 1.0f);
        }

        if (splashVfxPrefab != null)
        {
            Instantiate(splashVfxPrefab, transform.position + Vector3.up * 1f, Quaternion.identity);
        }

        interactable.PromptText = "Well is Dry (Depleted)";
        interactable.CanInteract = false;

        Debug.Log($"[WellStation] Player drank from the well and restored {healAmount} HP!");
    }
}
