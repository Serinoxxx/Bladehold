# TODO

## Enemy Zoo (config/test scene) — Unity Editor wiring

The C# is done: `Bladehold Scripts/Debug/EnemyZoo.cs` (guarded to `UNITY_EDITOR || DEVELOPMENT_BUILD`,
so it can't ship) spawns one of every `EnemyRosterSO` type in a labelled gallery grid, applying the
same CSV overrides the waves use via `WaveSpawner.ApplyDefinition` (made `public static` for this).
An IMGUI panel (same wiring-free idiom as `DevConsole`, drawn top-right) toggles **Battle Mode**
(freeze the gallery for inspection ↔ enable each enemy's `AIMovement` + `*Attack` components so they
chase/fight the player), **Respawn Gallery**, and a type picker that spawns a batch of one type on
demand (with **Clear Spawns**). World-space name/health labels are drawn per enemy via IMGUI. This
scene is a config/test tool only — **do not add it to Build Profiles**.9

- [ ] **New scene** `Assets/Bladehold/Bladehold Scenes/Enemy Zoo.unity` (first-party scenes live
      here). Add a large flat ground plane and **bake a NavMesh** over it (`com.unity.ai.navigation`)
      — enemies need it for battle mode; the gallery snaps slots onto it.
- [ ] Add a **Player prefab instance** (`Bladehold Prefabs/Player.prefab`) so `Player.Instance`
      exists — Battle Mode's `AIAttack.Start` hard-errors without it. Position it where you want to
      stand relative to the gallery.
