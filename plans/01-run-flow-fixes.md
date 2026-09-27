# 01: Run flow fixes

**Goal:** the macro loop matches the decided design in `/CLAUDE.md` ("Design direction"). Paths below are relative to `Assets/Bladehold/Bladehold Scripts/`.

## Tasks

- [x] **Battle Portal → Campaign Map.** `UI/Meta/BattlePortal.cs` loads `Bladehold Survivors Scene` after `RunSession.StartNewRun()`. Make it start a fresh campaign run (`CampaignManager.StartCampaignRun`) and load `Bladehold Campaign Map Scene` through `LoadingScreenManager`. Update the scene's serialized scene-name value, or drop the field if it becomes pointless.
- [x] **Survivors Scene is just a battle.** Remove the "no campaign active → load the map" prologue branch in `UI/DeathScreen.cs` `ProceedToCampaignMap`, so victory always goes through `CampaignManager.CompleteCurrentNodeAndOpenMap()`. Keep the scene playable standalone from the Editor: if there's no campaign run, victory should route somewhere sane (the map with a fresh run is fine) and log it.
- [x] **Death → Meta only.** Remove "Retry Level" from `UI/DeathScreen.cs` (the button, handlers, `RestartFromLevelOne`/`Reload`) and from `DeathScreen.prefab`; if MCP isn't available, list the prefab edit under "Needs Lance in the Editor". Defeat has one button: Return to Meta (`RunSession.ClearRun()`).
- [x] **Campaign map Return-to-Meta** (`Campaign/CampaignMapUI.cs` `HandleReturnToMeta`) must clear the run like death does, or be removed. Ask Lance which; the recommendation is to remove it, since the only way home should be death or campaign end.
- [x] **Towers refund instead of transfer:**
  - On leaving a sector by victory, sum each placed `DefenseStructure`'s remaining supply into `RunSession.InRunSupply`, and show it on the victory screen as "Towers dismantled: +N supply" (existing supply row).
  - Remove the `RunSession.SavedDefenses` save/restore in `Fort/TowerPlotManager.cs` and the `SavedDefenses` field.
  - Decide whether upgrade spend is refunded too. Default: remaining supply only. Check with Lance.
- [x] **Rest Area loses healing:** `UI/RestArea/RestAreaGate.cs` returns to the map before saving `RunSession.PlayerHealthRatio` in campaign mode. Save HP (and ultimate charge) before leaving.
- [x] **Buff fish compounding:** `Economy/RunSession.cs` (~L398-427) re-adds the Armored fish's `PlayerBonusMaxHealth += 10` on every scene load. Make buff-fish application idempotent: apply stat modifiers from `ConsumedBuffFish` on rehydrate, never mutate the persistent bonus.
- [x] **Double draft rehydration:** `Campaign/SupplyRoomController.cs:141` calls `RestoreInRunUpgrades` after `Player` already did. The scene is dead (plan 08), but check nothing else double-applies: grep every `RestoreInRunUpgrades` caller.
- [x] **Stale stage fields:** stop writing `highestUnlockedStage`/`selectedStage` in `DeathScreen.ProceedToCampaignMap` and `GameLoopManager.TriggerVictory`. Removing the fields is plan 08.
- [x] Update `/CLAUDE.md` "Design direction" and "The game loop": move the implemented items out of "not yet built".

## Acceptance

- The playtest list in `00-editor-checklist-human.md` §E passes for the flow items.
- `dotnet build` is clean (compile-check skill).
- `CHANGELOG.md` gets player-facing entries.

## Done (2026-09-25)

- Lance picked: remove the map Return-to-Meta button; refund = remaining supply **+ upgrade spend**.
- Also fixed: ult charge save/restore used `player.GetComponent<PlayerUltimateController>()` on the child `Player`, so it never found the root controller (RunSession, DeathScreen, RestAreaGate). `RestAreaDoor`, `VictoryScreenUI`, `UltimateBarUI`, and the legacy `GameLoopManager.HandleGateInteracted` have the same pattern, untouched.
- Not compiled: this checkout has no `Library/` or generated csprojs yet. Open in Unity and check the console.

