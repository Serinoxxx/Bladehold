# 22: Playtest feedback 2026-10-10

**Goal:** deliver Lance's 2026-10-10 list (wave cards, Putrid Horror, controller fixes, horse, towers, settings, tutorial, perf) in small, independently shippable slices.

**How to run it:** one slice (or one phase of small slices) per session.
- Say "execute slice N.x of `plans/22-playtest-feedback-oct10.md`".
- Finish it, run `compile-check`, tick the box, add a `changelog` entry if player-visible, then commit.
- Phases are ordered smallest/safest first. Within a phase, do slices in order.
- Editor-only steps go in `plans/editor/22-playtest-feedback-oct10.md` (`editor-wiring-todo` skill).

Paths are relative to `Assets/Bladehold/Bladehold Scripts/`; line numbers are from 2026-10-10.

---

## Phase 1: Quick fixes (each slice is one short session)

- [x] **1.1 Draft cards double-highlight on controller.** The middle card is highlighted by the (centred, hidden) mouse cursor's hover, and the gamepad selection adds a second highlight.
  - **Done 2026-10-10:** `UI/SurvivorsCardUI.cs` and `UI/WaveCardUI.cs` track pointer-inside separately and only count it as hover while keyboard/mouse is active; a `SchemeChanged` handler re-lights or unlights the card when the device flips. `UI/UISelectableJuice.cs` applies the same rule to menu buttons. `MetaPerkCardUI` hover only selects the detail panel, so it was left alone. Play-mode check: `plans/editor/22-playtest-feedback-oct10.md`.
  - Look at the draft card view's hover/`IPointerEnterHandler` handling and `UI/MenuFocusController.cs:231-270`.
  - Fix: while the last-used device is a gamepad, ignore pointer hover (or clear hover state when gamepad navigation starts); only the `EventSystem` selection drives the highlight. Pointer movement switches back to mouse mode.
  - Check every screen that shares the card view (war-banner/rest-area draft, `Fishing/UI/FishingDraftUI.cs`).
  - Verify: open a draft with a controller connected, no stray highlight; move the mouse, hover works again.
- [x] **1.2 Glyph missing on tower plot prompt the first time you build.**
  - **Fixed 2026-10-10:** in the HUD the prompt's glyph is sized by its layout group (serialized 0×0). The first `Refresh` ran before any layout pass, so `InputGlyph.FitWidth` measured height 0 and wrote `LayoutElement.preferredWidth = 0`, collapsing the glyph until the next show re-ran it. `FitWidth` now falls back to the `LayoutElement`'s preferred height and never writes a zero width. Verified on a freshly instantiated HUD: width 84 (was 0).
  - Earlier note: the first play-test didn't reproduce it, probably because the prompt had been laid out before the glyph bound.
  - Prompt UI: `UI/InteractionPromptView.cs` / `Input/InputGlyph.cs` / `Input/GlyphMapSO.cs`; plot: `Fort/TowerPlot.cs`.
  - Likely the glyph resolves before the active device / sprite asset is known (first enable), and only refreshes on device change. Fix: resolve on `OnEnable` and whenever the prompt text is (re)set.
  - Verify on keyboard and controller in a fresh scene load.
- [x] **1.3 Catapult projectile: red flaming boulder instead of a sphere.**
  - **Done 2026-10-10:** the Fire catapult's `RollingFireball.prefab` now uses the Synty `SM_Env_Rock_Round_03` mesh (spun by the existing roll code) with the new glowing-red `Materials/Mat_FireBoulder.mat` and a `Flames` child (`FX_Fireball_01`). The in-flight `CatapultBoulder.prefab` gets a red material, a `FireVisual` flame child and a fire-coloured trail through the new `CatapultProjectile.ApplyFireLook`, only for Fire-element catapults. Plain, Ice and Storm catapults keep the grey rock.
  - `Fort/CatapultProjectile.cs` + its prefab (and `CatapultStormCloud` if it reuses the mesh).
  - Swap the sphere mesh for a Synty rock/boulder mesh with a red/ember material, add a fire trail (particles on the prefab, not code-built) and a slow tumble. Use `find-and-import-assets` if no suitable rock/fire VFX exists.
  - Mostly prefab work: do it via Unity MCP; anything left goes in the editor checklist.
