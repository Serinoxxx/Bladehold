# Editor checklist: plan 22 (playtest feedback 2026-10-10)

## Slice 1.1: draft card double-highlight on controller

No wiring needed (code only).

Manual verification:
- [ ] With a controller, finish a wave so the upgrade draft opens: only the pad-focused card is lit, and the middle card isn't lit as well.
- [ ] Same check on the wave choice cards and the Fishing draft.
- [ ] Move the mouse over a card: it lights on hover and the pad highlight goes away. Press a stick or d-pad: hover drops and pad focus comes back.
- [ ] Pause menu buttons: no stray hover glow on the button under the hidden cursor while using the pad.

## Slice 1.2: tower plot glyph (not reproduced)

- [ ] Lance: note the scene, device and which glyph was blank (plot prompt, build wheel, tutorial hint) next time it happens.

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
