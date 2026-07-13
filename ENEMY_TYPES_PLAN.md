# Enemy types plan

Design for the 20 enemy types listed in `.claude/commands/plan-enemy-types.md`. This is the
**written deliverable** — none of these enemies are implemented yet (except the Troll, which
already shipped). Execute in the phased order at the bottom, one phase per session, using
`/add-enemy-type` for each enemy and `/generate-enemy-prefabs` for its prefab.

Ground rules that apply to every enemy below:

- **Prefab**: an `EnemyManifest` entry → generator-built variant of `Goblin Enemy (Base)`.
  All enemies reuse the goblin mesh until the manual art pass (materials/models are never
  auto-generated — user decision 2026-07-13).
- **CSV**: one row in `Config/Enemies.csv`; stat columns are optional overrides, `spawnChance`
  is a percent, `minSpawn` ramps +1/wave after `unlockWave`, capped by `maxConcurrent`.
  The draft rows below are starting points for a balance pass, not gospel.
- **New attack components** follow the `add-enemy-type` rules: config on a `*SO`, `OnValidate`
  auto-wire + `Start` null-check/`anyError`, unsubscribe in `OnDestroy`, stamp
  `Damage.source`/`sourcePosition`, wide AoEs `unparryable` + usually `DamageType.elemental`,
  stop on own death and player death, expose `SetDamage(float)` and join the chain in
  `WaveSpawner.ApplyDefinition`.
- **Animator**: any new trigger needs a manual state on the shared goblin controller (TODO entry
  per enemy). Until then attacks still deal damage on their wind-up timers — they just don't animate.

## Phase ① — CSV-only (no new code)

| Enemy | Concept | Draft CSV row (`id,display,hp,dmg,minG,maxG,speed,scale,unlock,chance,minSpawn,maxConc,impRes`) |
|---|---|---|
| **Dwarf** | Swarm: extreme speed, low HP | `dwarf,Dwarf,4,1,1,3,6.5,0.7,3,30,2,,0` |
| **Ancient Warrior** | Standard balanced | `ancient_warrior,Ancient Warrior,20,2,6,12,4,1,5,25,1,,1` |
| **Big Ork** | Heavy: high damage, medium speed | `big_ork,Big Ork,45,6,20,40,3.2,1.35,7,20,1,2,4` |
| **Troll** | Meat shield | **Already shipped** (`troll` row + `Troll Enemy Variant`). Skip. |

Manifest entries: structure-free (the `dwarf` seed entry is already in `EnemyManifest.cs` as the
generator smoke test — add its CSV row when this phase lands). Stock `AIAttack` does the rest.

## Phase ② — projectile family

Shared exemplar: `LightningBallAttack` + `LightningBall` (Storm Witch). All three disable base
`AIAttack`, set `navStoppingDistance`, add a fire-point child.

- **Forest Guardian** — fast straight projectiles. **Zero new code**: reuse `LightningBallAttack`
  with its own `LightningBallAttackSO` (high projectile speed, short cooldown) and its own
  projectile prefab (a re-tinted `LightningBall` — manual art). Draft row:
  `forest_guardian,Forest Guardian,18,3,10,20,3,1,8,20,1,2,2`.
- **Mystic** — slow homing orbs. New `HomingOrb` (copy `LightningBall`, add per-frame steering:
  rotate velocity toward the player capped at `turnRateDegPerSec`, give up homing after
  `homingSeconds` so orbs stay dodgeable) + `HomingOrbAttack` (copy `LightningBallAttack`).
  New SO: `HomingOrbAttackSO`. Draft row: `mystic,Mystic,16,4,12,24,2.8,1,10,15,1,2,2`.
- **Evil God** — 360° radial bursts. New `RadialBurstAttack`: every cooldown, spawn
  `projectileCount` straight projectiles (reuse `LightningBall` prefab class) at even angles;
  needs no line of sight. Big, slow, rare. New SO: `RadialBurstAttackSO` (count, cooldown,
  projectile speed/damage). Draft row: `evil_god,Evil God,220,5,80,160,2,1.5,16,8,,1,50`.

## Phase ③ — auras & on-death

- **Ancient Queen** — armored vs light attacks. New `ArmorPlating` hooking
  `Health.ScaleDamageTaken` (the `RageBuff` multiplier-hook precedent): hits with
  `damage.value < lightHitThreshold` are scaled by `lightHitMultiplier` (e.g. 0.4); heavy/charged
  hits pass through — the counter is "charge your swings". SO: `ArmorPlatingSO`. Draft row:
  `ancient_queen,Ancient Queen,90,3,40,80,3,1.2,11,12,,1,6`.
