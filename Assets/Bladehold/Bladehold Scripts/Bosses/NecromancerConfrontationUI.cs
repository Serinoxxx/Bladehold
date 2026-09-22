using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
///     Cinematic dialogue and confrontation UI for the Revelation Encounter in the Crypt.
///     Presents Malakor the Necromancer's revelation monologue: that he orchestrated the goblin invasion
///     to forge the player into an unstoppable executioner to kill Princess Katherine.
///     Offers two branching choices:
///     1. [Obey] Slay the Princess (Proceeds to Princess Sanctuary encounter)
///     2. [Defy] Slay the Necromancer (Begins Phase 1 of the Necromancer Boss Fight in the Crypt)
/// </summary>
public class NecromancerConfrontationUI : MonoBehaviour
{
    public static NecromancerConfrontationUI Instance { get; private set; }

    [Header("UI Panels & Canvas")]
    [SerializeField] private CanvasGroup dialogueCanvasGroup;
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private GameObject choicesContainer;

    [Header("Text Fields")]
    [SerializeField] private TextMeshProUGUI speakerNameText;
    [SerializeField] private TextMeshProUGUI speakerTitleText;
    [SerializeField] private TextMeshProUGUI dialogueBodyText;
    [SerializeField] private TextMeshProUGUI promptContinueText;

    [Header("Choice Buttons")]
    [SerializeField] private Button obeyButton;
    [SerializeField] private Button defyButton;
    [SerializeField] private TextMeshProUGUI obeyButtonText;
    [SerializeField] private TextMeshProUGUI defyButtonText;

    [Header("Audio")]
    [SerializeField] private AudioClip monologueVoiceSfx;
    [SerializeField] private AudioClip defyLaughSfx;
    [SerializeField] private AudioClip buttonClickSfx;

    [Header("Tuning")]
    [SerializeField] private float typewriterCharDelay = 0.025f;
    [SerializeField] private float fadeDuration = 0.4f;

    private NecromancerBossController activeBoss;
    private Coroutine typewriterRoutine;
    private Coroutine fadeRoutine;
    private int currentDialogueIndex = 0;
    private bool isTyping = false;
    private bool monologueFinished = false;

    private readonly string[] monologuePages = new string[]
    {
        "Ah... my greatest masterpiece stands before me. Look at you—tempered in the blood and fire of ten thousand fallen goblins.",
        "Did you truly believe those beasts breached Bladehold's gates by mere coincidence? No. I summoned every clan. I directed every war banner. I fed them to your blade.\n\nEvery parry, every strike, every agonizing drop of blood... was your crucible.",
        "The old King is dead. His throne lies shattered in ruins. Only one final obstacle remains before the kingdom is reborn under our dominion: Princess Katherine. She cowers in her inner sanctuary, weeping for a fallen crown.",
        "The hour has come, executioner. Will you fulfill your dark destiny and take the throne... or throw your life away in defiance?"
    };

    public event Action OnDefyChosen;
    public event Action OnObeyChosen;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (dialogueCanvasGroup == null)
        {
            dialogueCanvasGroup = GetComponent<CanvasGroup>();
        }

