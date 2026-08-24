# Bladehold Unity Editor Wiring

## Meta-Progression System, Periodic Elemental Imbuements, and In-Run Card Drafting Overhaul

### The C# is done
Implemented persistent meta-progression in the Main Menu, in-run card draft overhaul, 4 periodic elemental imbuements, active weapon limit, and card banish mechanics:
- **`StatType.cs` & `StatDisplay.cs`**: Added periodic stats (`PeriodicFire*`, `PeriodicIce*`, `PeriodicLightning*`, `PeriodicImpulse*`) and UI presentation table formatters.
- **`SkillNode.cs` & `SkillTreeSO.cs`**: Added `isMeta`, `isCard`, and `isActiveWeapon` flags with dynamic header-mapped CSV parsing.
- **`SkillTree*.csv`**: Updated config with the 3 classification flags, removed deprecated drop nodes, made Parry/Counter passive, made Multi-shot and Extended Blade in-game cards, and added the 4 Periodic Imbuement skills.
- **`PeriodicImbuementController.cs`**: Scene/Player component managing periodic cycling and on-hit procs for Fire (AoE explosions), Ice (slowing chill & freeze), Lightning (chain lightning), and Impulse (kinetic knockback fling).
- **`SkillTreeService.cs`**: Separated `metaLevels` (persisted to disk SaveData) from `runLevels` (in-run temporary draft choices), ensuring in-run upgrades never write to disk save files.
- **`SurvivorsLevelSystem.cs` & `Coin.cs` & `SurvivorsHUDUI.cs`**: Decoupled in-run XP from persistent Gold, showing in-run progress as XP while Gold is collected for Main Menu meta upgrades.
- **`SurvivorsCardSelector.cs`**: Filtered by `isCard`, enforced 4 active weapons slot cap, and added banish filter.
- **`SurvivorsCardUI.cs` & `SurvivorsCardSelectUI.cs`**: Added 1-per-draft Banish button that removes the card for the run and rolls a replacement immediately.
- **`MetaProgressionGridUI.cs` & `MainMenuManager.cs`**: Built Main Menu Upgrades screen showing a grid of all permanent meta skills, gold counter, and purchase buttons.

### Wiring checklist
- [x] **Main Menu Upgrades Screen (`MainMenu.unity`)**:
  - [x] Added `Button_Upgrades` to `Screen_Title/Buttons` with persistent onClick listener to `MainMenuManager.OnUpgradesClicked()`.
  - [x] Created `Screen_Upgrades` with header title, gold counter, scrollable grid content, and Back button.
  - [x] Attached `MetaProgressionGridUI` and wired all references (`skillTree`, `goldText`, `gridContent`, `backButton`, `mainMenuManager`).
  - [x] Wired `MainMenuManager.upgradesScreen` to `Screen_Upgrades`.
- [x] **Survivors Card Draft Modal (`Bladehold Survivors Scene.unity`)**:
  - [x] Added `Banish_Btn` to all 3 cards in `CardsRow` of `SurvivorsCardSelectModal`.
  - [x] Auto-wired `banishButton` references on each `SurvivorsCardUI`.
- [x] **Skill Tree Config & Agent Skills**:
  - [x] Updated `SkillTree.csv`, `SkillTreeBerserker.csv`, and `SkillTreeMage.csv`.
  - [x] Updated `add-skill-line/SKILL.md` and `AGENTS.md` to enforce human definition of `isMeta`, `isCard`, and `isActiveWeapon`.

### Manual verification
- [x] Headless C# compilation verified with `dotnet build Assembly-CSharp.csproj` (0 errors).
- [x] Unity Editor AssetDatabase refreshed via `refresh_unity` (0 console errors).
- [ ] Playtest Main Menu: Click "UPGRADES" -> verify grid of permanent upgrades appears with gold cost and level badges.
- [ ] Playtest Main Menu: Purchase an upgrade -> verify gold deducts, level increments, and save persists on reload.
- [ ] Playtest Survivors Mode: Verify starting run does not carry temporary card choices from previous runs.
- [ ] Playtest Survivors Mode: Level up -> verify 3 draft cards appear with "BANISH" button.
- [ ] Playtest Survivors Mode: Click "BANISH" on a card -> verify card is removed from the run pool, a replacement card rolls immediately, and banish buttons disable for the rest of that draft.
- [ ] Playtest Survivors Mode: Acquire Periodic Imbuement (Fire/Ice/Lightning/Impulse) -> verify elemental visual cycling and on-hit procs trigger during combat.

## Slayer Gate-Assault Objective, Generic Enemy Intro Cinematic, Boss Health Bar & Damage Retaliation — Unity Editor Wiring

### The C# is done
Implem...
