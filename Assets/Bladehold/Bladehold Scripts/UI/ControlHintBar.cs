using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
///     A row of on-screen control hints (glyph + label pairs), instantiated from a serialized entry
///     list — one instance sits bottom-right of the gameplay HUD (Attack / Aim / Sprint / Jump /
///     Pause…), another under the death-screen skill trees (Pan / Zoom / Buy / Switch Tab / Back).
///     Each entry either names an action on the player's rebindable map (glyph follows rebinds) or
///     carries fixed per-family control paths for hints no action covers (mouse drag, right stick).
///     Refreshes on device switches, rebinds, and language changes; entries with nothing to show in
///     the active family hide themselves. An optional CanvasGroup auto-fade keeps the gameplay HUD
///     clean after the hints have been on screen a while, waking on every device switch.
/// </summary>
public class ControlHintBar : MonoBehaviour
{
    [Serializable]
    public struct HintEntry
    {
        [Tooltip("Action name on the rebindable gameplay map (e.g. 'Attack'). Leave blank to use the fixed paths below.")]
        public string actionName;
        [Tooltip("Fixed keyboard/mouse control path when no action applies, e.g. '<Mouse>/leftButton'. Blank = hidden on KBM.")]
        public string kbmPath;
        [Tooltip("Fixed gamepad control path when no action applies, e.g. '<Gamepad>/rightStick'. Blank = hidden on pad.")]
        public string gamepadPath;
        [Tooltip("Loc key of the hint label, e.g. 'hint.attack'.")]
        public string locKey;
        [Tooltip("English fallback while the key is untranslated.")]
        public string english;
    }

    [SerializeField] private HintEntry[] entries;
    [Tooltip("Prefab of one glyph+label row, instantiated per entry under this bar's layout group.")]
    [SerializeField] private HintEntryView entryPrefab;
    [Tooltip("Optional CanvasGroup fade-out after this many seconds on screen; 0 = always visible. Any device switch shows the bar again.")]
    [SerializeField] private float autoHideSeconds = 0f;
    [SerializeField] private float fadeDuration = 0.5f;

    private readonly List<HintEntryView> rows = new List<HintEntryView>();
    private CanvasGroup canvasGroup;
    private float shownAt;
    private bool built;

    private void Start()
    {
        if (entryPrefab == null)
        {
            Debug.LogError("ControlHintBar has no entry prefab assigned.");
            enabled = false;
            return;
        }

        if (autoHideSeconds > 0f)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        Rebuild();
        InputDeviceWatcher.SchemeChanged += HandleSchemeChanged;
        InputDeviceWatcher.BindingsChanged += Rebuild;
        Loc.OnLanguageChanged += Rebuild;
        shownAt = Time.unscaledTime;
    }

    private void OnDestroy()
    {
        InputDeviceWatcher.SchemeChanged -= HandleSchemeChanged;
        InputDeviceWatcher.BindingsChanged -= Rebuild;
        Loc.OnLanguageChanged -= Rebuild;
    }

    private void OnEnable()
    {
        // Re-shown bars (the skill-tree bar toggles with its panel) restart their fade window.
        shownAt = Time.unscaledTime;
        if (built)
        {
            Rebuild();
        }
    }

    private void Update()
    {
        if (canvasGroup == null)
        {
            return;
        }
        float sinceShown = Time.unscaledTime - shownAt;
        float target = autoHideSeconds > 0f && sinceShown > autoHideSeconds ? 0f : 1f;
        canvasGroup.alpha = fadeDuration > 0f
            ? Mathf.MoveTowards(canvasGroup.alpha, target, Time.unscaledDeltaTime / fadeDuration)
            : target;
    }

    private void HandleSchemeChanged(ControlScheme scheme)
    {
        shownAt = Time.unscaledTime; // wake the bar so the player sees the new device's prompts
        Rebuild();
    }

    private void Rebuild()
    {
        built = true;

        // Rows are cheap and few — rebuild wholesale instead of diffing.
        for (int i = rows.Count - 1; i >= 0; i--)
        {
            if (rows[i] != null)
            {
                Destroy(rows[i].gameObject);
            }
        }
        rows.Clear();

        InputActionMap map = Player.Instance != null && Player.Instance.InputSettings != null
            ? Player.Instance.InputSettings.GetRebindableActionMap()
            : null;

        foreach (HintEntry entry in entries)
        {
            HintEntryView row = Instantiate(entryPrefab, transform);
            if (!string.IsNullOrEmpty(entry.actionName))
            {
                InputAction action = map?.FindAction(entry.actionName);
                if (action == null)
                {
                    // No such action (player dead/absent or a typo) — skip rather than show a blank chip.
                    Destroy(row.gameObject);
                    continue;
                }
                row.Bind(action, entry.locKey, entry.english);
            }
            else
            {
                row.Bind(entry.kbmPath, entry.gamepadPath, entry.locKey, entry.english);
            }
            rows.Add(row);
        }
    }
}
