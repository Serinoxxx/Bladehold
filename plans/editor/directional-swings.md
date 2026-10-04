# Editor to-do: Directional swings + armed arm pose

Not from a plan (chat request, 2026-10-04). Unity MCP connected: clips, animator, SO asset and Player.prefab wiring were all done by the agent and play-checked by driving the Animator (all 6 weapon×direction routes reach their states and fire the hitbox, 0.30–0.49s press→hit on a tap). What's left is your eyes and hands. Tick items off as you go and delete the file when it's empty.

What exists: `SwingDirectionSelector` + `SwingDirectionSO` (Player/), `StatType.OverheadSwingDamageMultiplier` (base 1.5, applied in `DamageTrigger`), `SwingDirection` int + 4 new 3-state chains on the **Melee** layer of `Player AC.controller`, a new **Armed Arm** layer (right arm, `Mask_Arm_R`), Mixamo Melee Axe Pack clips in `Assets/Third Party/Mixamo Melee Axe/Animations/`, sliced overhead clips in `Assets/Bladehold/Bladehold Animations/Directional Swings/`.

| Weapon (MeleeWeaponType) | Right (0) | Left (1) | Overhead (2) |
|---|---|---|---|
| Axe, Mace (1) | `Axe Windup` (existing horizontal) | `Axe Left *` (Kevin `Attack1H03_R`, release ×0.4) | `Axe Overhead *` (Mixamo downward, release ×1.1) |
| Sword, Staff, others (0) | `Sword Right *` (Kevin `Attack1H02_R`, release ×0.67) | `Sword_Stab_*` (existing) | `Sword Overhead *` (Mixamo downward, release ×1.6) |

## Playtest

- [ ] **Look left then attack → swing comes from the left; look right → from the right; look up → overhead chop.** Mouse and gamepad. Threshold is 3° of camera turn in the last 0.2s (`SwingDirectionSO` at `Assets/Bladehold/Bladehold Scripts/Player/SwingDirectionSO.asset`). Too twitchy or too hard to trigger → tune `yawThreshold` / `pitchThreshold` / `lookWindow` there.
- [ ] **Standing still and spamming attack alternates right/left**, never the same swing twice in a row. Overhead never happens without looking up.
- [ ] **Overhead chop numbers are ~1.5× a same-charge side swing** (damage numbers on a goblin). Must *never* apply to whirlwind ticks.
- [ ] **Every new swing actually hits** (Axe Left, Axe Overhead, Sword Right, Sword Overhead). A missing/misplaced event = silent no-hit swing. Events are `OneHandedSwordAttack` + `PlaySwordWoosh` on each Release clip; the Kevin ones live on the FBX takes (`Attack1H03_R_End`, `Attack1H02_R_End`), the overhead one on `Overhead Release.anim`.
- [ ] **Hold-to-charge pose reads well for each direction** (the Hold states freeze at speed 0.01). Overhead holds with the axe half-raised at the shoulder.
- [ ] **Swing tempo feels matched** across the three axe directions. Release state speeds are the knob (`Axe Left Release` 0.4, `Axe Overhead Release` 1.1 on the Melee layer).

## Look / decisions

- [ ] **Kevin `Attack1H03_R` (axe from the left) and `Attack1H02_R` (sword from the right) are placeholders** you said you might swap. The Mixamo pack's "backhand" was checked and rejected: it's a spinning rising swing that still comes from the right. To swap, point the state's Motion at a new clip and move the two events.
- [ ] **Sword overhead reuses the axe's Mixamo downward chop at ×1.6.** Keep it or swap for something sword-specific.
- [ ] **Armed Arm layer: running no longer swings the weapon through the head** (checked in a render; the axe is now carried upright beside the right shoulder). Check it in motion: walk, run, sprint, strafing, and **with the bow / throwing axe / wand equipped**, where the right arm still holds the carry pose. If that looks wrong, the layer needs its weight driven by weapon type (agent can add).
- [ ] **Mounted:** the Mounted layer sits above Armed Arm, so the rider pose should win. Confirm the arm isn't stuck in the carry pose on the horse.

## Housekeeping

- [ ] **Graveyard scene had unsaved changes when the frozen Editor was force-closed** (backup 21:22, saved file 16:55). The recovery dialog was answered No; the backup is at `%LOCALAPPDATA%\Temp\claude\…\scratchpad\scene-backups\0.backup` if you need it (rename to `.unity` to open).
