using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     HUD status indicator for the player mount:
///     - Displays the mount icon and [X] hotkey.
///     - Cast bar showing summoning progress.
///     - Active duration bar and countdown while mounted.
///     - Cooldown ring and countdown text when on cooldown.
/// </summary>
public class MountStatusUI : MonoBehaviour
{
    [Header("Mount Reference")]
    [Tooltip("The PlayerMount component. If null, auto-finds on the Player.")]
    [SerializeField] private PlayerMount mount;

    [Header("UI Visuals")]
    [SerializeField] private Image mountIcon;
    [SerializeField] private Image cooldownOverlay;
    [SerializeField] private TMP_Text cooldownText;
    [SerializeField] private TMP_Text hotkeyPromptText;

    [Header("Cast Bar")]
    [SerializeField] private GameObject castBarRoot;
    [SerializeField] private Slider castBarSlider;
    [SerializeField] private TMP_Text castBarText;

    [Header("Duration Bar")]
    [SerializeField] private GameObject durationBarRoot;
    [SerializeField] private Slider durationBarSlider;
    [SerializeField] private TMP_Text durationBarText;

    private bool anyError = false;

    private void Start()
    {
        FindDependencies();

        if (mount == null)
        {
            Player player = Player.Instance ?? FindAnyObjectByType<Player>();
            if (player != null)
            {
                mount = player.GetComponent<PlayerMount>() ?? player.GetComponentInChildren<PlayerMount>(true);
            }
        }

        if (mount != null)
        {
            mount.OnMountedChanged += HandleMountedChanged;
            mount.OnMountCastStarted += HandleCastStarted;
            mount.OnMountCastCancelled += HandleCastCancelled;
            mount.OnMountCastCompleted += HandleCastCompleted;
            mount.OnMountCooldownChanged += HandleCooldownChanged;
            mount.OnMountDurationChanged += HandleDurationChanged;
        }

        if (castBarRoot != null) castBarRoot.SetActive(false);
        if (durationBarRoot != null) durationBarRoot.SetActive(false);
        if (cooldownOverlay != null) cooldownOverlay.fillAmount = 0f;
        if (cooldownText != null) cooldownText.text = "";

        UpdateMountIcon();
    }

    private void OnDestroy()
    {
        if (mount != null)
        {
            mount.OnMountedChanged -= HandleMountedChanged;
            mount.OnMountCastStarted -= HandleCastStarted;
            mount.OnMountCastCancelled -= HandleCastCancelled;
            mount.OnMountCastCompleted -= HandleCastCompleted;
            mount.OnMountCooldownChanged -= HandleCooldownChanged;
            mount.OnMountDurationChanged -= HandleDurationChanged;
        }
    }

    private void FindDependencies()
    {
        if (mount == null)
        {
            mount = FindAnyObjectByType<PlayerMount>();
        }
    }

    private void Update()
    {
        if (mount == null)
        {
            FindDependencies();
            return;
        }

        // Realtime update for casting
        if (mount.IsCastingMount)
        {
            if (castBarRoot != null && !castBarRoot.activeSelf) castBarRoot.SetActive(true);
            if (castBarSlider != null) castBarSlider.value = mount.CastProgress;
            if (castBarText != null) castBarText.text = $"Summoning... {mount.CastProgress * 100f:F0}%";
        }
        else
        {
            if (castBarRoot != null && castBarRoot.activeSelf) castBarRoot.SetActive(false);
        }

        // Realtime update for cooldown
        if (mount.MountRemainingCooldown > 0f)
        {
            float maxCd = mount.MaxMountCooldown > 0f ? mount.MaxMountCooldown : 90f;
            float fraction = Mathf.Clamp01(mount.MountRemainingCooldown / maxCd);
            if (cooldownOverlay != null) cooldownOverlay.fillAmount = fraction;
            if (cooldownText != null) cooldownText.text = $"{Mathf.CeilToInt(mount.MountRemainingCooldown)}s";
            if (hotkeyPromptText != null) hotkeyPromptText.text = "Cooldown";
        }
        else
        {
            if (cooldownOverlay != null && cooldownOverlay.fillAmount > 0f) cooldownOverlay.fillAmount = 0f;
            if (cooldownText != null && !string.IsNullOrEmpty(cooldownText.text)) cooldownText.text = "";
            if (hotkeyPromptText != null) hotkeyPromptText.text = mount.IsMounted ? "[X] Dismount" : "[X] Mount";
        }

        // Realtime update for active duration
        if (mount.IsMounted)
        {
            if (durationBarRoot != null && !durationBarRoot.activeSelf) durationBarRoot.SetActive(true);
            float maxDur = mount.MaxMountDuration > 0f ? mount.MaxMountDuration : 30f;
            float frac = maxDur > 0f ? Mathf.Clamp01(mount.MountRemainingDuration / maxDur) : 0f;
            if (durationBarSlider != null) durationBarSlider.value = frac;
            if (durationBarText != null) durationBarText.text = $"{Mathf.CeilToInt(mount.MountRemainingDuration)}s";
        }
        else
        {
            if (durationBarRoot != null && durationBarRoot.activeSelf) durationBarRoot.SetActive(false);
        }
    }

    private void UpdateMountIcon()
    {
        if (mount == null) return;
        MountDefinitionSO def = mount.EquippedMount;
        if (def != null && def.icon != null && mountIcon != null)
        {
            mountIcon.sprite = def.icon;
        }
    }

    private void HandleMountedChanged(bool mounted)
    {
        if (durationBarRoot != null) durationBarRoot.SetActive(mounted);
        if (hotkeyPromptText != null) hotkeyPromptText.text = mounted ? "[X] Dismount" : "[X] Mount";
    }

    private void HandleCastStarted(float duration)
    {
        if (castBarRoot != null) castBarRoot.SetActive(true);
        if (castBarSlider != null) castBarSlider.value = 0f;
        if (castBarText != null) castBarText.text = "Summoning Mount...";
    }

    private void HandleCastCancelled()
    {
        if (castBarRoot != null) castBarRoot.SetActive(false);
    }

    private void HandleCastCompleted()
    {
        if (castBarRoot != null) castBarRoot.SetActive(false);
    }

    private void HandleCooldownChanged(float remaining, float max)
    {
        float frac = max > 0f ? Mathf.Clamp01(remaining / max) : 0f;
        if (cooldownOverlay != null) cooldownOverlay.fillAmount = frac;
        if (cooldownText != null) cooldownText.text = remaining > 0f ? $"{Mathf.CeilToInt(remaining)}s" : "";
    }

    private void HandleDurationChanged(float remaining, float max)
    {
        float frac = max > 0f ? Mathf.Clamp01(remaining / max) : 0f;
        if (durationBarSlider != null) durationBarSlider.value = frac;
        if (durationBarText != null) durationBarText.text = $"{Mathf.CeilToInt(remaining)}s";
    }
}
