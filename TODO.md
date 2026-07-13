# TODO

## Between-wave intermission (slow-mo + stats + Recover/Hold the Line) + Loot chests — Unity Editor wiring

The C# is done. **Hold the Line economy** (`Economy/HoldTheLineBonus.cs`): a scene-singleton greed
meter — `Extend()` banks one more consecutive Hold by adding `StatType.HoldTheLineGoldPerWave`
(base 5%, registered in `Start`) as a stacking Percent modifier on `StatType.GoldDropMultiplier`
(so `CoinDropper` needs zero changes — it already reads the multiplier fresh per kill). Both loss
conditions reset the whole stack: player `Health.OnDied` and `Gate.OnAnyGateDestroyed` (the two
fail-states — the gate must be re-enabled in the scene). Exposes `Instance`/`Multiplier`/
`StackCount`/`OnChanged`; optional `extendFeedback`/`resetFeedback` MMF_Players. **Wave flow**
(`Waves/WaveSpawner.cs`): after each `WaveCleared`, `RunWaves` now fires the new
`IntermissionStarted` event and parks on `WaitForPlayerChoice` until the UI calls `ChooseRecover()`
(normal countdown next) or `ChooseHoldTheLine()` (skips the countdown — next wave lands
immediately). **With no subscriber the spawner proceeds exactly as before**, so the game runs
un-wired. New top-level `IntermissionChoice` enum. **Intermission UI**
(`Waves/WaveIntermissionUI.cs` + `UI/WaveStatsPanel.cs`): the orchestrator plays a slow-mo
MMF_Player, reveals the stats, shows the two choices, opens the skill-tree panel on Recover, and
frees/re-locks the cursor (the `DeathScreen` pattern); the stats panel snapshots per-wave deltas
(`WaveStarted` → clear) and count-up-animates goblins/gold/total + Hold multiplier on unscaled
time. **Loot chests** (`Chests/Chest.cs`, `Chests/ChestLootTableSO.cs`, `Chests/ChestSpawner.cs`):
a chest is just a `Health` target (hit/break juice on its `damageFeedback`/`deathFeedback`
MMF_Players — no new hit script), dropping guaranteed gold (a `Coin`) + one weighted bonus item
from existing pickups; the spawner scatters N per wave on `WaveStarted` (NavMesh-snapped, away from
the player, weighted by chest level). **Reincarnate node** `greedy_stand` ("Greedy Stand", +2%/wave
per level ×4) added to `Config/Reincarnate.csv`. New `StatType.HoldTheLineGoldPerWave`.

- [ ] **Re-enable the gate in the scene** (your task) — the second fail-state. `HoldTheLineBonus`
      and the intermission already listen for `Gate.OnAnyGateDestroyed`; nothing else to wire for it.
- [ ] **HoldTheLineBonus**: add the component to a scene object (the player root or a systems object
      — it finds `Player.Instance.Stats` itself). Optional: assign `extendFeedback` (a chime/flash
      when the bonus grows) and `resetFeedback` (a deflating stinger on loss). `baseGoldPerWave` 0.05.
- [ ] **Intermission canvas** (new, or a panel on the HUD canvas):
  - [ ] A root object with `WaveIntermissionUI`; assign `spawner`/`holdTheLineBonus`/`statsPanel`
        (auto-wire via `OnValidate`), `intermissionRoot`, `choiceButtons` (container), the two
        `Button`s (`recoverButton` "Recover and Upgrade", `holdTheLineButton` "Hold the Line"), a
        `continueButton` (shown with the tree), and `skillTreePanel`.
  - [ ] `waveClearFeedback` MMF_Player = an **MMF Timescale Modifier** (~0.15× for ~0.5s, ease-out)
        + optional **MMF Bloom / Chromatic Aberration (URP)** grade + a riser **MMF Sound**. Keep its
        total duration ≤ `slowMoRealSeconds` (0.7) — the UI waits that long for the slow-mo to restore
        before it hard-freezes time, so they don't fight over `Time.timeScale`.
  - [ ] **Set every intermission MMF_Player to Unscaled time mode** (the MMF_Player's *TimeScale Mode:
        Unscaled*) — the stats/choice screen runs at `Time.timeScale = 0`, so scaled feedbacks freeze.
  - [ ] `WaveStatsPanel` on a child; assign the TMP labels (`waveLabel`, `goblinsSlainText`,
        `goldEarnedText`, `totalGoldText`, optional `bonusMultiplierText`), optional fill `Image`s
        (`goblinsBar`/`goldBar`), and per-line reveal MMF_Players (pop + tick sound + `MMF_TMPText`
        reveal). Tune `countUpDuration`/`lineStagger`.
  - [ ] `skillTreePanel` = a panel hosting a `SkillTreeView` bound to `SkillTreeService.Instance`
        (same as the death-screen gold tree — reuse that prefab/panel or make a sibling).
