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
