using System;
using System.Collections.Generic;
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
    float InteractionRadius { get; }
    void Interact(Player player);
}

/// <summary>
///     Central registry of all currently active in-world IInteractable objects.
///     Eliminates expensive per-frame scene-wide searches and allocations in PlayerInteraction.
/// </summary>
public static class InteractableRegistry
{
    private static readonly List<IInteractable> activeInteractables = new List<IInteractable>();
    public static IReadOnlyList<IInteractable> Active => activeInteractables;

    public static void Register(IInteractable interactable)
    {
        if (interactable != null && !activeInteractables.Contains(interactable))
        {
            activeInteractables.Add(interactable);
        }
    }

    public static void Unregister(IInteractable interactable)
    {
        if (interactable != null)
        {
            activeInteractables.Remove(interactable);
        }
    }

    public static void CleanUp()
    {
        activeInteractables.RemoveAll(x => x == null || (x is UnityEngine.Object obj && obj == null));
    }
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

    private void OnEnable()
    {
        InteractableRegistry.Register(this);
    }

    private void OnDisable()
    {
        InteractableRegistry.Unregister(this);
    }

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