- [ ] **Chest prefabs** (one per level/model): `Collider` **on a layer inside the sword
      `DamageTrigger.hitLayers` mask** (else BladeSweep won't hit it), a **kinematic** `Rigidbody`
      (or none), `Health` (+ a per-level `HealthSO`), `MMHealthBar` + `HealthBarUI`, `Chest`
      (assign `lootTable`, `coinPrefab`, your explosion `breakVfxPrefab`). Assign `Health`'s
      `damageFeedback` (squash/flash/thunk/splinters) and `deathFeedback` (big boom + hit-stop).
      **Do NOT add `ImpulseReceiver`/`KnockbackReceiver`** — that's what makes chests impulse-immune.
  - [ ] **ChestLootTableSO assets** (menu `Scriptable Objects/ChestLootTableSO`), one per tier:
        set `minGold`/`maxGold`, `bonusItemChance`, and the weighted `items` roster from existing
        pickup prefabs (`HealthPack`, `LightningOrb`, `ImpulseOrb`, a gold-bag `Coin`).
  - [ ] Place a **`ChestSpawner`** in the scene; fill `chestPrefabs` (prefab + weight + unlockWave
        per level), `minPerWave`/`maxPerWave`, and optional `spawnPoints`.
- [ ] **Greedy Stand icon**: `greedy_stand` ships with a blank icon — assign a sprite via
      **Bladehold > Skill Tree Editor** (and re-save so the row parses), or it shows no icon.
- [ ] **Balance**: `baseGoldPerWave` (5%), the `greedy_stand` cost/growth/maxLevel, chest
      `HealthSO`s / loot tables / per-wave counts, and the slow-mo strength/duration.
- [ ] **Optional polish**: a persistent HUD indicator for the live Hold multiplier
      (`HoldTheLineBonus.Instance.Multiplier` / `OnChanged`). (The choice screen freezes time — the
      loot window is the unfrozen countdown after choosing Hold the Line.)

## Manual verification (intermission + chests)

- [ ] Clear a wave → brief slow-mo, then **time freezes** and the stats cascade in (goblins/gold
      count up, bars wipe) with the cursor freed; the player can't move or loot while deciding.
- [ ] "Hold the Line" → time resumes and the **pre-wave countdown runs as a loot window** (grab the
      coins on the cleared field before the next wave hits); coin drops are visibly larger, and
      holding wave after wave stacks the bonus higher (`bonusMultiplierText` / HUD climbing x1.05 →
      x1.10 → …).
- [ ] "Recover and Upgrade" → the skill tree opens **still frozen** (untimed shopping); buy nodes,
      hit Continue → time resumes and the next wave starts **immediately** (no loot window). The
      banked Hold multiplier is retained (not grown, not reset).
- [ ] Die, or let the gate be destroyed → the Hold multiplier resets to x1 (verify the indicator
      drops immediately and the next run starts fresh); checkpoint restart returns to the died-on wave.
- [ ] With no intermission UI in the scene, waves still chain via the normal countdown (un-wired
      safety).
- [ ] Chests spawn each wave, away from the player; smashing one depletes its health bar, plays hit
      feedback per swing, and on break plays the explosion + drops gold plus sometimes one item
      (health pack / lightning orb / impulse orb).
- [ ] A chest is **never** flung or knocked by impulse-buffed / knockback sword hits (kinematic,
      no `ImpulseReceiver`); different chest levels show different models/health/loot.
- [ ] Reincarnate, buy **Greedy Stand** → the per-wave Hold bonus is visibly larger on the next run.

## Mounted Knight enemy + rideable Horse — Unity Editor wiring

The C# is done. **Shared horse** (`Horse/` — new folder): `HorseSO.cs` (all locomotion/charge/trample
tunables), `HorseMotor.cs` (player-mode driving: W/S accel/brake/reverse, A/D turn, held Shift at
speed = charge; speeds × `StatType.HorseSpeedMultiplier`), `HorseAnimation.cs` (transform-delta →
damped `Speed`/`Turn` animator params + `Rear`/`Charge`/`Death`, one script for AI/riderless/player
modes), `HorseChargeDamage.cs` (the shared trample box — the `TrollSlamAttack` impulse-stamping shape
with a per-target re-hit cooldown, used by both the knight's charge and the player's),
`HorseMountable.cs` (jump-into-trigger mounting, occupancy-gated), `HorsePickupProxy.cs` (a ridden
horse collects pickups on the rider's behalf — `Coin`/`HealthPack`/`ImpulseOrb`/`LightningOrb`
redirect through it; `HealthPack` also heals the horse with the Stable Diet node). **Knight**
(`Enemies/`): `MountedKnightSO.cs`, `MountedKnightBrain.cs` (standoff ring → aim → rear + telegraph
lane pre-clamped with `NavMesh.Raycast` → `agent.Move` dash with the trample open → decelerate →
cooldown), `MountedKnightRider.cs` (knight root = the spawned enemy; detaches the horse child at
Awake, seat-syncs in LateUpdate, forwards all horse damage to the knight via `TryBlockDamage`,
unseats him below 50% HP — enabling his stock goblin components, which ship disabled — and hands the
horse over riderless at full HP; killed mounted = corpse-dismount, wave still clears). **Player**
(`Player/`): `PlayerMount.cs` (mount/dismount state machine, invulnerable-while-mounted by
forwarding hits to the horse, sword `SetReachBonus`/`SetIgnoredTarget` while mounted, Barded Steed
`ScaleMaxHealth` once per horse), `MountedCombat.cs` (re-fires `StartAttack`/`IsHoldingAttack` since
the Synty controller is disabled in the saddle), `MountedCombatLook.cs` (the `BowAimLook` sibling —
spine yaw/pitch toward the camera while attacking/aiming mounted), `StartMountedSpawner.cs` (the
Reincarnate Cavalier node). Core changes: `DamageTrigger` gained `SetReachBonus` (samples past the
blade tip — no visual change) + `SetIgnoredTarget`; `Health.ScaleMaxHealth` (fraction-preserving);
`PlayerBow` gained the `HorseArcheryUnlocked` gate + `SetIgnoredTarget`; `AIAttack` range is now
planar XZ (saddle height); 6 new `StatType`s; `knight` row in `Config/Enemies.csv` (wave 7, 60 HP,
dmg 4 → charge 16, max 1); `WaveSpawner.ApplyDefinition` routes damage to
`MountedKnightBrain.SetDamage`; a `Dismount` action (X / gamepad East) added to the vendored
`Controls.inputactions` + `InputReader.cs` (marked additions); `horse_*` branch in
`Config/SkillTree.csv` off `range_ext`, `start_mounted` root in `Config/Reincarnate.csv`.

- [ ] **Regenerate the input class**: select
      `Assets/Third Party/Synty/AnimationBaseLocomotion/Samples/Scripts/InputSystem/Controls.inputactions`,
      and Apply/"Generate C# Class" so `Controls.cs` picks up the new `Dismount` action (the
      interface gains `OnDismount`, already implemented in `InputReader`). Until then dismount
      falls back to a direct X-key read (keyboard only — `PlayerMount` logs a warning).
- [ ] **Create SO asset instances**: a `HorseSO` (menu `Scriptable Objects/HorseSO` — defaults are
      authored in-code: maxSpeed 8, chargeSpeed 12, trample 15 dmg / impulse 10/14), a
      `MountedKnightSO` (`Scriptable Objects/MountedKnightSO` — standoff 12, rear 1.2s, charge 14
      m/s ×4 dmg, dismount at 50%), and a horse `HealthSO` (~100 max health).
- [ ] **Horse prefab** (`Bladehold Prefabs/Horse.prefab`) from
      `Assets/Malbers Animations/Horse AnimSet Pro/Undead Horse/Models/Undead_Horse_Re.fbx`:
      root with `Health` (+ horse `HealthSO`), `HorseAnimation`, `HorseChargeDamage`,
      `HorsePickupProxy`, `CorpseDespawner`, `DisableCollidersOnDeath`, optional
      `DamageNumberSpawner`, a body collider, and — **all disabled** — `NavMeshAgent`
      (horse-sized), `CharacterController` (horse-sized), `HorseMotor`. Children: a `RiderSeat`
      empty on the saddle (assign on `HorseMotor.riderSeat` and `MountedKnightRider.riderSeat`) and
      a `HorseMountable` trigger collider over the saddle (**enabled** — riderless is the prefab's
      default state). NO `Enemy`/`CoinDropper`/`EnemyRagdoll` — it's a vehicle: no kill credit, no
      gold, animation-only death.
  - [ ] **Horse animator controller**: params `Speed` (float, m/s), `Turn` (float -1..1), `Charge`
        (bool), `Rear` (trigger), `Death` (trigger). Blend tree on Speed: `H_Idle_01` → `H_Walk` →
        `H_Trot` → `H_Canter` → `H_Gallop` (blend the `_Left`/`_Right` variants on Turn), `Charge`
        → a gallop/lean state, `Rear` → `H_Attack_Front_Legs`, `Death` → `H_Death01`. Clips under
        `Assets/Malbers Animations/Horse AnimSet Pro/2 - Animations/Animations Clips/Horse/`.
- [ ] **Knight prefab** (`Knight Enemy (Mounted).prefab`): knight ROOT with enabled `Health` (+
      HealthSO), `Enemy`, `CoinDropper`, `DamageNumberSpawner`, `DisableCollidersOnDeath`,
      `CorpseDespawner`, `EnemyRagdoll`, `MountedKnightRider`, `MountedKnightBrain`, optional
      `PowerupDropper`, body capsule + `VulnerableSpot` head child; **disabled** (enabled at
      dismount): `NavMeshAgent` (goblin-sized), `AIMovement`, `AIAttack`, `AIAnimation`,
      `ImpulseReceiver`, `KnockbackReceiver`. Nest the Horse prefab as a child at the root origin
      (`MountedKnightRider.Awake` detaches it at runtime); most refs auto-wire via `OnValidate`,
      assign `riderSeat` + the SOs + `telegraphPrefab` by hand.
  - [ ] **Knight model**: try `Undead_Knight.fbx` (same Malbers folder) imported as **Humanoid**
        and retarget the goblin animator controller; if the rig won't map, fall back to a Synty
        goblin rig with knight-ish materials. Add a `Riding` bool state (seated pose from the
        Malbers `Rider/` clips, e.g. `Rider_Weapon_Sword` idle) and a `Dismount` trigger
        (`Rider_Mount_Dismount_Left` or a simple cut); keep the stock `Attack`/`Death`/`Cheer`.
  - [ ] **Telegraph prefab**: a flat ground quad (the Troll telegraph precedent) — it gets scaled
        to (laneWidth, y, laneLength) and oriented along the charge; assign on
        `MountedKnightBrain.telegraphPrefab`. Optional `MMF_Player`s: rear whinny → `rearFeedback`,
        charge thunder → `chargeFeedback`.
  - [ ] Register the prefab in `WaveSpawner.enemyPrefabs` under id `knight` (row already in
        `Config/Enemies.csv`).
