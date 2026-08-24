using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bladehold.UI
{
    /// <summary>
    ///     Controls the Main Menu Level Select Screen.
    ///     Presents 5 wide stage tiles styled with the Synty Fantasy Warrior parchment aesthetic,
    ///     Texturina typography, atmospheric gradients, timer badges, and lock indicators.
    ///     Disables the 3D character rotunda while active and provides instant hover updates.
    /// </summary>
    public class LevelSelectScreenUI : MonoBehaviour
    {
        [Serializable]
        public class StageInfo
        {
            public int stageNumber;
            public string stageName;
            public string stageSubtitle;
            [TextArea(2, 4)] public string stageDescription;
            public string duration = "20:00";
            public Sprite previewSprite;
        }

        [Header("Stage Definitions")]
        [SerializeField] private List<StageInfo> stages = new List<StageInfo>
        {
            new StageInfo
            {
                stageNumber = 1,
                stageName = "Bladehold Fortress",
                stageSubtitle = "The Inner Gate",
                stageDescription = "Defend the inner castle courtyard and fortress gate against 20 minutes of relentless goblin infantry, battering rams, and siege catapults.",
                duration = "20:00"
            },
            new StageInfo
            {
                stageNumber = 2,
                stageName = "Outer Ramparts",
                stageSubtitle = "Perimeter Defense",
                stageDescription = "Hold the elevated battlements against flying bomber goblins, armored brutes, and rapid-fire siege engines.",
                duration = "20:00"
            },
            new StageInfo
            {
                stageNumber = 3,
                stageName = "The Dark Citadel",
                stageSubtitle = "The Shadow Keep",
                stageDescription = "An ancient stronghold overrun by dark knights and corrupted sorcerers. Face maximum horde density and elite strike waves.",
                duration = "20:00"
            },
            new StageInfo
            {
                stageNumber = 4,
                stageName = "Dragon's Breach",
                stageSubtitle = "Fiery Mountain Pass",
                stageDescription = "A treacherous volcanic mountain pass besieged by barbarian giants, volcanic drakes, and magma golems.",
                duration = "20:00"
            },
            new StageInfo
            {
                stageNumber = 5,
                stageName = "The Molten Core",
                stageSubtitle = "Heart of the Volcano",
                stageDescription = "The ultimate siege. Stand alone against the apocalyptic horde, magma titans, and the catastrophic Ancient Overlord.",
                duration = "20:00"
            }
        };

        [Header("UI References")]
        [SerializeField] private TMP_Text screenTitleText;
        [SerializeField] private Transform cardsContainer;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private TMP_Text selectedStageTitleText;
        [SerializeField] private TMP_Text selectedStageSubtitleText;
        [SerializeField] private TMP_Text selectedStageDescText;
        [SerializeField] private TMP_Text selectedStageDurationText;
        [SerializeField] private Button playButton;
        [SerializeField] private TMP_Text playButtonText;
        [SerializeField] private Button backButton;
        [SerializeField] private MainMenuManager mainMenuManager;

        [Header("Preview Sprites")]
        [SerializeField] private Sprite[] stagePreviewSprites;

        private readonly List<LevelSelectCardUI> spawnedCards = new List<LevelSelectCardUI>();
        private int highestUnlockedStage = 1;
        private int currentSelectedStage = 1;
        private CharacterRotunda cachedRotunda;

        private void Awake()
        {
            if (mainMenuManager == null)
            {
                mainMenuManager = GetComponentInParent<MainMenuManager>() ?? FindObjectOfType<MainMenuManager>();
            }

            if (playButton != null)
            {
                playButton.onClick.RemoveAllListeners();
                playButton.onClick.AddListener(HandlePlayClicked);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(HandleBackClicked);
            }

            cachedRotunda = FindObjectOfType<CharacterRotunda>();
        }

        private void OnEnable()
        {
            // Disable 3D Character Preview mannequin while in Level Select
            if (cachedRotunda != null && cachedRotunda.rotundaCenter != null)
            {
                cachedRotunda.rotundaCenter.gameObject.SetActive(false);
            }

            LoadProgression();
            BuildCards();
            SelectStage(currentSelectedStage);
        }

        private void OnDisable()
        {
            if (cachedRotunda != null && cachedRotunda.rotundaCenter != null)
            {
                cachedRotunda.rotundaCenter.gameObject.SetActive(true);
            }
        }

        private void LoadProgression()
        {
            SaveData data = SaveSystem.Load();
            highestUnlockedStage = data != null ? Mathf.Max(1, data.highestUnlockedStage) : 1;
            currentSelectedStage = data != null ? Mathf.Clamp(data.selectedStage, 1, stages.Count) : 1;
        }

        private void BuildCards()
        {
            // Clean up existing cards
            if (cardsContainer != null)
            {
                for (int i = cardsContainer.childCount - 1; i >= 0; i--)
                {
                    var child = cardsContainer.GetChild(i);
                    if (Application.isPlaying)
                    {
                        Destroy(child.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }
            }
            spawnedCards.Clear();

            if (cardsContainer == null || cardPrefab == null) return;

            for (int i = 0; i < stages.Count; i++)
            {
                var stage = stages[i];
                int stageNum = i + 1;
                bool isUnlocked = stageNum <= highestUnlockedStage;
                bool isSelected = stageNum == currentSelectedStage;

                Sprite preview = stage.previewSprite;
                if (preview == null && stagePreviewSprites != null && i < stagePreviewSprites.Length)
                {
                    preview = stagePreviewSprites[i];
                }

                GameObject go = Instantiate(cardPrefab, cardsContainer);
                go.name = $"Card_Stage_{stageNum}";
                go.SetActive(true);

                LevelSelectCardUI cardUI = go.GetComponent<LevelSelectCardUI>();
                if (cardUI == null) cardUI = go.AddComponent<LevelSelectCardUI>();

                cardUI.Setup(
                    stageNum,
                    stage.stageName,
                    stage.stageDescription,
                    stage.duration,
                    isUnlocked,
                    isSelected,
                    preview,
                    HandleCardClicked,
                    HandleCardHovered
                );

                spawnedCards.Add(cardUI);
            }
        }

        private void HandleCardClicked(LevelSelectCardUI card)
        {
            SelectStage(card.StageNumber);
        }

        private void HandleCardHovered(LevelSelectCardUI card)
        {
            // Hover feedback only - do not change selected stage description
        }

        public void SelectStage(int stageNumber)
        {
            currentSelectedStage = stageNumber;

            foreach (var card in spawnedCards)
            {
                if (card != null)
                {
                    card.SetSelected(card.StageNumber == stageNumber);
                }
            }

            UpdateDescriptionPanel(stageNumber);

            bool isUnlocked = stageNumber <= highestUnlockedStage;
            if (playButton != null)
            {
                playButton.interactable = isUnlocked;
            }
            if (playButtonText != null)
            {
                playButtonText.text = isUnlocked ? "PLAY STAGE" : $"LOCKED (BEAT STAGE {stageNumber - 1})";
            }
        }

        private void UpdateDescriptionPanel(int stageNumber)
        {
            if (stageNumber < 1 || stageNumber > stages.Count) return;
            var stage = stages[stageNumber - 1];
            bool isUnlocked = stageNumber <= highestUnlockedStage;

            if (selectedStageTitleText != null)
            {
                selectedStageTitleText.text = isUnlocked ? $"STAGE {stage.stageNumber}: {stage.stageName.ToUpper()}" : $"STAGE {stage.stageNumber}: {stage.stageName.ToUpper()} [LOCKED]";
            }

            if (selectedStageSubtitleText != null)
            {
                selectedStageSubtitleText.text = stage.stageSubtitle;
            }

            if (selectedStageDescText != null)
            {
                selectedStageDescText.text = isUnlocked
                    ? stage.stageDescription
                    : $"<color=#FF7777>Locked Battlefield.</color> Survive the 20-minute siege in <b>Stage {stage.stageNumber - 1}</b> to unlock this stage.";
            }

            if (selectedStageDurationText != null)
            {
                selectedStageDurationText.text = $"Duration: {stage.duration}";
            }
        }

        private void HandlePlayClicked()
        {
            if (currentSelectedStage > highestUnlockedStage) return;

            SaveData data = SaveSystem.Load();
            if (data != null)
            {
                data.selectedStage = currentSelectedStage;
                SaveSystem.Save(data);
            }

            if (mainMenuManager != null)
            {
                mainMenuManager.OnLevelSelected();
            }
        }

        private void HandleBackClicked()
        {
            if (mainMenuManager != null)
            {
                mainMenuManager.OnBackToTitle();
            }
        }
    }
}
