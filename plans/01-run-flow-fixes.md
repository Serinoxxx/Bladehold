# 01: Run flow fixes

**Goal:** the macro loop matches the decided design in `/CLAUDE.md` ("Design direction"). Paths below are relative to `Assets/Bladehold/Bladehold Scripts/`.

## Tasks

- [ ] **Battle Portal → Campaign Map.** `UI/Meta/BattlePortal.cs` loads `Bladehold Survivors Scene` after `RunSession.StartNewRun()`. Make it start a fresh campaign run (`CampaignManager.StartCampaignRun`) and load `Bladehold Campaign Map Scene` through `LoadingScreenManager`. Update the scene's serialized scene-name value, or drop the field if it becomes pointless.
- [ ] **Survivors Scene is just a battle.** Remove the "no campaign active → load the map" prologue branch in `UI/DeathScreen.cs` `ProceedToCampaignMap`, so victory always goes through `CampaignManager.CompleteCurrentNodeAndOpenMap()`. Keep the scene playable standalone from the Editor: if there's no campaign run, victory should route somewhere sane (the map with a fresh run is fine) and log it.
- [ ] **Death → Meta only.** Remove "Retry Level" from `UI/DeathScreen.cs` (the button, handlers, `RestartFromLevelOne`/`Reload`) and from `DeathScreen.prefab`; if MCP isn't available, list the prefab edit under "Needs Lance in the Editor". Defeat has one button: Return to Meta (`RunSession.ClearRun()`).
- [ ] **Campaign map Return-to-Meta** (`Campaign/CampaignMapUI.cs` `HandleReturnToMeta`) must clear the run like death does, or be removed. Ask Lance which; the recommendation is to remove it, since the only way home should be death or campaign end.
- [ ] **Towers refund instead of transfer:**
  - On leaving a sector by victory, sum each placed `DefenseStructure`'s remaining supply into `RunSession.InRunSupply`, and show it on the victory screen as "Towers dismantled: +N supply" (existing supply row).
  - Remove the `RunSession.SavedDefenses` save/restore in `Fort/TowerPlotManager.cs` and the `SavedDefenses` field.
  - Decide whether upgrade spend is refunded too. Default: remaining supply only. Check with Lance.
- [ ] **Rest Area loses healing:** `UI/RestArea/RestAreaGate.cs` returns to the map before saving `RunSession.PlayerHealthRatio` in campaign mode. Save HP (and ultimate charge) before leaving.
- [ ] **Buff fish compounding:** `Economy/RunSession.cs` (~L398-427) re-adds the Armored fish's `PlayerBonusMaxHealth += 10` on every scene load. Make buff-fish application idempotent: apply stat modifiers from `ConsumedBuffFish` on rehydrate, never mutate the persistent bonus.
- [ ] **Double draft rehydration:** `Campaign/SupplyRoomController.cs:141` calls `RestoreInRunUpgrades` after `Player` already did. The scene is dead (plan 08), but check nothing else double-applies: grep every `RestoreInRunUpgrades` caller.
- [ ] **Stale stage fields:** stop writing `highestUnlockedStage`/`selectedStage` in `DeathScreen.ProceedToCampaignMap` and `GameLoopManager.TriggerVictory`. Removing the fields is plan 08.
- [ ] Update `/CLAUDE.md` "Design direction" and "The game loop": move the implemented items out of "not yet built".

## Acceptance

- The playtest list in `00-editor-checklist-human.md` §E passes for the flow items.
- `dotnet build` is clean (compile-check skill).
- `CHANGELOG.md` gets player-facing entries.
