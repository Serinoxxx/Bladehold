using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bladehold.UI
{
    /// <summary>
    ///     Data container for area/scene meta information.
    /// </summary>
    [Serializable]
    public class AreaMetadata
    {
        public string sceneName;
        public int stageNumber;
        public string displayName;
        public string subtitle;
        public string description;
        public Sprite previewSprite;

        public AreaMetadata() { }

        public AreaMetadata(string sceneName, int stageNumber, string displayName, string subtitle, string description, Sprite preview = null)
        {
            this.sceneName = sceneName;
            this.stageNumber = stageNumber;
            this.displayName = displayName;
            this.subtitle = subtitle;
            this.description = description;
            this.previewSprite = preview;
        }

        public static AreaMetadata FromSO(AreaDefinitionSO so)
        {
            if (so == null) return null;
            return new AreaMetadata(
                so.sceneName,
                so.stageNumber,
                so.displayName,
                so.subtitle,
                so.description,
                so.previewSprite
            );
        }
    }

    /// <summary>
    ///     Static registry and resolver for area/scene metadata across Bladehold.
    ///     Pre-populated with the old 5 stages and key scenes,
    ///     guaranteeing rich "Entering [DisplayName]" and lore text during loading transitions
    ///     even before any custom ScriptableObject assets are wired.
    /// </summary>
    public static class AreaDatabase
    {
        private static readonly Dictionary<string, AreaMetadata> BySceneName = new Dictionary<string, AreaMetadata>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<int, AreaMetadata> ByStageNumber = new Dictionary<int, AreaMetadata>();

        static AreaDatabase()
        {
            InitializeDefaults();
        }

        private static void InitializeDefaults()
        {
            // Stage 1: Bladehold Fortress
            Register(new AreaMetadata(
                "Bladehold Survivors Scene",
                1,
                "Bladehold Fortress",
                "The Inner Gate",
                "Defend the inner castle courtyard and fortress gate against relentless goblin infantry, battering rams, and siege catapults."
            ));

            // Stage 2: Outer Ramparts
            Register(new AreaMetadata(
                "Outer Ramparts",
                2,
                "Outer Ramparts",
                "Perimeter Defense",
                "Hold the elevated battlements against flying bomber goblins, armored brutes, and rapid-fire siege engines."
            ));

            // Stage 3: The Dark Citadel
            Register(new AreaMetadata(
                "The Dark Citadel",
                3,
                "The Dark Citadel",
                "The Shadow Keep",
                "An ancient stronghold overrun by dark knights and corrupted sorcerers. Face maximum horde density and elite strike waves."
            ));

            // Stage 4: Dragon's Breach
            Register(new AreaMetadata(
                "Dragon's Breach",
                4,
                "Dragon's Breach",
                "Fiery Mountain Pass",
                "A treacherous volcanic mountain pass besieged by barbarian giants, volcanic drakes, and magma golems."
            ));

            // Stage 5: The Molten Core
            Register(new AreaMetadata(
                "The Molten Core",
                5,
                "The Molten Core",
                "Heart of the Volcano",
                "The ultimate siege. Stand alone against the apocalyptic horde, magma titans, and the catastrophic Ancient Overlord."
            ));

            // Rest Area
            Register(new AreaMetadata(
                "Bladehold Rest Area Scene",
                0,
                "The Sanctuary",
                "Rest Area",
                "A peaceful sanctuary to mend wounds, draft divine abilities, and prepare for the battle ahead."
            ));

            // Meta Area
            Register(new AreaMetadata(
                "Bladehold Meta Area Scene",
                0,
                "Hall of Champions",
                "Ancestral Stronghold",
                "Honor the fallen, unlock ancestral weapons, and channel goblin blood into permanent strength."
            ));

            // Campaign Overview Map
            Register(new AreaMetadata(
                "Bladehold Campaign Map Scene",
                0,
                "Castle Campaign",
                "War Room Map",
                "Survey the fortress battlements, choose your tactical route through the sectors, and confront the clan captains."
            ));

            // Supply Room
            Register(new AreaMetadata(
                "Bladehold Supply Room",
                0,
                "Supply Room",
                "Castle Storehouse",
                "An abandoned underground storage depot packed with smashable crates and barrels. Plunder free resources with zero enemy resistance."
            ));

            // Castle Campaign Levels
            // Tier 1: Castle Courtyard
            Register(new AreaMetadata(
                "Bladehold Castle Courtyard",
                1,
                "Castle Courtyard",
                "Courtyard Gate",
                "Defend the courtyard gates against the initial goblin assault. Fortify tower defense slots and secure the entryway."
            ));

            // Tier 2A: Castle Ramparts
            Register(new AreaMetadata(
                "Bladehold Castle Ramparts",
                2,
                "Castle Ramparts",
                "High Battlements",
                "Hold the elevated stone battlements against Captain Fraglob and his vanguard. High elevation, windy battlements."
            ));

            // Tier 2B: Castle Armory
            Register(new AreaMetadata(
                "Bladehold Castle Armory",
                2,
                "Castle Armory",
                "Weapons Depot",
                "Clear out Captain Kombusta's incendiary sappers before they ignite the castle's fortified armory storehouse."
            ));

            // Tier 4: Great Hall
            Register(new AreaMetadata(
                "Bladehold Great Hall",
                4,
                "Great Hall",
                "Grand Banquet Hall",
                "Fight through high-density enemy hordes among long banquet tables, chandeliers, and fallen tapestries."
            ));

            // Tier 5A: Castle Dungeons
            Register(new AreaMetadata(
                "Bladehold Castle Dungeons",
                5,
                "Castle Dungeons",
                "Prison Oubliette",
                "Delve into the castle depths where brute wardens guard heaps of harvested goblin blood."
            ));

            // Tier 5B: Castle Conservatory
            Register(new AreaMetadata(
                "Bladehold Castle Conservatory",
                5,
                "Castle Conservatory",
                "Royal Greenhouse",
                "Battle through shattered glass greenhouses and overgrown flora guarded by Captain Kombusta's firebrands."
            ));

            // Tier 7: Throne Antechamber
            Register(new AreaMetadata(
                "Bladehold Throne Antechamber",
                7,
                "Throne Antechamber",
                "The Obsidian Portico",
                "Intense frontline defense outside the throne room doors. Waves of armored elites attempt to halt your advance."
            ));

            // Tier 8: Necromancer's Crypt
            Register(new AreaMetadata(
                "Bladehold Necromancer Crypt",
                8,
                "Necromancer's Crypt",
                "Catacombs of the Fallen",
                "The subterranean tomb where Malakor orchestrated the siege. Face the dark architect and his legion of reanimated dead."
            ));

            // Tier 8: Princess Sanctuary (Alternate Branch)
            Register(new AreaMetadata(
                "Bladehold Princess Sanctuary",
                8,
                "Princess Sanctuary",
                "The Royal Throne Annex",
                "The regal inner sanctuary where Princess Katherine commands her devoted elite guard. Strike down her armored knights and prevent her holy revivals to claim dark dominion over Bladehold."
            ));
        }

        public static void Register(AreaMetadata metadata)
        {
            if (metadata == null) return;

            if (!string.IsNullOrEmpty(metadata.sceneName))
            {
                BySceneName[metadata.sceneName] = metadata;
            }

            if (metadata.stageNumber > 0)
            {
                ByStageNumber[metadata.stageNumber] = metadata;
            }
        }

        public static void Register(AreaDefinitionSO so)
        {
            if (so == null) return;
            Register(AreaMetadata.FromSO(so));
        }

        /// <summary>
        ///     Looks up metadata by scene name. Falls back to a formatted scene name if unknown.
        /// </summary>
        public static AreaMetadata GetMetadata(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return new AreaMetadata("", 0, "Unknown Realm", "", "");
            }

            if (BySceneName.TryGetValue(sceneName, out var meta))
            {
                return meta;
            }

            // Fallback: clean up scene filename (e.g. "Bladehold Survivors Scene" -> "Bladehold Survivors")
            string cleaned = sceneName.Replace(" Scene", "").Trim();
            return new AreaMetadata(sceneName, 0, cleaned, "Unknown Territory", "Prepare yourself for the battles ahead.");
        }

        /// <summary>
        ///     Looks up metadata by stage number (1-5).
        /// </summary>
        public static AreaMetadata GetMetadata(int stageNumber)
        {
            if (ByStageNumber.TryGetValue(stageNumber, out var meta))
            {
                return meta;
            }

            return new AreaMetadata("", stageNumber, $"Stage {stageNumber}", "Unknown Region", "Survive the oncoming waves.");
        }
    }
}
