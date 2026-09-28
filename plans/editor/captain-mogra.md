# Editor to-do: Captain Mogra Hexfang + dash i-frames

Not from a plan: a direct request on 2026-09-29. Unity MCP was connected. Everything below the "done" list needs your eyes, ears or hands. Tick items off as you go and delete the file when it's empty.

**He's fully wired and spawns already.** Any card that brings a captain (3 skulls, or wave 5's Captain Assault) has a 35% chance to bring Mogra instead of the node's captain.

## Done by an agent via MCP (2026-09-29)

- [x] **Assets built** with **Bladehold/Captains/Build Captain Mogra Assets** (`Editor/CaptainMograBuilder.cs`). Re-run it after changing the builder; it rebuilds these and keeps `CaptainMograSO` tuning.
  - `Captain Mogra Enemy Variant.prefab`: the Goblin Shaman model swapped onto the goblin rig, `SM_Wep_Staff_03` in `hand_r`, the `Mogra AC`, 9 MMF players under `Mogra Feedbacks`, and a `Ritual Cast Bar` (BossCastBar instance).
  - `Bladehold Prefabs/Captains/Mogra/`: HexBolt, HexRuneBlast, BoneTotem, HexShockwaveRing, RitualSafeCircle, RitualDanger, plus green VFX (`VFX/HexBurst*`, `HexDaze`) and materials.
  - `Bladehold Animations/Mogra/Mogra AC.controller`: a from-scratch controller (goblin locomotion; Sorceress casts, channel and stun; Synty roar, death and cheer).
  - `Player AC`: a new full-body **Dodge** layer (above Melee, below Death) that plays `A_MOD_SWD_Dodge_F_Neut` on the `Dodge` trigger.
- [x] **Play-checked** in `Bladehold Survivors Scene`, with the player made invulnerable and every incoming hit logged:
  - Every attack fires and telegraphs: runes, bolts, Hex Step, totems and rings, the ritual.
  - Phase 2 at 66% HP raised totems. Phase 3 at 33% started the ritual.
  - Breaking a totem staggered him, and he took 1.5× damage while staggered.
  - Dealing 10% of his HP broke the ritual. An unbroken ritual hit for 31 at Enraged.
  - On death, his totems crumbled and he paid out. The test's Goblin Blood was taken back out of your save.
  - A dash ignored a hit during its i-frames, and the dodge pose played.
  - The scene was not saved.
- [x] **Checks:** **Bladehold/Tests/Captain Mogra Checks (Edit Mode)** passes 29/29. The same checks run as section 28 of the main benchmark.

## Verify

- [ ] **Staff grip.** The staff is parented to `hand_r` at rotation (0, 0, 90). Check it sits in his hand while he walks and casts, not through his arm. Fix it on the variant's `Mogra Staff` object, then copy the rotation into `CaptainMograBuilder.ConfigureVariant` or a re-run will reset it. *(MCP-able)*
- [ ] **Cast timing.** Bolts leave 0.55 s into `CastBolt` (Sorceress RangeAttack1 at 1.6× speed) and runes appear 0.5 s into `CastRunes` (SpecialAttack1 at 1.5×). Check the hand gesture lines up with the spell. Tune the state speeds in the builder, or `boltWindupSeconds` / `runeWindupSeconds` on `CaptainMograSO`.
- [ ] **Player dash animation.** Dash in a sector with the sword, bow and staff. The forward dodge (1.6× speed) should read as a quick lunge and hand back to locomotion cleanly. It's skipped while mounted and with Nimble Strike.

## Audio and VFX review

- [ ] **Sounds are placeholders from existing project clips.** All are `MMSoundManager Sound` feedbacks on the variant's `Mogra Feedbacks` children and the spell prefabs:

  | Moment | Clip |
  |---|---|
  | Phase roar | goblin angry laugh |
  | Bolt cast | fuse fizzle |
  | Rune cast | magic poof |
  | Rune eruption | short explosion, volume 0.3, max 3 at once |
  | Blink | big whoosh |
  | Summon | goblin attack bark |
  | Ritual channel | acid sizzle loop |
  | Ritual broken | goblin whimper |
  | Ritual detonation | large explosion |
  | Stagger | monster "ugh" |

  Swap in better ones with `/find-and-import-assets` if you like.
- [ ] **Colours.** His runes are green (his theme), while the rest of the game telegraphs danger in red. The countdown fill makes it read as danger, but confirm it reads right in a busy wave. Ritual safe circles are blue.

## Decisions

- [ ] **Wandering-captain chance** is 35% (`Resources/WaveChoiceConfig.asset` → `wanderingCaptainChance`). Raise it, lower it, or pin him to specific nodes instead (set a node's `captainName` to "Captain Mogra Hexfang").
- [ ] **Map badge / tooltip icon.** He has no `captainIcon`, and the map only names captains that are set on nodes. As a wandering captain he shows up on the wave card ("Captain Mogra Hexfang joins the fight") and in the intro banner.

## Playtest

Reach him fast: DevConsole (backquote) → **Spawn Captain** → pick a tier with ◄/► → **Mogra**. That uses the real spawn path (tier scaling, boss bar, intro). The plain spawn-type picker skips all three.

- [ ] **Fight length and fairness at Enraged (625 HP):**
  - Every hit should be avoidable by moving or dashing.
  - An idle player in phase 2 took about 7 DPS in the test (before the ring was toned down to 8 damage every 4.5 s).
  - Aim for a 40-70 s fight.
  - Tune on `CaptainMograSO`: `baseMaxHealth`, `boltDamage`, `runeDamage`, `ringDamage`, `recoveryByPhase`.
- [ ] **Rune patterns** (Rings, Cross, Checkerboard, Spiral): each should leave a gap you can find in time. The tightest is the Rings gap, about 1.9 m between the inner disc and the outer ring. You should never take damage from two overlapping runes in one step.
- [ ] **Totem rings:** a dash through a ring never hurts (0.3 s i-frames). Walking into one does, once per ring.
- [ ] **Totem backlash:** breaking a totem visibly stuns him (daze burst and stun loop) for 2.5 s.
- [ ] **Ritual:**
  - A mid-run build can break it in 6 s. The benchmark says that takes 10-17 DPS depending on tier.
  - Standing in a blue circle takes no damage.
  - Breaking it stuns him for 4 s.
  - The cast bar faces the camera and shows `BREAK THE RITUAL Ns`.
- [ ] **Never:**
  - He never casts after dying or during the end-of-wave rout.
  - He never damages other goblins.
  - The boss bar never stays up after he dies.
  - Goblin Blood is never paid twice.
- [ ] **Dash i-frames everywhere:** check the i-frames don't make other fights trivial. Examples: dashing through Kombusta's fire aura, or through the Troll slam at the right moment. The window is `iFrameDuration` on `Bladehold Config/PlayerDodgeSO.asset` (0.3 s).
