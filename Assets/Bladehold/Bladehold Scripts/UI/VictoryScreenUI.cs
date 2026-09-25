using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
///     Displays the celebratory Victory Screen upon defeating all 5 waves of a defence level node.
///     Presents sector clear stats (waves, kills, gold, supply), plays victorious fanfare audio,
///     and allows the player to click to proceed directly to the Campaign Map (or Meta Area if outside campaign).
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class VictoryScreenUI : MonoBehaviour
{
    public static VictoryScreenUI Instance { get; private set; }

    [Header("UI Component References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private TMP_Text wavesCompletedText;
    [SerializeField] private TMP_Text killsText;
    [SerializeField] private TMP_Text goldEarnedText;
    [SerializeField] private TMP_Text supplyEarnedText;
    [SerializeField] private Button proceedButton;
    [SerializeField] private TMP_Text proceedButtonText;

    [Header("Audio Feedback")]
    [SerializeField] private AudioClip victoryFanfareClip;

    [Header("Settings")]
    [SerializeField] private float fadeDuration = 0.6f;

    private bool isShown = false;
    private Coroutine fadeRoutine;

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

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (proceedButton != null)
        {
            proceedButton.onClick.RemoveAllListeners();
            proceedButton.onClick.AddListener(HandleProceedClicked);
        }
    }

    private void Start()
    {
        if (victoryFanfareClip == null)
        {
            #if UNITY_EDITOR
            victoryFanfareClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/Triumphant Victory.wav");
            #endif
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (proceedButton != null)
        {
            proceedButton.onClick.RemoveListener(HandleProceedClicked);
        }

        if (isShown)
        {
            CursorLockManager.SetUnlock("VictoryScreen", false);
        }
    }

    /// <summary>
    ///     Opens the victory screen, formats stats, unlocks cursor, and starts the fade-in sequence.
    /// </summary>
    public void OpenVictory(int wavesCleared, int totalWaves, int killsThisRun, int goldEarned, string sectorTitle = null)
    {
        if (isShown) return;
        isShown = true;

        // Unlock cursor for clicking the proceed button
        CursorLockManager.SetUnlock("VictoryScreen", true);

        // Format titles
        if (titleText != null)
        {
            titleText.text = "SECTOR DEFENDED!";
        }

        if (subtitleText != null)
        {
            string title = !string.IsNullOrEmpty(sectorTitle) ? sectorTitle : "Courtyard Battlements";
            subtitleText.text = $"{title} - All Waves Cleared";
        }

        // Format stats
        if (wavesCompletedText != null)
        {
            wavesCompletedText.text = $"Waves Cleared: {wavesCleared} / {totalWaves}";
        }

        if (killsText != null)
        {
            killsText.text = $"Enemies Slain: {killsThisRun}";
        }

        if (goldEarnedText != null)
        {
            goldEarnedText.text = $"Gold Earned: +{goldEarned}g";
        }

        if (supplyEarnedText != null)
        {
            supplyEarnedText.text = $"Defensive Supply: {RunSession.InRunSupply}";
        }

        // Format button text based on campaign context
        if (proceedButtonText != null)
        {
            bool isCampaign = CampaignManager.Instance != null && CampaignManager.Instance.IsCampaignActive;
            proceedButtonText.text = isCampaign ? "PROCEED TO CAMPAIGN MAP" : "RETURN TO META AREA";
        }

        // Play victorious fanfare
        PlayVictoryFanfare();

        // Fade in
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeInRoutine());
    }

    private void PlayVictoryFanfare()
    {
        if (victoryFanfareClip != null)
        {
            AudioSource.PlayClipAtPoint(victoryFanfareClip, Camera.main != null ? Camera.main.transform.position : transform.position, 1.0f);
        }
    }

    private IEnumerator FadeInRoutine()
    {
        if (canvasGroup == null) yield break;

        canvasGroup.blocksRaycasts = true;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
    }

    /// <summary>
    ///     Button callback to proceed: saves player health ratio, relocks cursor,
    ///     and either advances campaign or returns to meta area.
    /// </summary>
    public void HandleProceedClicked()
    {
        // 1. Relock cursor
        CursorLockManager.SetUnlock("VictoryScreen", false);

        // 2. Preserve player health ratio so it carries over to the next node
        if (Player.Instance != null && Player.Instance.Health != null)
        {
            RunSession.PlayerHealthRatio = Mathf.Clamp01(Player.Instance.Health.CurrentHealth / Player.Instance.Health.MaxHealth);
        }

        // 3. Preserve player ultimate charge
        if (Player.Instance != null)
        {
            var ult = Player.Instance.GetComponent<PlayerUltimateController>();
            if (ult != null)
            {
                RunSession.PlayerUltimateCharge = ult.CurrentCharge;
            }
        }

        // 4. Complete campaign node and open Campaign Overview Map
        if (CampaignManager.Instance != null && CampaignManager.Instance.IsCampaignActive)
        {
            Debug.Log("[VictoryScreenUI] Proceeding to Campaign Map...");
            CampaignManager.Instance.CompleteCurrentNodeAndContinue();
        }
        else
        {
            Debug.Log("[VictoryScreenUI] Non-campaign session: Returning to Meta Area Scene...");
            if (Application.isPlaying)
            {
                SceneManager.LoadScene("Bladehold Meta Area Scene");
            }
        }
    }

    /// <summary>
    ///     Ensures a VictoryScreenUI instance exists in the scene, generating a procedural fallback if none is placed.
    /// </summary>
    public static VictoryScreenUI EnsureInstance()
    {
        if (Instance != null) return Instance;

        Instance = FindAnyObjectByType<VictoryScreenUI>();
        if (Instance != null) return Instance;

        // Build runtime fallback UI
        GameObject rootGo = new GameObject("VictoryScreen_RuntimeFallback");
        Canvas canvas = rootGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        rootGo.AddComponent<CanvasScaler>();
        rootGo.AddComponent<GraphicRaycaster>();
        CanvasGroup cg = rootGo.AddComponent<CanvasGroup>();

        // Background panel
        GameObject bgObj = new GameObject("Panel_Background");
        bgObj.transform.SetParent(rootGo.transform, false);
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.04f, 0.04f, 0.07f, 0.88f);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // Content Container
        GameObject containerObj = new GameObject("Container");
        containerObj.transform.SetParent(bgObj.transform, false);
        RectTransform contRect = containerObj.AddComponent<RectTransform>();
        contRect.anchorMin = new Vector2(0.5f, 0.5f);
        contRect.anchorMax = new Vector2(0.5f, 0.5f);
        contRect.sizeDelta = new Vector2(600f, 450f);

        VerticalLayoutGroup layout = containerObj.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 15f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;

        // Title
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(containerObj.transform, false);
        TextMeshProUGUI titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
        titleTMP.text = "SECTOR DEFENDED!";
        titleTMP.fontSize = 42;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.color = new Color(1f, 0.82f, 0.2f, 1f); // Gold
        titleTMP.alignment = TextAlignmentOptions.Center;

        // Subtitle
        GameObject subObj = new GameObject("SubtitleText");
        subObj.transform.SetParent(containerObj.transform, false);
        TextMeshProUGUI subTMP = subObj.AddComponent<TextMeshProUGUI>();
        subTMP.text = "All 5 Waves Successfully Repelled";
        subTMP.fontSize = 22;
        subTMP.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        subTMP.alignment = TextAlignmentOptions.Center;

        // Waves text
        GameObject waveObj = new GameObject("WavesText");
        waveObj.transform.SetParent(containerObj.transform, false);
        TextMeshProUGUI waveTMP = waveObj.AddComponent<TextMeshProUGUI>();
        waveTMP.text = "Waves: 5 / 5";
        waveTMP.fontSize = 20;
        waveTMP.color = Color.white;
        waveTMP.alignment = TextAlignmentOptions.Center;

        // Kills text
        GameObject killsObj = new GameObject("KillsText");
        killsObj.transform.SetParent(containerObj.transform, false);
        TextMeshProUGUI killsTMP = killsObj.AddComponent<TextMeshProUGUI>();
        killsTMP.text = "Enemies Slain: 0";
        killsTMP.fontSize = 20;
        killsTMP.color = Color.white;
        killsTMP.alignment = TextAlignmentOptions.Center;

        // Gold text
        GameObject goldObj = new GameObject("GoldText");
        goldObj.transform.SetParent(containerObj.transform, false);
        TextMeshProUGUI goldTMP = goldObj.AddComponent<TextMeshProUGUI>();
        goldTMP.text = "Gold Earned: 0g";
        goldTMP.fontSize = 20;
        goldTMP.color = new Color(1f, 0.88f, 0.4f, 1f);
        goldTMP.alignment = TextAlignmentOptions.Center;

        // Supply text
        GameObject supplyObj = new GameObject("SupplyText");
        supplyObj.transform.SetParent(containerObj.transform, false);
        TextMeshProUGUI supplyTMP = supplyObj.AddComponent<TextMeshProUGUI>();
        supplyTMP.text = "Defensive Supply: 0";
        supplyTMP.fontSize = 20;
        supplyTMP.color = new Color(0.4f, 0.85f, 1f, 1f);
        supplyTMP.alignment = TextAlignmentOptions.Center;

        // Button
        GameObject btnObj = new GameObject("ProceedButton");
        btnObj.transform.SetParent(containerObj.transform, false);
        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.18f, 0.52f, 0.28f, 1f); // Forest green
        Button btn = btnObj.AddComponent<Button>();
        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.sizeDelta = new Vector2(360f, 55f);

        GameObject btnTextObj = new GameObject("ButtonText");
        btnTextObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI btnTMP = btnTextObj.AddComponent<TextMeshProUGUI>();
        btnTMP.text = "PROCEED TO CAMPAIGN MAP";
        btnTMP.fontSize = 20;
        btnTMP.fontStyle = FontStyles.Bold;
        btnTMP.color = Color.white;
        btnTMP.alignment = TextAlignmentOptions.Center;
        RectTransform btnTextRect = btnTextObj.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.sizeDelta = Vector2.zero;

        VictoryScreenUI comp = rootGo.AddComponent<VictoryScreenUI>();
        comp.canvasGroup = cg;
        comp.titleText = titleTMP;
        comp.subtitleText = subTMP;
        comp.wavesCompletedText = waveTMP;
        comp.killsText = killsTMP;
        comp.goldEarnedText = goldTMP;
        comp.supplyEarnedText = supplyTMP;
        comp.proceedButton = btn;
        comp.proceedButtonText = btnTMP;

        Instance = comp;
        return comp;
    }
}