- **Forest Witch** — support aura. New `AllyAura`: tick ~1 Hz, `OverlapSphere` for `Enemy` roots,
  either `Health.Heal(healPerTick)` (exists already) or a temporary `ScaleDamageTaken` defense
  buff on allies (heal is simpler — recommend heal-only v1). Never affects itself (or does — a
  design choice; default: excludes self so she stays killable). SO: `AllyAuraSO`. Draft row:
  `forest_witch,Forest Witch,25,2,15,30,3,1,9,15,,2,2`.
- **Mutant Guy** — toxic pool on death. New `ToxicPoolOnDeath`: `Health.OnDied` listener (the
  `CoinDropper` idiom) spawning a pool prefab modeled on `LightningStormZone` (periodic
  `elemental`, `unparryable` AoE, self-destructs after `duration`). Pool prefab is manual art;
  the zone script is a copy-tune of `LightningStormZone`. Draft row:
  `mutant_guy,Mutant Guy,30,3,12,25,3.5,1.1,9,18,1,3,2`.
- **Medusa** — cone slow aura. New `MedusaGazeAura`: ~5 Hz cone test against `Player.Instance`
  (range + `Vector3.Dot(transform.forward, toPlayer) >= cos(halfAngle)` — the
  `Parry.facingDotThreshold` shape). On enter: `PlayerStats.AddModifier(StatType.MoveSpeed,
  Percent, -0.5f)`; on exit/own death/`OnDestroy`/player death: add the exact negative back
  (**there is no `RemoveModifier`** — the `HoldTheLineBonus` idiom). **Static refcount so two
  Medusas can't stack to a frozen player** (only the first applies). `PlayerMoveSpeedBinder`
  picks the change up live via `OnStatChanged`. SO: `MedusaGazeAuraSO`. Draft row:
  `medusa,Medusa,35,3,20,40,2.8,1.1,12,12,,1,4`.

## Phase ④ — movement specials

- **Spirit Demon** — no body-blocking. Mostly prefab config: per-enemy `AIMovementSO` with both
  avoidance tiers set to none, `CapsuleCollider.excludeLayers` = the enemy layer (layer 7)
  (both settable by generator wiring), optional ghost material (manual art). No new component
  unless float-bobbing is wanted (tiny `FloatBob` cosmetic). Draft row:
  `spirit_demon,Spirit Demon,14,2.5,8,16,4.5,1,10,20,1,3,50` (high impulse resistance — reads
  better than a ragdolling ghost).
- **Dark Elf** — lateral dodge when targeted. New `DodgeDash`: on trigger, burst-strafe via
  `NavMeshAgent.Move` over ~0.25s with a cooldown. **Open design question — "when targeted":**
  recommend v1 = dodge on a timer while within the player's `attackRange`+2m and in the player's
  facing cone (the Medusa cone test, reversed); a bow-aim-ray trigger can layer on later for the
  Swordsman. SO: `DodgeDashSO`. Draft row: `dark_elf,Dark Elf,15,3,15,30,4.2,1,11,15,1,3,1`.
