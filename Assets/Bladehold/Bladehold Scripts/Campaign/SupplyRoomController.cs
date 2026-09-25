using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
///     Master controller for the Supply Room scene (Tier 3 tactical node in Castle Campaign).
///     Manages the non-hostile loot room:
///     - Confirms zero enemy spawners exist in this scene.
///     - Player state rehydrates itself in <see cref="Player" />.Start; don't call RestoreInRunUpgrades here too.
///     - Wires exit gate/door <see cref="Interactable"/> to complete the node and return to Castle Map.
///     - Tracks remaining smashable crates count for UI feedback ("Supply Crates Smashed: X / Y").
/// </summary>
public class SupplyRoomController : MonoBehaviour
{
    public static SupplyRoomController Instance { get; private set; }

    [Header("Exit Gate / Door")]
    [Tooltip("Interactable component on the exit gate/door. Defaults to searching scene.")]
    [SerializeField] private Interactable exitDoorInteractable;

    [Tooltip("Prompt text shown when approaching the exit door.")]
    [SerializeField] private string exitPromptText = "[E] Return to Castle Map";

    [Tooltip("Fallback map scene if not loaded via CampaignManager.")]
    [SerializeField] private string campaignMapSceneName = "Bladehold Campaign Map Scene";

    [Header("UI Feedback")]
    [Tooltip("Optional HUD or world text displaying smash progress (e.g. 'Supply Crates Smashed: 5 / 20').")]
    [SerializeField] private TMP_Text cratesSmashedText;

    private int totalCrates = 0;
    private int smashedCrates = 0;

    public int TotalCrates => totalCrates;
    public int SmashedCrates => smashedCrates;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // 1. Confirm zero enemy spawners exist in this non-hostile scene
        VerifyZeroEnemySpawners();

        // 2. Scan and count all SupplyBoxes in scene
        InitializeCrateTracking();

        // 3. Configure Exit Door
        ConfigureExitDoor();

        // 4. Subscribe to supply box broken events
        SupplyBox.OnAnySupplyBoxSmashed += HandleSupplyBoxSmashed;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        SupplyBox.OnAnySupplyBoxSmashed -= HandleSupplyBoxSmashed;

        if (exitDoorInteractable != null)
        {
            exitDoorInteractable.OnInteractedEvent -= HandleExitDoorInteracted;
        }
    }

    private void VerifyZeroEnemySpawners()
    {
        var survivorsSpawner = FindAnyObjectByType<SurvivorsSpawner>();
        if (survivorsSpawner != null)
        {
            Debug.LogWarning("[SupplyRoomController] SurvivorsSpawner found in non-hostile Supply Room! Disabling it.");
            survivorsSpawner.gameObject.SetActive(false);
        }
    }

    private void InitializeCrateTracking()
    {
        SupplyBox[] boxes = FindObjectsByType<SupplyBox>(FindObjectsSortMode.None);
        totalCrates = boxes.Length;
        smashedCrates = 0;
        UpdateCrateProgressUI();
        Debug.Log($"[SupplyRoomController] Initialized Supply Room with {totalCrates} smashable containers.");
    }

    private void ConfigureExitDoor()
    {
        if (exitDoorInteractable == null)
        {
            exitDoorInteractable = FindAnyObjectByType<Interactable>();
        }

        if (exitDoorInteractable != null)
        {
            exitDoorInteractable.PromptText = exitPromptText;
            exitDoorInteractable.CanInteract = true;
            exitDoorInteractable.OnInteractedEvent -= HandleExitDoorInteracted;
            exitDoorInteractable.OnInteractedEvent += HandleExitDoorInteracted;
            Debug.Log($"[SupplyRoomController] Wired Exit Door Interactable on '{exitDoorInteractable.gameObject.name}'.");
        }
        else
        {
            Debug.LogWarning("[SupplyRoomController] No exit door Interactable found in scene!");
        }
    }

    private void HandleSupplyBoxSmashed(SupplyBox box)
    {
        smashedCrates++;
        UpdateCrateProgressUI();
    }

    private void UpdateCrateProgressUI()
    {
        if (cratesSmashedText != null)
        {
            cratesSmashedText.text = $"Supply Crates Smashed: {smashedCrates} / {totalCrates}";
        }
    }

    private void HandleExitDoorInteracted(Player player)
    {
        Debug.Log("[SupplyRoomController] Exit door interacted. Completing Supply Room node...");
        Time.timeScale = 1f;

        // Preserve player health ratio
        if (player != null && player.Health != null)
        {
            RunSession.PlayerHealthRatio = player.Health.CurrentHealth / player.Health.MaxHealth;
        }

        // Complete current campaign node and return to overview map
        if (CampaignManager.Instance != null)
        {
            CampaignManager.Instance.CompleteCurrentNodeAndContinue();
        }
        else
        {
            if (Bladehold.UI.LoadingScreenManager.Instance != null)
            {
                Bladehold.UI.LoadingScreenManager.Instance.LoadScene(campaignMapSceneName);
            }
            else
            {
                SceneManager.LoadScene(campaignMapSceneName);
            }
        }
    }
}
