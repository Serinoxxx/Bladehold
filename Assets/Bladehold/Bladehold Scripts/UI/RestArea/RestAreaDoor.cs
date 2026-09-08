using UnityEngine;

namespace Bladehold.UI
{
    /// <summary>
    ///     Modular door component placed in the Rest Area to allow exiting to different scenes/stages.
    ///     Coordinates with Interactable to display contextual prompts ("Enter Outer Ramparts", "Return to Fortress")
    ///     and triggers LoadingScreenManager with rich meta information while preserving RunSession state.
    /// </summary>
    [RequireComponent(typeof(Interactable))]
    public class RestAreaDoor : MonoBehaviour
    {
        [Header("Area Configuration")]
        [Tooltip("Optional ScriptableObject defining this door's target scene, name, lore, and artwork.")]
        [SerializeField] private AreaDefinitionSO areaDefinition;

        [Tooltip("Target scene to load if no AreaDefinitionSO is assigned.")]
        [SerializeField] private string targetSceneName = "Bladehold Survivors Scene";

        [Tooltip("Player-facing name if no AreaDefinitionSO is assigned.")]
        [SerializeField] private string targetDisplayName = "Bladehold Fortress";

        [Tooltip("Subtitle or region tagline if no AreaDefinitionSO is assigned.")]
        [SerializeField] private string targetSubtitle = "The Inner Gate";

        [Tooltip("Lore blurb or tactical description displayed on the loading screen.")]
        [TextArea(2, 4)]
        [SerializeField] private string targetDescription;

        [Tooltip("Optional preview artwork sprite.")]
        [SerializeField] private Sprite targetPreviewSprite;

        [Tooltip("Stage number for level progression tracking (1-5). Set 0 to disable stage tracking.")]
        [SerializeField] private int targetStageNumber = 1;

        [Header("Lock Settings")]
        [Tooltip("Whether this door is manually locked.")]
        [SerializeField] private bool isLocked = false;

        [Tooltip("Minimum unlocked stage required in SaveData (0 = no requirement).")]
        [SerializeField] private int requiredStageUnlocked = 0;

        [Tooltip("Optional custom prompt override (e.g. 'Return to Battle'). If empty, uses 'Enter [DisplayName]'.")]
        [SerializeField] private string customPromptText;

        private Interactable interactable;

        public string TargetSceneName => areaDefinition != null ? areaDefinition.sceneName : targetSceneName;
        public string TargetDisplayName => areaDefinition != null ? areaDefinition.displayName : targetDisplayName;
        public string TargetSubtitle => areaDefinition != null ? areaDefinition.subtitle : targetSubtitle;
        public string TargetDescription => areaDefinition != null ? areaDefinition.description : targetDescription;
        public Sprite TargetPreviewSprite => areaDefinition != null ? areaDefinition.previewSprite : targetPreviewSprite;
        public int TargetStageNumber => areaDefinition != null ? areaDefinition.stageNumber : targetStageNumber;

        public bool IsLocked
        {
            get
            {
                if (isLocked) return true;
                if (areaDefinition != null && areaDefinition.isLocked) return true;

                int requiredStage = areaDefinition != null ? areaDefinition.requiredStageUnlocked : requiredStageUnlocked;
                if (requiredStage > 0)
                {
                    SaveData data = SaveSystem.Load();
                    if (data != null && data.highestUnlockedStage < requiredStage)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (interactable == null)
            {
                interactable = GetComponent<Interactable>();
            }

            if (interactable != null)
            {
                UpdatePromptText();
                interactable.OnInteractedEvent -= HandleDoorInteracted;
                interactable.OnInteractedEvent += HandleDoorInteracted;
            }
        }

        public void SetLocked(bool locked)
        {
            isLocked = locked;
            UpdatePromptText();
        }

        public void UpdatePromptText()
        {
            if (interactable == null) return;

            if (IsLocked)
            {
                int req = areaDefinition != null ? areaDefinition.requiredStageUnlocked : requiredStageUnlocked;
                interactable.PromptText = req > 0
                    ? $"[Locked] Requires Stage {req}"
                    : $"[Locked] {TargetDisplayName}";
                return;
            }

            if (!string.IsNullOrEmpty(customPromptText))
            {
                interactable.PromptText = customPromptText;
                return;
            }

            if (areaDefinition != null)
            {
                interactable.PromptText = areaDefinition.GetDoorPrompt();
                return;
            }

            interactable.PromptText = $"Enter {TargetDisplayName}";
        }

        private void OnDestroy()
        {
            if (interactable != null)
            {
                interactable.OnInteractedEvent -= HandleDoorInteracted;
            }
        }

        private void HandleDoorInteracted(Player player)
        {
            if (IsLocked)
            {
                Debug.Log($"[RestAreaDoor] Door to '{TargetDisplayName}' is locked.");
                return;
            }

            ExitThroughDoor(player);
        }

        /// <summary>
        ///     Executes the exit sequence: preserves RunSession state, updates wave/stage,
        ///     and triggers LoadingScreenManager.
        /// </summary>
        public void ExitThroughDoor(Player player = null)
        {
            Debug.Log($"[RestAreaDoor] Exiting through door to '{TargetDisplayName}' (Scene: '{TargetSceneName}')...");
            Time.timeScale = 1f;

            // 1. Preserve player health ratio
            if (player != null && player.Health != null)
            {
                RunSession.PlayerHealthRatio = player.Health.CurrentHealth / player.Health.MaxHealth;
            }
            else if (Player.Instance != null && Player.Instance.Health != null)
            {
                RunSession.PlayerHealthRatio = Player.Instance.Health.CurrentHealth / Player.Instance.Health.MaxHealth;
            }

            // 2. Preserve player ultimate charge
            if (player != null)
            {
                var ult = player.GetComponent<PlayerUltimateController>();
                if (ult != null)
                {
                    RunSession.PlayerUltimateCharge = ult.CurrentCharge;
                }
            }
            else if (Player.Instance != null)
            {
                var ult = Player.Instance.GetComponent<PlayerUltimateController>();
                if (ult != null)
                {
                    RunSession.PlayerUltimateCharge = ult.CurrentCharge;
                }
            }

            // 3. Preserve fortress gate health
            if (Gate.All != null && Gate.All.Count > 0)
            {
                foreach (var g in Gate.All)
                {
                    if (g != null && g.GetComponent<Health>() != null)
                    {
                        var gh = g.GetComponent<Health>();
                        RunSession.FortressGateCurrentHealth = gh.CurrentHealth;
                        RunSession.FortressGateMaxHealth = gh.MaxHealth;
                        break;
                    }
                }
            }

            // 4. Advance to next wave after rest
            RunSession.CurrentWave = Mathf.Max(1, RunSession.RestVisitsCount * 3 + 1);

            // 5. Update stage progress if applicable
            int stage = TargetStageNumber;
            if (stage > 0)
            {
                SaveData data = SaveSystem.Load();
                if (data != null)
                {
                    data.selectedStage = stage;
                    SaveSystem.Save(data);
                }
            }

            // 6. Launch transition via LoadingScreenManager
            if (Application.isPlaying)
            {
                if (areaDefinition != null)
                {
                    LoadingScreenManager.Instance.LoadArea(areaDefinition);
                }
                else
                {
                    AreaMetadata meta = new AreaMetadata(
                        TargetSceneName,
                        TargetStageNumber,
                        TargetDisplayName,
                        TargetSubtitle,
                        TargetDescription,
                        TargetPreviewSprite
                    );
                    LoadingScreenManager.Instance.LoadScene(TargetSceneName, meta);
                }
            }
        }
    }
}
