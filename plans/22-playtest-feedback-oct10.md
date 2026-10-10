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
- [ ] **1.2 Glyph missing on tower plot prompt the first time you build.**
  - **Not reproduced 2026-10-10:** played Valley Stronghold on keyboard/mouse and forced gamepad. The plot prompt "Build Defence", the tutorial "[X] Build an Arrow Tower" hint, the build wheel's Cancel glyph and the built tower's "Upgrade" prompt all showed their glyph on the first build. Needs repro details from Lance: which scene, which glyph (HUD prompt, world marker, wheel) and which device. The only blank glyph seen was after a mid-play script reload, which players never hit.
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

- [ ] **2.1 Stamina sources while riding.**
  - Remove passive regen while mounted: `Horse/HorseMotor.cs:468-481` and the run-mount mirror in `Player/PlayerMount.cs:559`. Decide in session whether a dismissed/stabled horse still regens (default: no passive regen at all; carrots and on-foot combat only).
  - Trample kills (`HorseMotor.cs:651`, `StatType.HorseTrampleKillStamina`) must give 0 unless the draft perk raises the stat: confirm the base is 0.
  - On-foot kills/damage keep feeding stamina via `PlayerMount.cs:452` → `AddStamina`. Gate that path to *not mounted*.
  - Update the `HorseMotor` class doc comment (:22).
- [ ] **2.2 Centre-screen charge stamina bar.** A copy of the stamina bar in the middle of the screen, visible only while charging, burning down with a particle effect and a carrot icon. Prefab UI (`ui-mockup`, `mm-progress-bars` skills), MMF for the burn particles. Flag for human UI review.
- [ ] **2.3 "Mount is tired" feedback.** Trying to charge with too little stamina flashes the 2.2 bar (MMF) and shows a localized line: `Your mount is tired` / `{mountGlyph} to rest, or eat some {carrotGlyph}` with glyphs resolved through `InputGlyph`. Find the charge-start check in `Player/MountChargeAbilities.cs` / `HorseMotor`.

## Phase 3: Settings menu tabs

- [ ] **3.1 Split Keyboard & Mouse and Controller settings into separate tabs.**
  - Generated by `Editor/SettingsPanelBuilder.cs` (view: `UI/SettingsPanelView.cs`, service: `Settings/GameSettingsService.cs`). Change the builder and regenerate; never hand-edit the prefab.
  - Tabs must be navigable by gamepad (shoulder buttons) and by mouse. Rebind rows (`UI/RebindButtonView.cs`) go to the matching tab.

## Phase 4: Controller bow aim assist (depends on 3.1)

- [ ] **4.1 Aim assist toward enemy heads.**
  - Bow code: `Player/PlayerBow.cs`, `Player/BowAimLook.cs`, `Player/BowAimCamera.cs`, tunables in `Player/BowSO.cs`.
  - Gamepad only. While aiming, find the best living enemy inside a screen-space window around the reticle (`Health.IsDead` filtered), target its head (Animator `HumanBodyBones.Head`, falling back to collider top), and apply a slowdown + pull of the look input toward it. Never snap; never through walls (line-of-sight check).
  - Defaults on a `ScriptableObject`; strength/window registered as settings.
- [ ] **4.2 Settings sliders.** "Aim assist strength" (0 = off) and "Aim assist window" in the Controller tab (persisted in `SaveData`, bump `settingsVersion`).
- [ ] **4.3 Mechanic check.** Add a `test-mechanic` assertion: with a dummy in the window, input is pulled toward the head; strength 0 means no change.

## Phase 5: Tutorial directional attacks

