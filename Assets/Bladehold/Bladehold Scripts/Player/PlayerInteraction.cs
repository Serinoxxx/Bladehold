using System;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using TMPro;
using UnityEngine;

/// <summary>
///     Attached to the Player to handle player-driven interactions via 'E' (InputReader.onInteractPerformed).
///     Detects nearby IInteractable objects within range, displays an interaction prompt, and executes the interaction.
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private InputReader inputReader;
    [SerializeField] private Player player;
    [SerializeField] private float maxInteractionDistance = 4.0f;

    [Header("Prompt UI (Optional)")]
    [Tooltip("Optional TextMeshPro label for displaying the interaction prompt.")]
    [SerializeField] private TMP_Text promptLabel;
    [Tooltip("Optional GameObject containing the prompt UI to show/hide.")]
    [SerializeField] private GameObject promptContainer;
    [Tooltip("Optional prefab to instantiate if prompt UI is not assigned.")]
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
        if (promptContainer == null || promptLabel == null)
        {
            SetupPromptUI();
        }
        HidePrompt();
    }

    private void SetupPromptUI()
    {
        // 1. Try finding an existing prompt container in the active scene/HUD
        var existingContainer = GameObject.Find("InteractionPrompt");
        if (existingContainer != null)
        {
            promptContainer = existingContainer;
            promptLabel = existingContainer.GetComponentInChildren<TMP_Text>(true);
            return;
        }

        // 2. Instantiate from assigned prefab or Resources / AssetDatabase
        GameObject prefabToSpawn = promptPrefab;
#if UNITY_EDITOR
        if (prefabToSpawn == null)
        {
            prefabToSpawn = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/UI/InteractionPrompt.prefab");
        }
#endif
        if (prefabToSpawn != null)
        {
            Canvas targetCanvas = FindAnyObjectByType<Canvas>();
            Transform parent = targetCanvas != null ? targetCanvas.transform : null;
            GameObject instance = Instantiate(prefabToSpawn, parent);
            instance.name = "InteractionPrompt";
            promptContainer = instance;
            promptLabel = instance.GetComponentInChildren<TMP_Text>(true);
        }
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

        // Query all active Interactable components in the scene
        Interactable[] interactables = FindObjectsByType<Interactable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < interactables.Length; i++)
        {
            Interactable candidate = interactables[i];
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
                ShowPrompt($"[E] {currentTarget.PromptText}");
            }
            else
            {
                HidePrompt();
            }
        }
    }

    private void HandleInteract()
    {
        if (player == null) ResolveDependencies();
        if (currentTarget != null && currentTarget.CanInteract)
        {
            currentTarget.Interact(player);
        }
    }

    public void ShowPrompt(string text)
    {
        if (promptContainer != null) promptContainer.SetActive(true);
        if (promptLabel != null) promptLabel.text = text;
    }

    public void HidePrompt()
    {
        if (promptContainer != null) promptContainer.SetActive(false);
        if (promptLabel != null) promptLabel.text = string.Empty;
    }
}
