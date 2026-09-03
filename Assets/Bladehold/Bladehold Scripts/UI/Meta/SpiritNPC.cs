using UnityEngine;

/// <summary>
///     The Spirit NPC in the Meta Area scene.
///     Interacting with the Spirit opens the MetaUpgradesUI.
/// </summary>
[RequireComponent(typeof(Interactable))]
public class SpiritNPC : MonoBehaviour
{
    private Interactable interactable;

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        interactable.PromptText = "Commune with Spirit (Meta Upgrades)";
        interactable.OnInteractedEvent += HandleCommune;
    }

    private void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.OnInteractedEvent -= HandleCommune;
        }
    }

    private void HandleCommune(Player player)
    {
        if (MetaUpgradesUI.Instance != null)
        {
            MetaUpgradesUI.Instance.Open();
        }
        else
        {
            Debug.LogWarning("[SpiritNPC] MetaUpgradesUI.Instance not found!");
        }
    }
}
