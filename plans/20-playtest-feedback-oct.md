# 20: Playtest feedback (2026-10-06)

**Goal:** fix everything from Lance's 2026-10-06 playtest. Work through it **one phase per session**: open a fresh session, say "execute phase N of `plans/20-playtest-feedback-oct.md`", finish it, compile-check, tick the boxes, then commit. Each phase touches a different area, so they don't share files. Do them in order: Phase 1 has the blockers.

Rules from `/CLAUDE.md` and `plans/README.md` apply: MMF for every sound/VFX/shake (`feel-integration` skill), no visuals built in code, prefab-based UI (`modify-ui` skill), and Editor-only steps go in `plans/editor/20-playtest-feedback-oct.md`. Paths are relative to `Assets/Bladehold/Bladehold Scripts/`. File pointers are first guesses from a quick grep, so check them.

> Screenshots referenced in the feedback (Image #7 ultimate HUD, Image #8 end-of-demo screen) weren't saved with this plan. Ask Lance for them, or take your own via MCP, before starting those items.

---

## Phase 1: Blockers in the tutorial/defense flow

- [ ] **No victory after the tutorial's post-wall-building waves.** After the defence waves, every wall and tower disappears and no victory screen shows. The objective probably completes, but the hand-off to victory never fires (`Tutorial/TutorialDirector.cs`, the last `TutorialStep`, `GameLoopManager.TriggerVictory`, the tower refund on leaving a sector). The walls vanishing looks like the victory "towers dismantled" refund running without the screen. Trace it and fix it.
- [ ] **Enemies get inside the gate.** They walk through the door, turn around and attack the gate from the inside. This happened before Lance moved the gates back and still happens after. Check the NavMesh carving/obstacle on the gate, whether the gate is still on the baked NavMesh after the move (rebake?), and the enemy target point (which side of the gate they aim for). If it's a generated scene, fix the spec or generator (`generate-defense-scene` skill) rather than hand-editing.
- [ ] **Prewarm leftovers on scene load.** Blood stains and health bars from prewarmed enemies are visible when the scene loads (`Waves/EnemyPrewarmer.cs`, `EnemyPrewarmConfigSO.cs`). Prewarming has to hide or suppress decals and health-bar UI, or clear them before the first frame shows.

## Phase 2: Quick text, data and audio fixes

- [ ] **Death text.** `UI/DeathScreen.cs:269` says "YOU DIDN'T HOLD THE DOOR". On player death it should say **"The hero has fallen"**. Keep the gate-destroyed wording if that's a separate failure path. Update localization if the string is keyed.
- [ ] **Remove the Golden Goblin objective** from the `SurvivorsObjectiveManager` pool (`Objectives/GoldenGoblinObjective.cs`; see the `add-objective` skill for the pool and cleanup). Remove or park the code, and check nothing else references it (DevConsole, benchmark).
- [ ] **Tutorial hint sound is too loud or harsh.** Tone down its MMF (`Tutorial/TutorialHintUI.cs` / the hint prefab's feedback).
- [ ] **Tutorial hint pulse is too strong.** Lower the scale/amplitude of the pulse tween.
- [ ] **Rope snap sound.** Shooting the ropes in the tutorial needs a satisfying rope-snap or fabric-tear sound (`Tutorial/TutorialBreakable.cs` / `DestroyTargetsStep.cs`). Source it with `find-and-import-assets`.
- [ ] **Fishing level-up sound.** Play a victory sting on fishing level-up.
- [ ] **Sanctuary of Spirits hover sound only plays once.** Hovering a button makes a sound the first time and never again. Probably a one-shot MMF that isn't reset, or the feedback being disabled after the first play.

## Phase 3: Combat ability fixes

- [ ] **Blazing Trail on dash doesn't work** (`Player/PlayerDodge.cs`, `Player/FireTrailSegment.cs`). Check that the draft card's stat is read, that segments spawn, and that they deal damage. Add a benchmark check (`test-mechanic` skill).
- [ ] **Colossal Smash (full-charge rock slam draft):** spawn `FX_GroundCrack_Blase_01` instead of the current rock effect, and place it **at the enemy's feet** (raycast or NavMesh-sample down to the ground) instead of mid-air (`Player/MaceCombatController.cs` / the smash handler).

## Phase 4: HUD and prompt readability

- [ ] **Ultimate HUD placement** (`UI/UltimateBarUI.cs` + HUD prefab). It currently looks odd and covers the other controls (Image #7). Re-lay it out so it looks deliberate and doesn't overlap.
- [ ] **Interact prompt.** Make it easier to read (backing plate, outline, size) and use input glyphs instead of plain text. When the player can't afford the action (build a wall, resupply ammo, and so on), play a subtle **denied** sound and **pulse the prompt red**.
- [ ] **Horse "hold to charge" tip** shows a generic keyboard icon instead of the **Shift** glyph. Fix the binding/glyph lookup (`Tutorial/ChargeStep.cs` / `FirstTimeHints.cs`).
- [ ] **World events UI is too big.** Make it smaller and move it higher so it doesn't block the view (`UI/WorldEventBannerUI.cs`, `WorldEventEffectRowView.cs`).
- [ ] **Meta area currencies don't update on the HUD after a purchase.** Refresh on purchase (subscribe to the currency-changed event instead of reading once).
- [ ] **Campaign map doesn't show metal.** Add metal to the map's currency display.

## Phase 5: Audio state

- [ ] **Horse gallop keeps playing behind menus.** Pause it while a menu or pause screen is open and resume it when the menu closes (`Horse/HorseHoofbeatAudio.cs`). Prefer one hook on the shared pause/menu-open signal over per-menu calls.

## Phase 6: Flow and screen polish

- [ ] **Rest Area: one exit door** that leads back to the campaign map. Remove the other doors (`UI/RestArea/RestAreaGate.cs`, `RestAreaDoor`).
- [ ] **End-of-demo "Thanks for playing" screen.** Restyle it to the house UI theme (Image #8) and add a **Wishlist on Steam** button that opens `https://store.steampowered.com/app/5279820/Bladehold/` (`Application.OpenURL`, gamepad-focusable).

## Phase 7: New juice/features (requested during the freeze)

These are new features, so confirm the scope with Lance before starting each one.

- [ ] **Gate-destroyed cinematic.** When the gate explodes, cut to a cinematic Cinemachine camera on the gate with a slow-mo explosion for 3–4 seconds (unscaled-time MMF), then show the failed screen.
- [ ] **Horse stall at the gate.** During the prep phase, a stall next to the gate sells a horse for **500g**. Uses the interact prompt from Phase 4, including the denied state. Decide what happens if the player already has a mount.
- [ ] **Weapon unlock fanfare.** Unlocking a new weapon gets a big moment: sound, banner and sparks (MMF + a banner prefab).

---

## Acceptance (per phase)

- `dotnet build` is clean (`compile-check` skill).
- Player-visible changes get `CHANGELOG.md` entries (`changelog` skill).
- Editor-only leftovers go in `plans/editor/20-playtest-feedback-oct.md`.
- Each item gets a playtest note so Lance knows what to check.