- [x] **1.4 Horse trample: smaller hitbox, no knockback unless charging.**
  - **Done 2026-10-10 (data only):** `HorseSO.asset` sets `cruiseKnockbackFraction` 0.5 → 0, so hits without a charge carry zero knockback, which `KnockbackReceiver` ignores. The trample box shrinks from half extents (0.8, 0.8, 1.2) at 2.19 ahead to (0.6, 0.8, 0.8) at 2.2: 1.2 m wide instead of 1.6 for a 0.84 m horse, reaching about 1 m past the nose instead of 1.3 m. The small cruise damage stays. The script defaults match. The knight AI's charge shares this box.
  - `Horse/HorseChargeDamage.cs` (`SetFactors` :155, knockback at :231) and `Horse/HorseSO.cs`.
  - Non-charging contact: `knockbackScale = 0` (keep or zero the small damage, decide in session; default: keep damage, zero knockback).
  - Shrink the trample trigger (collider on the horse prefab / SO radius). Verify against a crowd in Enemy Zoo.

## Phase 2: Horse stamina ("carrot energy")

- [x] **2.1 Stamina sources while riding.**
  - **Done 2026-10-10 (data + text):** `HorseSO.asset` sets `staminaRegenPerSecond` 2 → 0 (no passive regen mounted or banked on foot) and `mountedKillStaminaFraction` 0.5 → 0 (saddle kills earn nothing). Stamina now comes only from carrots, on-foot kills and Bloodlust trample kills (`HorseTrampleKillStamina`, base 0 confirmed). The code paths stay, so either value can be raised again. Script defaults and doc comments match. The tutorial `StaminaKills` hint (Strings.csv + Valley Stronghold fallback) now reads "Kills on foot refill charge stamina / Kills from the saddle don't - so do it on foot or eat carrots". An exhausted horse now stays exhausted until fed (35% threshold), which 2.3's "mount is tired" message covers. Damage on foot doesn't give stamina (it never did); only kills count.
  - Remove passive regen while mounted: `Horse/HorseMotor.cs:468-481` and the run-mount mirror in `Player/PlayerMount.cs:559`. Decide in session whether a dismissed/stabled horse still regens (default: no passive regen at all; carrots and on-foot combat only).
  - Trample kills (`HorseMotor.cs:651`, `StatType.HorseTrampleKillStamina`) must give 0 unless the draft perk raises the stat: confirm the base is 0.
  - On-foot kills/damage keep feeding stamina via `PlayerMount.cs:452` → `AddStamina`. Gate that path to *not mounted*.
  - Update the `HorseMotor` class doc comment (:22).
- [x] **2.2 Centre-screen charge stamina bar.** A copy of the stamina bar in the middle of the screen, visible only while charging, burning down with a particle effect and a carrot icon. Prefab UI (`ui-mockup`, `mm-progress-bars` skills), MMF for the burn particles. Flag for human UI review.
- [x] **2.3 "Mount is tired" feedback.** Trying to charge with too little stamina flashes the 2.2 bar (MMF) and shows a localized line: `Your mount is tired` / `{mountGlyph} to rest, or eat some {carrotGlyph}` with glyphs resolved through `InputGlyph`. Find the charge-start check in `Player/MountChargeAbilities.cs` / `HorseMotor`.
  - **Done 2026-10-10 (2.2 + 2.3 together):** New `UI/ChargeStaminaCentreUI.cs` on `Bladehold Prefabs/UI/ChargeStaminaCentre.prefab`, nested in the HUD under `ScreenSpace`. It fades in while `PlayerMount.IsCharging`, burns down from `MountStaminaFraction` with pooled ember sparks (`ChargeEmberUI` / `ChargeEmber.prefab`) shed from the fill edge, and has an orange carrot icon. `HorseMotor.OnChargeDenied` fires when a charge is attempted while exhausted, or when stamina runs dry mid-charge. The UI then plays `DeniedMMF` (red fill flash and scale bumps) and shows `hud.mount.tired_title` / `hud.mount.tired_rest` for 2.5 s, with a live Dismount glyph and a carrot icon. Agent mockup, flagged for UI review.

## Phase 3: Settings menu tabs

