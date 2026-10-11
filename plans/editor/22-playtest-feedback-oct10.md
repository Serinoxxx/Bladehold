# Editor checklist: plan 22 (playtest feedback 2026-10-10)

## Slice 1.1: draft card double-highlight on controller

No wiring needed (code only).

Manual verification:
- [ ] With a controller, finish a wave so the upgrade draft opens: only the pad-focused card is lit, and the middle card isn't lit as well.
- [ ] Same check on the wave choice cards and the Fishing draft.
- [ ] Move the mouse over a card: it lights on hover and the pad highlight goes away. Press a stick or d-pad: hover drops and pad focus comes back.
- [ ] Pause menu buttons: no stray hover glow on the button under the hidden cursor while using the pad.

## Slice 1.2: tower plot glyph

- [ ] Fresh scene load: walk up to an empty tower plot. "Build Defence" shows its key/button glyph the first time (keyboard and pad).

## Slice 1.3: red flaming catapult boulder

Wired via MCP: `Mat_FireBoulder.mat`, `RollingFireball.prefab` (rock mesh + `Flames`), `CatapultBoulder.prefab` (`FireVisual` + fire fields).

Manual verification:
- [ ] Build a Fire catapult: the boulder flies red with flames and an orange trail. The rolling fireball after impact is a red tumbling rock with flames, not a sphere.
- [ ] A plain, Ice or Storm catapult still throws the grey rock with the smoke trail.
- [ ] Flame size and brightness look right at gameplay distance (tune the `FireVisual`/`Flames` scale or `Mat_FireBoulder` emission if not).

## Slice 1.4: horse trample

Manual verification:
- [ ] Ride (no charge) into a goblin crowd: they get bumped and take light damage, but no one is knocked back or ragdolled.
- [ ] Charge into a crowd: knockback and ragdolls as before. Enemies beside the horse but not in front are no longer hit.
- [ ] The Mounted Knight's charge still connects with the player.

## Slice 2.1: horse stamina sources

Manual verification:
- [ ] Mounted, not charging: the stamina bar doesn't refill on its own. Dismounted: banked stamina doesn't refill on its own either.
- [ ] Kills from the saddle (sword/bow) add no stamina. Kills on foot do (the bar on the HUD rises).
- [ ] Carrots still refill. With the Bloodlust card, trample kills refund stamina.
- [ ] Tutorial (Valley Stronghold) stamina step shows the new two-line hint and still completes.

## Slices 2.2 + 2.3: centre charge bar and "mount is tired"

Built via MCP: `Bladehold Prefabs/UI/ChargeStaminaCentre.prefab` (nested in `Bladehold HUD.prefab` > `Screen_HUD_Adventure_01/ScreenSpace`) and `ChargeEmber.prefab`.

Manual verification:
- [ ] Charge on the horse: a large stamina bar with a carrot icon fades in just below the centre of the screen, burns down with ember sparks off the fill edge, and fades out when the charge ends.
- [ ] Try to charge while the horse is exhausted: the bar flashes red and bumps, and "Your mount is tired / [X] to rest, or eat some [carrot]" shows for about 2.5 s. The glyph matches the device (X on keyboard, D-pad up on pad) and follows rebinds.
- [ ] Run stamina to zero mid-charge: the same flash and message appear.
- [ ] **UI review (agent mockup):** size and position (root scale 1.8 on a 0.5-scale canvas), Texturina/Grenze text, the carrot icon is the orange-tinted `gi_carrot` silhouette (swap in a coloured carrot sprite if wanted), and the ember look (`ChargeEmber` is a plain 9 px square; a soft particle sprite might read better).
## Slice 3.1: Keyboard & Mouse / Controller settings tabs

Regenerated via `Bladehold > UI > Rebuild Settings Panel` (pause menu prefab + MainMenu). The dev scenes Bladehold Test Scene, Demo Scene and Enemy Zoo still have old copies; rebuild them with the open-scene menu item if you use them.