- [ ] Add a **camera** (the Player prefab's rig, or a plain one) and a light so the gallery renders.
- [ ] Add an empty GameObject **"EnemyZoo"** with the `EnemyZoo` component. Assign the same
      `EnemyRosterSO` asset the `WaveSpawner` uses, and fill the **prefab map** (id → prefab) for
      every roster row you want shown (Goblin, Goblin Brute, Bomber, Storm Witch, Troll). Set
      `spawnPoint` (empty transform where on-demand batches appear) and tune `galleryOrigin` /
      `columns` / spacing so the grid sits in front of the player and on the NavMesh.
- [ ] Optional: drop a **GameStats** object in the scene if you want kill accounting during battle
      (enemies report kills to `GameStats.Instance`; harmless if absent).

## Manual verification (Enemy Zoo)

- [ ] Enter Play in the Enemy Zoo scene — one of each roster type stands in a grid, each with a
      name + health label, none chasing (Battle Mode OFF).
- [ ] Click **Battle Mode** — every gallery enemy activates and moves toward/attacks the player;
      click again to freeze them in place.
- [ ] Pick a type, set a count, **Spawn** — that many spawn at the spawn point and immediately
      fight; **Clear Spawns** removes them.
- [ ] Confirm scaled types (e.g. Goblin Brute) render at the right size — proves
      `ApplyDefinition` overrides are being applied.

## Stuck arrows + arrow impact feedback — Unity Editor wiring

## Manual verification (stuck arrows + impact feedback)

- [ ] Multi Shot — every fanned arrow that lands sticks its own prop and plays its own feedback;
      a Bounce Shot arc does **not** stick a second arrow at the bounce target.


## Bomber enemy + Flaming Arrows skill line — Unity Editor wiring

The C# is done: **Bomber** (`Enemies/BomberAttack.cs` + `Enemies/BomberAttackSO.cs`) chases with a
torch; within `triggerRange` (8m) it plants for a short ignite pause (`AIMovement.SetMovementPaused`,
the Troll wind-up precedent), lights the dynamite (a `LightFuse` animator trigger, torch hidden,
spark visuals shown), then sprints at `fuseSpeedMultiplier`× via the new
`AIMovement.SetSpeedMultiplier` (folded into `BaseSpeed` so `SlowStatus` slows compose with it).
`fuseSeconds` (5s) after lighting it explodes: AoE damage + impulse fling to everything in
`explosionRadius` except itself (the `TrollSlamAttack` shape — elemental, `unparryable`), then
force-kills itself through `Health.ReceiveDamage` so wave/coin/corpse accounting runs (the
`ImpulseReceiver` precedent). Killed before the fuse burns down = no explosion, sparks out.
**Flaming Arrows** (`flamearrow_1..5` in `Config/SkillTree.csv`, fresh root column at x=18):
`PlayerBow` deals `StatType.FlamingArrowsDamagePercent` (25% from the unlock) of each arrow hit as
a separate elemental fire hit, and rolls `StatType.FlamingArrowsBomberDetonateChance` (10-50%) per
arrow hit to call `BomberAttack.Detonate()` — rolled *before* the arrow damage lands so a lethal
arrow still gets its explosion (corpses never explode). New `bomber` row in `Config/Enemies.csv`
(unlocks wave 5, 20% chance, 1 guaranteed ramping to 3 concurrent, resistance 2);
`WaveSpawner.ApplyDefinition` routes the CSV damage column to `BomberAttack.SetDamage`.

- [ ] **Create SO asset instance**: a `BomberAttackSO` (menu `Scriptable Objects/BomberAttackSO`) —
      tune `triggerRange` (8), `igniteSeconds` (0.6), `fuseSeconds` (5 — total from lighting to
      boom, ignite pause included), `fuseSpeedMultiplier` (1.6), `explosionRadius` (4), `damage`
      (25 — the CSV column overrides it per spawn), `impulsePower` (3) / `impulseForce` (12).
- [ ] **Bomber prefab** (a goblin variant works as the base): `Health`, `Enemy`, `AIMovement` (its
      own or the goblin `AIMovementSO` — the CSV speed 5.5 overrides it), `AIAnimation`,
      `BomberAttack` (assign `attackData`; `animator`/`health`/`movement`/`targetSelector`
      auto-wire via `OnValidate`), `CoinDropper`, `CorpseDespawner`, `KnockbackReceiver`,
      `EnemyRagdoll` + `ImpulseReceiver`, optional `GoldenGoblin`/`ImpulseGoblin`/
      `AITargetSelector`, and a `VulnerableSpot` head collider (arrow headshots).
  - [ ] **Props/VFX on the prefab**: a torch prop in one hand → `torchVisual`; dynamite/spark
        objects (e.g. one per hand, each with a sparking `ParticleSystem`), authored **inactive** →
        `fuseSparkVisuals`; an explosion VFX prefab → `explosionVfxPrefab`; optional
        `igniteFeedback`/`explodeFeedback` `MMF_Player`s (fuse hiss, big boom).
  - [ ] **Animator**: add a `LightFuse` trigger + a short crouch/ignite state on the bomber's
        controller (the `BomberAttack.lightFuseTrigger` default), timed to roughly `igniteSeconds`.
  - [ ] Register the prefab in `WaveSpawner`'s `enemyPrefabs` list under id `bomber` (row already
        in `Config/Enemies.csv`).
- [ ] **Skill icons**: the `flamearrow_*` rows have blank icons (no fire sprite registered yet) —
      assign in **Bladehold > Skill Tree Editor** when art exists.
- [ ] **Balance pass**: tune the `bomber` CSV row, the `BomberAttackSO` numbers, and the
      placeholder `flamearrow_*` costs/positions to taste.

## Manual verification (Bomber + Flaming Arrows)

- [ ] Reach wave 5 (or lower `unlockWave`) — a bomber spawns and runs at the player holding the
      torch, faster than a regular goblin.
- [ ] Get within ~8m — it stops, plays the ignite animation (torch swaps for sparking dynamite in
      both hands), then sprints at you noticeably faster; ~5s after lighting it explodes, hurting
      you if you're inside the radius and damaging/flinging any goblins caught in it.
- [ ] Outrun the blast — standing outside the radius when it pops takes no damage; the bomber dies
      in its own explosion either way (coins drop, wave count decrements, corpse pipeline runs).