        SetupButtonListeners();
    }

    private void Start()
    {
        if (dialogueCanvasGroup != null && !dialogueCanvasGroup.gameObject.activeInHierarchy)
        {
            dialogueCanvasGroup.alpha = 0f;
            dialogueCanvasGroup.blocksRaycasts = false;
            dialogueCanvasGroup.interactable = false;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void SetupButtonListeners()
    {
        if (obeyButton != null)
        {
            obeyButton.onClick.RemoveAllListeners();
            obeyButton.onClick.AddListener(HandleObeyClicked);
        }

        if (defyButton != null)
        {
            defyButton.onClick.RemoveAllListeners();
            defyButton.onClick.AddListener(HandleDefyClicked);
        }
    }

    /// <summary>
    ///     Opens the cinematic confrontation UI, freezes combat controls, and begins Malakor's monologue.
    /// </summary>
    public void OpenConfrontation(NecromancerBossController boss)
    {
        activeBoss = boss;
        currentDialogueIndex = 0;
        monologueFinished = false;

        if (speakerNameText != null) speakerNameText.text = "MALAKOR";
        if (speakerTitleText != null) speakerTitleText.text = "Architect of the Siege";

        if (choicesContainer != null) choicesContainer.SetActive(false);
        if (promptContinueText != null) promptContinueText.gameObject.SetActive(true);

        gameObject.SetActive(true);
        if (dialoguePanel != null) dialoguePanel.SetActive(true);

        FadeCanvasGroup(1f, fadeDuration);

        // Pause or restrict player movement
        if (Player.Instance != null)
        {
            var rb = Player.Instance.GetComponent<Rigidbody>();
            if (rb != null) rb.linearVelocity = Vector3.zero;
        }

        ShowDialoguePage(currentDialogueIndex);
    }

    private void Update()
    {
        if (dialogueCanvasGroup == null || dialogueCanvasGroup.alpha < 0.5f) return;

        // Press Space or Left Click or E to advance monologue
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0))
        {
            if (isTyping)
            {
                // Instant complete current page
                if (typewriterRoutine != null) StopCoroutine(typewriterRoutine);
                if (dialogueBodyText != null) dialogueBodyText.text = monologuePages[currentDialogueIndex];
                isTyping = false;
            }
            else if (!monologueFinished)
            {
                AdvanceDialogue();
            }
        }
    }

    private void AdvanceDialogue()
    {
        currentDialogueIndex++;
        if (currentDialogueIndex < monologuePages.Length)
        {
            ShowDialoguePage(currentDialogueIndex);
        }
        else
        {
            // Reached choices!
            monologueFinished = true;
            if (promptContinueText != null) promptContinueText.gameObject.SetActive(false);
            if (choicesContainer != null) choicesContainer.SetActive(true);
        }
    }

    private void ShowDialoguePage(int index)
    {
        if (typewriterRoutine != null) StopCoroutine(typewriterRoutine);
        typewriterRoutine = StartCoroutine(TypewriterRoutine(monologuePages[index]));
    }

    private IEnumerator TypewriterRoutine(string text)
    {
        isTyping = true;
        if (dialogueBodyText != null) dialogueBodyText.text = "";

        if (monologueVoiceSfx != null)
        {
            AudioSource.PlayClipAtPoint(monologueVoiceSfx, Camera.main != null ? Camera.main.transform.position : transform.position, 0.7f);
        }

        for (int i = 0; i < text.Length; i++)
        {
            if (dialogueBodyText != null) dialogueBodyText.text += text[i];
            if (text[i] != ' ')
            {
                yield return new WaitForSecondsRealtime(typewriterCharDelay);
            }
        }

        isTyping = false;
    }

    private void HandleObeyClicked()
    {
        PlayButtonSfx();
        Debug.Log("[NecromancerConfrontationUI] Player chose: OBEY. Transitioning to Princess Sanctuary.");

        OnObeyChosen?.Invoke();

        FadeCanvasGroup(0f, 0.3f);

        // Transition via CampaignManager
        if (CampaignManager.Instance != null)
        {
            CampaignManager.Instance.DeployToNode("tier8_princess_boss");
        }
        else if (Bladehold.UI.LoadingScreenManager.Instance != null)
        {
            Bladehold.UI.LoadingScreenManager.Instance.LoadScene(
                "Bladehold Princess Sanctuary",
                "Princess Sanctuary",
                "Inner Royal Bower",
                "Confront Princess Katherine in her sanctuary."
            );
        }
        else
        {
            SceneManager.LoadScene("Bladehold Princess Sanctuary");
        }
    }

    private void HandleDefyClicked()
    {
        PlayButtonSfx();
        Debug.Log("[NecromancerConfrontationUI] Player chose: DEFY. Starting Necromancer Boss Battle in the Crypt!");

        if (defyLaughSfx != null)
        {
            AudioSource.PlayClipAtPoint(defyLaughSfx, Camera.main != null ? Camera.main.transform.position : transform.position, 1.0f);
        }

        OnDefyChosen?.Invoke();

        FadeCanvasGroup(0f, 0.35f);

        if (activeBoss != null)
        {
            activeBoss.StartBossFight();
        }
        else
        {
            var boss = FindAnyObjectByType<NecromancerBossController>();
            if (boss != null) boss.StartBossFight();
        }
    }

    private void PlayButtonSfx()
    {
        if (buttonClickSfx != null)
        {
            AudioSource.PlayClipAtPoint(buttonClickSfx, Camera.main != null ? Camera.main.transform.position : transform.position, 0.8f);
        }
    }

    private void FadeCanvasGroup(float targetAlpha, float duration)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha, duration));
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        if (dialogueCanvasGroup == null) yield break;

        float startAlpha = dialogueCanvasGroup.alpha;
        float elapsed = 0f;

        if (targetAlpha > 0.01f)
        {
            dialogueCanvasGroup.blocksRaycasts = true;
            dialogueCanvasGroup.interactable = true;
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            dialogueCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        dialogueCanvasGroup.alpha = targetAlpha;

        if (targetAlpha <= 0.01f)
        {
            dialogueCanvasGroup.blocksRaycasts = false;
            dialogueCanvasGroup.interactable = false;
        }
    }
}