- [x] **3.1 Split Keyboard & Mouse and Controller settings into separate tabs.**
  - Generated by `Editor/SettingsPanelBuilder.cs` (view: `UI/SettingsPanelView.cs`, service: `Settings/GameSettingsService.cs`). Change the builder and regenerate; never hand-edit the prefab.
  - Tabs must be navigable by gamepad (shoulder buttons) and by mouse. Rebind rows (`UI/RebindButtonView.cs`) go to the matching tab.
  - **Done 2026-10-10:** Tabs are now General | Keyboard & Mouse | Controller | Graphics. `SettingsPanelView` keeps a generic tab list, so the new `gamepadTab*` fields and every tab after General are optional. Old dev-scene copies (Test, Demo, Enemy Zoo) still show their single Controls tab with two-column rows. Keyboard & Mouse holds mouse sensitivity, Invert X/Y and keyboard/mouse-only binding rows. Controller holds pad sensitivity, the runtime-cloned Stick Dead Zone row, mirrored Invert X/Y toggles (one shared setting) and pad-only binding rows. `RebindButtonView.ShowSingleColumn` hides the other column and re-anchors the kept one to 0.45–0.85, matching the builder header. Regenerated `PauseMenuCanvas.prefab`, `RebindRow.prefab` and `MainMenu.unity`. New loc keys: `settings.tab_keyboard`, `settings.tab_controller`, `settings.section_pad_bindings`.

## Phase 4: Controller bow aim assist (depends on 3.1)

- [x] **4.1 Aim assist toward enemy heads.** Done: `Player/ControllerAimAssist.cs` (added to the camera pivot on Start in every scene) + `Resources/AimAssist.asset` (`AimAssistSO`). Head = `VulnerableSpot` > head bone > collider top. Applies to every hold-aim weapon.
  - Bow code: `Player/PlayerBow.cs`, `Player/BowAimLook.cs`, `Player/BowAimCamera.cs`, tunables in `Player/BowSO.cs`.
  - Gamepad only. While aiming, find the best living enemy inside a screen-space window around the reticle (`Health.IsDead` filtered), target its head (Animator `HumanBodyBones.Head`, falling back to collider top), and apply a slowdown + pull of the look input toward it. Never snap; never through walls (line-of-sight check).
  - Defaults on a `ScriptableObject`; strength/window registered as settings.
- [x] **4.2 Settings sliders.** Done: rows cloned at runtime below Stick Dead Zone (every panel copy, no prefab regen); no `settingsVersion` bump needed (new fields default). "Aim assist strength" (0 = off) and "Aim assist window" in the Controller tab (persisted in `SaveData`, bump `settingsVersion`).
- [x] **4.3 Mechanic check.** Done: benchmark section 29 tests the pure `ControllerAimAssist.AdjustLook` maths. Add a `test-mechanic` assertion: with a dummy in the window, input is pulled toward the head; strength 0 means no change.

## Phase 5: Tutorial directional attacks

- [x] **5.1 Directional-attack lesson in the Tutorial Dungeon.**
  - **Done 2026-10-11 (code):** new `Tutorial/DirectionalAttackStep.cs` tracks which of the three swings (overhead / right / left) have landed on a training dummy, shows a ticked counter line and completes when all three are in. Swing side now rides on the hit: `Damage.meleeSwingDirection` (nullable `SwingDirection`) is stamped in `DamageTrigger.BuildDamage` from the new `PlayerAttack.CurrentSwingDirection`, so the dummy reads direction straight off its own `OnDamaged` — no reaching into player internals. The dummy is kept alive through the lesson via `Health.TryPreventDeath` so a hard overhead can't end it early. Loc: `tutorial.directional`, `tutorial.directional_tip`, `tutorial.swing.overhead/right/left`. Scene dummy + door wiring and the step's place in `TutorialDirector.steps`: `plans/editor/22-playtest-feedback-oct10.md`.
  - `Tutorial/TutorialDirector.cs`, `TutorialStep.cs`, `TutorialHint.cs`, `TutorialGateOpener.cs`.

## Phase 6: Meta and Rest Area lag