- **Slayer** — telegraphed line dash. New `SlayerDashAttack` (exemplar: `MountedKnightBrain`'s
  telegraph/charge beats + `TrollSlamAttack`'s telegraph prefab handling): red line telegraph
  (stretched `SlamTelegraph`-style quad, manual art) for `telegraphSeconds`, then near-instant
  dash along the locked line (agent teleport via `agent.Warp` at the end; capsule-overlap the
  swept lane for damage — `unparryable`). SO: `SlayerDashAttackSO`. Draft row:
  `slayer,Slayer,40,8,30,60,3.5,1.1,13,10,,1,5`.
- **Red Demon** — leap & slam. New `LeapSlamAttack`: TrollSlam-style ground telegraph at the
  player's position, parabolic flight to it with the agent disabled, `agent.Warp` re-seat on
  landing (borrow `ImpulseReceiver`'s NavMesh-recovery pattern), then the slam AoE (reuse
  `TrollSlamAttack`'s impact block: `unparryable`, impulse-stamped). SO: `LeapSlamAttackSO`.
  Draft row: `red_demon,Red Demon,120,15,60,120,3,1.4,15,8,,1,50`.

## Phase ⑤ — the hard four (each needs a support-system change or a prototype pass)

- **Pig Butcher** — the hook. New `HookProjectileAttack` + `HookProjectile` (`LightningBall`
  pattern; `sharp`, **parryable** — single readable projectile) + **new `PlayerPullReceiver`**
  on the Player prefab: on `Pull(target, ~0.5s, stopDistance)`, disable the PlayerMount-style
  control-component list but **keep the `CharacterController` enabled** and `Move()` toward the
  butcher each frame — walls interrupt the drag for free. No-op while mounted (`PlayerMount` owns
  the controller) or dead; restore with PlayerMount's dead-player guard (`Player/PlayerMount.cs`
  ~lines 329–338, 392, 442–447). Verify the Cinemachine follow doesn't pop on re-enable.
  Draft row: `pig_butcher,Pig Butcher,70,6,40,80,3,1.3,14,10,,1,6`.
- **Barbarian Giant** — whirlwind that eats projectiles. New `WhirlwindAttack` (periodic
  self-centered damage pulse while moving; `unparryable`, elemental-style wide AoE) + **new
  `IPlayerProjectile { Vector3 Position; void Shatter(); }`** with a static `Live` registry
  (the `EnemyRagdoll.ActiveCount` flavor), implemented by `AxeProjectile` and
  `MagicMissileProjectile` in `OnEnable`/`OnDisable`; the whirlwind iterates a copy each
  `FixedUpdate` and `Shatter()`s anything in radius (safe — `AxeProjectile` already destroys
  itself mid-flight liberally, and `PlayerThrownAxe` keeps no in-flight reference). Bow is
  hitscan — no interaction, and the whirlwind must **not** carry a solid collider on hittable
  layers or it eats bow shots. Draft row:
  `barbarian_giant,Barbarian Giant,150,10,70,140,3.2,1.5,17,8,,1,50`.
- **Fort Golem** — dwarf spawner. New `MinionSpawner` (serialized minion prefab + roster ref;
  every `interval` spawn `count` dwarves nearby, NavMesh-snapped, apply the dwarf CSV row via the
  public static `WaveSpawner.ApplyDefinition` — the EnemyZoo precedent) + **new
  `WaveSpawner.RegisterExternalEnemy(GameObject)`**: `waveGoblinTotal++`, `aliveCount++`,
  `aliveEnemies.Add`, self-unsubscribing `OnDied` → `HandleEnemyDied()`. **Do not touch
  `remainingToSpawn`** — the debug-cheat precedent routes through `SpawnEnemy`, minions don't.
  Registration failing (no spawner, intermission) must degrade gracefully — minions still work
  standalone since kill credit/coins/corpses are all `Health`-event-driven. Draft row:
  `fort_golem,Fort Golem,200,4,80,160,1.2,1.6,18,8,,1,50`.
- **Mechanical Golem** — pinball charge. New `PinballCharge`: **highest risk, prototype first** —
  rev-up telegraph, then agent detached and manual velocity movement with `NavMesh.Raycast` wall
  reflection (bounce = reflect velocity about the hit normal), damage on contact, re-seat with
  `agent.Warp` when done. Non-goblin silhouette wanted eventually; until then high
  `impulseResistance` and no ragdoll concerns (still the goblin rig). Draft row:
  `mechanical_golem,Mechanical Golem,100,12,50,100,2,1.4,19,8,,1,50`.

## Support-system changes ledger (small, shared)

| Change | Needed by | Shape |
|---|---|---|
| `WaveSpawner.RegisterExternalEnemy` | Fort Golem | grow wave total + alive set, never `remainingToSpawn` |
| `PlayerPullReceiver` (Player prefab) | Pig Butcher | CharacterController.Move drag, PlayerMount disable-list idiom |
| `IPlayerProjectile` + `Live` registry | Barbarian Giant | on `AxeProjectile`, `MagicMissileProjectile` |
| Medusa slow refcount | Medusa | static count guarding the MoveSpeed modifier |
| `ApplyDefinition` chain entries | every new attack | add `?.SetDamage(...)` per new component |

## Per-enemy manual pass (every phase, via `/editor-wiring-todo`)

Animator state for any new trigger; MMF feedbacks; telegraph/projectile/pool art; per-enemy
material (visual identity — currently everything is a goblin); balance pass on the CSV row;
EnemyZoo eyeball + DevConsole `DebugSetNextWave` to the unlock wave.