Manual verification:
- [ ] Pause > Settings shows four tabs; Q/E and LB/RB cycle through all four, and clicking each works.
- [ ] Keyboard & Mouse: mouse sensitivity, invert toggles, keyboard/mouse bindings only. Rebinding a key still works and persists.
- [ ] Controller: pad sensitivity, stick dead zone, invert toggles, gamepad bindings only. Rebinding a button works. D-pad navigation goes up and down the list and off the bottom to Reset Settings.
- [ ] Toggling Invert Y in one tab shows it ticked in the other.
- [ ] Main menu Settings shows the same four tabs.
- [ ] "Keyboard & Mouse" fits its tab in every language (the label auto-shrinks).

## Controller aim assist (4.1-4.3)

Code: `Player/ControllerAimAssist.cs`, `Player/AimAssistSO.cs`, config `Assets/Bladehold/Resources/AimAssist.asset`, sliders cloned by `SettingsPanelView`.

- [ ] Run **Bladehold/Benchmarks/Run Weapon Reach & Damage Benchmark** (from MainMenu); section 29 should pass.
- [ ] Feel check with a pad: aim the bow near a goblin's head, the crosshair should slow over it and drift on; strafing targets should be easier to track. Tune `AimAssist.asset` (pull deg/s, slowdown) if too sticky or too weak.
- [ ] Strength 0 in Settings > Controller turns it fully off; a wide window (15) grabs heads further off the crosshair.
- [ ] Mouse aiming is unaffected.
- [ ] Heads behind walls aren't assisted (if a wall layer is missed, add it to the asset's Occluder Layers).

## Slice 5.1: directional-attack tutorial lesson

Code: `Tutorial/DirectionalAttackStep.cs` (new step), `Damage.meleeSwingDirection` stamped by `DamageTrigger`, `PlayerAttack.CurrentSwingDirection`. Loc keys: `tutorial.directional`, `tutorial.directional_tip`, `tutorial.swing.overhead/right/left`.