- [x] **6.1 Profile.** Profiled both hub scenes in play mode via `unityMCP manage_profiler` (Render counters + FrameTimingManager). **Top offender in both: a pile of realtime shadow-casting additional lights with no baked lighting.** Neither scene has baked lightmap data (not in `Bladehold Scenes/` lightmap folders), and `MixedBakeMode` is Subtractive, so every Mixed light runs fully realtime; at runtime all lights report `lightmapBakeType=Realtime`.
  - **Meta Area:** 32 lights, 30 shadow casters (21 Point + 9 Spot), main directional inactive. Baseline render counters: **Draw Calls 3981, Batches 3936, SetPass 359, Triangles 3.16M, Shadow Casters 2300.** The `Shadows.*` CPU counters (DrawSRPBatcher, ExecuteDrawShadows, CullShadowCasters…) dominated the main thread; main-thread frame ~26–30 ms in-editor. Point-light shadows are the worst (6 cube faces each).
  - **Rest Area:** 10 lights, 8 shadow casters (7 Point + 1 Directional sun). Baseline: **Draw Calls 4395, Batches 4371, SetPass 257, Triangles 1.73M, Shadow Casters 2922.**
  - Secondary (minor): the three Meta pedestals (`WeaponPedestal`/`MountPedestal`/`ArmourPedestal`) called `Camera.main` (a tagged Find + alloc) every frame to billboard their world canvas. No per-frame `Find`/alloc in the Rest Area shop/station scripts. No particle-overdraw hotspot found (10 particle systems each, none large).
- [x] **6.2 Fix the top offenders.** Killed the realtime additional-light shadow explosion and cached the pedestal camera. Scene edits saved to the two `.unity` assets; C# compile-checked clean.
  - **Meta Area:** disabled realtime shadows on all 21 Point + 9 Spot fill lights, kept one bright central Spot (intensity 50, single shadow map) for character grounding. **After: Draw Calls 1685 (−58%), Batches 1625 (−59%), SetPass 240, Triangles 1.40M (−56%), Shadow Casters ~0.** Main-thread frame ~25 ms.
  - **Rest Area:** disabled realtime shadows on the 7 Point lights, kept the directional sun's soft shadow (whole-scene grounding). **After: Draw Calls 3058 (−30%), Batches 3038 (−30%), SetPass 145 (−44%), Triangles 0.91M (−47%), Shadow Casters 1757 (−40%, the single sun pass).**
  - Secondary: `Camera.main` is now cached per pedestal (resolved lazily, re-resolved if null) in the three Meta pedestal scripts.
  - Note: in-editor play-mode frame times are noisy (editor overhead, the profiler query itself spikes a frame); the Render counter reductions are the reliable before/after signal and translate directly to player builds.
  - **Visual review needed** (lighting look with fewer shadows): see `plans/editor/22-playtest-feedback-oct10.md`.

## Phase 7: Wave cards choose enemies, objectives become optional

This **reverses plan 15's design table** (stance, offence failure, card-chosen objective). Update plan 15's decisions table in slice 7.1.

Assumptions (change in session if Lance disagrees):
- The card no longer has a stance; skulls stay and drive count/HP/captain.
- One optional bonus objective is rolled per wave independently of the card and shown on the HUD as "Bonus".
- The gate is still the run's only failure state.