- [ ] Kill the bomber before the fuse burns down (without the detonate roll) — sparks go out,
      **no explosion**, normal death.
- [ ] The explosion is never parried (elemental + unparryable), even with Parry maxed.
- [ ] Buy `flamearrow_1` — every arrow hit now pops a second, smaller damage number (~25% of the
      arrow's) on the same target; Multi Shot side arrows each get their own fire hit; bounce hits
      don't reroll it.
- [ ] Shoot bombers with `flamearrow_1` — roughly 1 in 10 hits detonates one on the spot (full
      explosion where it stands, nuking its own horde); higher tiers detonate visibly more often.
- [ ] A bomber the arrow would have killed anyway still explodes on a winning roll (the roll
      happens before the arrow damage lands).
- [ ] Shooting a bomber **corpse** never explodes it.
- [ ] Die (or lose a gate) while a fuse is burning — the bomber fizzles (sparks out, no explosion)
      and celebrates with the rest.
- [ ] Slow a lit bomber (Freezing Draw / Brain Freeze) — the slow visibly reduces the sprint, and
      when it expires the bomber returns to *sprint* speed, not walking pace (the multiplier and
      the slow compose).

## Parry + Counterstrike skill lines — Unity Editor wiring

The C# is done: `Player/Parry.cs` hooks `Health.TryBlockDamage` with a chance roll
(`StatType.ParryChance`) gated on facing the attacker (dot product of the player's forward vs. the
direction to `Damage.sourcePosition`) — only "melee" (sharp/blunt) hits qualify, elemental hits
(Storm Witch) can never be parried, and neither can anything stamped `Damage.unparryable` (the
Troll's ground slam — a wide AoE with no single directional swing to read/block).
`Player/Counterstrike.cs` listens to the new `Parry.OnParried` event (the `VampiricBlade`/sword-
`OnHit` precedent) and deals `StatType.CounterstrikePercent` of effective sword damage back to the
attacker via the new `Damage.source` field, which `AIAttack` and `TrollSlamAttack` now stamp with
their own `Health` alongside `sourcePosition` (previously melee attacks against the player didn't
set either). New `parry_*`/`counter_*` rows already in `Config/SkillTree.csv` (fresh column at
x=6, y=0-8, chained off `solid_1`).

- [ ] **Player prefab** (`Assets/Bladehold/Bladehold Prefabs/Player.prefab`):
  - [ ] Add a `Parry` component on the player root (next to `Health`/`DamageBlocker`); optionally
        assign a `parryFeedback` `MMF_Player` (a parry clang/flash) and tune `facingDotThreshold`
        (0.3 default — wider than dead-on, but not the whole front hemisphere).
  - [ ] Add a `Counterstrike` component on the player root; `parry` auto-wires via `OnValidate`
        (`GetComponent<Parry>()`).
- [ ] **Skill icon**: `parry_*`/`counter_*` reuse already-registered icon names
      (`Warriorskill_18_block`, `IncreaseStrength_2/3/4_nobg`), so no new icon drag-and-drop should
      be needed — confirm they render in **Bladehold > Skill Tree Editor**.
- [ ] **Balance pass**: tune the placeholder costs/positions of the new rows to taste.

## Manual verification (Parry + Counterstrike)

- [ ] Buy a `Parry` tier — facing a goblin as it lands a melee hit sometimes blocks it entirely (no
      health loss, no damage feedback); getting hit from behind or the side never parries even at
      100% rolled luck.
- [ ] A Storm Witch's lightning ball/storm damage is never parried, even with `Parry` maxed.
- [ ] Without `Counterstrike` bought, a successful parry blocks damage but the attacker takes
      nothing back.
- [ ] Buy a `Counterstrike` tier — a successful parry now also damages the goblin that hit you
      (damage number on the goblin), scaling with the node's %; the sword's own swing damage is
      unaffected.
- [ ] Face a Troll and let its ground slam land on you — it's never parried (no block, no
      counterstrike) even with `Parry`/`Counterstrike` maxed; you just take the damage normally.
- [ ] The existing `Solid` auto-block still works independently (both can trigger; whichever's
      handler runs first on a given hit wins that hit).



## Bow weapon + bow skill lines + Raw Power — Unity Editor wiring

- [ ] **Skill icons**: the new `multishot_*` and `multidmg_*` rows have blank icons (no bow-ish
      sprite is registered yet) — drop sprites on them in **Bladehold > Skill Tree Editor** when
      suitable art exists; the other new rows reuse already-registered icon names.


## Frost/orb/conduit skill lines — Unity Editor wiring

The C# for eight new skill lines is done: **Freezing Draw** (`Player/FreezingDraw.cs` — slows
enemies near the player while the bow is drawn), **Brain Freeze** (headshots chill the target) and
**Elongated Freeze** (slows linger longer) via the new runtime-added `Enemies/SlowStatus.cs` (scales
`NavMeshAgent.speed` + `Animator.speed`, no prefab wiring — the `EnemyRagdoll` lazy-build idiom;
`AIMovement.BaseSpeed` is the restore source), **Ice Breaker** (sword bonus vs slowed enemies, in
`DamageSystem/DamageTrigger.cs`), **Exploding Heads** (headshot impulse blasts) / **Arrows of
Midas** (`GoldenGoblin.TryConvertToGolden`) / **Unstable Orbs** (main arrow detonates orbs — orbs'
new `TryDetonate`, `ChainLightning.ForceChain`) in `Player/PlayerBow.cs`, and **Conduit** (reduced
lightning-ball damage + chain proc, in `Enemies/LightningBall.cs`, bases registered by
`ChainLightningBuff`). New CSV rows: `freezedraw_*`, `brainfreeze_*`, `elongfreeze_*`,
`icebreaker_*`, `explodeheads_*`, `midas_*`, `conduit_*`, `unstableorbs_1`.