- [ ] **Player prefab**: add `PlayerMount` (assign the sword `DamageTrigger` explicitly — the
      VampiricBlade precedent; fill `componentsToDisableWhileMounted` with the
      `SamplePlayerAnimationController`, `CombatFacing`, `AttackCancelsSprint` — NOT `InputReader`/
      `PlayerAttack`/`PlayerBow`, they stay live for mounted combat, and NOT `PlayerMount` in
      `PlayerDeath`'s list), `MountedCombat`, `MountedCombatLook`, `StartMountedSpawner` (assign
      the Horse prefab). Everything else auto-wires.
  - [ ] **Player animator**: add `IsMounted` (Bool) + `HorseSpeed` (Float) params and a "Riding"
        layer — seated idle/gait blend on `HorseSpeed` from the Malbers `Rider/` clips — plus an
        upper-body-masked attack layer above it **reusing the existing sword attack clips** (their
        `OneHandedSwordAttack`/`PlaySwordWoosh` animation events must ride along, or the mounted
        sword deals no damage); the bow layer sits above Riding so mounted archery poses compose.
        Until wired, `PlayerMount` logs a one-time warning and everything works with the rig stuck
        in its ground pose.
- [ ] **Scene**: optionally place one riderless Horse prefab in `Bladehold Test Scene` for quick
      mount testing (it needs no NavMesh; the player horse is CharacterController-driven).
- [ ] **Skill icons**: `horse_unlock`/`horse_health`/`horse_speed`/`horse_heal`/`horse_archery`
      (gold tree) and `start_mounted` (Reincarnate) have blank icons — assign in
      **Bladehold > Skill Tree Editor** when art exists.
- [ ] **Balance pass**: the `knight` CSV row, `HorseSO`, `MountedKnightSO`, horse `HealthSO`, the
      `horse_*` node costs/positions, and `PlayerMount.mountedReachBonus` (0.6).

## Manual verification (Mounted Knight + Horse)

- [ ] Reach wave 7 (or dev-set next wave) — one knight spawns riding the horse; it circles at a
      ~12m standoff instead of closing to melee.
- [ ] Telegraph: the horse turns to face you, **rears** (front-legs clip) with a ground lane shown,
      then charges dead straight along it — sidestepping the lane avoids everything; near a wall
      the lane (and the run) is visibly shorter, and the horse never leaves the NavMesh.
- [ ] Standing in the charge hurts (~16) and goblins in the lane ragdoll-fling exactly like
      Impulse-buff hits; the charge is never parried; nothing is hit twice per pass.
- [ ] Sword/arrow hits on the horse OR the knight both pop damage numbers on the knight; below half
      HP he lands beside the horse and fights exactly like a goblin (chase, melee, knockback,
      Impulse fling); Impulse hits while mounted never fling him, they just unseat him faster.
- [ ] After the unseat the horse idles alive at **full HP**, is damageable, and shows no rider.
- [ ] Kill the knight while still mounted (burst / `DebugWipeWave`) — corpse drops beside the
      horse, coins + kill credit + corpse sink all run, the horse survives, and the **wave clears
      with the horse alive**.
- [ ] Without Saddle Up: jumping into the saddle does nothing. Buy it — jumping into a riderless
      horse's saddle mounts (walking through the trigger does not); the camera follows.
- [ ] W/S accelerates/brakes, A/D turns with gait blending; holding Shift at speed charges —
      goblins ahead take damage and ragdoll-fling; X (and gamepad East after the input-class
      regen) dismounts beside the horse.
- [ ] Mounted, the player takes ZERO damage while enemy hits visibly drain the horse's HP; at 0 the
      horse plays `H_Death01`, the player lands beside the corpse with controls back, and the dead
      horse can't be re-mounted. Solid/Parry cooldowns are NOT consumed by mounted hits.
- [ ] Mounted sword: swings connect noticeably farther with **no visible blade change**, and never
      damage your own horse; dismounted, the reach returns to normal.
- [ ] Bow from horseback only works with Horse Archer (aiming does nothing without it); arrows
      never stop on the horse's own body.
- [ ] Ride over coins/orbs/health packs — the player's wallet/buffs/health collect them (riderless
      horses collect nothing); with Stable Diet, packs also heal a wounded horse.
- [ ] Barded Steed raises the ridden horse's max HP (fraction-preserving on a wounded horse);
      Thoroughbred visibly raises its speed.
- [ ] Reincarnate (wiping the gold tree), buy Cavalier — the next run starts already mounted and
      riding still works with zero gold-tree nodes; restart-from-wave while mounted reloads clean.
- [ ] Die on foot next to a riderless horse — the corpse doesn't mount; die mid-ride (if ever
      possible) — controls stay dead.

## Bow unlock skill — Unity Editor wiring

The C# is done: `StatType.BowUnlocked` (base 0 = locked) is registered by `PlayerBow.Start`, and
`PlayerBow.StartAim` now early-returns while locked (aiming does nothing, sword stays out) —
`PlayerBow.IsUnlocked` exposes the state. A `bow_unlock` node ("Bow", cost 60) in
`Config/SkillTree.csv` sets `BowUnlocked` to 1. The bow now only works once that node is bought.

- [ ] **Icon**: the node names `Archerskill_01_nobg`, which isn't in the gold `SkillTreeSO`'s `icons`
      list yet. Add it via **Bladehold > Skill Tree Editor** (drag the
      `Assets/Bladehold/Bladehold Images/Skills/Archerskill_01_nobg.png` sprite onto the node) or the
      node just shows no icon.
- [ ] **Balance**: `cost 60` is a placeholder for a whole-weapon unlock — tune to taste.

## Manual verification (bow unlock)

- [ ] Fresh save (or after a Reincarnate): holding aim (right-click) does nothing and the sword still
      swings; buy the "Bow" node on the death-screen tree, restart, and aim now draws/fires the bow.
- [ ] Existing bow upgrade nodes (Multi Shot etc.) are still independently buyable and take effect
      once the bow is unlocked.

## Leveled skill nodes (collapsed duplicates) — Unity Editor wiring