- [ ] **5.1 Directional-attack lesson in the Tutorial Dungeon.**
  - `Tutorial/TutorialDirector.cs`, `TutorialStep.cs`, `TutorialHint.cs`, `TutorialGateOpener.cs`.
  - New step: hint explaining **Overhead = extra damage, needs precision; Right = wide arc, slower; Left = quick, shorter range**. A training dummy tracks which directions have hit it (listen to the hit/`OnDamaged` with attack direction; add direction to `Damage` only if it's not already exposed), shows 3 ticks, and opens the next door when all 3 land.
  - Localize the strings; the dummy and door wiring in the scene goes via Unity MCP / editor checklist.

## Phase 6: Meta and Rest Area lag

- [ ] **6.1 Profile.** Use `unityMCP manage_profiler` (or the Profiler window) in `Bladehold Meta Area Scene` and `Bladehold Rest Area Scene`: CPU main thread, rendering (batches/shadow casters/realtime lights), GC alloc. Write findings into this slice.
- [ ] **6.2 Fix the top offenders** found in 6.1 (likely candidates: realtime shadow-casting lights, un-baked lighting, per-frame `Find`/allocations in NPC/shop scripts, particle overdraw). Re-profile and record before/after frame times.

## Phase 7: Wave cards choose enemies, objectives become optional

This **reverses plan 15's design table** (stance, offence failure, card-chosen objective). Update plan 15's decisions table in slice 7.1.

Assumptions (change in session if Lance disagrees):
- The card no longer has a stance; skulls stay and drive count/HP/captain.
- One optional bonus objective is rolled per wave independently of the card and shown on the HUD as "Bonus".
- The gate is still the run's only failure state.

- [ ] **7.1 Card data: enemy composition instead of objective.** `Waves/WaveChoice/WaveCard.cs`, `WaveCardGenerator.cs`, `WaveChoiceConfigSO.cs`. A card holds enemy types + counts (rolled from `Config/Enemies.csv` threat/wave gating), an optional captain, skulls, and the reward bundle. `GameLoopManager` spawns from the card's composition. Rewards are granted on **surviving** the wave.
- [ ] **7.2 Battering ram as an enemy type.** Move `Objectives/BatteringRam.cs` out of `StopBatteringRamObjective` into the roster (`add-enemy-type` skill): an `Enemies.csv` row the card can roll, spawned at the ram lane, threat = gate damage. Retire `StopBatteringRamObjective` from the pool; keep `Editor/BatteringRamTest.cs` passing.
- [ ] **7.3 Optional objectives.** `Objectives/SurvivorsObjectiveManager.cs` + every `ISurvivorsObjective`: remove the wave-fail path, completion pays a bonus (gold/supply/etc. per objective), failure/timeout just clears the objective. Wave end is driven by the card's enemies, not the objective.
- [ ] **7.4 Card + HUD UI.** `UI/WaveCardUI.cs` shows enemy icons/counts, captain and reward; `UI/ObjectiveTrackerUI.cs` labels the objective "Bonus" with its reward; `UI/WaveClearedBannerUI.cs` shows base + bonus rewards. Human UI review.

## Phase 8: Putrid Horror (Graveyard elite)

Model: `Synty/PolygonFantasyRivals/Prefabs/Characters/SM_Chr_BR_MutantGuy_01.prefab`, weapon `.../Weapons/SM_Wep_MutantGuy_01.prefab`. Use `add-enemy-type` + `generate-enemy-prefabs` (no hand-wired prefabs).

- [ ] **8.1 Putrid acid pool hazard.** Reusable component + prefab: 2 dps to the player only while standing in it, finite lifetime, very obvious green bubbling VFX/decal (prefab, MMF for audio), tunables on an SO. Plus a trail emitter that drops pools at an interval while moving.
- [ ] **8.2 Mini mutant.** Same model at reduced scale, melee 5 damage, leaves the acid trail. Enemies.csv row, not drafted on its own outside the Graveyard.
- [ ] **8.3 Putrid Horror body.** ~2× human scale, slow melee when close dealing 20 damage at the swing apex (animation event via `AnimationEvents`), MutantGuy weapon attached, acid trail, spawns mini mutants around himself every 10 s (cap the live count).
- [ ] **8.4 Acid spit.** Arcing projectile at the player that leaves an acid pool on impact; telegraphed and dodgeable.
- [ ] **8.5 Graveyard gating + checks.** Elite flag so he only appears in `Bladehold Graveyard` (and on Phase 7 cards there), benchmark assertions for damage numbers (20 / 5 / 2 dps), Enemy Zoo spot-check, changelog.