- [x] **7.1 Card data: enemy composition instead of objective.** `Waves/WaveChoice/WaveCard.cs`, `WaveCardGenerator.cs`, `WaveChoiceConfigSO.cs`. A card holds enemy types + counts (rolled from `Config/Enemies.csv` threat/wave gating), an optional captain, skulls, and the reward bundle. `GameLoopManager` spawns from the card's composition. Rewards are granted on **surviving** the wave. _Done (2026): `WaveEnemyEntry` + `composition` on `WaveCard`; new pure `WaveCompositionRoller` (fodder floor + weighted variety from the roster, scene-roster allow-list aware); `WaveCardGenerator.Build` rolls it (skipped for the captain wave / dev scenes with no roster); `SurvivorsSpawner.SetWaveComposition` bag + `NextCompositionType`, trickle disabled in composition mode; `GameLoopManager` spawns the composition, subscribes to `OnWaveWiped` to end the wave on survival (`success=true` → reward), and demotes the objective to a non-gating bonus (`compositionWaveActive`). Falls back to the old quota path when a card has no composition. `dotnet build` + Unity compile clean. Objective fail-path removal + bonus payout is 7.3; card/HUD UI is 7.4._
- [x] **7.2 Battering ram as an enemy type.** Move `Objectives/BatteringRam.cs` out of `StopBatteringRamObjective` into the roster (`add-enemy-type` skill): an `Enemies.csv` row the card can roll, spawned at the ram lane, threat = gate damage. Retire `StopBatteringRamObjective` from the pool; keep `Editor/BatteringRamTest.cs` passing. _Done (2026): `StopBatteringRamObjective` renamed in place (same GUID) to `BatteringRamLane` — a plain spawn/destination marker, so existing prefab/scene instances keep their refs; `stop_battering_ram` row removed from `WaveObjectives.csv`. `BatteringRam` self-initialises from the nearest lane (else nearest gate), registers in a static `Active` list (`NearestRolling` for escorts), takes `SetDamage` from the roster and stops when routed. `Enemies.csv` row `battering_ram` (HP 1000, gate dmg 50, minThreat 1, minWave 2, maxConcurrent 1) + `EnemyPrefabMap` entry; `SurvivorsSpawner` spawns ram prefabs at the lane. Assumption: `WaveCompositionRoller` now also treats `maxConcurrent` as a per-wave cap, so a card never rolls more rams than that (overflow goes to fodder). Benchmark 18D added and passing. A rolled ram counts toward the wave, so the wave ends only once it's broken._
- [x] **7.3 Optional objectives.** `Objectives/SurvivorsObjectiveManager.cs` + every `ISurvivorsObjective`: remove the wave-fail path, completion pays a bonus (gold/supply/etc. per objective), failure/timeout just clears the objective. Wave end is driven by the card's enemies, not the objective. _Done (2026): `WaveObjectives.csv` gained `bonusGold`/`bonusSupply` columns (parsed onto `WaveObjectiveDefinition`, `HasBonus`): Hold the Gate/Captain 0, Siegebreaker + Goblin Rush 40g/20s, Prisoners + Siege Engines 60g/30s, Wagon 40g/60s, Golden Goblin 100g. `GameLoopManager.PayObjectiveBonus` pays it on completion (gold popup, `Summary.RecordBonus`, new `OnBonusObjectiveCompleted` event + `BonusGold/SupplyThisWave` for 7.4's UI); in composition waves a completed or failed objective is cleared 3 s later and never touches the wave. Legacy quota waves (captain wave, roster-less scenes, Golden Goblin) still end on the objective, but a failure now resolves the wave as **survived** (card reward kept). The failed-wave gate-assault path is gone (`GateAssaultRoutine`, `IsGateAssault`, `OnGateAssaultStarted`, its banner and `gateAssaultMaxSeconds`). Objectives themselves needed no logic change (they only raise `OnFailed`); their "card reward is lost" comments are updated, as is `Waves/CLAUDE.md`. Assumption: bonuses are flat (not scaled by skulls). Benchmark check added and passing._
- [ ] **7.4 Card + HUD UI.** `UI/WaveCardUI.cs` shows enemy icons/counts, captain and reward; `UI/ObjectiveTrackerUI.cs` labels the objective "Bonus" with its reward; `UI/WaveClearedBannerUI.cs` shows base + bonus rewards. Human UI review.

## Phase 8: Putrid Horror (Graveyard elite)

Model: `Synty/PolygonFantasyRivals/Prefabs/Characters/SM_Chr_BR_MutantGuy_01.prefab`, weapon `.../Weapons/SM_Wep_MutantGuy_01.prefab`. Use `add-enemy-type` + `generate-enemy-prefabs` (no hand-wired prefabs).

- [ ] **8.1 Putrid acid pool hazard.** Reusable component + prefab: 2 dps to the player only while standing in it, finite lifetime, very obvious green bubbling VFX/decal (prefab, MMF for audio), tunables on an SO. Plus a trail emitter that drops pools at an interval while moving.
- [ ] **8.2 Mini mutant.** Same model at reduced scale, melee 5 damage, leaves the acid trail. Enemies.csv row, not drafted on its own outside the Graveyard.
- [ ] **8.3 Putrid Horror body.** ~2× human scale, slow melee when close dealing 20 damage at the swing apex (animation event via `AnimationEvents`), MutantGuy weapon attached, acid trail, spawns mini mutants around himself every 10 s (cap the live count).
- [ ] **8.4 Acid spit.** Arcing projectile at the player that leaves an acid pool on impact; telegraphed and dodgeable.
- [ ] **8.5 Graveyard gating + checks.** Elite flag so he only appears in `Bladehold Graveyard` (and on Phase 7 cards there), benchmark assertions for damage numbers (20 / 5 / 2 dps), Enemy Zoo spot-check, changelog.
