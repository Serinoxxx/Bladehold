using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using MoreMountains.Feedbacks;

/// <summary>
///     Top-level pause menu: Resume / Settings / Photo Mode / Quit. Shows/hides via
///     <see cref="PauseMenuController.OnPauseChanged" /> using a <see cref="CanvasGroup" /> fade on
///     unscaled time (the same approach as <see cref="DeathScreen" />'s fade) so it's visible while
///     <see cref="Time.timeScale" /> is 0. Also swaps to the Photo Mode panel via
///     <see cref="ScreenshotModeController.OnActiveChanged" /> rather than reaching in — Photo Mode
///     doesn't know this view exists.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class PauseMenuView : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button backFromSettingsButton;
    [SerializeField] private Button photoModeButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private GameObject settingsPanel;
    [Tooltip("Hidden whenever the Settings panel or Photo Mode is open, shown alongside it otherwise.")]
    [SerializeField] private GameObject mainButtonsPanel;
    [Tooltip("The full-screen dim image behind the menus. Hidden during Photo Mode — it would tint the shot and, as a full-screen raycast target, swallow the click-and-drag camera input.")]
    [SerializeField] private GameObject backdrop;
    [SerializeField] private ScreenshotModeController screenshotMode;
    [SerializeField] private GameObject photoModePanelRoot;
    [Tooltip("The scene to load when quitting from the pause menu.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private bool anyError = false;

    private void OnValidate()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
        if (screenshotMode == null)
        {
            screenshotMode = FindFirstObjectByType<ScreenshotModeController>();
        }
    }

    private void Start()
    {
        if (canvasGroup == null)
        {
            Debug.LogError("CanvasGroup is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (resumeButton == null || settingsButton == null || quitButton == null)
        {
            Debug.LogError("Resume/Settings/Quit buttons are not all assigned in the inspector.");
            anyError = true;
        }
        if (PauseMenuController.Instance == null)
        {
            Debug.LogError("No PauseMenuController found for PauseMenuView to listen to.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (photoModePanelRoot != null) photoModePanelRoot.SetActive(false);

        resumeButton.onClick.AddListener(HandleResume);
        settingsButton.onClick.AddListener(HandleOpenSettings);
        if (backFromSettingsButton != null) backFromSettingsButton.onClick.AddListener(ShowMainButtons);
        if (photoModeButton != null) photoModeButton.onClick.AddListener(HandleOpenPhotoMode);
        quitButton.onClick.AddListener(HandleQuit);

        PauseMenuController.Instance.OnPauseChanged += HandlePauseChanged;
        if (screenshotMode != null)
        {
            screenshotMode.OnActiveChanged += HandleScreenshotModeActiveChanged;
        }
    }

    private void OnDestroy()
    {
        if (PauseMenuController.Instance != null)
        {
            PauseMenuController.Instance.OnPauseChanged -= HandlePauseChanged;
        }
        if (screenshotMode != null)
        {
            screenshotMode.OnActiveChanged -= HandleScreenshotModeActiveChanged;
        }
        if (resumeButton != null) resumeButton.onClick.RemoveListener(HandleResume);
        if (settingsButton != null) settingsButton.onClick.RemoveListener(HandleOpenSettings);
        if (backFromSettingsButton != null) backFromSettingsButton.onClick.RemoveListener(ShowMainButtons);
        if (photoModeButton != null) photoModeButton.onClick.RemoveListener(HandleOpenPhotoMode);
        if (quitButton != null) quitButton.onClick.RemoveListener(HandleQuit);
    }

    private void HandlePauseChanged(bool paused)
    {
        canvasGroup.alpha = paused ? 1f : 0f;
        canvasGroup.interactable = paused;
        canvasGroup.blocksRaycasts = paused;

        if (paused)
        {
            ShowMainButtons();
        }
    }

    private void HandleScreenshotModeActiveChanged(bool active)
    {
        if (photoModePanelRoot != null) photoModePanelRoot.SetActive(active);
        if (backdrop != null) backdrop.SetActive(!active);

        if (active)
        {
            if (mainButtonsPanel != null) mainButtonsPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
        }
        else
        {
            ShowMainButtons();
        }
    }

    /// <summary>Public so pad-cancel (MenuFocusController.onCancel on the Settings panel) can bind it in the inspector.</summary>
    public void ShowMainButtons()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (photoModePanelRoot != null) photoModePanelRoot.SetActive(false);
        if (mainButtonsPanel != null) mainButtonsPanel.SetActive(true);
        if (backdrop != null) backdrop.SetActive(true);
    }

    private void HandleResume() => PauseMenuController.Instance.SetPaused(false);

    private void HandleOpenSettings()
    {
        if (mainButtonsPanel != null) mainButtonsPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    private void HandleOpenPhotoMode() => screenshotMode?.Enter();

    private void HandleQuit()
    {
        if (PauseMenuController.Instance != null)
        {
            PauseMenuController.Instance.SetPaused(false);
        }
        else
        {
            CursorLockManager.SetUnlock("PauseMenu", false);
        }

        Time.timeScale = 1f;
        MMTimeScaleEvent.Reset();
        Bladehold.UI.MainMenuManager.OpenUpgradesOnLoad = false;
        RunState.StartingWave = 1;

        string sceneName = string.IsNullOrEmpty(mainMenuSceneName) ? "MainMenu" : mainMenuSceneName;
        SceneManager.LoadScene(sceneName);
    }
}
