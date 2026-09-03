using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
///     Interface for any object or station in the game world that the player can interact with via the 'E' key.
/// </summary>
public interface IInteractable
{
    string PromptText { get; }
    bool CanInteract { get; }
    Vector3 InteractionPosition { get; }
    void Interact(Player player);
}

/// <summary>
///     Generic component for interactable world objects (Gates, Wells, Merchants, Pedestals, etc.).
///     Can be configured in Inspector with a prompt string, range, and UnityEvents.
/// </summary>
public class Interactable : MonoBehaviour, IInteractable
{
    [Tooltip("Prompt text displayed to the player, e.g. 'Rest', 'Drink from Well', 'Open Shop'.")]
    [SerializeField] private string promptText = "Interact";

    [Tooltip("Maximum distance from player to interact if no trigger collider is used.")]
    [SerializeField] private float interactionRadius = 3.5f;

    [Tooltip("Whether interaction is currently enabled.")]
    [SerializeField] private bool isInteractable = true;

    [Tooltip("Optional transform where interaction position is measured from. Defaults to this transform.")]
    [SerializeField] private Transform interactionAnchor;

    [Header("Events")]
    [SerializeField] private UnityEvent<Player> onInteracted;

    public event Action<Player> OnInteractedEvent;

    public string PromptText
    {
        get => promptText;
        set => promptText = value;
    }

    public bool CanInteract
    {
        get => isInteractable && gameObject.activeInHierarchy;
        set => isInteractable = value;
    }

    public Vector3 InteractionPosition => interactionAnchor != null ? interactionAnchor.position : transform.position;
    public float InteractionRadius => interactionRadius;

    public void Interact(Player player)
    {
        if (!CanInteract) return;

        onInteracted?.Invoke(player);
        OnInteractedEvent?.Invoke(player);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(InteractionPosition, interactionRadius);
    }
}
