using UnityEngine;
using UnityEngine.UI;

namespace Bladehold.UI
{
    public class UILevelCarousel : MonoBehaviour
    {
        public RectTransform[] items;
        public int currentIndex = 0;
        public float spacing = 500f;
        public float centerScale = 1.2f;
        public float sideScale = 0.7f;
        public float lerpSpeed = 10f;
        
        [Header("Stage Config & UI References")]
        public Button playButton;
        public TMPro.TextMeshProUGUI playButtonText;
        public TMPro.TextMeshProUGUI stageNameText;
        public TMPro.TextMeshProUGUI stageDescriptionText;
        public MainMenuManager mainMenuManager;

        public string[] stageNames = new string[]
        {
            "Stage 1: Bladehold Fortress",
            "Stage 2: Outer Ramparts",
            "Stage 3: The Dark Citadel"
        };

        public string[] stageDescriptions = new string[]
        {
            "Defend the inner fortress gate against 20 minutes of relentless siege assault.",
            "Hold the outer ramparts. Stronger enemy compositions and rapid siege engine spawns.",
            "The frontlines of the Dark Citadel. Survive maximum horde intensity and elite titans."
        };

        private float[] targetX;
        private float currentX;
        private int highestUnlockedStage = 1;

        void Awake()
        {
            if (mainMenuManager == null)
            {
                mainMenuManager = FindObjectOfType<MainMenuManager>();
            }
        }

        void OnEnable()
        {
            LoadStageProgression();
            RefreshStageUI();
        }

        void Start()
        {
            if (items == null || items.Length == 0) return;
            targetX = new float[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                targetX[i] = i * spacing;
            }

            LoadStageProgression();
            RefreshStageUI();
        }

        private void LoadStageProgression()
        {
            SaveData data = SaveSystem.Load();
            highestUnlockedStage = data != null ? Mathf.Max(1, data.highestUnlockedStage) : 1;
            if (data != null && data.selectedStage > 0 && data.selectedStage <= items.Length)
            {
                currentIndex = Mathf.Clamp(data.selectedStage - 1, 0, items.Length - 1);
            }
        }

        void Update()
        {
            if (items == null || items.Length == 0) return;
            
            float targetScroll = -currentIndex * spacing;
            currentX = Mathf.Lerp(currentX, targetScroll, Time.deltaTime * lerpSpeed);

            for (int i = 0; i < items.Length; i++)
            {
                float pos = targetX[i] + currentX;
                float dist = Mathf.Abs(pos);
                
                // Scale based on distance from center
                float scale = Mathf.Lerp(centerScale, sideScale, dist / spacing);
                scale = Mathf.Clamp(scale, sideScale, centerScale);
                
                items[i].anchoredPosition = new Vector2(pos, 0);
                items[i].localScale = new Vector3(scale, scale, 1);
                
                // Alpha fade
                CanvasGroup cg = items[i].GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    float alpha = Mathf.Lerp(1f, 0.3f, dist / spacing);
                    cg.alpha = alpha;
                }
                
                // Bring center to front
                if (dist < spacing * 0.5f)
                {
                    items[i].SetAsLastSibling();
                }
            }
        }

        public void Next()
        {
            currentIndex = Mathf.Min(currentIndex + 1, items.Length - 1);
            RefreshStageUI();
        }

        public void Previous()
        {
            currentIndex = Mathf.Max(currentIndex - 1, 0);
            RefreshStageUI();
        }

        public void RefreshStageUI()
        {
            int stageNum = currentIndex + 1;
            bool isUnlocked = stageNum <= highestUnlockedStage;

            if (stageNameText != null)
            {
                string name = currentIndex < stageNames.Length ? stageNames[currentIndex] : $"Stage {stageNum}";
                stageNameText.text = isUnlocked ? name : $"{name} [LOCKED]";
            }

            if (stageDescriptionText != null)
            {
                string desc = currentIndex < stageDescriptions.Length ? stageDescriptions[currentIndex] : "";
                stageDescriptionText.text = isUnlocked ? desc : $"Survive Stage {stageNum - 1} for 20 minutes to unlock this stage.";
            }

            if (playButton != null)
            {
                playButton.interactable = isUnlocked;
            }

            if (playButtonText != null)
            {
                playButtonText.text = isUnlocked ? "PLAY STAGE" : "LOCKED";
            }
        }

        public void OnPlayStageClicked()
        {
            int stageNum = currentIndex + 1;
            if (stageNum > highestUnlockedStage) return;

            SaveData data = SaveSystem.Load();
            if (data != null)
            {
                data.selectedStage = stageNum;
                SaveSystem.Save(data);
            }

            if (mainMenuManager != null)
            {
                mainMenuManager.OnLevelSelected();
            }
        }
    }
}