The C# is done: each skill is now **one node upgraded through levels** instead of many duplicate
rows. `SkillNode` gained `maxLevel`, per-level `costPerLevel` (from a base `cost` × `growth`), an
`upgradeText`, and per-level effect amounts (`SkillEffect.amounts` + `AmountForLevel`); `SkillTreeSO`
parses the new 14-column CSV (`id,displayName,description,upgradeText,cost,growth,maxLevel,stat,kind,
amount,prereqs,x,y,icon`) with `;` between stats and `|` between per-level values. Both services
(`SkillTreeService`/`ReincarnateService`) track a `Dictionary<string,int>` of levels, persisted as a
**multiset** in `SaveData.purchasedNodeIds`/`purchasedReincarnateNodeIds` (id once per level owned) —
`GetCost` is the next level's cost, `TryPurchase` buys one level and applies that level's increment,
`GetLevel`/`IsMaxed` are new on `ISkillTreeService`. `SkillNodeView` shows a level badge + `Maxed`
state; `SkillTooltip` swaps unlock→upgrade text and previews the next level. Both `Config/*.csv` were
migrated to collapsed leveled nodes. The Skill Tree editors dropped `family`/`New Skill Level` and
gained Upgrade Text / Growth / Max Level fields.

- [ ] **Node prefab — level label**: add an optional `TMP_Text` to the `SkillNodeView` node prefab
      and assign it to the new `levelText` field, so multi-level nodes show `n/max` (e.g. `3/10`).
      Code degrades gracefully if left unwired (no label, everything else works). The prefab is the
      one referenced by both `SkillTreeView.nodePrefab` in `SkillTreePreview.unity` (and the
      death-screen canvas).
- [ ] **Re-check icons list**: every icon name in the migrated CSVs was already used before, so all
      should resolve — if any node shows no icon, add the sprite via **Bladehold > Skill Tree Editor**.
- [ ] **Balance pass**: the `growth` multipliers were fitted to roughly match the old hand-tuned cost
      ladders but the exact per-level numbers shifted — tune `cost`/`growth`/`maxLevel` to taste.

## Manual verification (leveled skill nodes)

- [ ] Open `Assets/Bladehold/Bladehold Scenes/SkillTreePreview.unity`, Play, and on the gold tree:
      clicking a leveled node (e.g. Sharpened Edge) buys level 1, reveals its linked nodes (Like
      Butter), and the badge reads `1/10`; further clicks raise the level, escalate the cost, and
      stack the stat; at the cap the node reads `Maxed` and can't be clicked.
- [ ] Tooltip shows the unlock text before purchase and the upgrade text once owned, with the
      next-level before→after preview.
- [ ] Spot-check the Reincarnate tree (points currency) — Golden Scent / Grave Robber level up.
- [ ] `ClearSave` on the preview resets between runs; a save from before the migration loads without
      errors (old ids are simply skipped).

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

## Chain Lightning bolt visual (SineVFX LightningSystemChain) — Unity Editor wiring

The C# is done: `Player/ChainLightningVfx.cs` drives a single, reused SineVFX `LightningSystemChain`
so a chain draws an animated bolt through the enemies it hops across (previously only the one-off
`bounceVfxPrefab` flashed at each target). It owns a pool of world-stable anchor transforms, and
`ShowChain(points)` snaps them to the captured hop positions, assigns them as the chain's
`chainPoints`, flips `vfxEnabled` on, and auto-off after `flashDuration` (0.15s). `ChainLightning`
collects the origin + each hop into `chainPointsBuffer` and calls `ShowChain` at the end of a chain;
`chainVfx` auto-wires from `Player.Instance.GetComponentInChildren<ChainLightningVfx>()`. Reused
single instance by design — if several sword hits in one swing each chain, the most recent wins the
shared bolt (all damage still lands). Both new files added to `Assembly-CSharp.csproj`; build clean.

- [ ] **Player prefab** (`Assets/Bladehold/Bladehold Prefabs/Player.prefab`): drag in a
      **`SingleVFXOnly`** Chain prefab as a child of the player —
      `Assets/Third Party/SineVFX/LightningSystem/CompleteEffectsPrefabs/SingleVFXOnly/Chain/LS_Chain_0X.prefab`
      (pick a look; the `04_ColorBlend` variants read as electric-blue). **Not** a `WithExampleMeshes`
      variant — those carry visible demo spheres. Clear/ignore its authored `chainPoints` (we overwrite
      them at runtime).
