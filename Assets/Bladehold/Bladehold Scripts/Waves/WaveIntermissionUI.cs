using System.Collections;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Runs the between-wave intermission. On <see cref="WaveSpawner.IntermissionStarted" /> it plays a
///     short slow-mo flourish, reveals the animated stats (<see cref="WaveStatsPanel" />), then offers
///     two choices: "Recover and Upgrade" (opens the skill-tree panel, untimed, with a Continue button)
///     or "Hold the Line" (dives straight into the next wave for a stacking gold bonus via
///     <see cref="HoldTheLineBonus" />). Frees the cursor while the choice is up and re-locks it on
///     resume (the <see cref="DeathScreen" /> pattern), and runs on unscaled time so the slow-mo never
///     slows the UI itself.
///
///     The spawner stays UI-unaware: this calls <see cref="WaveSpawner.ChooseRecover" /> /
///     <see cref="WaveSpawner.ChooseHoldTheLine" /> to release the parked wave loop. Recover keeps the
///     loop parked (skill tree open) until Continue is pressed.
/// </summary>
public class WaveIntermissionUI : MonoBehaviour
{
    [SerializeField] private WaveSpawner spawner;
    [SerializeField] private HoldTheLineBonus holdTheLineBonus;
    [SerializeField] private WaveStatsPanel statsPanel;

    [Header("Canvas / panels")]
    [Tooltip("Root object of the intermission UI (stats + choice buttons). Hidden except during the intermission.")]
    [SerializeField] private GameObject intermissionRoot;
    [Tooltip("The choice-buttons container, shown after the stats finish revealing.")]
    [SerializeField] private GameObject choiceButtons;
    [Tooltip("The gold skill-tree panel (hosts a SkillTreeView), shown when the player picks Recover and Upgrade.")]
    [SerializeField] private GameObject skillTreePanel;

    [Header("Buttons")]
    [SerializeField] private Button recoverButton;
    [SerializeField] private Button holdTheLineButton;
    [Tooltip("Shown with the skill tree after Recover; closes it and starts the next wave's countdown.")]
    [SerializeField] private Button continueButton;

    [Header("Slow-mo (optional)")]
    [Tooltip("Played the moment the wave clears — put an MMF Timescale Modifier here for the slow-mo, plus any grade/sound.")]
    [SerializeField] private MMF_Player waveClearFeedback;
    [Tooltip("Real seconds to let the slow-mo feedback play and restore before the choice screen hard-freezes time. Set at least the MMF Timescale Modifier's duration so the two don't fight over Time.timeScale.")]
    [SerializeField] private float slowMoRealSeconds = 0.7f;

    [Header("HUD Hiding")]
    [Tooltip("Optional HUD root/CanvasGroup to hide during skill tree intermission. If unassigned, auto-finds 'Bladehold HUD', 'HUD Canvas', or 'HUD'.")]
    [SerializeField] private GameObject hudRoot;
    private CanvasGroup hudCanvasGroup;

    private void EnsureHUDFound()
    {
        if (hudRoot == null)
        {
            hudRoot = GameObject.Find("Bladehold HUD") ?? GameObject.Find("HUD Canvas") ?? GameObject.Find("HUD");
            if (hudRoot == null)
            {
                var waveUI = FindObjectOfType<WaveUI>();
                if (waveUI != null) hudRoot = waveUI.transform.root.gameObject;
            }
        }
        if (hudCanvasGroup == null && hudRoot != null)
        {
            hudCanvasGroup = hudRoot.GetComponent<CanvasGroup>();
        }
    }

    private void SetHUDVisible(bool visible)
    {
        EnsureHUDFound();
        if (hudCanvasGroup != null)
        {
            hudCanvasGroup.alpha = visible ? 1f : 0f;
            hudCanvasGroup.interactable = visible;
            hudCanvasGroup.blocksRaycasts = visible;
        }
        else if (hudRoot != null)
        {
            hudRoot.SetActive(visible);
        }
    }

    private bool anyError = false;

    private void OnValidate()
    {
        if (spawner == null)
        {
            spawner = FindObjectOfType<WaveSpawner>();
        }
        if (holdTheLineBonus == null)
        {
            holdTheLineBonus = FindObjectOfType<HoldTheLineBonus>();
        }
        if (statsPanel == null)
        {
            statsPanel = GetComponentInChildren<WaveStatsPanel>(true);
        }
    }