## Needs Lance in the Editor

- [x] `DeathScreen.prefab`: **Try Again** is gone (you'd already deleted it). The **Wave 1** restart button and the Reincarnate/gold-tree panels are orphaned legacy; delete them if you want.
- [x] `DeathScreen.prefab`: your "Towers dismantled" label is now wired to `DeathScreen.towerRefundText` (renamed `TowerRefundText`) by an agent via MCP 2026-09-26. Needs a UI review on a real victory.
- [x] `Bladehold Campaign Map Scene`: **ReturnToMetaButton** deleted (yours).
- [x] Benchmark run 2026-09-26: 110 passed, 2 failed (the old Bulwark attack + Bannerman rig checks). 10E2 and 21C2 pass.
- [ ] Playtest `00-editor-checklist-human.md` §E flow items: portal → map, die → meta (one button), clear sector → map with supply refund, Rest Area heal carries over, eat Armored fish then load 2+ scenes and check max HP stays +10.

## Lance's Testing notes:

 1. Errors when running weapon reach & damage benchmark:
 PlayerAttack is not assigned or found on the GameObject.
UnityEngine.Debug:LogError (object)
SwordChargeFeedback:Start () (at Assets/Bladehold/Bladehold Scripts/Player/SwordChargeFeedback.cs:43)

[KnockbackReceiver] Essential dependency missing on TrainingDummy_MetaArea (health: True, agent: True, ragdoll: True, animator: True, rootCollider: True, aiMovement: False, aiAnimation: True, config: True). Incapacitation and death reactions will not run.
UnityEngine.Debug:LogError (object)
KnockbackReceiver:Start () (at Assets/Bladehold/Bladehold Scripts/DamageSystem/KnockbackReceiver.cs:102)

[UIClickFeedback] QuitButton: clickFeedback is not assigned, the button has no click sound.
UnityEngine.Debug:LogError (object,UnityEngine.Object)
UIClickFeedback:Start () (at Assets/Bladehold/Bladehold Scripts/UI/UIClickFeedback.cs:35)

[UIClickFeedback] PhotoModeButton: clickFeedback is not assigned, the button has no click sound.
UnityEngine.Debug:LogError (object,UnityEngine.Object)
UIClickFeedback:Start () (at Assets/Bladehold/Bladehold Scripts/UI/UIClickFeedback.cs:35)

[UIClickFeedback] SettingsButton: clickFeedback is not assigned, the button has no click sound.
UnityEngine.Debug:LogError (object,UnityEngine.Object)
UIClickFeedback:Start () (at Assets/Bladehold/Bladehold Scripts/UI/UIClickFeedback.cs:35)

Invalid AABB inAABB
UnityEngine.Canvas:SendWillRenderCanvases ()

PlayerAmmo: outOfAmmoFeedback is not assigned.
UnityEngine.Debug:LogError (object,UnityEngine.Object)
PlayerAmmo:Start () (at Assets/Bladehold/Bladehold Scripts/Player/PlayerAmmo.cs:91)
System.Reflection.MethodBase:Invoke (object,object[])
WeaponReachBenchmark:RunBenchmark () (at Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs:1309)
WeaponReachBenchmark:RunBenchmarkMenuItem () (at Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs:17)

Destroy may not be called from edit mode! Use DestroyImmediate instead.
Destroying an object in edit mode destroys it permanently.
UnityEngine.Object:Destroy (UnityEngine.Object)
DynamiteProjectile:Detonate () (at Assets/Bladehold/Bladehold Scripts/Enemies/Captain/DynamiteProjectile.cs:127)
WeaponReachBenchmark:RunBenchmark () (at Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs:1480)
WeaponReachBenchmark:RunBenchmarkMenuItem () (at Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs:17)

Destroy may not be called from edit mode! Use DestroyImmediate instead.
Destroying an object in edit mode destroys it permanently.
UnityEngine.Object:Destroy (UnityEngine.Object)
DynamiteProjectile:Detonate () (at Assets/Bladehold/Bladehold Scripts/Enemies/Captain/DynamiteProjectile.cs:169)
WeaponReachBenchmark:RunBenchmark () (at Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs:1480)
WeaponReachBenchmark:RunBenchmarkMenuItem () (at Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs:17)

Assertion failed on expression: 'ShouldRunBehaviour()'
UnityEngine.Component:SendMessage (string,UnityEngine.SendMessageOptions)
WeaponReachBenchmark:RunBenchmark () (at Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs:1936)
WeaponReachBenchmark:RunBenchmarkMenuItem () (at Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs:17)

Destroy may not be called from edit mode! Use DestroyImmediate instead.
Destroying an object in edit mode destroys it permanently.
UnityEngine.Object:Destroy (UnityEngine.Object)
TowerPlot:ClearDefense () (at Assets/Bladehold/Bladehold Scripts/Fort/TowerPlot.cs:172)
WeaponReachBenchmark:RunBenchmark () (at Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs:2025)
WeaponReachBenchmark:RunBenchmarkMenuItem () (at Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs:17)

Assertion failed on expression: 'ShouldRunBehaviour()'
UnityEngine.Component:SendMessage (string,UnityEngine.SendMessageOptions)
WeaponReachBenchmark:RunBenchmark () (at Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs:2159)
WeaponReachBenchmark:RunBenchmarkMenuItem () (at Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs:17)

Destroy may not be called from edit mode! Use DestroyImmediate instead.
Destroying an object in edit mode destroys it permanently.
UnityEngine.Object:Destroy (UnityEngine.Object)
NetProjectile:Impact () (at Assets/Bladehold/Bladehold Scripts/Fort/NetProjectile.cs:92)
WeaponReachBenchmark:RunBenchmark () (at Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs:2230)
WeaponReachBenchmark:RunBenchmarkMenuItem () (at Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs:17)

Destroy may not be called from edit mode! Use DestroyImmediate instead.
Destroying an object in edit mode destroys it permanently.
UnityEngine.Object:Destroy (UnityEngine.Object)
NetRootStatus:RemoveCaptureVisual () (at Assets/Bladehold/Bladehold Scripts/Enemies/NetRootStatus.cs:146)
NetRootStatus:RestoreMovement () (at Assets/Bladehold/Bladehold Scripts/Enemies/NetRootStatus.cs:156)
WeaponReachBenchmark:RunBenchmark () (at Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs:2253)
WeaponReachBenchmark:RunBenchmarkMenuItem () (at Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs:17)

[GameLoopManager] Captain Kombusta has no prefab (captainKombustaPrefab empty and no captain_kombusta roster row). No captain spawned.
UnityEngine.Debug:LogError (object)
GameLoopManager:SpawnCaptainForWave (BannerDifficultyTier,string) (at Assets/Bladehold/Bladehold Scripts/Waves/GameLoopManager.cs:897)
GameLoopManager:StartWave (int) (at Assets/Bladehold/Bladehold Scripts/Waves/GameLoopManager.cs:265)
WeaponReachBenchmark:RunBenchmark () (at Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs:2519)
WeaponReachBenchmark:RunBenchmarkMenuItem () (at Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs:17)

[KillRemainingEnemiesObjective] No skull icon: assign skullIcon or the tracker's cleanupEnemySkullIcon.
UnityEngine.Debug:LogError (object,UnityEngine.Object)
KillRemainingEnemiesObjective:EnsureSkullIcon () (at Assets/Bladehold/Bladehold Scripts/Objectives/KillRemainingEnemiesObjective.cs:63)
KillRemainingEnemiesObjective:StartObjective () (at Assets/Bladehold/Bladehold Scripts/Objectives/KillRemainingEnemiesObjective.cs:68)
WeaponReachBenchmark:RunBenchmark () (at Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs:2656)
WeaponReachBenchmark:RunBenchmarkMenuItem () (at Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs:17)

Tag: Enemy is not defined.
UnityEngine.Component:CompareTag (string)
KillRemainingEnemiesObjective:StartObjective () (at Assets/Bladehold/Bladehold Scripts/Objectives/KillRemainingEnemiesObjective.cs:98)
WeaponReachBenchmark:RunBenchmark () (at Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs:2656)
WeaponReachBenchmark:RunBenchmarkMenuItem () (at Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs:17)

Tag: Enemy is not defined.
UnityEngine.Component:CompareTag (string)
KillRemainingEnemiesObjective:StartObjective () (at Assets/Bladehold/Bladehold Scripts/Objectives/KillRemainingEnemiesObjective.cs:98)
WeaponReachBenchmark:RunBenchmark () (at Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs:2656)
WeaponReachBenchmark:RunBenchmarkMenuItem () (at Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs:17)

[KillRemainingEnemiesObjective] No skull icon: assign skullIcon or the tracker's cleanupEnemySkullIcon.
UnityEngine.Debug:LogError (object,UnityEngine.Object)
KillRemainingEnemiesObjective:EnsureSkullIcon () (at Assets/Bladehold/Bladehold Scripts/Objectives/KillRemainingEnemiesObjective.cs:63)
KillRemainingEnemiesObjective:GetActiveWaypointTargets (System.Collections.Generic.List`1<ObjectiveWaypointTarget>) (at Assets/Bladehold/Bladehold Scripts/Objectives/KillRemainingEnemiesObjective.cs:224)
WeaponReachBenchmark:RunBenchmark () (at Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs:2676)
WeaponReachBenchmark:RunBenchmarkMenuItem () (at Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs:17)

[KillRemainingEnemiesObjective] No skull icon: assign skullIcon or the tracker's cleanupEnemySkullIcon.
UnityEngine.Debug:LogError (object,UnityEngine.Object)
KillRemainingEnemiesObjective:EnsureSkullIcon () (at Assets/Bladehold/Bladehold Scripts/Objectives/KillRemainingEnemiesObjective.cs:63)
KillRemainingEnemiesObjective:GetActiveWaypointTargets (System.Collections.Generic.List`1<ObjectiveWaypointTarget>) (at Assets/Bladehold/Bladehold Scripts/Objectives/KillRemainingEnemiesObjective.cs:224)
WeaponReachBenchmark:RunBenchmark () (at Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs:2700)
WeaponReachBenchmark:RunBenchmarkMenuItem () (at Assets/Bladehold/Bladehold Scripts/Editor/WeaponReachBenchmark.cs:17)



2. Loading the Campaign map from the meta scene - I don't see any map and clicking in the scene just swallows my cursor

## Agent follow-up to the testing notes (2026-09-26)

1. **Benchmark console errors.** Fixed; the run now logs one error instead of ~20:
   - `SwordChargeFeedback` "PlayerAttack is not assigned": the weapon **pedestals** spawn the real weapon prefab as their display model, so its combat components ran with no wielder. `WeaponPedestal` now switches off `DamageTrigger`, `SwordChargeFeedback` and colliders on the display model. `Pedestal_Weapon.prefab` also had a stale `1H_Sword(Clone)` saved inside it (every pedestal showed an extra sword); removed.
   - `KnockbackReceiver` on `TrainingDummy_MetaArea`: `aiMovement` is now optional. Every use was already null-checked, and a dummy doesn't move.
   - `UIClickFeedback` Quit/PhotoMode/Settings: see plan 09 §2 (MenuButton now has its own click MMF; duplicates removed).
   - `Destroy may not be called from edit mode`: `DynamiteProjectile`, `TowerPlot.ClearDefense`, `NetProjectile` and `NetRootStatus` now use `DestroyImmediate` outside Play mode.
   - `Assertion failed: ShouldRunBehaviour()`: the benchmark's `SendMessage("OnEnable")` calls now invoke the method by reflection.
   - `Tag: Enemy is not defined`: "Enemy" is a **layer**, not a tag. `KillRemainingEnemiesObjective` and `BatteringRam` now check the layer.
   - Kombusta "no prefab" and "no skull icon": the benchmark's bare test objects now get a stand-in captain template and icon.
   - Left: `PlayerAmmo: outOfAmmoFeedback is not assigned` from the benchmark's bare test `PlayerAmmo` (a stand-in MMF can't initialise in edit mode). The real Player prefab is wired. `Invalid AABB` is a Unity UI message with no stack; not traced.
2. **Campaign map: no map, cursor swallowed.** Fixed; see `editor/02-campaign-map.md`.