- [x] **Player prefab**: add a `FreezingDraw` component next to `PlayerBow`; assign `config` (the
      same `BowSO` asset) and set `enemyLayers` to the enemy layer (exclude player/environment).
      `bow`/`stats` auto-wire via `OnValidate`.
- [x] **BowSO asset**: tune the new fields — `freezingDrawRadius` (8), `brainFreezeSeconds` (3 —
      the `brainfreeze_*` descriptions assume this), and the shared impulse-blast tunables
      `impulseBlastRadius` (4) / `impulseBlastPower` (2 — flings default-resistance goblins; raise
      to topple brutes) / `impulseBlastForce` (10).
- [x] **Vulnerable spots**: Brain Freeze and Exploding Heads trigger on the same `VulnerableSpot`
      head colliders as Precision Shot — the wiring item in the bow section above covers all three.
- [ ] **Skill icons**: the `freezedraw_*`, `brainfreeze_*`, and `elongfreeze_*` rows have blank
      icons (no frost sprite registered yet) — assign in **Bladehold > Skill Tree Editor** when art
      exists. The other new rows reuse registered names.
- [ ] **Balance pass**: costs/positions of the new rows are placeholders (frost branch sits at
      y6-y8 under the bow region, Conduit at x9-10/y3-4 by the lightning branch).
- [ ] **Optional polish (future)**: slows have no VFX/tint yet — a frost material swap or aura on
      `SlowStatus` would telegraph the state; an `MMF_Player`/VFX on the impulse blasts and orb
      detonations (currently the orb just plays its pickup feedback and vanishes).

## Manual verification (frost/orb/conduit skills)

- [ ] Buy Freezing Draw, hold aim near goblins — they visibly crawl (movement and animation) while
      the bow is drawn, and resume full speed ~instantly after releasing aim (or lingering, with
      Elongated Freeze bought).
- [ ] Buy Brain Freeze (after head colliders exist) — a headshot slows that goblin for ~3s; body
      shots don't. The slow expires and speed restores exactly.