    private void Start()
    {
        if (spawner == null)
        {
            Debug.LogError("WaveIntermissionUI has no WaveSpawner to listen to.");
            anyError = true;
        }
        if (recoverButton == null || holdTheLineButton == null)
        {
            Debug.LogError("WaveIntermissionUI is missing its Recover / Hold the Line buttons.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        if (intermissionRoot != null)
        {
            intermissionRoot.SetActive(false);
        }
        if (skillTreePanel != null)
        {
            skillTreePanel.SetActive(false);
        }
        SetHUDVisible(true);

        recoverButton.onClick.AddListener(ChooseRecover);
        holdTheLineButton.onClick.AddListener(ChooseHoldTheLine);
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(ContinueAfterRecover);
        }

        spawner.IntermissionStarted += HandleIntermission;
    }

    private void OnDestroy()
    {
        if (spawner != null)
        {
            spawner.IntermissionStarted -= HandleIntermission;
        }
        if (recoverButton != null)
        {
            recoverButton.onClick.RemoveListener(ChooseRecover);
        }
        if (holdTheLineButton != null)
        {
            holdTheLineButton.onClick.RemoveListener(ChooseHoldTheLine);
        }
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(ContinueAfterRecover);
        }
    }

    private void HandleIntermission(int clearedWave)
    {
        StartCoroutine(IntermissionSequence(clearedWave));
    }

    private IEnumerator IntermissionSequence(int clearedWave)
    {
        // Slow-mo flourish on the kill that cleared the wave (an MMF Timescale Modifier). Let it play
        // and restore on its own before the hard-freeze below, so the two don't fight over Time.timeScale.
        if (waveClearFeedback != null)
        {
            waveClearFeedback.PlayFeedbacks();
            yield return new WaitForSecondsRealtime(slowMoRealSeconds);
        }

        // Freeze the arena while the player deliberates, so they can't clean up stragglers / vacuum the
        // loot for free — looting is the Hold-the-Line reward, earned during that choice's countdown.
        Time.timeScale = 0f;

        if (intermissionRoot != null)
        {
            intermissionRoot.SetActive(true);
        }
        if (choiceButtons != null)
        {
            choiceButtons.SetActive(false);
        }
        if (skillTreePanel != null)
        {
            skillTreePanel.SetActive(false);
        }

        // Free the cursor so the choice buttons are clickable (the camera pivot locks it for look).
        CursorLockManager.SetUnlock("WaveIntermission", true);

        if (choiceButtons != null)
        {
            choiceButtons.SetActive(true);
        }

        if (statsPanel != null)
        {
            yield return statsPanel.PlayReveal(clearedWave);
        }
    }

    private void ChooseRecover()
    {
        // Clear all consumables/pickups from the level since the player forfeits them.
        foreach (var coin in FindObjectsOfType<Coin>()) Destroy(coin.gameObject);
        foreach (var hp in FindObjectsOfType<HealthPack>()) Destroy(hp.gameObject);
        foreach (var impulse in FindObjectsOfType<ImpulseOrb>()) Destroy(impulse.gameObject);
        foreach (var lightning in FindObjectsOfType<LightningOrb>()) Destroy(lightning.gameObject);
        foreach (var node in FindObjectsOfType<ElementNode>()) Destroy(node.gameObject);

        // Open the skill tree; time stays frozen (untimed shopping) and the wave loop stays parked
        // until Continue (the spawner isn't released yet).
        if (choiceButtons != null)
        {
            choiceButtons.SetActive(false);
        }

        if (skillTreePanel != null)
        {
            skillTreePanel.SetActive(true);
            SetHUDVisible(false);
        }
        else
        {
            // No tree wired — just proceed to the next wave.
            ContinueAfterRecover();
        }
    }

    private void ContinueAfterRecover()
    {
        CloseIntermission();
        spawner.ChooseRecover();
    }

    private void ChooseHoldTheLine()
    {
        if (holdTheLineBonus != null)
        {
            holdTheLineBonus.Extend();
        }
        CloseIntermission();
        spawner.ChooseHoldTheLine();
    }

    private void CloseIntermission()
    {
        if (skillTreePanel != null)
        {
            skillTreePanel.SetActive(false);
        }
        if (intermissionRoot != null)
        {
            intermissionRoot.SetActive(false);
        }
        SetHUDVisible(true);

        // Unfreeze and re-lock the cursor for gameplay look. Both commit paths (Hold / Continue) route
        // through here, so time always resumes exactly once.
        Time.timeScale = GameSettingsService.TargetTimeScale;
        CursorLockManager.SetUnlock("WaveIntermission", false);
    }
}
