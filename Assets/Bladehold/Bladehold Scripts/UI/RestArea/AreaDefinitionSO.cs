using UnityEngine;

namespace Bladehold.UI
{
    /// <summary>
    ///     Defines metadata for an area or stage destination in Bladehold.
    ///     Used by Rest Area exit doors, level select, and the loading screen to display
    ///     rich player-facing info ("Entering [DisplayName]", subtitles, lore, artwork)
    ///     instead of raw scene asset filenames.
    /// </summary>
    [CreateAssetMenu(fileName = "AreaDefinition", menuName = "Scriptable Objects/Bladehold/Area Definition")]
    public class AreaDefinitionSO : ScriptableObject
    {
        [Header("Scene Identification")]
        [Tooltip("The actual Unity scene name in Build Settings (e.g. 'Bladehold Survivors Scene').")]
        public string sceneName = "Bladehold Survivors Scene";

        [Tooltip("Associated stage number in level progression (1 - 5).")]
        public int stageNumber = 1;

        [Header("Display Metadata")]
        [Tooltip("Player-facing name of the area (e.g. 'Bladehold Fortress', 'Outer Ramparts').")]
        public string displayName = "Bladehold Fortress";

        [Tooltip("Subtitle or region tagline (e.g. 'The Inner Gate', 'Perimeter Defense').")]
        public string subtitle = "The Inner Gate";

        [Tooltip("Atmospheric lore blurb, objective description, or gameplay tips displayed during loading.")]
        [TextArea(3, 6)]
        public string description = "Defend the inner castle courtyard and fortress gate against relentless goblin infantry, battering rams, and siege catapults.";

        [Tooltip("Optional preview artwork or wallpaper sprite for the loading screen.")]
        public Sprite previewSprite;

        [Header("Interaction & Unlocks")]
        [Tooltip("Format string for door prompt. {0} is replaced with displayName.")]
        public string doorPromptFormat = "Enter {0}";

        [Tooltip("Whether this area/door is permanently locked until explicitly unlocked.")]
        public bool isLocked;

        [Tooltip("Minimum unlocked stage required in SaveData to enter this area (0 = always available).")]
        public int requiredStageUnlocked = 1;

        /// <summary>
        ///     Returns the formatted door interaction prompt.
        /// </summary>
        public string GetDoorPrompt()
        {
            if (string.IsNullOrEmpty(doorPromptFormat))
                return $"Enter {displayName}";

            return string.Format(doorPromptFormat, displayName);
        }
    }
}