- [ ] Buy Ice Breaker — sword hits on a chilled goblin show boosted damage numbers; unslowed
      goblins take normal damage. Death Nova damage is unaffected (it isn't melee).
- [ ] Buy Exploding Heads — a headshot detonates a blast that damages/flings the goblins around the
      victim for the node's % of the arrow's damage.
- [ ] Buy Arrows of Midas — roughly 1 in 20 arrow hits (at tier 1) turns a live goblin gold
      mid-fight (material swap + bonus coin on death); already-golden goblins are unaffected.
- [ ] Buy Conduit, let a Storm Witch ball hit you — damage taken drops by the node's %, and ~1 in
      10 hits arcs chain lightning from the impact into nearby enemies (needs the Chain Lightning
      unlock for bounces/damage to be non-zero).
- [ ] Buy Unstable Orbs — shoot your main arrow through an Impulse Orb (impulse blast at the orb)
      and a Lightning Orb (chain lightning from the orb, no buff needed); the orb is consumed and
      grants no buff. Multi Shot side arrows and walk-over pickups behave as before.
- [ ] Player is never slowed by anything (SlowStatus only attaches to NavMesh enemies).


## Impulse skill line — Unity Editor wiring

- [ ] Add feedback/VFX on player when charged by an Impulse Orb (e.g. a small shockwave + SFX) so the player knows the
      buff is active.

## Storm Witch enemy + Chain Lightning skill line — Unity Editor wiring

- [ ] Add feedback/VFX on player when charged by a Storm Witch's ball (e.g. a small lightning flash + SFX) so the player knows the
      buff is active.


## Manual verification (skill icons + new skill lines)

- [ ] Buy Solid, take a goblin hit → first hit negated (no damage number/feedback), next hits land
      normally until the cooldown elapses; higher tiers shorten the window.
- [ ] Buy Amplified Knockback with Heavy Strike owned → a fully charged swing shoves goblins visibly
      further than an uncharged one, scaling per tier.

## Combat facing (stationary attack/aim turns to camera) — Unity Editor wiring

The C# is done: `Player/CombatFacing.cs` yaw-rotates a stationary player toward the camera heading
while the attack button is held or the bow is aiming, filling the gap where the Synty controller's
stationary strafe branch only drives the turn-in-place animator offset and never rotates the root.
Attack hold comes from `InputReader` press/release events; bow aim reads `PlayerBow.IsAiming`;
movement is detected via the `CharacterController`, so the controller's own strafe rotation takes
over untouched the moment the player moves.

- [ ] **Player prefab** (`Assets/Bladehold/Bladehold Prefabs/Player.prefab`): add a `CombatFacing`
      component on the player root. `inputReader`/`bow`/`characterController` auto-wire via
      `OnValidate`; `facingCamera` defaults to `Camera.main`. Defaults for `rotationSmoothing` (10,
      matching the controller) and `stationarySpeedThreshold` (0.1) should be fine.
- [ ] **PlayerDeath**: add the new `CombatFacing` component to `PlayerDeath`'s inspector list of
      control components it disables on death, so a corpse holding attack doesn't keep turning.

## Manual verification (combat facing)

- [ ] Stand still, hold left click (sword), and swing the camera around → the character smoothly
      turns to face where the camera looks, both mid-hold and during a held charge.
- [ ] Stand still, hold right click (bow aim), and swing the camera → same smooth turn; arrows and
      the body agree on direction (the `BowAimLook` spine bend still lines up).
- [ ] Attack/aim **while moving** → rotation feels exactly as before (the controller's strafe
      rotation, no fighting or jitter at the moving/stationary boundary).
- [ ] Release attack/aim while stationary → the character stops tracking the camera and idle
      turn-in-place behaviour is back to normal.
- [ ] Die while holding attack → the corpse doesn't rotate with the camera (requires the
      `PlayerDeath` list wiring above).
