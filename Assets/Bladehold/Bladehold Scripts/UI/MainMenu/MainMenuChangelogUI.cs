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
        [SerializeField] private string newFeaturesColor = "#2D5438";
        [SerializeField] private string fixesColor = "#8E3626";
        [SerializeField] private string balanceColor = "#8C651E";
        [SerializeField] private string generalColor = "#4E463E";
        [SerializeField] private string versionHeaderColor = "#3E3024";
        [SerializeField] private string bodyTextColor = "#433A33";

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
            string[] lines = rawMarkdown.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            StringBuilder sb = new StringBuilder();

            bool skipTopTitle = true;

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

                    sb.AppendLine();
                    sb.AppendLine($"<size=115%><b><color={versionHeaderColor}>Version {ver}</color></b> <color=#7D6E61><size=80%>({date})</size></color></size>");
                    continue;
                }

                // Category headers: ### Category
                if (trimmed.StartsWith("### "))
                {
                    string cat = trimmed.Substring(4).Trim();
                    string color = bodyTextColor;

                    if (cat.IndexOf("New Feature", StringComparison.OrdinalIgnoreCase) >= 0)
                        color = newFeaturesColor;
                    else if (cat.IndexOf("Fix", StringComparison.OrdinalIgnoreCase) >= 0)
                        color = fixesColor;
                    else if (cat.IndexOf("Balance", StringComparison.OrdinalIgnoreCase) >= 0)
                        color = balanceColor;
                    else if (cat.IndexOf("General", StringComparison.OrdinalIgnoreCase) >= 0)
                        color = generalColor;

                    sb.AppendLine();
                    sb.AppendLine($"<size=90%><color={color}><b>{cat.ToUpperInvariant()}</b></color></size>");
                    continue;
                }

                // Bullets: - Some text
                if (trimmed.StartsWith("- "))
                {
                    string bulletText = trimmed.Substring(2).Trim();
                    sb.AppendLine($"  <color={bodyTextColor}>•  {bulletText}</color>");
                    continue;
                }

                // Divider: ---
                if (trimmed == "---")
                {
                    sb.AppendLine();
                    sb.AppendLine("<color=#C1B6A5>────────────────────────────────────────</color>");
                    continue;
                }

                // Normal text / empty line
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    sb.AppendLine();
                }
                else
                {
                    sb.AppendLine($"<color={bodyTextColor}>{line}</color>");
                }
            }

            return sb.ToString().Trim();
        }
    }
}
