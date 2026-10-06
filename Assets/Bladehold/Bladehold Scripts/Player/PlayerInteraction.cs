using System;
using System.Collections.Generic;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using UnityEngine;

/// <summary>
///     Attached to the Player to handle player-driven interactions via 'E' (InputReader.onInteractPerformed).
///     Detects nearby IInteractable objects within range, shows the HUD's <see cref="InteractionPromptView" />
///     (Interact glyph + prompt text), and executes the interaction, or plays the prompt's denied feedback when
///     an <see cref="IAffordableInteractable" /> can't be paid for.
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private InputReader inputReader;
    [SerializeField] private Player player;
    [SerializeField] private float maxInteractionDistance = 4.0f;

    [Header("Prompt UI")]
    [Tooltip("The HUD's interact prompt. Empty = the scene's InteractionPromptView (the HUD carries one), else one spawned from promptPrefab.")]
    [SerializeField] private InteractionPromptView promptView;
    [Tooltip("Spawned when the scene has no InteractionPromptView.")]
    [SerializeField] private GameObject promptPrefab;

    private IInteractable currentTarget;

    public IInteractable CurrentTarget => currentTarget;

    public Vector3 PlayerPosition
    {
        get
        {
            if (player != null) return player.transform.position;
            if (Player.Instance != null) return Player.Instance.transform.position;
            return transform.position;
        }
    }

    private void Awake()
    {
        ResolveDependencies();
    }

    private void ResolveDependencies()
    {
        if (inputReader == null) inputReader = GetComponentInChildren<InputReader>();
        if (player == null) player = GetComponentInChildren<Player>();
        if (player == null) player = Player.Instance ?? FindAnyObjectByType<Player>();
    }

    private void Start()
    {
        ResolveDependencies();
        if (promptView == null)
        {
            SetupPromptUI();
        }
        if (promptView == null)
        {
            Debug.LogError("[PlayerInteraction] No InteractionPromptView in the scene and no promptPrefab to spawn one.", this);
        }
        HidePrompt();
    }

    private void SetupPromptUI()
    {
        // 1. The HUD's prompt (inactive if something already hid it).
        promptView = FindAnyObjectByType<InteractionPromptView>(FindObjectsInactive.Include);
        if (promptView != null || promptPrefab == null) return;

        // 2. Spawn one from the prefab.
        Canvas targetCanvas = FindAnyObjectByType<Canvas>();
        GameObject instance = Instantiate(promptPrefab, targetCanvas != null ? targetCanvas.transform : null);
        instance.name = "InteractionPrompt";
        promptView = instance.GetComponent<InteractionPromptView>();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        if (inputReader != null)
        {
            inputReader.onInteractPerformed += HandleInteract;
        }
    }

    private void OnDisable()
    {
        if (inputReader != null)
        {
            inputReader.onInteractPerformed -= HandleInteract;
        }
        HidePrompt();
    }

    private void Update()
    {
        UpdateClosestInteractable();
    }

    private void UpdateClosestInteractable()
    {
        IInteractable bestTarget = null;
        float bestDistanceSqr = maxInteractionDistance * maxInteractionDistance;
        Vector3 playerPos = PlayerPosition;

        InteractableRegistry.CleanUp();
        IReadOnlyList<IInteractable> interactables = InteractableRegistry.Active;
        for (int i = 0; i < interactables.Count; i++)
        {
            IInteractable candidate = interactables[i];
            if (candidate == null || !candidate.CanInteract) continue;

            float maxDist = Mathf.Min(candidate.InteractionRadius, maxInteractionDistance);
            float dSqr = (candidate.InteractionPosition - playerPos).sqrMagnitude;
            if (dSqr <= maxDist * maxDist && dSqr < bestDistanceSqr)
            {
                bestDistanceSqr = dSqr;
                bestTarget = candidate;
            }
        }

        if (bestTarget != currentTarget)
        {
            currentTarget = bestTarget;
            if (currentTarget != null)
            {
                ShowPrompt(currentTarget.PromptText);
            }
            else
            {
                HidePrompt();
            }
        }
        else if (currentTarget != null)
        {
            if (!currentTarget.CanInteract)
            {
                currentTarget = null;
                HidePrompt();
            }
            else
            {
                ShowPrompt(currentTarget.PromptText);
            }
        }
    }

    private void HandleInteract()
    {
        if (player == null) ResolveDependencies();
        if (currentTarget == null || !currentTarget.CanInteract) return;

        if (currentTarget is IAffordableInteractable paid && !paid.CanAfford)
        {
            if (promptView != null) promptView.PlayDenied();
            return;
        }
        currentTarget.Interact(player);
    }

    public void ShowPrompt(string text)
    {
        if (promptView != null) promptView.Show(text);
    }

    public void HidePrompt()
    {
        if (promptView != null) promptView.Hide();
    }
}
