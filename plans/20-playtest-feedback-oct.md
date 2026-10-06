# 20: Playtest feedback (2026-10-06)

**Goal:** fix everything from Lance's 2026-10-06 playtest. Work through it **one phase per session**: open a fresh session, say "execute phase N of `plans/20-playtest-feedback-oct.md`", finish it, compile-check, tick the boxes, then commit. Each phase touches a different area, so they don't share files. Do them in order: Phase 1 has the blockers.

Rules from `/CLAUDE.md` and `plans/README.md` apply: MMF for every sound/VFX/shake (`feel-integration` skill), no visuals built in code, prefab-based UI (`modify-ui` skill), and Editor-only steps go in `plans/editor/20-playtest-feedback-oct.md`. Paths are relative to `Assets/Bladehold/Bladehold Scripts/`. File pointers are first guesses from a quick grep, so check them.

> Screenshots referenced in the feedback (Image #7 ultimate HUD, Image #8 end-of-demo screen) weren't saved with this plan. Ask Lance for them, or take your own via MCP, before starting those items.

---

## Phase 1: Blockers in the tutorial/defense flow

- [x] **No victory after the tutorial's post-wall-building waves.** After the defence waves, every wall and tower disappears and no victory screen shows. The objective probably completes, but the hand-off to victory never fires (`Tutorial/TutorialDirector.cs`, the last `TutorialStep`, `GameLoopManager.TriggerVictory`, the tower refund on leaving a sector). The walls vanishing looks like the victory "towers dismantled" refund running without the screen. Trace it and fix it.
- [x] **Enemies get inside the gate.** They walk through the door, turn around and attack the gate from the inside. This happened before Lance moved the gates back and still happens after. Check the NavMesh carving/obstacle on the gate, whether the gate is still on the baked NavMesh after the move (rebake?), and the enemy target point (which side of the gate they aim for). If it's a generated scene, fix the spec or generator (`generate-defense-scene` skill) rather than hand-editing.
- [x] **Prewarm leftovers on scene load.** Blood stains and health bars from prewarmed enemies are visible when the scene loads (`Waves/EnemyPrewarmer.cs`, `EnemyPrewarmConfigSO.cs`). Prewarming has to hide or suppress decals and health-bar UI, or clear them before the first frame shows.

### Phase 1 notes (2026-10-06)

- **Victory:** `Bladehold Tutorial Gate.unity` had no `DeathScreen` (added in `497ada289`, then lost in `dbead25ae`). `TriggerVictory` dismantled the towers and walls for the refund, then found no end screen and did nothing. Player death in T3 had the same gap. Fix: re-added `DeathScreen.prefab` at the scene root and checked in Play mode that VICTORY! shows. `GameLoopManager.TriggerVictory` now logs an error if a scene has no end screen.
- **Prewarm leftovers:**
  - Decals: `BloodDecalManager.ClearAll()` now runs at the end of the rehearsal. The decal holder is DontDestroyOnLoad, so this also clears the previous scene's decals.
  - Health bars: Feel's `MMHealthBar` builds its drawn bar as a separate root object (`ParentHealthBar|…`) and never destroys it. `HealthBarUI.OnDestroy` now destroys that container, which also stops the leak on every corpse despawn. Both checked in Play mode (12 decals and 4 orphaned bars before, 0 after).
- **Gate pathing (it was the built walls, not the castle gate):** the `ShortWallWithGate` gatehouse has its portcullis, door `Blocker` and interact point 4.6 m out from the wall line. `WallStructure` still put its face at `Thickness * 0.5`, so the wall claimed goblins as they arrived and sent them to an attack point *behind* the portcullis. The door's non-carving obstacle and the gatehouse colliders don't stop NavMesh agents, so they walked through the gate and attacked it from inside. Fix: the new `WallStructure.FaceDepth` takes the face from the door Blocker; attack points, the claim box and `FindBlockingAhead` use it. Checked in Play mode (T3, 3 walls, 40 goblins, player behind Wall_2): attack points moved from z=51 to 55.7, and goblins attack from in front of the gates until a wall falls. `Fort/CLAUDE.md` updated.
  - Follow-up (Lance: "they attack off to the side, and sometimes teleport through the gate to get me"):
    - Attack points are now limited to the door blocker's width (`GetDoorSpan`), not the whole wall.
    - `IsOutside` now measures from the face. A player standing inside a deep gatehouse used to count as "outside", so nearby goblins dropped the wall and walked through the portcullis to reach them.
    - The claim box now reaches back to the wall line, so anything shoved into the gatehouse is claimed.
    - Retest: player inside the Wall_2 gatehouse, 40 goblins. All 40 claimed by walls, 0 targeting the player, 0 damage taken, none past the wall line. The overflow lines up beside the gate; a few get pushed just inside it by crowd pressure and keep attacking the gate.
  - Testing gotcha: the scattered NullReferenceExceptions (bow UI, `AIAnimation`, Feel `MMTimeManager`) and a null `Player.Instance` came from script recompiles landing *during* Play mode, which wipe every static. Wait for compilation to finish before entering Play.

## Phase 2: Quick text, data and audio fixes

- [x] **Death text.** `UI/DeathScreen.cs:269` says "YOU DIDN'T HOLD THE DOOR". On player death it should say **"The hero has fallen"**. Keep the gate-destroyed wording if that's a separate failure path. Update localization if the string is keyed.
- [x] **Remove the Golden Goblin objective** from the `SurvivorsObjectiveManager` pool (`Objectives/GoldenGoblinObjective.cs`; see the `add-objective` skill for the pool and cleanup). Remove or park the code, and check nothing else references it (DevConsole, benchmark).
- [x] **Tutorial hint sound is too loud or harsh.** Tone down its MMF (`Tutorial/TutorialHintUI.cs` / the hint prefab's feedback).
- [x] **Tutorial hint pulse is too strong.** Lower the scale/amplitude of the pulse tween.
- [x] **Rope snap sound.** Shooting the ropes in the tutorial needs a satisfying rope-snap or fabric-tear sound (`Tutorial/TutorialBreakable.cs` / `DestroyTargetsStep.cs`). Source it with `find-and-import-assets`.
- [x] **Fishing level-up sound.** Play a victory sting on fishing level-up.
- [x] **Sanctuary of Spirits hover sound only plays once.** Hovering a button makes a sound the first time and never again. Probably a one-shot MMF that isn't reset, or the feedback being disabled after the first play.

### Phase 2 notes (2026-10-06)

- **Death text:** gate battles had a hardcoded "YOU DIDN'T HOLD THE DOOR" branch. It's gone, so every player death uses `death.player_title`, now "The Hero Has Fallen" in all languages. The gate-destroyed path is unchanged.
- **Golden Goblin:** parked, not deleted (Lance's call after auto mode blocked the prefab delete). The CSV row is `draftable=false`, so cards never offer it. Removing it from the prefab's fallback pool is optional, in the editor checklist.
- **Hint sound and pulse:** the HUD's hint Show/Complete scale peaked at 2.5x. It's now about 1.15x. The chime is at 0.35 volume and 0.9 pitch, and the counter bump is 15–20 (was 40–60).
- **Rope snap:** Lance picked `FABRIC_Tear_05_Quick` (imported). It's on all three ropes' SnapMMF.
- **Fishing level-up:** new `FishingManager.levelUpFeedback` → `LevelUpMMF` (`SuccessBig.wav`, unscaled), once per catch.
- **Hover once:** HoverMMF's 0.06 s cooldown ran on scaled time and never expired at timeScale 0. Every HoverMMF (5 prefabs, 5 scenes) and both UI builders are now unscaled.
- Playtest checklist: `plans/editor/20-playtest-feedback-oct.md`.

## Phase 3: Combat ability fixes

- [x] **Blazing Trail on dash doesn't work** (`Player/PlayerDodge.cs`, `Player/FireTrailSegment.cs`). Check that the draft card's stat is read, that segments spawn, and that they deal damage. Add a benchmark check (`test-mechanic` skill).
- [x] **Colossal Smash (full-charge rock slam draft):** spawn `FX_GroundCrack_Blase_01` instead of the current rock effect, and place it **at the enemy's feet** (raycast or NavMesh-sample down to the ground) instead of mid-air (`Player/MaceCombatController.cs` / the smash handler).

### Phase 3 notes (2026-10-06)

- **Blazing Trail:** the card and stat were fine. `PlayerDodge` sits on the static `Player` root, but the CharacterController (the body that moves) is on the child `SidekickSyntyCharacter`. Every `transform.position` in the dash read the root, which never moves. So the trail dropped one segment back at the Player prefab's scene position, and the 0.6 m spacing check never fired again.
  - Fix: a `Body` property (the CharacterController's transform) for every dash position: trail, hit sphere, dash VFX parent, Frost Step pulse, Nimble Strike feedback and the dash-facing rotation (it used to rotate the root, swinging the body around the spawn point).
  - Trail spacing now walks the gap in 0.6 m steps, so a low-fps frame can't leave holes.
  - Side effect: Nimble Strike, the dash damage/knockback cards and Frost Step had the same bug (they hit around the spawn point), so they're fixed too.
  - Play mode check (Enemy Zoo): segments spawn along the dash with VFX. A Troll standing in the trail lost 13 HP over 3 s at 4 DPS.
  - Benchmark section 6 used to re-implement the trail loop on a test object, so it passed while the real code was broken. It now drives the real `PlayerDodge` on a root/body rig shaped like the prefab and checks a segment actually burns an Enemy-layer target.
- **Colossal Smash** is the Earthshaker card (`mace_seismic_shock`, `MaceShockwaveDamage`). `MaceShockwaveMMF` on `Player.prefab` now spawns `FX_GroundCrack_Blast_01` (the plan's "Blase" was a typo) instead of `FX_ShardRock_Explosion_01_NoLoop`. The shockwave centre is the struck enemy's feet (its `Health` root), raycast down onto non-enemy geometry with a NavMesh fallback. Play mode check: hit point at 1.43 m, crack at y 0.00 under the Troll, 15 damage dealt. Also in benchmark section 6.
- **Same root/body bug elsewhere (not fixed, outside this phase):** other components on the Player root still use `transform.position`: `MageUltimate` (hover/raycast/centre), `PlayerSummonMount` (spawn feedback position), `PlayerArmourManager` (equip feedback), `PlayerInteraction` (fallback origin), and the `PlayerUltimateController` fallbacks. Worth a pass, especially `MageUltimate`.
- Benchmark: 180 passed, 4 failed. The 4 failures (Bulwark attack, Fishing Pond node count, Bannerman rig, Victory next label) fail the same way without these changes.

## Phase 4: HUD and prompt readability

- [x] **Ultimate HUD placement** (`UI/UltimateBarUI.cs` + HUD prefab). It currently looks odd and covers the other controls (Image #7). Re-lay it out so it looks deliberate and doesn't overlap.
- [x] **Interact prompt.** Make it easier to read (backing plate, outline, size) and use input glyphs instead of plain text. When the player can't afford the action (build a wall, resupply ammo, and so on), play a subtle **denied** sound and **pulse the prompt red**.
- [x] **Horse "hold to charge" tip** shows a generic keyboard icon instead of the **Shift** glyph. Fix the binding/glyph lookup (`Tutorial/ChargeStep.cs` / `FirstTimeHints.cs`).
- [x] **World events UI is too big.** Make it smaller and move it higher so it doesn't block the view (`UI/WorldEventBannerUI.cs`, `WorldEventEffectRowView.cs`).
- [x] **Meta area currencies don't update on the HUD after a purchase.** Refresh on purchase (subscribe to the currency-changed event instead of reading once).
- [x] **Campaign map doesn't show metal.** Add metal to the map's currency display.
- [x] **(Added by Lance mid-phase) HUD currency panel:** give the currencies their own panel under the minimap on the right.

### Phase 4 notes (2026-10-06)

- **Ultimate HUD:** the two 443-unit orbs sat at x ±260 on top of the melee/ranged/dodge/mount slots. They're now at 75% scale and flank the ability cluster at x ±680 (`Bladehold HUD.prefab` → Bottom/Ult Meter, Ult Meter Ranged), vertically centred on it, clear of the health and gate bars. No scene overrides them.
- **Interact prompt:**
  - `InteractionPrompt.prefab` is rebuilt: a dark sliced plate (same as the tutorial hint panel) that hugs an Interact `InputGlyph` (stretch keycap, follows device and rebinds) and the prompt text (Grenze Underlay, 48).
  - The new `UI/InteractionPromptView` owns it. `PlayerInteraction` finds it in the scene (the HUD nests one) and no longer prefixes "[E] ". TowerPlot's "[E] Build Defence" and gate repair's "[Hold E]" strings lost their key text.
  - **Denied:** the new `IAffordableInteractable.CanAfford` is on WallCraftingStation (build cost), AmmoChest (gold), DefenseStructure (resupply needs any supply, the old Lv-up path needs its cost; the upgrade wheel prices itself), GateRepairStation and `Interactable` (meta pedestals set it). Pressing interact on one you can't afford skips `Interact` and plays the prompt's `DeniedFeedback`: the shop slot's invalid pattern (shake, a red flash on a separate `DeniedFlash` layer, Feel's Denied sound at 0.45), on unscaled time.
  - Meta pedestals used to set `CanInteract = false` when you couldn't afford an unlock, which hid the prompt. They now keep it and set `CanAfford`; their handlers still re-check the metal.
- **Shift glyph:** no Shift sprite exists, so "Left Shift" was wrapped onto a 60 px square keycap and read as a blank key. Both tutorial hint glyphs now use the stretch keycap (`UI_Keycap_Sliced`, like the rebind grid) with no-wrap text. `InputGlyph` also shortens "Left/Right Shift/Ctrl/Alt" to the bare key on prompts (the pinned rebind grid keeps the side).
- **World events:** `WorldEventBanner.prefab` Banner scale 0.9 → 0.62, y -215 → -92. It now ends about 31% down the screen instead of 52%. The timer chip moved from y -400 to -478 so it sits under the new currency panel.
- **Meta currencies:** the pedestals and `MetaUpgradesUI` subtracted metal and blood straight from `SaveData`, so `OrcishMetalUI`/`GoblinBloodUI` never heard about it. New `RunSession.SpendGoblinBlood` / `SpendOrcishMetal` raise the change events. All 5 spend sites use them.
- **Campaign map metal:** it was wired and showing (checked in Play mode: 9 metal), but the icon was a thin blue hammer silhouette that read as an arrow. It's now the iron ingot the battle HUD uses for metal. If Lance meant something else (e.g. a label), ask.
- **HUD currency panel:** `Top Left/Currencies` is gone. Its five entries live in `ScreenSpace/Currency Panel`: a dark plate under the minimap (600 wide, rows Gold | Supply, Blood | Metal, then Crystals, which hides itself at 0). The new `UI/HudCurrencyPanel` docks it under the minimap frame and moves it into the top-right corner when the scene has no minimap. `BuildCastleLevels.EnsureDefensesHUD` still looks for the old path but is null-guarded (legacy builder).
- Edit-mode screenshots of the HUD need the canvas switched to Screen Space - Camera with the plane just past the camera's near clip, or world geometry draws over the UI. Dynamic TMP atlases render with missing glyphs until Play.

## Phase 5: Audio state

- [x] **Horse gallop keeps playing behind menus.** Pause it while a menu or pause screen is open and resume it when the menu closes (`Horse/HorseHoofbeatAudio.cs`). Prefer one hook on the shared pause/menu-open signal over per-menu calls.

### Phase 5 notes (2026-10-06)

- **Gallop behind menus:** the gait loop is one `AudioSource` that never stops. `Update` fades its volume with `Time.deltaTime`, so at timeScale 0 the fade froze wherever it was and the loop kept playing at that volume.
- **Shared signal:** there's no common menu-open event. `PauseMenuController.OnPauseChanged` only covers the pause menu, while the draft, shop, meta upgrades, death screen, enemy intro and fishing draft each set `Time.timeScale = 0` themselves. Every one of them freezes time, so `HorseHoofbeatAudio` treats `Time.timeScale == 0` as "a menu is open": it `Pause()`s the source and `UnPause()`s it when time resumes, so the gait picks up where it left off.
- **Hitstop:** the Player has `MMF_TimescaleModifier` feedbacks. The new `freezeGraceTime` (0.15 s unscaled) means time must stay frozen that long before the loop pauses, so a freeze frame doesn't chop the gait.
- Not checked in Play mode (a horse must be summoned and ridden). It's in the editor checklist.

## Phase 6: Flow and screen polish

- [x] **Rest Area: one exit door** that leads back to the campaign map. Remove the other doors (`UI/RestArea/RestAreaGate.cs`, `RestAreaDoor`).
- [x] **End-of-demo "Thanks for playing" screen.** Restyle it to the house UI theme (Image #8) and add a **Wishlist on Steam** button that opens `https://store.steampowered.com/app/5279820/Bladehold/` (`Application.OpenURL`, gamepad-focusable).

### Phase 6 notes (2026-10-06)

- **Rest Area exit door:** Lance already did this before the session.
- **Thanks for playing screen** (`DemoEndScreen.prefab`, shown by `CampaignMapUI` once the demo tier is cleared):
  - Restyled with the new `ScreenRestyleBuilder.RestyleDemoEnd` (**Bladehold > UI > Restyle > Demo End Screen**, also in All Screens), the same in-place reskin as the death screen and save slots. It uses the CampaignMap theme. The torn parchment is now the menu window (dark fill, gold frame), with a spaced accent title, flourishes, a gold rule, Grenze body text, and a dimmer plus vignette over the map. Wishlist on Steam is the primary parchment button and Play Again is a ghost button, both with hover glow and juice. The hierarchy and references are unchanged, so re-run the builder rather than hand-editing.
  - **The Wishlist button already existed but never showed:** `DemoEndScreenUI` hides it when `DemoConfig.steamStoreUrl` is empty, and it was empty. It's now set to `https://store.steampowered.com/app/5279820/Bladehold/` (`Application.OpenURL`).
  - Gamepad: `Show()` now selects Wishlist first (Play Again if there's no URL). Left/right moves between the two. The panel's `MenuFocusController` still traps focus.
  - Checked with an off-screen render of the prefab at 1920x1080. Not checked in Play mode: reaching the demo end needs the cutoff tier cleared.

## Phase 7: New juice/features (requested during the freeze)

These are new features, so confirm the scope with Lance before starting each one.

- [x] **Gate-destroyed cinematic.** When the gate explodes, cut to a cinematic Cinemachine camera on the gate with a slow-mo explosion for 3–4 seconds (unscaled-time MMF), then show the failed screen.
- [x] **Horse stall at the gate.** During the prep phase, a stall next to the gate sells a horse for **500g**. Uses the interact prompt from Phase 4, including the denied state. Decide what happens if the player already has a mount.
- [x] **Weapon unlock fanfare.** Unlocking a new weapon gets a big moment: sound, banner and sparks (MMF + a banner prefab).

### Phase 7 notes (2026-10-06)

- **Gate-destroyed cinematic** (Lance picked a hard cut with the HUD hidden):
  - The new `Waves/GateFallCinematic` lives on `CameraRig.prefab`, so every gate scene has it.
  - `DeathScreen.HandleGateDestroyed` hands off to it. It cuts to `Gate Fall Camera` (brain blend set to Cut), framed from the attackers' side (`Gate.OutwardDirection`, three-quarter angle) with a slow push-in. It hides the HUD except the letterbox (`EnemyIntroUI.ShowLetterbox`), holds timeScale 0.25 for 3.5 s real time, then calls back so the death screen freezes time and shows the failure.
  - It re-asserts the timescale every frame so a hitstop can't snap time back to normal. A player death during the cinematic is ignored.
  - `GateDestructionMMF`'s particles moved from `FireExplosionUnscaled` (which plays at full speed) to the scaled `FX_Fire_Explosion_Large_01`, so the blast actually slows down. A scene without the cinematic logs an error and falls back to the old freeze.
  - Checked in Play mode in Outer Gate and Graveyard. The Graveyard's green fog swallows the fireball (see the editor checklist).
- **Horse stall and weapon fanfare:** started in this session but **not committed**. **Horse stall finished and committed in a follow-up session (2026-10-06):** checked in Play mode, prompt strings localised, the fallen-horse hint now names the stall. They're still in the working tree for the next session: `Horse/HorseStall.cs`, `Economy/HorseStall.prefab` (placed in Outer, Desert and Graveyard; generator spec fields `placeHorseStall`/`horseStallPosition`; off in the Tutorial Gate spec), and the `WeaponPedestal` unlock fanfare (`UnlockMMF` on `Pedestal_Weapon.prefab`, banner via `EnemyIntroUI`). Lance's horse decision: the stall lets you rebuy your horse after it dies. Neither has been checked in Play mode.
- **Weapon unlock fanfare** (finished 2026-10-06): `WeaponPedestal.PlayUnlockFanfare` runs after a successful Orcish Metal unlock. It plays `UnlockMMF` on `Pedestal_Weapon.prefab` (the `Fanfare Win` sting and a level-up particle burst at the floating model) and shows the HUD banner through `EnemyIntroUI.ShowIntro` with no skulls and no letterbox: eyebrow "New weapon unlocked", the weapon name, and "Ultimate: X" when the weapon has one. New strings `weapon_unlock.eyebrow` and `weapon_unlock.ultimate` are localised. Checked in Play mode in the Meta Area by invoking the fanfare through `execute_code`, which leaves the save untouched: all 5 pedestals have the feedback wired, and the banner reads "New weapon unlocked / DWARVEN AXE / Ultimate: WHIRLWIND". Audio and the burst at the pedestal itself still need checking by ear and eye (see the editor checklist).

---

## Acceptance (per phase)

- `dotnet build` is clean (`compile-check` skill).
- Player-visible changes get `CHANGELOG.md` entries (`changelog` skill).
- Editor-only leftovers go in `plans/editor/20-playtest-feedback-oct.md`.
- Each item gets a playtest note so Lance knows what to check.
