using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bladehold.UI
{
    public class MainMenuChangelogUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI changelogText;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private TextMeshProUGUI versionBadgeText;
        [SerializeField] private TextAsset fallbackChangelog;

        [Header("Styling")]
        [Tooltip("Theme roles for the inline colours (the text itself is painted by its UIThemedGraphic).")]
        [SerializeField] private UIColorRole newFeaturesColor = UIColorRole.Success;
        [SerializeField] private UIColorRole fixesColor = UIColorRole.Danger;
        [SerializeField] private UIColorRole balanceColor = UIColorRole.Cost;
        [SerializeField] private UIColorRole generalColor = UIColorRole.AccentMuted;
        [SerializeField] private UIColorRole versionHeaderColor = UIColorRole.Accent;
        [SerializeField] private UIColorRole bodyTextColor = UIColorRole.Text;
        [Tooltip("Release dates and divider rules.")]
        [SerializeField] private UIColorRole dimTextColor = UIColorRole.TextDim;

        private bool anyError = false;

        private void Awake()
        {
            if (scrollRect == null)
            {
                scrollRect = GetComponentInChildren<ScrollRect>();
            }
        }

        private void Start()
        {
            if (changelogText == null)
            {
                Debug.LogError("[MainMenuChangelogUI] changelogText reference is missing!");
                anyError = true;
            }
            if (scrollRect == null)
            {
                Debug.LogError("[MainMenuChangelogUI] scrollRect reference is missing!");
                anyError = true;
            }

            if (anyError) return;

            LoadAndDisplayChangelog();
        }

        private void OnEnable()
        {
            if (!anyError)
            {
                LoadAndDisplayChangelog();
            }
        }

        public void ReloadChangelog()
        {
            LoadAndDisplayChangelog();
        }

        private void LoadAndDisplayChangelog()
        {
            string rawContent = TryReadChangelogFile();
            if (string.IsNullOrEmpty(rawContent))
            {
                if (changelogText != null)
                {
                    changelogText.text = "No changelog found.";
                }
                return;
            }

            string formattedText = FormatMarkdownForTMP(rawContent, out string latestVersion);

            if (changelogText != null)
            {
                changelogText.text = formattedText;
            }

            if (versionBadgeText != null)
            {
                if (!string.IsNullOrEmpty(latestVersion))
                {
                    versionBadgeText.text = $"v{latestVersion}";
                }
                else
                {
                    versionBadgeText.text = $"v{Application.version}";
                }
            }

            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(ResetScrollToTop());
            }
        }

        private IEnumerator ResetScrollToTop()
        {
            // Wait until layout calculations settle
            yield return null;
            yield return new WaitForEndOfFrame();
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private string TryReadChangelogFile()
        {
            string[] potentialPaths = new string[]
            {
                // Editor / Project Root
                Path.Combine(Application.dataPath, "../CHANGELOG.md"),
                // Standalone StreamingAssets
                Path.Combine(Application.streamingAssetsPath, "CHANGELOG.md"),
                // Direct relative working directory
                Path.Combine(Directory.GetCurrentDirectory(), "CHANGELOG.md"),
                // Standalone Data directory
                Path.Combine(Application.dataPath, "StreamingAssets/CHANGELOG.md"),
                Path.Combine(Application.dataPath, "CHANGELOG.md")
            };

            foreach (string path in potentialPaths)
            {
                try
                {
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        return File.ReadAllText(path, Encoding.UTF8);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MainMenuChangelogUI] Error attempting to read {path}: {ex.Message}");
                }
            }

            if (fallbackChangelog != null)
            {
                return fallbackChangelog.text;
            }

            return null;
        }

        private string FormatMarkdownForTMP(string rawMarkdown, out string latestVersion)
        {
            latestVersion = null;
            UIThemeSO theme = UITheme.For(this);
            string versionHeaderHex = Hex(theme, versionHeaderColor);
            string bodyHex = Hex(theme, bodyTextColor);
            string dimHex = Hex(theme, dimTextColor);
            string[] lines = rawMarkdown.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            StringBuilder sb = new StringBuilder();

            bool skipTopTitle = true;
            // Headers carry their own spacing: the markdown's blank line after one is dropped.
            bool afterHeader = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd();
                string trimmed = line.Trim();

                // Skip top title "# Bladehold - Changelog"
                if (skipTopTitle && trimmed.StartsWith("# "))
                {
                    continue;
                }
                if (skipTopTitle && string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }
                skipTopTitle = false;
                bool blank = string.IsNullOrWhiteSpace(trimmed);
                if (blank && afterHeader)
                {
                    continue;
                }
                afterHeader = false;

                // Version header: ## [0.1.12] - 2026-08-29
                Match vMatch = Regex.Match(trimmed, @"^##\s+\[(.*?)\](?:\s*-\s*(.*))?");
                if (vMatch.Success)
                {
                    string ver = vMatch.Groups[1].Value.Trim();
                    string date = vMatch.Groups.Count > 2 ? vMatch.Groups[2].Value.Trim() : "";

                    if (string.IsNullOrEmpty(latestVersion))
                    {
                        latestVersion = ver;
                    }

                    AppendBlank(sb);
                    afterHeader = true;
                    sb.AppendLine($"<size=115%><b><color={versionHeaderHex}>Version {ver}</color></b> <color={dimHex}><size=80%>({date})</size></color></size>");
                    continue;
                }

                // Category headers: ### Category
                if (trimmed.StartsWith("### "))
                {
                    string cat = trimmed.Substring(4).Trim();
                    UIColorRole color = bodyTextColor;

                    if (cat.IndexOf("New Feature", StringComparison.OrdinalIgnoreCase) >= 0)
                        color = newFeaturesColor;
                    else if (cat.IndexOf("Fix", StringComparison.OrdinalIgnoreCase) >= 0)
                        color = fixesColor;
                    else if (cat.IndexOf("Balance", StringComparison.OrdinalIgnoreCase) >= 0)
                        color = balanceColor;
                    else if (cat.IndexOf("General", StringComparison.OrdinalIgnoreCase) >= 0)
                        color = generalColor;

                    AppendBlank(sb);
                    afterHeader = true;
                    sb.AppendLine($"<size=90%><color={Hex(theme, color)}><b>{cat.ToUpperInvariant()}</b></color></size>");
                    continue;
                }

                // Bullets: - Some text
                if (trimmed.StartsWith("- "))
                {
                    string bulletText = trimmed.Substring(2).Trim();
                    sb.AppendLine($"<color={bodyHex}>  •<indent=1.1em>{bulletText}</indent></color>");
                    continue;
                }

                // Divider: ---
                if (trimmed == "---")
                {
                    AppendBlank(sb);
                    sb.AppendLine($"<color={dimHex}>────────────────────────────────────────</color>");
                    continue;
                }

                // Normal text / empty line
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    AppendBlank(sb);
                }
                else
                {
                    sb.AppendLine($"<color={bodyHex}>{line}</color>");
                }
            }

            return sb.ToString().Trim();
        }

        private static string Hex(UIThemeSO theme, UIColorRole role)
        {
            Color color = theme != null ? theme.Get(role) : Color.white;
            return "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        /// <summary>One blank line between blocks: markdown spacing plus the headers' own gaps never stack up.</summary>
        private static void AppendBlank(StringBuilder sb)
        {
            // Count the line breaks the text already ends with (AppendLine may write \r\n).
            int breaks = 0;
            for (int i = sb.Length - 1; i >= 0 && breaks < 2; i--)
            {
                if (sb[i] == '\n') breaks++;
                else if (sb[i] != '\r') break;
            }
            bool alreadyBlank = sb.Length == 0 || breaks >= 2;
            if (!alreadyBlank) sb.AppendLine();
        }
    }
}