Scene wiring (Tutorial Dungeon — the dungeon scene with the existing melee lessons; find the `TutorialDirector` there):
- [ ] Add a **training dummy** to the scene: any goblin/dummy prefab with a `Health`. Give it plenty of HP (the step revives it each lethal hit, so it won't die, but a high max avoids corpse flinch). Stand it in a clear practice spot with a `waypointTarget` anchor.
- [ ] Add a GameObject with the **`DirectionalAttackStep`** component. Assign:
  - `dummy` = the training dummy's `Health`.
  - `stepId` = e.g. `Directional`.
  - `waypointTarget` = the dummy (or an anchor by it).
  - `hint.locKey` = `tutorial.directional`, `hint.actionName` = `Attack`.
  - `secondaryHint.locKey` = `tutorial.directional_tip` (no glyph).
- [ ] Insert the step into `TutorialDirector.steps` at the right point — after the basic quick/heavy attack lessons, before the bow step (or wherever the directional lesson reads best).
- [ ] Wire the gated door: the step's `onStepCompleted` opens the next door / `TutorialGateOpener`, same pattern as the other lessons (or rely on the director's normal advance if there's no gate here).

Manual verification:
- [ ] Enter the lesson: the hint explains the three swings and the counter shows `• Overhead • Right • Left`.
- [ ] Land an overhead (look up before swinging): its label turns green with a tick. Repeat for right (turn right) and left (turn left) — all three tick and the step completes, opening the door.
- [ ] A strong overhead doesn't "kill" the dummy and strand the lesson (it revives).
- [ ] Works on both keyboard/mouse and controller (direction is read from the camera turn, so both work).

## Slice 7.1: wave cards choose enemies (composition data model)

Code only (no new SO assets): `WaveCard.composition` (`WaveEnemyEntry`), new `WaveCompositionRoller`, `WaveCardGenerator.Build` rolls the composition, `SurvivorsSpawner.SetWaveComposition`/`NextCompositionType`, `GameLoopManager` spawns it and ends the wave on `OnWaveWiped` (survival = reward), objective demoted to a non-gating bonus (`compositionWaveActive`). Reads the existing `RoundPacingConfigSO` (`fodderEnemyId`, `fodderShare`) and the enemy roster — nothing to wire. Composition is skipped on the captain wave and in scenes with no roster (dev scenes fall back to the old quota path).

Manual verification (playtest — the card/HUD still show the objective until slices 7.3/7.4):
- [ ] Play a sector wave with a wave card: the crowd that spawns matches a rolled mix (mostly fodder, some variety), and the **wave ends when you've cleared the crowd** rather than when the objective finishes.
- [ ] Failing/ignoring the wave's objective no longer fails the wave — it just logs a "bonus objective" line and the wave still ends on clearing the enemies. The gate falling still ends the run.
- [ ] Surviving the wave still grants the card's gold/supply/bonus reward.
- [ ] The captain (final) wave is unchanged: it spawns the captain via its objective, no composition crowd.
- [ ] Golden Goblin / suppress-spawn objectives still run their own spawns (no composition crowd on those waves).
- [ ] A scene with a pinned `SceneEnemyRoster` only rolls enemies from that list; a dev scene with no roster still spawns the old threat-gated way.
- [ ] Watch the console for `Composition enemy '<id>' has no spawnable type/prefab` warnings — any roster id with no prefab is skipped; add it to the prefab map if it should appear.

## Slice 7.2: battering ram as a roster enemy

Code done: `StopBatteringRamObjective` renamed in place (same GUID) to `Objectives/BatteringRamLane.cs` — the `SurvivorsObjectives.prefab` instance kept its `WagonSpawnPoint`/`GateDestinationPoint` refs (verified via MCP). `Enemies.csv` row `battering_ram` + `EnemyPrefabMap` entry → `Objectives/BatteringRam.prefab`. `stop_battering_ram` removed from `WaveObjectives.csv`. Benchmark 18D passes.

Wiring:
- [ ] Open `Bladehold Survivors Scene` and each generated defence scene: confirm a `BatteringRamLane` component exists with spawn + gate destination set. Scenes without a lane still work — rams spawn at a normal spawn point and roll at the nearest gate — but re-run **Generate Defense Scene** (or **Bladehold/Setup Battering Ram (Prefab + Lane)** for the Survivors scene) to get a proper lane.

Manual verification:
- [ ] From wave 2 on, a card sometimes rolls a Battering Ram (never more than one per wave). It spawns at the ram lane, goblins gather and push it, and it batters the gate for 50 per hit.
- [ ] The wave doesn't end until the ram is destroyed; breaking it counts as a kill.
- [ ] Captain wave rout: a ram that's still alive stops rolling when the captain dies.
- [ ] Stop the Battering Ram no longer shows up as a wave objective.

## Slice 7.3: optional bonus objectives

Code only: `WaveObjectives.csv` `bonusGold`/`bonusSupply` columns, `GameLoopManager.PayObjectiveBonus` / `ClearResolvedObjective`, gate-assault fail path removed. Nothing to wire. (The HUD still says "Quest completed/failed" rather than "Bonus" until slice 7.4.)

Manual verification:
- [ ] Complete a wave's objective (e.g. free every prisoner): a gold popup shows, gold/supply go up by the CSV bonus, the tracker clears ~3 s later, and the wave carries on until the enemies are dead.
- [ ] Let a timed objective run out: no bonus, the leftover cages/engines vanish ~3 s later, the wave carries on, and surviving it still pays the card reward and draft.
- [ ] No "THEY STORM THE GATE!" banner ever appears after a failed objective.
- [ ] Captain wave: killing the captain still ends the sector as before.
- [ ] Victory screen gold/supply totals include bonus payouts.



Code/scene done headlessly via MCP: realtime additional-light shadows disabled in
`Bladehold Meta Area Scene` (21 Point + 9 Spot, kept one bright Spot) and
`Bladehold Rest Area Scene` (7 Point, kept the directional sun). `Camera.main` cached
in the three Meta pedestal scripts. Before/after render counters are in
`plans/22-playtest-feedback-oct10.md` slices 6.1/6.2.

Visual review (the only human-only part):
- [ ] Walk the Meta Area: with the 30 fill-light shadows off and one key Spot kept, the
      scene still reads well — no flat/floating-character look that bothers you. If a
      spot needs grounding, re-enable shadows on one nearby light (prefer a Spot, not a
      Point) rather than all of them.
- [ ] Walk the Rest Area: the sun still grounds the player/NPCs; the shop/well/draft
      props don't look flat without their point-light shadows.
- [ ] Optional, bigger follow-up: bake lighting for both scenes (Mixed + Subtractive is
      already set) so static geometry gets baked shadows back for free — would let a few
      key lights stay shadowed cheaply. Not required; the realtime-shadow cut already
      lands the perf win.
