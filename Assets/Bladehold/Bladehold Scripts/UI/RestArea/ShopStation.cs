using UnityEngine;

/// <summary>
///     Station 2 in the Rest Area: The Merchant Shop.
///     Interacting with the merchant opens the ShopUI.
/// </summary>
[RequireComponent(typeof(Interactable))]
public class ShopStation : MonoBehaviour
{
    private Interactable interactable;

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        interactable.PromptText = "Open Shop";
        interactable.OnInteractedEvent += HandleOpenShop;
    }

    private void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.OnInteractedEvent -= HandleOpenShop;
        }
    }

    private void HandleOpenShop(Player player)
    {
        if (ShopUI.Instance != null)
        {
            ShopUI.Instance.OpenShop();
        }
        else
        {
            Debug.LogWarning("[ShopStation] ShopUI.Instance not found in scene!");
        }
    }
}