- [ ] Add a **`ChainLightningVfx`** component on the player root; assign its `lightningChain` to the
      child `LightningSystemChain` (auto-wires via `OnValidate` if it's the only one in children).
      Defaults for `flashDuration` (0.25) and `maxAnchors` (16) are fine. `ChainLightningVfx` now
      **forces `autoScaleEnabled = false` and sets `masterScale = boltScale` at startup** — the raw
      prefab ships with `autoScaleEnabled = true` and a null `autoScaleAnchor`, which NREs every frame
      in `ProcessAutoScale()` and stops the bolt rendering, so this must stay code-driven. No manual
      autoScale wiring needed.
- [ ] On the existing **`ChainLightning`** component, leave `chainVfx` blank to auto-wire, or drag the
      `ChainLightningVfx` in explicitly. (The old `bounceVfxPrefab` per-target flash still works and
      complements the bolt — keep or clear as desired.)
- [ ] Tune `ChainLightningVfx.boltScale` (default 1.5) so the arc reads at gameplay camera distance;
      bump it up if the bolt still looks too thin for enemy-to-enemy spans.

## Manual verification (chain lightning bolt)

- [ ] Buy Chain Lightning, pick up a Lightning Orb, hit a goblin in a crowd → a visible animated bolt
      arcs from the hit point through each chained goblin (matching the damage numbers), then fades.
- [ ] Kill/scatter enemies so a chain finds no second target → no bolt (needs ≥2 points), no errors.
- [ ] Run around while chaining → the bolt stays anchored in the world for its flash (doesn't drag
      along with the player).


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


## Dynamic skill tooltips (before→after / % increase) — Unity Editor wiring

The C# is done: `SkillTooltip.Show(node, service)` now appends a tier numeral to a multi-level
(family) node's name and, for a still-buyable **single-effect** family node, replaces the authored
description with a live block read from `Player.Instance.Stats` —
`"{Label} {before} -> {after}"` plus a `+{pct}%` line (via the new `StatDisplay` label/unit table
and `PlayerStats.PreviewValue`). Non-family / multi-effect / unlock / purchased nodes keep their
authored description. `SkillTreeView` passes the service into both `Show` call sites.

- [ ] **Center-align the tooltip description** so the divider + `+%` block reads like the mock: set
      the `descriptionText` TMP field's alignment to Center on the `SkillTooltip` prefab/GameObject
      on **both** the gold tree and Reincarnate tree canvases (the block uses a `──────────` divider
      that only looks right centered). Optional — left-aligned still renders correctly, just off-mock.

## Manual verification (dynamic skill tooltips)

- [ ] Play → die to open the death-screen skill tree. Hover an **unowned** Sharpened Edge node →
      name shows a numeral (e.g. "Sharpened Edge II"), description shows "Sword Damage 10 -> 1X" and
      "+YY%". Buy one, hover another → numeral increments and the "before" reflects new current damage.
- [ ] Hover a **percent** node (Keen Eye / Parry / Fleet Footed) → values render as "5% -> 10%".
- [ ] Hover a **cooldown** node (Solid) → "10s -> 9s" with **no** misleading % line.
- [ ] Hover a **multi-effect / unlock** node (Heavy Strike, Impulse, Conduit, Flaming Arrows I) →
      **authored** description unchanged (no block).
- [ ] Hover a **purchased** node → name keeps its numeral, cost shows "Owned", authored description.
- [ ] Repeat on the **Reincarnate** tree — points/values render, no null-refs.

## Berserker class prototype — Unity Editor wiring

The C# lands in stages (A: class infrastructure, B: reincarnate class select, C: throwing axe,
D: rage + pain into power, E: berserker skill tree + rage bar). **Stage A is done in C#**:
`Player/ClassDefinitionSO.cs` (per-class asset: id, display name/blurb, optional
`AnimatorOverrideController`, per-class `chargeTimePerLevel`, optional per-class `SkillTreeSO`),
`Player/PlayerClassController.cs` (on the player root; in **Awake** — strictly before every Start —
reads `SaveData.playerClassId`, activates the chosen slot's weapon GameObjects and class components
while deactivating the rest (an inactive weapon never registers the shared `SwordDamage`/range/crit
bases, so two `readsPlayerStats` triggers can't clobber each other), re-points
`AnimationEvents`/`VampiricBlade`/`ChainLightning`/`ImpulseHitFeedback`/`PlayerMount` at the active
melee `DamageTrigger` via new setters, applies the class's animator override + charge pacing).
`SkillTreeService.Start` now adopts the active class's `skillTree` when set (saved ids missing from
the active tree were already skipped, so the other class's purchases just go dormant).
`SaveData.playerClassId` ("swordsman" default, wiped by `ResetProgress`). DevConsole gained a class
◄/► picker + "Switch & Reload" cheat; telemetry `run_start` now logs `class=` and binds only the
**active** weapon's trigger.

- [ ] **Axe weapon prefab** (`Bladehold Prefabs/1H_Axe.prefab`): duplicate `1H_Sword.prefab`, swap
      the mesh for an axe model, create a new `DamageSO` (~1.4× sword baseDamage) + `DamageTriggerSO`
      for it, keep BladeSweep `Blade Base`/`Blade Tip` on the new mesh and `readsPlayerStats` ON.
      Nest under the same right-hand bone in `Player.prefab`, **inactive** by default. Re-assign the
      copy's out-of-prefab refs (they don't survive duplication): `DamageTrigger.playerAttack`,
      `SwordHitFeedback.animator`, `SwordChargeFeedback.playerAttack`.
- [ ] **Berserker AnimatorOverrideController** based on `AC_Sidekick_Masculine.controller`: override
      the Attack-layer 1H clip with `Assets/Third Party/Kevin Iglesias/Human Animations/Animations/
      Male/Combat/2H/HumanM@Attack2H01.fbx` (or a Synty `A_MOD_SWD_Heavy*` clip). **Bake animation
      events on the new clip's import settings**: `PlaySwordWoosh` early in the swing and
      `OneHandedSwordAttack` on the impact frame — same names as the 1H clip, so `AnimationEvents`
      routes them to whichever weapon is active. A missing event = silent no-hit swings.
- [ ] **ClassDefinitionSO assets ×2** (menu `Scriptable Objects/ClassDefinitionSO`):
      `swordsman` (displayName "Swordsman", null override, chargeTimePerLevel 1.0, null skillTree)
      and `berserker` (displayName "Berserker", the override controller, ~1.2, skillTree left null
      until Stage E).
- [ ] **Player.prefab**: add `PlayerClassController` to the root. Slot 0 = swordsman: weaponObjects
      `[1H_Sword]`, meleeTrigger/hitFeedback = the sword's, classComponents `[PlayerBow,
      FreezingDraw]`. Slot 1 = berserker: weaponObjects `[1H_Axe]`, the axe's trigger/feedback,
      classComponents empty for now (gains `PlayerThrownAxe`/`RageBuff`/`PainIntoPower` in stages
      C/D). The shared-component refs (`AnimationEvents`, `PlayerAttack`, `VampiricBlade`,
      `ChainLightning`, `ImpulseHitFeedback`, `PlayerMount`, child `Animator`) auto-wire via
      OnValidate — verify they resolved.

**Manual verification (Stage A)**
- [ ] Swordsman unchanged end-to-end: swing, charge, bow, vamp/lightning, mount, tree, reincarnate.
- [ ] Dev console (backquote) → Class picker → "Switch to Berserker & Reload": axe in hand, sword
      gone; axe swings deal the axe SO's damage; sword skill nodes (Sharpened Edge etc.) scale it.
- [ ] Console shows no base-clobber weirdness: only one weapon's `DamageTrigger` registers bases.
- [ ] Telemetry CSV `run_start` row carries `class=berserker` after the switch.

**Stage B (reincarnate class select) — done in C#**: `UI/ClassSelectPanel.cs` (authored buttons, one
per `ClassDefinitionSO`; fills name/description labels, toggles a selected highlight, pre-selects
the saved class so an untouched panel keeps it) and `DeathScreen` hooks: the panel appears with the
Reincarnate tree on the first Reincarnate click, and the second click ("Begin Next Life") persists
`SelectedClassId` via `PlayerClassController.SetSavedClass` right before the scene reload.

- [ ] **Class-select panel** under the death-screen canvas, next to `reincarnateTreePanel`:
      a `ClassSelectPanel` with two authored buttons (Swordsman / Berserker), each with a name TMP
      label, an optional description TMP label, and an optional selected-highlight object. Assign
      the two `ClassDefinitionSO` assets, then assign the panel on `DeathScreen.classSelectPanel`.
      Active/inactive state doesn't matter — `DeathScreen.Start` hides it until points are banked.

**Manual verification (Stage B)**
- [ ] Die → Reincarnate: class panel appears beside the Reincarnate tree, current class highlighted.
- [ ] Pick Berserker → "Begin Next Life": next run is the Berserker with an empty gold tree.
- [ ] Reincarnate without touching the panel: class unchanged.

**Stage C (throwing axe) — done in C#**: `Player/PlayerThrownAxe.cs` (the `PlayerBow` skeleton:
hold aim = wind-up in charge levels tuned on `Player/ThrownAxeSO.cs`, press attack = throw — a
hitscan **sphere cast**, a straight line with `AxeThrowWidth` metres of width, damaging + knocking
back every unique enemy along it with a fresh crit roll each, up to a pierce budget
(`AxeThrowPierceCount` + charge × `piercePerChargeLevel`), stopping at environment or when the
budget runs out; melee suppressed while aiming exactly like the bow; **`AxeThrowUnlocked` base is
temporarily 1** until the `axe_unlock` node ships in Stage E). `Player/AxeProjectileVisual.cs`
(cosmetic spinning axe flying to the line's end, the `BowTracer` sibling).
`Player/IChargedAimWeapon.cs` + `Player/AimWeaponResolver.cs`: `BowAimCamera`, `BowCrosshairUI`,
and `BowReloadUI` now poll whichever aim weapon the active class carries (serialized bow while
enabled, else `PlayerClassController.ActiveAimWeapon`) — aim-camera framing values surface through
the interface from each weapon's SO, so `BowAimCamera` no longer needs its `BowSO` reference.
7 new StatTypes (`AxeThrow*`). `PlayerAttack` also skips melee charge while the axe aims.

- [ ] **ThrownAxeSO asset** (menu `Scriptable Objects/ThrownAxeSO`): defaults are authored in-code
      (dmg 25, range 25 m, cooldown 0.6 s, width 0.6 m, pierce 2, charge 3 levels @ +50%/level,
      knockback 6 + 25%/level; aim-camera block mirrors BowSO).
- [ ] **Throwing-axe projectile prefab**: an axe mesh + optional trail with `AxeProjectile`
      (renamed from `AxeProjectileVisual` — see the projectile-axe follow-up section below);
      assign on `PlayerThrownAxe.projectilePrefab`. **Now required, not cosmetic** — the projectile
      carries the throw's damage.
- [ ] **Player.prefab**: add `PlayerThrownAxe` to the root **disabled**; assign the SO; optional
      `meleeWeaponModel` (the 1H_Axe) / `thrownAxeModel` (an axe prop in the throwing hand) for the
      in-hand swap while aiming. Add the component to the **berserker slot's classComponents** on
      `PlayerClassController` so the class system enables it (and the aim UI finds it).
- [ ] Optional: berserker override controller re-skins the bow layer's aim/fire states with 2H
      wind-up/throw poses (the axe drives the same `IsAiming`/`BowFire` params).

**Manual verification (Stage C)**
- [ ] Berserker: hold aim → camera shoulders in, crosshair fades in and tightens with charge;
      release → back to normal. Attack while aiming throws (no melee swing).
- [ ] A charged throw pierces more goblins in a visible line, knocks them back harder; the reload
      radial runs between throws.
- [ ] Swordsman: bow behaves exactly as before (camera/crosshair/reload unchanged).
- [ ] Mounted berserker: aiming does nothing (no mounted throwing yet — intended).

**Stage D (rage + pain into power) — done in C#**: `Health` gained its third alter-hook,
**`ScaleDamageTaken`** (`event Func<Damage, float>` — handlers return multipliers, products
combined, `Damage.value` scaled in place so damage numbers/telemetry/knockback all see the
mitigated value; CLAUDE.md's Health paragraph updated). `Player/RageSO.cs` + `Player/RageBuff.cs`
(the rage meter: builds from dealing damage — melee `OnHit` via the class controller's active
trigger, plus `PlayerThrownAxe.OnHit` — and ×4 from taking damage; drains after an idle grace;
at full meter +50% damage (read by `DamageTrigger`/`PlayerThrownAxe` like `ImpulseBuff`), +20% move
speed (quantized signed-delta MoveSpeed percent modifiers), −30% damage taken (the new hook); gain
and retention are the upgradeable half via `RageGainMultiplier`/`RageRetentionMultiplier`, base
1.0). `Player/PainIntoPower.cs` (damage taken while melee-charging or axe-aiming banks
`PainIntoPowerPercent` — base 0 = locked — of it; consumed once per melee activation /
throw and added **flat** to every target of that one attack). DevConsole shows a live
`Rage 62/100 | Pain +8` readout for the Berserker. 3 new StatTypes.

- [ ] **RageSO asset** (menu `Scriptable Objects/RageSO`): defaults authored in-code (max 100,
      +1 rage/dmg dealt, +4 rage/dmg taken, 3 s grace then −10/s; at full: +50% dmg, +20% move,
      −30% taken).
- [ ] **Player.prefab**: add `RageBuff` (assign the RageSO) and `PainIntoPower` to the root,
      both **disabled**; add both to the berserker slot's `classComponents` on
      `PlayerClassController`.

**Manual verification (Stage D)**
- [ ] Berserker: rage climbs on hits dealt AND (faster) on hits taken; decays after ~3 s idle.
- [ ] At high rage: bigger damage numbers, visibly faster run, smaller incoming damage numbers.
- [ ] Hold a charged swing / axe wind-up, tank a goblin hit, release → that attack's numbers jump
      (needs a `pain_1` node or a temporary PainIntoPowerPercent base > 0 to see it pre-Stage-E).
- [ ] Swordsman: no rage readout in the dev console, no behaviour change.

**Stage E (berserker skill tree + rage bar + telemetry) — done in C#**:
`Config/SkillTreeBerserker.csv` — 32 rows carried over from the gold tree (all melee/crit/charge/
knockback/vamp/impulse/lightning/conduit/parry/solid/economy/mount lines work on the axe unchanged,
descriptions re-worded sword→axe) with the 17 bow rows dropped (incl. horse_archery), prereqs
scrubbed (`sword_dmg`/`sprint` now link to `axe_unlock`), plus 9 new nodes in the old bow footprint:
`axe_unlock` (Throwing Axe, 60g), `axe_dmg` (Heavier Head ×7), `axe_pierce` (Skull Splitter ×5),
`axe_width` (Wide Arc ×4), `axe_knock` (Crushing Throw ×4), `axe_charge` (Wound Up ×3, +1 level
+25%/level), `pain_1` (Pain into Power ×4: 50/75/100/125%), `rage_fury` (Boiling Blood ×5,
+15%/lvl gain), `rage_retain` (Slow Burn ×4, +20%/lvl retention). `AxeThrowUnlocked` base flipped
back to **0** (locked until `axe_unlock`). `UI/RageBarUI.cs` (polls `RageFraction`, drives a Filled
Image + optional TMP label; hides itself when the class has no enabled RageBuff). `RunTelemetry`
now also accumulates thrown-axe hits into damage-dealt (bow damage was never telemetered; the axe
is included deliberately for face-tank balance reading).

- [ ] **Berserker SkillTreeSO asset** (menu `Scriptable Objects/SkillTreeSO`), csv =
      `Config/SkillTreeBerserker.csv`, hasHeaderRow on; **copy the gold tree SO's `icons` list**
      so the carried-over nodes' icon names resolve (the 9 new nodes ship without icons — add via
      the Skill Tree Editor's drag-and-drop later). Sanity-check in **Bladehold > Skill Tree
      Editor** (parse errors surface on save/reload).
- [ ] Assign the new tree SO on the **berserker `ClassDefinitionSO.skillTree`** (swordsman stays
      null = default gold tree).
- [ ] **HUD rage bar**: a Filled Image (+ frame, + optional TMP number) under the HUD canvas with
      `RageBarUI`; it hides itself for the Swordsman automatically.

**Manual verification (Stage E)**
- [ ] Berserker death screen shows the berserker tree (axe/pain/rage branch where the bow was);
      Swordsman still shows the gold tree. Node icons render on carried-over nodes.
- [ ] Fresh berserker: aiming does nothing until `axe_unlock` is bought; after buying, the full
      Stage C loop works and `axe_*` nodes visibly raise damage/pierce/width/knockback/charge.
- [ ] `pain_1`/`rage_fury`/`rage_retain` purchases visibly change the Stage D behaviours.
- [ ] Rage bar fills/drains in sync with the DevConsole readout; absent for the Swordsman.
- [ ] Telemetry damage-dealt rows include thrown-axe hits.

**Berserker follow-ups (projectile axe + Boomerang + class model swap) — done in C#**:
The throwing axe is now a **real projectile**: `Player/AxeProjectile.cs` (renamed from
`AxeProjectileVisual.cs` — no longer a cosmetic tracer, it *carries the damage*; a missing prefab
is now a Start error on `PlayerThrownAxe`). It flies at `ThrownAxeSO.projectileSpeed`
(deliberately slow, default 12 m/s) and every `FixedUpdate` **sphere casts from its last position
to its current one** (radius = `AxeThrowWidth`/2, so the existing "Wide Arc" nodes are the
axe-area skill — they widen the swept damage volume *and* scale the prop visually), damaging each
unique enemy once per leg via `PlayerThrownAxe.CreateHitDamage` (fresh crit roll per target;
charge + Pain into Power captured at release and carried by the axe) until the pierce budget runs
out or terrain lodges it; hits flow back through `PlayerThrownAxe.ReportHit`, so `RageBuff` and
telemetry `OnHit` listeners are unchanged. New **Boomerang** node (`axe_boomerang`, 120g, prereq
`axe_charge`, new `AxeBoomerangUnlocked` StatType, base 0 = locked): the axe turns around on
terrain/pierce-spend/max-range and homes back to the hand at `ThrownAxeSO.returnSpeedMultiplier`
× speed, damaging enemies on the return leg too (fresh target set + pierce budget; return ignores
terrain, despawns on catch). And classes now swap the **character model**:
`ClassDefinitionSO.characterModelPrefab` (null = keep authored, i.e. Swordsman) —
`PlayerClassController.Awake` re-binds each of the prefab's `SkinnedMeshRenderer`s onto the
existing rig **by bone name** (Synty Sidekicks share the skeleton) and disables the authored
renderers, so the Animator, animation events, weapon bones, and camera need no re-wiring.

- [ ] **Throwing-axe projectile prefab** (if not already made in Stage C): axe mesh + optional
      trail with `AxeProjectile`; assign on `PlayerThrownAxe.projectilePrefab` — now **required**.
      Inspector tunables (spin, linger, `catchRadius` 0.75 m, safety `maxLifetimeSeconds` 15)
      have sane defaults. New `ThrownAxeSO` fields (`projectileSpeed` 12, `returnSpeedMultiplier`
      1.25) pick up their in-code defaults on the existing asset — just eyeball them.
- [ ] **Berserker character model**: pick a Synty Sidekick character prefab (same skeleton as the
      player rig; only its `SkinnedMeshRenderer`s are used — bone-attached static props won't
      carry over) and assign it on the **berserker `ClassDefinitionSO.characterModelPrefab`**.
      Swordsman stays null (keeps the authored model).
- [ ] **Berserker SkillTreeSO**: reload the tree asset (or just re-save in **Bladehold > Skill
      Tree Editor**) so the new `axe_boomerang` row parses; optionally drag an icon onto it.

**Manual verification (follow-ups)**
- [ ] Throw: the axe visibly travels (slow enough to outrun briefly), damaging goblins *as it
      passes them* — damage numbers pop along the flight, not all at once on release.
- [ ] A goblin sprinting across the axe's path between physics ticks still gets hit (the
      last-to-current sweep) — hard to force, but no "walked through it unharmed" moments.
- [ ] Without Boomerang: the axe lodges in terrain / its last pierced target and despawns.
- [ ] Buy `axe_boomerang`: the axe turns around at walls/max range and flies back to the hand,
      hitting goblins on the return (an enemy straddling the turnaround point gets hit twice —
      once per leg). It despawns in the hand; no orphaned axes after 15 s even when sprinting away.
- [ ] Wide Arc purchases visibly fatten the projectile and let it clip goblins farther off-line.
- [ ] Rage still builds from axe hits; telemetry damage-dealt still includes them.
- [ ] Reincarnate into Berserker: the character model is the assigned Sidekick, animating
      normally (walk/sprint/swing/aim all play; no T-pose, no floating meshes); weapons still sit
      in the hand. Back to Swordsman: original model, no leftovers.
- [ ] Player death/ragdoll-free flows unaffected by the model swap (death anim plays on the
      swapped mesh).

## Mage class (staff + wand + elemental imbuement)

**Stage A (class shell + PlayerAttack refactor) — done in C#**:
`Player/PlayerAttack.cs` no longer hard-types `PlayerBow`/`PlayerThrownAxe` for aim suppression —
it now asks `PlayerClassController.ActiveAimWeapon` (the `IChargedAimWeapon` the class controller
already resolves in Awake), so the wand (and any future class's aim weapon) suppresses the melee
charge with zero further edits. A missing class controller (e.g. `SkillTreePreview.unity`)
degrades to "no suppression". Everything else about adding a class is data-driven — no other code.

- [ ] **Staff weapon prefab** (`1H_Staff`): duplicate the sword weapon object, swap the mesh for a
      staff, give it its own `DamageSO` (~sword-level damage — the Mage's squish is kit-side, not
      melee-nerf-side) + `DamageTriggerSO`; BladeSweep `Blade Base`/`Blade Tip` along the shaft,
      `readsPlayerStats` ON. Nest under the right-hand bone in `Player.prefab`, **inactive**.
      Re-assign the out-of-prefab refs (`DamageTrigger.playerAttack`, `SwordHitFeedback.animator`,
      `SwordChargeFeedback.playerAttack`) — they don't survive duplication (the 1H_Axe precedent).
- [ ] **Mage AnimatorOverrideController** based on `AC_Sidekick_Masculine.controller`: staff attack
      clip(s); **bake the `PlaySwordWoosh` + `OneHandedSwordAttack` animation events** on the clip
      import (missing events = silent no-hit swings).
- [ ] **ClassDefinitionSO asset** `mage` (menu `Scriptable Objects/ClassDefinitionSO`): id `mage`
      (never rename once shipped), displayName "Mage", description blurb, the override controller,
      a robed Synty Sidekick on `characterModelPrefab`, `chargeTimePerLevel` ~1.1, `skillTree` =
      the Mage tree SO (Stage E below).
- [ ] **Player.prefab**: third `PlayerClassController` slot — definition = mage SO, weaponObjects
      `[1H_Staff]`, meleeTrigger/hitFeedback = the staff's, classComponents `[PlayerWand,
      MageImbuement]` (both added below, both **disabled** on the prefab).
- [ ] **ClassSelectPanel**: third authored `ClassOption` (button + name/description labels +
      highlight) wired to the mage SO.

**Stage B (wand: hold-aim magic missile) — done in C#**:
`Player/WandSO.cs` (tunables incl. aim-camera block), `Player/PlayerWand.cs` (the `PlayerThrownAxe`
skeleton: aim/charge/cooldown/melee-suppression/animator params `IsAiming`+`BowFire`/model swap;
implements `IChargedAimWeapon` so the shared aim camera/crosshair/reload UI work via
`ActiveAimWeapon`; registers `WandUnlocked` **0 = locked**, `WandDamage` 12, `WandMaxChargeLevels`
3, `WandChargeDamageBonus` 0.5, `WandKnockback` 2; damage = shared crit stats ×
`AllDamageMultiplier` × per-target Ice Breaker, `type = elemental`, stamps `Damage.source`),
`Player/MagicMissileProjectile.cs` (SphereCast-swept real projectile, no pierce; collects
`ElementNode`s it flies past and keeps going; per-element child visuals via `SetElement`).
`RunTelemetry` accumulates wand hits into damage-dealt (the axe precedent; elemental
riders/zones/chains stay untelemetered like chain lightning).

- [ ] **WandSO asset** (menu `Scriptable Objects/WandSO`).
- [ ] **Magic-missile prefab**: glow mesh/trail + `MagicMissileProjectile`; author the per-element
      child visuals (neutral bolt, fireball, spark, frost) all inactive; optional impact VFX.
- [ ] **`PlayerWand` on the player root, disabled**: assign WandSO + missile prefab + castOrigin
      (wand tip / chest) + hitLayers (the bow's mask); optional in-hand wand model + staff model
      for the aim swap; add to the mage slot's classComponents (it must be the slot's first
      `IChargedAimWeapon`).

**Stages C+D (imbuement core + element effects + runestones) — done in C#**:
`Player/ElementType.cs` (Fire/Lightning/Ice — append-only), `Player/MageImbuementSO.cs`,
`Player/MageImbuement.cs` — the one shared buff (ImpulseBuff skeleton + element identity):
timed + charge-stacked, **pickup RESETS the timer** (never adds); same element = +1 charge
(capped), different = replace at 1 charge; runestone = replace with `MageRunestoneCharges`
(base 2) charges, same-element runestone = timer-only; expiry clears all. Listens to the staff
trigger's + wand's `OnHit`: a flat elemental damage rider per charge (+ Searing Focus on fire),
then per element — **Fire** explosion (direct OverlapSphere at the hit point once Combustion is
owned; skips the direct target) + `Player/FlameZone.cs` burning ground (a player-owned
`LightningStormZone` clone with an enemy-layer mask, one zone per 2 s, NavMesh-snapped) once
Scorched Earth is owned; **Lightning** arcs via the existing `ChainLightning.ForceChain` (new
`excludeTarget` param; the Mage tree raises the shared `ChainLightning*` stats — a Mage never
activates the orb buff, so the two paths can't double-fire); **Ice** applies `SlowStatus`
(Ice Breaker pays off through the staff's existing per-target check and the wand's mirror of it).
`Economy/ElementNode.cs` (4th pickup sibling; a failed grant does NOT consume it, so non-Mages
leave nodes lying; 60 s lifetime; `TryCollectRemote` for wand flybys),
`Waves/ElementNodeSpawner.cs` (ChestSpawner idiom, self-disables for non-Mage runs),
`Waves/Runestone.cs` (IDamageable arena furniture; only player-`source` hits activate it —
`DamageTrigger.BuildDamage` (readsPlayerStats branch) and the wand now stamp `Damage.source`, so
enemy melee/Storm-Witch splash can never flip the element; per-stone 1 s cooldown),
`UI/MageElementUI.cs` (RageBarUI pattern: element icon/tint, charge count, time fill; self-hides),
DevConsole `DrawImbuementReadout()` (`Imbue: Fire x3 (8.2s)`).

- [ ] **MageImbuementSO asset** (menu `Scriptable Objects/MageImbuementSO`).
- [ ] **`MageImbuement` on the player root, disabled**: assign the SO, the **staff's**
      DamageTrigger (explicit — the VampiricBlade rule), the wand, enemyLayers (exclude player /
      gates / runestones / environment), per-element styles (aura child objects + activation MMFs
      + HUD icon sprites + tints), deactivation MMF, explosion VFX prefab, FlameZone prefab; add
      to the mage slot's classComponents.
- [ ] **3 ElementNode prefabs** (fire/lightning/ice): tinted orb mesh + trigger collider +
      `ElementNode` (set the element!) + DamageNumbersPro popup + pickup MMF.
- [ ] **FlameZone prefab**: looping ground-fire VFX + `FlameZone`; optional per-tick burn VFX/SFX.
- [ ] **Explosion VFX prefab** (cosmetic; the damage is code-side).
- [ ] **`ElementNodeSpawner` scene object** near the arena centre (spawnRadius ~12, min 2 / max 4
      per wave) — safe to leave in the scene for all classes, it self-disables.
- [ ] **3 Runestone prefabs placed near the gate** in `Bladehold Test Scene`: rock mesh + **solid**
      collider + `Runestone` (set the element) + activate/fizzle MMFs + per-element glow. Layer
      check: runestones must be inside the staff's + wand's `hitLayers` but **outside**
      `MageImbuement.enemyLayers` and `ChainLightning.enemyLayers` (chains/explosions/zones must
      never flip elements — switching is a deliberate, aimed act).
- [ ] **ChestLootTableSO**: add the 3 node prefabs as bonus items at low weight (~0.5) — a
      non-Mage chest roll leaves an inert node that expires (bounded waste, like a full-HP
      HealthPack).
- [ ] **HUD `MageElementUI` widget** under the HUD canvas: element icon Image + Filled time Image
      + optional "x3" TMP label, an `activeGroup` child for the active-only contents; hides itself
      for other classes.

**Stage E (Mage skill tree) — done in C#**:
`Config/SkillTreeMage.csv` — carried melee/crit/charge/knockback/vamp/plunder/sprint/impulse/
alldmg/medpack/horse lines reworded sword→staff, with the Berserker's axe/rage branch and the
gold tree's `solid`/`parry`/`counter` (glass cannon — no defensive lines) and orb-lightning family
dropped; 19 new nodes: `wand_unlock` (60g) → `wand_dmg`/`wand_charge`; fire line `fire_dmg` →
`fire_explode` (Combustion) → `fire_radius`/`fire_zone` (Scorched Earth) → `fire_zone_up`;
lightning line `light_unlock` (grants ChainLightningBounces+DamagePercent) →
`light_bounce`/`light_dmg`/`light_crit`; ice line `ice_deep` → `ice_dur` → `ice_break`; imbuement
QoL `imbue_dur`/`imbue_power`/`imbue_max` → `rune_charges` (runestones 2→3→4 charges) in the old
lightning-family footprint (x 29–30). New StatTypes: 5 wand + 11 Mage (see `Stats/StatType.cs`),
with `StatDisplay` rows.

- [ ] **Mage SkillTreeSO asset** (menu `Scriptable Objects/SkillTreeSO`), csv =
      `Config/SkillTreeMage.csv`, hasHeaderRow on; **copy the gold tree SO's `icons` list** so
      carried-over icon names resolve (the 19 new nodes ship without icons). Sanity-check in
      **Bladehold > Skill Tree Editor**; assign on the mage `ClassDefinitionSO.skillTree`.

**Manual verification (Mage)**
- [ ] Swordsman + Berserker regress-check first: bow aim and axe aim still suppress the melee
      charge (the PlayerAttack refactor's risk); Parry/Counterstrike still work (the
      `Damage.source` stamp is additive); `SkillTreePreview` scene throws no NPEs.
- [ ] DevConsole → Switch to Mage & Reload: robed model, staff swings, melee nodes scale it;
      telemetry `run_start` shows `class=mage`.
- [ ] Fresh Mage: aiming does nothing until `wand_unlock`; after buying — aim shoulders the camera
      in, crosshair + cooldown radial work, missiles fly and damage; charge scales damage; no
      mounted casting.
- [ ] Element nodes scatter on wave start (Mage only); walk-over imbues (aura + HUD + DevConsole
      readout); same element = +1 charge & timer reset; different element = swap at 1 charge;
      expiry clears everything at once; imbued staff and wand hits pop a second elemental damage
      number scaling with charges.
- [ ] Fire: rider only until `fire_explode`; then explosions at hit points (neighbours damaged,
      direct target not double-dipped); `fire_zone` adds burning ground, max one per 2 s, ticking
      enemies but never the player/gate/chests. Wand + fire reads as a fireball.
- [ ] Lightning: inert until `light_unlock`; then hits arc (never back into the struck enemy);
      a Lightning Orb pickup does NOT start the old orb buff for the Mage (no double chains).
- [ ] Ice: hits visibly slow (agent + animator); `ice_break` (Shatter) raises staff AND wand
      damage vs chilled targets; `ice_dur` lingers the slow.
- [ ] Runestones: blast from range = element swap with 2 charges (3/4 with `rune_charges`);
      same-element re-blast = timer refresh only; goblin attacks / Storm Witch storms hitting the
      stone never flip the element; non-Mage blast = fizzle feedback only.
- [ ] Wand missile flying over a ground node collects it mid-flight without stopping.
- [ ] Chest bonus rolls can drop element nodes; a non-Mage walking over one leaves it lying (it
      expires after ~60 s).
- [ ] Reincarnate wipe restores every gate (wand locked, fire/lightning lines inert).

**Mage follow-ups (recorded, not scheduled)**
- [ ] Extract a shared pickup base class — `ElementNode` is the fourth sibling of
      Coin/ImpulseOrb/LightningOrb; a fifth pickup (or a second remote-collect consumer) is the cue.
- [ ] Optional: `LightningOrb` grants a Lightning imbuement charge to a Mage instead of a dead
      no-op consume.
