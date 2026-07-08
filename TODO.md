# TODO

## Enemy Zoo (config/test scene) — Unity Editor wiring

The C# is done: `Bladehold Scripts/Debug/EnemyZoo.cs` (guarded to `UNITY_EDITOR || DEVELOPMENT_BUILD`,
so it can't ship) spawns one of every `EnemyRosterSO` type in a labelled gallery grid, applying the
same CSV overrides the waves use via `WaveSpawner.ApplyDefinition` (made `public static` for this).
An IMGUI panel (same wiring-free idiom as `DevConsole`, drawn top-right) toggles **Battle Mode**
(freeze the gallery for inspection ↔ enable each enemy's `AIMovement` + `*Attack` components so they
chase/fight the player), **Respawn Gallery**, and a type picker that spawns a batch of one type on
demand (with **Clear Spawns**). World-space name/health labels are drawn per enemy via IMGUI. This
scene is a config/test tool only — **do not add it to Build Profiles**.

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

## Instant gold + gold bags + Health Pack powerups — Unity Editor wiring

The C# is done: `CoinDropper` now grants each kill's gold **instantly** to the Wallet/GameStats
(optional `goldPopup` DamageNumber shows it at the corpse) and only rarely (`goldBagChance`, default
5%) drops a pickup — a **gold bag** worth `goldBagMultiplier`× (default 5×) the enemy's rolled gold,
spawned scaled up by `goldBagScale`; the old `coinPrefab` field was renamed to `goldBagPrefab` via
`FormerlySerializedAs`, so existing prefab references carry over and the plain coin doubles as the
bag until a distinct prefab exists. A new generic rare-drop system covers powerups:
`Enemies/PowerupDropper.cs` (on every enemy) rolls the shared `Enemies/PowerupDropSO` table on
death; `Economy/HealthPack.cs` is the first powerup — heals `StatType.HealthPackHealPercent` of max
HP (base 10%, registered in `Player.Start`; not consumed at full health), raised by the new
6-node **Field Medic** family in `Config/SkillTree.csv` (15→40%). The bow's Pickup Arrows also
collect Health Packs (`PlayerBow.CollectPickupsAlongPath`).

- [x] **HealthPack prefab** (`Assets/Bladehold/Bladehold Prefabs/HealthPack.prefab`): a small
      medkit/cross visual + a trigger SphereCollider + the `HealthPack` component; assign a
      DamageNumbersPro popup (a green-tinted variant of the coin pickup popup reads as healing)
      and an optional `MMF_Player` pickup feedback.
- [x] **PowerupDropSO asset** (Create > Scriptable Objects > PowerupDropSO, e.g. at
      `Assets/Bladehold/Bladehold Scripts/Enemies/PowerupDropSO.asset`): one entry — the
      HealthPack prefab at chance **0.03**.
- [x] Add **`PowerupDropper`** to every enemy prefab mapped in `WaveSpawner`'s roster list
      (Goblin, Goblin Brute, Bomber, Storm Witch, Troll) and assign the `PowerupDropSO` asset
      (`health` auto-wires via OnValidate).
- [x] On each enemy prefab's **CoinDropper**: optionally assign `goldPopup` (the coin pickup
      DamageNumber asset) so instant gold pops at the kill; tune `goldBagChance` /
      `goldBagMultiplier` / `goldBagScale` if 5% / 5× / 1.5 feel wrong.
- [x] Optional: a distinct **GoldBag prefab** (Coin component on a bag mesh) assigned to
      `goldBagPrefab` on each enemy, replacing the scaled-up coin stand-in.
- [x] Optional: assign an icon to the Field Medic nodes via **Bladehold > Skill Tree Editor**
      (the `icon` cells are blank for now).

## Manual verification (instant gold + powerups)

- [ ] Kill a goblin — the gold counter rises immediately with no coin left on the ground (and the
      popup shows at the corpse if wired).
- [ ] Grind ~20 kills — roughly one drops a visibly larger coin (the gold bag); collecting it
      grants ~5× that enemy's gold on top of the instant grant.
- [ ] Buy a Plunder node — both the instant grant and a gold bag's value scale up.
- [ ] Golden Goblins still drop their bonus coin; Grave Robber on death now only collects
      whatever bags/bonus coins are actually on the ground.
- [ ] Take damage, then grind kills until a Health Pack drops (~3%) — walking over it heals 10%
      of max HP with a popup; at full HP the pack is *not* consumed and stays until its 60s
      lifetime expires.
- [ ] Buy Field Medic tiers — packs heal 15/20/…% as described.
- [ ] With Retriever (Pickup Arrows), an arrow flying past a gold bag collects it, and past a
      Health Pack heals (only while hurt).

## Settings menu tabs + two-column rebind list — Unity Editor wiring

The C# is done: the settings panel is now split into a **General** tab (audio, sensitivity, max
ragdolls, invert, FoV) and a **Controls** tab (the rebind list), switched by two tab buttons under
the Back button (`SettingsPanelView` owns the switching + selected-tab tint, always reopening on
General). The rebind list is now **one row per action with separate Keyboard/Mouse and Gamepad
columns** (column headers above the list): `SettingsPanelView.BuildRowSlots` pairs each action's
KBM and gamepad bindings by display label (classified by the binding's authored `<Gamepad>` path,
so a row keeps its column even after remapping to another device); a column with no binding (e.g.
gamepad "Move" is one stick binding while KBM has per-direction WASD parts) shows a disabled "—"
button, and the arrow-key alternates to WASD get their own "(Alt)" rows. `RebindButtonView` now
drives two binding buttons per row (one interactive rebind at a time). `SettingsMenuGenerator`
builds all of it on regeneration — including auto-rebuilding the outdated single-button
`RebindRow.prefab` — but the existing generated menu in the scene predates the tab hierarchy, so
regeneration is the way to go:

- [ ] In the gameplay scene (`Demo_01_Sidekick`), delete **`PauseMenuCanvas`** and **`GameMenu`**,
      then re-run **Bladehold > Generate Settings Menu** (it rebuilds
      `Assets/Bladehold/Bladehold Prefabs/UI/RebindRow.prefab` to the two-column layout on its own;
      the other Menu* control prefabs are reused as-is). Redo any styling done on the old canvas.

## Manual verification (settings tabs + rebind columns)

- [ ] Open Settings — the General tab is selected (highlighted) and shows only the sliders/toggles;
      Back / Reset Settings / Delete Save are visible on both tabs.
- [ ] Click Controls — the sliders disappear, the rebind list appears with "Keyboard / Mouse" and
      "Gamepad" column headers, and each action (Aim, Crouch, LockOn, …) is a single row with its
      KBM binding in the left column and its gamepad binding in the right.
- [ ] "Move" shows the gamepad stick in one row (KBM side "—", disabled) and per-direction
      WASD rows (gamepad side "—"); the arrow-key alternates appear as "(Alt)" rows.
- [ ] Rebind a key in each column — only the clicked column enters "Press any key..." and updates;
      the remap persists across a restart, and Reset Settings restores both columns' labels.
- [ ] Close and reopen Settings mid-run — it reopens on the General tab.

## Reset Settings button + progress-only Delete Save — Unity Editor wiring

The C# is done: **Delete Save** now wipes only progress — `SaveData.ResetProgress()` (gold, both
trees' purchases, Reincarnate points) — keeping every settings field, and resets
`RunState.StartingWave` before the scene reload. A new **Reset Settings** button restores all
settings (audio/controls/video/performance/button remaps) to their authored defaults via
`GameSettingsService.ResetToDefaults()` → `SaveData.ResetSettings()`, applied live with no reload
(sliders/toggles and rebind row labels refresh in place; the unused `ResetInputOverrides` was folded
into it). `ConfirmDialog.Show` now takes a `confirmLabel` so the shared dialog says "Delete" vs
"Reset". `SettingsMenuGenerator` builds and wires the new button on regeneration, but the existing
generated menu in the scene predates it — `SettingsPanelView.Start` will error until it's wired.

- [ ] **SettingsPanel** in the scene's `PauseMenuCanvas`: duplicate `Content/DeleteSaveButton`,
      rename to `ResetSettingsButton`, move it **above** DeleteSaveButton, set its label to
      "Reset Settings", clear any copied `onClick` entries, and assign it to `SettingsPanelView`'s
      new `resetSettingsButton` field. (Or delete `PauseMenuCanvas` + `GameMenu` and re-run
      **Bladehold > Generate Settings Menu**, then redo any styling.)

## Manual verification (Reset Settings + Delete Save)

- [ ] Change several settings (volume, sensitivity, FoV, invert, a button remap), then Reset
      Settings → confirm dialog says "Reset"; all sliders/toggles snap to defaults, the remapped
      binding's row shows its default key again, and gold/upgrades are untouched.
- [ ] Buy an upgrade, earn gold, reach a wave > 1, then Delete Save → confirm dialog says
      "Delete"; scene reloads at wave 1 with zero gold and empty trees, but every changed setting
      (and button remap) is still in effect after the reload.
- [ ] Quit and relaunch after each of the two actions — the kept half persists on disk (settings
      after a delete; progress after a reset).

## Stuck arrows + arrow impact feedback — Unity Editor wiring

The C# is done: `PlayerBow` now raises **`OnArrowImpact(ArrowImpact)`** once per physical arrow that
damaged a target (bounces and the Flaming Arrows bonus hit don't re-raise it) — a richer sibling of
`OnHit` carrying the flight direction, the exact collider struck, the `VulnerableSpot` flag, and the
charge level. Two new reactive listeners consume it (the `SwordHitFeedback` pattern — the bow stays
unaware of them): **`Player/StuckArrowSpawner.cs`** plants a **`Player/StuckArrow.cs`** prop at the
hit point, aligned to the flight direction with a random roll around the shaft, sunk in by a random
`minPenetration`..`maxPenetration` depth (+`penetrationPerChargeLevel` per charge level), parented
to the struck collider so it rides animation/ragdoll and dies with the corpse (plus a 20s lifetime
backstop); **`DamageSystem/BowHitFeedback.cs`** plays a hit sound and a blood burst oriented back
along the arrow's path, with distinct sound pools / blood prefabs for **critical** and
**vulnerable (headshot)** hits — vulnerable outranks crit, each falls back to normal when unassigned.

- [x] **StuckArrow prefab** (`Assets/Bladehold/Bladehold Prefabs/StuckArrow.prefab`): an arrow mesh
      (the Synty bow pack has one near `Wep_RecurveBow_01`) under an empty root with the
      `StuckArrow` component. Author the root so the **tip sits at the origin and the shaft points
      down +Z** (tip forward) — `Embed` sinks the origin along the flight direction, so the tip ends
      up `penetration` metres inside the surface. No colliders/rigidbody. Tune `lifetime` (20).
- [x] **Player prefab** (`Assets/Bladehold/Bladehold Prefabs/Player.prefab`): on the object holding
      `PlayerBow` (or a child), add:
  - [x] `StuckArrowSpawner` — assign `arrowPrefab`; `bow` auto-wires via `OnValidate`. Tune
        `minPenetration` (0.15) / `maxPenetration` (0.35) / `penetrationPerChargeLevel` (0.05).
  - [x] `BowHitFeedback` — `bow`/`audioSource` auto-wire; assign `hitSounds` (arrow thunk/flesh
        impact pool), `critHitSounds` (meatier variant), `vulnerableHitSounds` (headshot sting —
        outranks crit), and blood prefabs: `bloodParticlePrefab` (the sword's blood prefab reuses
        fine), optional `critBloodParticlePrefab` / `vulnerableBloodParticlePrefab` (bigger burst
        for headshots). Blood cone is spawned facing back along the arrow path, so a narrow-cone
        particle shape reads best.

## Manual verification (stuck arrows + impact feedback)

- [ ] Shoot a goblin in the body — an arrow prop appears exactly at the hit point, pointing the way
      the shot flew, sunk partway in; it follows the goblin as it runs and animates.
- [ ] Fire several arrows into one enemy — penetration depths visibly vary and fletching rolls
      differ (no two arrows identical).
- [ ] Full-draw shot — the arrow buries noticeably deeper than a snap shot.
- [ ] Each hit plays an impact sound and a blood burst spraying back toward the shooter; a crit
      sounds/looks different; a headshot (`VulnerableSpot`) sounds/looks different again, and wins
      over crit when both happen on one arrow.
- [ ] Multi Shot — every fanned arrow that lands sticks its own prop and plays its own feedback;
      a Bounce Shot arc does **not** stick a second arrow at the bounce target.
- [ ] Fling a stuck-full enemy with Impulse — arrows ride the ragdoll bones; kill it — arrows sink
      and vanish with the corpse; leave one stuck long enough — it despawns on its own at `lifetime`.

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

## Troll slam — hold still during the wind-up (Unity Editor wiring)

The C# is done: `TrollSlamAttack` now pauses the troll's `AIMovement` (new
`AIMovement.SetMovementPaused(bool)`) for the wind-up and landing, since the telegraph is locked to
the troll's position when the swing starts and it looked wrong for the troll to keep chasing/sliding
out from under it. `TrollSlamAttack` gained a `movement` field that auto-wires via `OnValidate`
(`GetComponent<AIMovement>()`), same as its other dependencies.

- [ ] Open the Troll prefab in the Editor once so `OnValidate` runs and the new `Movement` field on
      `TrollSlamAttack` gets serialized (should auto-populate from the existing `AIMovement` on the
      same prefab — just confirm it's not empty in the Inspector, then save).

## Manual verification (troll slam)

- [ ] Aggro a troll and let it start a slam — it should plant and hold its position for the entire
      wind-up/telegraph and the landing, not keep advancing toward the player.
- [ ] After the slam lands, the troll resumes chasing normally.
- [ ] Kill the troll mid-wind-up — no errors, no lingering paused state (the corpse doesn't move,
      obviously, but nothing throws).

## Game loop expansion — enemy variety, elemental orbs, gate defense (design plan, not yet built)

Design discussion, no code written yet. Core philosophy: the player should feel *more* powerful
over time, never less — no enemy ability should slow, stun, root, or otherwise directly hinder the
player (freezing/CC-ing *enemies* is fine, since that's a buff to the player). Difficulty scales via
enemy variety/count/toughness and map objectives, not player-facing friction.

- [ ] **Goblin variety** — new enemy archetypes alongside the current melee goblin, reusing
      `Health`/`AIMovement`/`AIAttack`/`EnemySO` per-archetype configs:
  - [ ] "Big boi" — tanky, high HP/damage, slow.
  - [ ] "Quick gob" — fast, low HP, likely the archetype that beelines gates (see below).
  - [ ] "Bomb chucka" — ranged/AOE attacker, needs a new ranged `AIAttack` variant (lob a projectile
        or telegraphed AOE rather than melee range-check).
  - [ ] Escalate difficulty by introducing progressively stronger/more varied mixes per wave/level,
        not by buffing any single stat into CC territory.

- [ ] **Elemental orb pickups** — world pickups (drop from goblins? spawn on the level?) that grant a
      temporary stacking elemental "charge" buff on walkover, visibly orbiting the player while
      active (akin to Wallet/Coin pickup pattern, but timed buffs instead of currency):
  - [ ] Fire charge — stacking `+25/50/75/100%` attack speed per charge (new `StatType`, e.g.
        `AttackSpeed`, modified via `PlayerStats` percent modifiers while charges are active).
  - [ ] Frost charge — `5/10/15/20%` chance per charge to freeze an enemy solid (a new
        status-effect component on the goblin, e.g. disable `NavMeshAgent`/`AIAttack` for a duration,
        listened to via `Health.OnDamaged` the way `KnockbackReceiver` already reacts), plus
        `+50/100/150/200%` damage per charge against already-frozen targets.
  - [ ] Lightning charge — `10/20/30/40%` chance per charge to proc a chain-lightning hit on
        successful attacks (likely a new `DamageTrigger`-style hitbox that jumps between nearby
        `IDamageable`s from the hit point).
  - [ ] Charges are temporary (decay on a timer) and stack up to a cap; skill-tree nodes (new rows in
        `SkillTree.csv`) unlock/improve the per-charge percentages, following the existing
        `stat;kind;amount` multi-effect convention.

- [x] **Gate defense objective** — castle gates as a second loss condition alongside player death.
      **C# done** — see the "Gate defense — Unity Editor wiring" section below for the remaining
      scene/prefab work:
  - [x] A `Gate` is just another `Health`/`IDamageable` object (`Waves/Gate.cs`); `AIAttack` now
        attacks the current target (gate or player) through the selector below.
  - [x] `AIMovement`/`AIAttack`/`TrollSlamAttack` consult an optional `AITargetSelector`
        (`Enemies/AITargetSelector.cs`): assigned/nearest gate by default, the player within engage
        range wins. No selector (or no gates in the scene) = player-only, exactly as before.
  - [x] Mini-waves on a fixed interval alternating gates: `Waves/GateAssaultSpawner.cs` (round-robin
        over alive gates; count scales with the main wave number, never the timing).
  - [x] Round ends (loss) when any gate's `Health.OnDied` fires — routed through
        `Gate.OnAnyGateDestroyed` into the same `DeathScreen`/`WaveSpawner` run-over path as player
        death (time freezes on gate loss since the player is still alive).
  - [x] Alternation stays predictable/learnable; difficulty levers are mini-wave count and gate HP.

## Failure reason banner — Unity Editor wiring

The C# is done: the new `UI/FailureBanner.cs` is a purely presentational full-screen banner (own
`CanvasGroup` + TMP label; fade in → hold → fade out on unscaled time, so it works through the
gate-loss time freeze), and `DeathScreen` gained an optional `failureBanner` reference plus two
per-condition message strings (`playerDiedReason` "The hero has fallen. All hope is lost." /
`gateFellReason` "The gate was destroyed. We were overrun."). When assigned, the run-over sequence
plays the banner to completion first and only then unlocks the cursor and fades the death screen
in; unassigned = the old immediate fade, unchanged.

- [ ] **Banner object**: under the same Canvas as the death screen (or its own overlay Canvas), add
      a "FailureBanner" GameObject **outside the death screen's `CanvasGroup` hierarchy** (group
      alphas multiply — parented under it, the banner would stay invisible). Give it a `CanvasGroup`,
      the `FailureBanner` component (`canvasGroup`/`messageText` auto-wire via `OnValidate`), a
      full-screen dark backing `Image` (e.g. black ~60% alpha) and a centred TMP text child (large,
      dramatic — this shows lines like "The gate was destroyed. We were overrun."). Tune
      `fadeInDuration` (1s) / `holdDuration` (2.5s) / `fadeOutDuration` (0.75s) on the component.
- [ ] **DeathScreen**: assign the new `failureBanner` field on the death screen's `DeathScreen`
      component; tweak the two reason strings in the inspector if the defaults don't read well.
- [ ] Make sure the banner renders **above** the gameplay HUD (sibling order under the canvas is
      fine). Banner-vs-death-screen ordering doesn't matter — they never overlap, the banner has
      fully faded out before the screen fades in.

## Manual verification (failure reason banner)

- [ ] Die to enemies — "The hero has fallen. All hope is lost." fades in, holds a beat, fades out,
      and only then does the death screen (skill tree) fade in; the cursor stays locked until the
      death screen appears.
- [ ] Let a gate fall — time freezes, "The gate was destroyed. We were overrun." plays through the
      freeze (unscaled time), then the death screen fades in as before.
- [ ] With `failureBanner` unassigned on `DeathScreen`, dying goes straight to the death screen fade
      (old behaviour).
- [ ] Restart from the death screen — the banner is invisible again on the new run and replays
      correctly on the next loss.

## Bow weapon + bow skill lines + Raw Power — Unity Editor wiring

The C# for the bow (hold right click to aim/charge, left click to fire hitscan arrows drawn with
line-renderer tracers, sword/bow model toggle, sword-swing suppression while aiming), its seven
skill lines (Multi Shot, Heavy Arrows, Bounce Shot, Impulse Arrow, Storm Arrow, Retriever/pickup,
Precision Shot), and the "Raw Power" +50%-all-damage family is done. See
`Assets/Bladehold/Bladehold Scripts/Player/PlayerBow.cs`, `Player/BowSO.cs`, `Player/BowTracer.cs`,
`Enemies/VulnerableSpot.cs`, the `AllDamageMultiplier` handling in `DamageSystem/DamageTrigger.cs` +
`Player/Player.cs`, `ChainLightning.TryChain`, the pickups' new `TryCollect`, and the new
`multishot_*`/`multidmg_*`/`bounce_*`/`precision_*`/`pickuparrow_1`/`imparrow_1`/`stormarrow_1`/
`alldmg_*` rows in `Config/SkillTree.csv` for the code side.

- [x] **Create SO asset instance**: a `BowSO` (menu `Scriptable Objects/BowSO`) — tune `baseDamage`,
      `maxRange`, `fireCooldownSeconds`, charge pacing (`chargeTimePerLevel`/`baseMaxChargeLevels`/
      `baseChargeDamageBonus`), `multishotSpreadDegrees`, `bounceRadius`, `pickupRadius`.

- [x] **BowTracer prefab**: a GameObject with a `LineRenderer` (own the looks: width curve, material,
      start/end colors — an additive/unlit material reads best) + the `BowTracer` component; tune
      `holdSeconds`/`fadeSeconds`.

- [x] **Player prefab** (`Assets/Bladehold/Bladehold Prefabs/Player.prefab`):
  - [x] Add a `PlayerBow` component on the player root; assign `config` (the `BowSO`), `tracerPrefab`,
        and `arrowOrigin` (an empty child at chest/bow height — defaults to the player root if left
        empty). `inputReader`/`stats`/`playerAnimator` auto-wire via `OnValidate`; `aimCamera`
        defaults to `Camera.main`; `impulseBuff`/`chainLightning` default to the player's own.
  - [x] Set `hitLayers`/`bounceLayers` to exclude the player's own layer (and, for bounce, the
        environment) if arrows ever clip the player or bounces fizzle on scenery.
  - [x] Assign `swordModel` (the `Wep_Sword_01` child) so it hides while aiming. Leave `bowModel`
        empty until a bow model is added — everything works without it, the player just aims
        empty-handed.
  - [x] Confirm `PlayerAttack` picked up the new `bow` field via `OnValidate` (skips sword
        hold-to-charge while aiming).
  - [x] Optional `drawFeedback`/`fireFeedback` `MMF_Player`s (bow creak on aim, string snap on fire).

- [x] **Vulnerable spots (for Precision Shot)**: on each enemy prefab (`Goblin Enemy`, brute variant,
      Storm Witch, Troll), add a small trigger `SphereCollider` on the head bone (find it under the
      rig; roughly skull-sized) with the `VulnerableSpot` component. Without these, Precision Shot
      nodes simply never trigger.

- [ ] **Skill icons**: the new `multishot_*` and `multidmg_*` rows have blank icons (no bow-ish
      sprite is registered yet) — drop sprites on them in **Bladehold > Skill Tree Editor** when
      suitable art exists; the other new rows reuse already-registered icon names.

- [ ] **Balance pass**: tune the placeholder costs/positions of the new CSV rows (`multishot_*`,
      `multidmg_*`, `bounce_*`, `precision_*`, `pickuparrow_1`, `imparrow_1`, `stormarrow_1`, and the
      scattered `alldmg_*` "Raw Power" family) and the `BowSO` numbers to taste.

## Manual verification (bow + Raw Power)

- [ ] Hold right click — sword model hides (bow shows once assigned), left click fires a visible
      tracer that damages the first enemy it crosses; release right click — sword returns and left
      click swings as before.
- [ ] While aiming, mash left click — no sword swing animation plays, no sword damage lands, and no
      sword charge VFX starts.
- [ ] Hold aim without firing — arrow damage steps up per charge level (damage numbers grow); firing
      resets the draw while aim stays held.
- [ ] Buy Multi Shot tiers — extra tracers fan out left/right, each dealing 25% (then more with
      Heavy Arrows tiers) of the main arrow.
- [ ] Buy Bounce Shot — at 20% roughly one in five hits draws a second tracer to a nearby enemy;
      at 100% every hit bounces.
- [ ] Buy Precision Shot (after head colliders are added) — headshots deal the boosted damage,
      body shots don't.
- [ ] Buy Retriever — arrows fired over coins/orbs collect them from range.
- [ ] Buy Impulse Arrow, grab an Impulse Orb — arrow hits fling goblins like sword hits do; without
      the node (or with the buff expired) they don't.
- [ ] Buy Storm Arrow, grab a Lightning Orb — arrow hits chain to nearby enemies like sword hits.
- [ ] Buy a Raw Power node — sword swings, arrows, Death Nova, and chain lightning all hit ~50%
      harder (damage numbers confirm).

## Bow aim feel (camera zoom, crosshair, travelling tracer, animations) — Unity Editor wiring

The C# for the bow's aim presentation is done: **aim camera** (`Player/BowAimCamera.cs` — blends the
vendored `SampleCameraController`'s private `_cameraDistance`/`_cameraHorizontalOffset` boom fields
by cached reflection, the `PlayerMoveSpeedBinder` precedent, plus a `Camera.main` FOV narrow;
tunables on `BowSO`'s new "Aim camera" header), **crosshair** (`UI/BowCrosshairUI.cs` — CanvasGroup
fade while `PlayerBow.IsAiming`, reticle tightens per charge level), **travelling tracer**
(`Player/BowTracer.cs` — the streak's head now flies at `travelSpeed` m/s with a `tailLength` tail;
damage stays instant hitscan), and **bow animations** (`Player/PlayerBow.cs` now drives `IsAiming`
(Bool) / `BowFire` (Trigger) animator params, warning once and degrading gracefully until the
params exist).

- [x] **BowSO asset**: tune the new "Aim camera" fields — `aimCameraDistance` (2.75; the rig's
      authored boom is ~5), `aimCameraHorizontalOffset` (0.7 = over the right shoulder),
      `aimFieldOfView` (50), `aimBlendSeconds` (0.2).

- [x] **Player prefab** (`Assets/Bladehold/Bladehold Prefabs/Player.prefab`): add a `BowAimCamera`
      component on the player root next to `PlayerBow`; assign `config` (the `BowSO`).
      `bow`/`cameraController` auto-wire via `OnValidate` (the Synty camera rig is the nested
      CameraController child); `aimCamera` defaults to `Camera.main`.

- [x] **HUD canvas**: add a "Crosshair" object anchored to screen centre — an `Image` with a
      crosshair/dot sprite (~48px, works at alpha 0.8), a `CanvasGroup`, and the `BowCrosshairUI`
      component. `canvasGroup`/`reticle` auto-wire to the object itself; `bow` defaults to
      `Player.Instance`'s. Tune `fadeSeconds` (0.15), `fullChargeScale` (0.6), `tightenSeconds` (0.1).

- [x] **BowTracer prefab** (`Assets/Bladehold/Bladehold Prefabs/BowTracer.prefab`): tune the new
      `travelSpeed` (90 m/s) and `tailLength` (3 m). `travelSpeed 0` restores the old
      instant full-length streak.

- [x] **Animator** (`AC_Sidekick_Masculine.controller`): add parameters `IsAiming` (Bool) and
      `BowFire` (Trigger), then a **Bow** layer above Attack — Override blending, mask
      `Assets/Bladehold/Bladehold Data Objects/Upper Body Mask.mask` (the Attack layer's mask),
      default weight 1, empty default state (the Attack-layer pattern; an empty state on a masked
      layer contributes nothing):
  - [x] Empty → `Bow_Draw` (`A_POLY_BOW_Rcv_Stand_Aiming_ToDrawn_Neut`) when `IsAiming` == true
        (no exit time, ~0.15s duration).
  - [x] `Bow_Draw` → `Bow_Aim` (`A_POLY_BOW_Rcv_Stand_Aiming_Drawn_Neut`; tick **Loop Time** on the
        FBX's animation import settings) on exit time.
  - [x] `Bow_Aim` → `Bow_Fire` (`A_POLY_BOW_Rcv_Stand_Shoot_Reload_Neut` — release + nock the next
        arrow) on the `BowFire` trigger (no exit time, ~0.05s duration); `Bow_Fire` → `Bow_Aim` on
        exit time.
  - [x] Every bow state → Empty when `IsAiming` == false (~0.2s duration) so releasing aim melts
        back into the sword pose.
  - Clips live under `Assets/Third Party/Synty/AnimationBowCombat/Animations/Polygon/Neutral/
    Standing/` (`Aim/Rcv`, `Shoot/Rcv` — recurve variants matching the equipped `Wep_RecurveBow_01`;
    generic non-`Rcv` equivalents exist one folder up). Until both params exist `PlayerBow` logs a
    single warning and skips them.
    **Note (post-wiring):** the layer was wired with the *Humanoid character* clips (the generic
    non-`Rcv` ones) — correct: the `Rcv`/`Lng`/`Cmp` variants are Generic-rig **bow-prop** clips,
    consumed by `BowPropAnimator` below, not by this layer.
  - [x] ~~Optional polish later: bind the rigged bow's string/limbs to the draw~~ — done in C#, see
    the "Bow prop animation + aim look" section below.

## Manual verification (bow aim feel)

- [ ] Hold right click — camera eases in and over the right shoulder with a slight FOV zoom, the
      crosshair fades in, and the character raises and draws the bow (upper body) while the legs
      keep strafe-walking; release — camera, FOV, crosshair, and pose all return.
- [ ] Hold aim without firing — the crosshair tightens one step per charge level up to max draw.
- [ ] Fire — the release/reload animation plays and the tracer streak visibly flies from the bow to
      the target rather than appearing instantly; the damage number still pops the moment of the
      click (damage is hitscan; only the visual travels).
- [ ] With Multi Shot bought, fire — every fanned arrow draws its own travelling streak.
- [ ] Die while aiming — the camera framing and FOV snap back to normal, the crosshair fades out,
      and the corpse isn't stuck drawing a bow.

## Bow reload indicator — Unity Editor wiring

The C# is done: `PlayerBow` now exposes `CooldownFraction` (0 the instant a shot fires → 1 when the
bow can fire again) and `IsCoolingDown`, and the new `UI/BowReloadUI.cs` polls them (the
`BowCrosshairUI` pattern) — while aiming with the cooldown running it fades a `CanvasGroup` in and
drives a radial-filled `Image`'s `fillAmount` from empty to full, fading back out once ready.

- [ ] **HUD canvas**: add a "BowReload" object next to the Crosshair object (anchored to screen
      centre, offset slightly below the crosshair so it reads as part of the reticle) — an `Image`
      with a ring/circle sprite (~40px, e.g. Unity's built-in `Knob`), **Image Type = Filled,
      Fill Method = Radial 360** (Fill Origin: Top, clockwise reads best), a `CanvasGroup`, and the
      `BowReloadUI` component (`canvasGroup`/`fillImage` auto-wire via `OnValidate`; `bow` resolves
      through `Player.Instance`).
- [ ] Optional: a second faint full-circle `Image` behind the filled one as a backing track, so the
      empty portion of the ring is still legible. Author it on the same object's children — the
      `CanvasGroup` fades both together.

## Manual verification (bow reload indicator)

- [ ] Aim and fire — the reload circle appears at the crosshair and sweeps from empty to full over
      the cooldown (`BowSO.fireCooldownSeconds`, 0.35s), then fades out; the next shot restarts it.
- [ ] Hold aim without firing — no reload circle (only the crosshair).
- [ ] Fire and immediately release aim — the circle fades out with the crosshair rather than
      lingering on screen.
- [ ] Fire repeatedly (spam left click) — the circle restarts cleanly each shot with no flicker.

## Bow prop animation + aim look — Unity Editor wiring

The C# is done for **bow-prop animation** (`Player/BowPropAnimator.cs` — a Playables graph on the
rigged bow's own Animator; Synty ships no AnimatorController for the bow props, the prefab's
controller reference is dangling, so the graph replaces it: draw clip once on aim start → drawn
loop → release/reload clip on the new `PlayerBow.OnFired` event → back to the loop, crossfades
matching the character Bow layer's transition times) and **aim look** (`Player/BowAimLook.cs` —
LateUpdate spine/chest/upper-chest pitch toward the camera aim so the upper body points at the
crosshair; yaw already works because the Synty controller strafe-faces the camera while aiming;
blends over `BowSO.aimBlendSeconds`, clamped by the new `BowSO.aimLookMaxPitchDegrees`).

**Also fixed headlessly in `AC_Sidekick_Masculine.controller`** (precise YAML edits, no Editor work
needed, but reopen/refocus Unity so it reimports):
- The Bow layer's entry was an **Any State → Draw** transition on `IsAiming` — Any State
  re-evaluates every frame, so with `IsAiming` still true the `Aim` state kept getting yanked back
  into `Draw` (the reported bug). The same transition now hangs off the empty default state instead.
- The **Aim → Fire** transition had no conditions and no exit time (never taken — the fire
  animation could never play); it now has the intended `BowFire` trigger condition.

- [ ] **Bow prop** (the `Wep_RecurveBow_01` instance nested in
      `Assets/Bladehold/Bladehold Prefabs/Player.prefab`, already assigned as `PlayerBow.bowModel`):
      add a `BowPropAnimator` component next to its Animator (`bow`/`animator` auto-wire via
      `OnValidate`), then drag in the three **Generic-rig bow-prop clips** (the `Rcv` variants, NOT
      the Humanoid character clips one folder up), from
      `Assets/Third Party/Synty/AnimationBowCombat/Animations/Polygon/Neutral/Standing/`:
  - [ ] `drawClip` — `Aim/Rcv/A_POLY_BOW_Rcv_Stand_Aiming_ToDrawn_Neut`
  - [ ] `aimLoopClip` — `Aim/Rcv/A_POLY_BOW_Rcv_Stand_Aiming_Drawn_Neut` (looped in code — the
        FBX's Loop Time setting doesn't matter)
  - [ ] `fireClip` — `Shoot/Rcv/A_POLY_BOW_Rcv_Stand_Shoot_Reload_Neut`
  - If the bow stays rigid in Play mode, the clips aren't binding to the prop's bones: on
    `Models/Bows/Wep_RecurveBow_01.fbx` confirm Rig > Animation Type is **Generic** / Avatar
    Definition **Create From This Model**, and on the three `Rcv` animation FBXs set **Copy From
    Other Avatar** pointing at it.

- [ ] **Player prefab**: add a `BowAimLook` component on the player root next to `PlayerBow`;
      assign `config` (the `BowSO`). `bow`/`animator` auto-wire via `OnValidate`; `aimCamera`
      defaults to `Camera.main`.

- [ ] **BowSO asset**: tune the new `aimLookMaxPitchDegrees` (60) if the bend at extreme angles
      looks too strong.

- [ ] Optional: add `BowAimLook` to `PlayerDeath`'s disabled-components list so a corpse can't hold
      the spine bend (it also melts out on its own since `IsAiming` goes false on death).

## Manual verification (bow prop animation + aim look + animator fixes)

- [ ] Hold right click — the character draws once and **stays** in the drawn pose (no repeated
      draw-jerk: the old Any State loop is gone).
- [ ] While drawn, the bow model itself bends and the string pulls with the hands; on release the
      bow returns to rest.
- [ ] Fire — the character's release/reload animation now actually plays (Aim → Fire was previously
      unreachable), and the bow prop plays its own release/reload in sync; then both settle back
      into the drawn loop.
- [ ] Aim the camera up and down — the upper body pitches to keep the bow pointed at the crosshair
      (legs/locomotion unaffected); the bend eases in/out with the camera zoom on aim start/stop.
- [ ] Yaw while aiming — the whole character turns with the camera (Synty strafe mode, unchanged).
- [ ] Die while aiming — no lingering spine bend on the corpse, bow model hidden, no errors.

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

## Troll enemy — Unity Editor wiring

The C# for the Troll (telegraphed ground slam: reveals the damage area, waits a configurable
telegraph window, then deals massive damage + an impulse fling to everything in the area — player,
gates, and other enemies alike) is done, reusing the player's impulse/resistance system
(`ImpulseReceiver`). See `Assets/Bladehold/Bladehold Scripts/Enemies/TrollSlamAttack.cs`,
`Enemies/TrollSlamAttackSO.cs`, and the new `troll` row in `Config/Enemies.csv` for the code side.

- [x] **Create SO asset instances**:
  - [x] `TrollSlamAttackSO` — tune `triggerRange`/`slamRadius`/`forwardOffset`/`telegraphSeconds`
        (the dodge window)/`attackCooldown`/`damage`/`impulsePower`/`impulseForce`. The CSV `damage`
        column (40) overrides the SO's damage per spawn.
  - [x] Its own `AIMovementSO` (slow: the CSV speed column is 2.5) and `HealthSO`/`EnemySO` if not
        reusing existing assets with CSV overrides.

- [x] **Telegraph prefab**: a flat quad/decal (unlit, red ring/circle material, ~1m diameter at scale
      1 — the code scales x/z to the slam diameter) with **no collider**.

- [x] **Troll prefab**: build like the other enemies (Synty rig or a scaled brute as placeholder):
      `Health`, `Enemy`, `AIMovement`, `AIAnimation`, `TrollSlamAttack` (assign `attackData`, the
      telegraph prefab, optional `impactVfxPrefab`/`windupFeedback`/`slamFeedback`), `CoinDropper`,
      `CorpseDespawner`, `KnockbackReceiver`, `EnemyRagdoll` + `ImpulseReceiver` (CSV resistance 6 —
      only a heavily-stacked Impulse build should fling a troll), `GoldenGoblin`/`ImpulseGoblin` if
      trolls may roll those variants, and optionally an `AITargetSelector` so it sieges gates.
  - [x] **Animator**: add a `Slam` trigger + a long wind-up slam state on the troll's controller
        (the `TrollSlamAttack.slamTrigger` default). Time the clip so the impact lines up with
        `telegraphSeconds`.
  - [x] Register the prefab in `WaveSpawner`'s `enemyPrefabs` list under id `troll` (row already in
        `Config/Enemies.csv`: unlocks wave 8, 15% chance, 1 guaranteed/max concurrent, resistance 6).

- [ ] **Balance pass**: tune the `troll` CSV row and `TrollSlamAttackSO` numbers to taste.

## Manual verification (Troll)

- [ ] Reach wave 8 (or temporarily lower `unlockWave`) — a troll spawns, lumbers at the player.
- [ ] Get in range — the ground telegraph appears ahead of the troll, holds for `telegraphSeconds`,
      then the slam lands: standing in it hurts a lot; stepping out is safe.
- [ ] Let goblins wander into the area — they take the damage too and get ragdoll-flung by the
      impulse (knocked down if the horde ragdoll cap is full).
- [ ] Kill the troll mid-wind-up — no slam lands, the telegraph disappears.
- [ ] Kill the troll — coins drop, corpse pipeline runs, wave accounting stays correct.

## Gate defense — Unity Editor wiring

The C# for gates (second loss condition, enemy target-selection layer, fixed-interval mini-waves
alternating gates) is done. See `Assets/Bladehold/Bladehold Scripts/Waves/Gate.cs`,
`Waves/GateAssaultSpawner.cs`, `Enemies/AITargetSelector.cs`, and the selector integration in
`Enemies/AIMovement.cs`/`Enemies/AIAttack.cs`/`Enemies/TrollSlamAttack.cs`, plus the run-over
routing in `Waves/WaveSpawner.cs`/`UI/DeathScreen.cs` for the code side. Scenes without gates play
exactly as before — every gate feature is inert until a `Gate` exists.

- [x] **Gate object(s)** in the scene: a mesh (wall/door) with a solid `Collider`, a `Health`
      component (create a beefy `HealthSO`, e.g. 500+), and the `Gate` component (`attackPoint`
      optional — an empty child at the doors; defaults to the gate's own transform). Place it on/next
      to the baked NavMesh so enemies can path to it. Optionally add `DisableCollidersOnDeath` and a
      `HealthBarUI` so gate damage is readable.
- [x] **Enemy prefabs**: add an `AITargetSelector` to each enemy that should siege gates (goblin,
      brute, troll…); tune `playerEngageRange` (distance at which they drop the gate and turn on the
      player). `AIMovement`/`AIAttack` pick it up via `OnValidate`. Prefabs without the selector keep
      hunting only the player.
- [x] **GateAssaultSpawner** (scene object, only for levels with gates): assign `enemyPrefab` (e.g.
      the goblin — a fast "quick gob" variant fits the design), `assaultInterval` (~30s), `baseCount`/
      `countAddedPerWave`, and `spawnPoints` near/behind the gates (falls back to a radius around
      itself). The prefab needs an `AITargetSelector` to actually beeline its gate. Mini-wave enemies
      are extra pressure — they do NOT count toward the main wave's kill total.
- [ ] **DeathScreen**: optionally assign the new `titleText` label (+ tweak `playerDiedTitle`/
      `gateFellTitle`) so gate losses read differently from deaths.
- [ ] **Balance pass**: gate HP, engage range, mini-wave size/interval, gate spacing (travel time
      between gates is the intended difficulty lever).

## Manual verification (gate defense)

- [ ] Scene with no gates: everything behaves exactly as before (enemies hunt the player; no
      warnings besides an inert `GateAssaultSpawner` if one was left in).
- [ ] Place one gate + selectors on goblins: distant goblins path to the gate and beat on it (gate
      health drops); walking near them pulls them onto the player; retreating past engage range
      sends them back to the gate.
- [ ] Mini-waves: every ~30s a group spawns and beelines the gate; with two gates, consecutive
      mini-waves alternate targets; group size grows with the wave number.
- [ ] Let a gate die: time freezes, the death screen shows the gate-fell title, spawning stops, and
      both restart buttons work (timescale restored, wave resumes correctly).
- [ ] Player death still works exactly as before with gates in the scene.

## Reincarnate system — Unity Editor wiring

The C# for the Reincarnate meta-progression system (Death Nova, Golden Goblin, Grave Robber) is
done. The following can only be done in the Unity Editor (asset creation, prefab/scene wiring,
art/audio) — Claude Code can't safely author these headlessly. See
`Assets/Bladehold/Bladehold Scripts/Reincarnate/`, `Player/DeathNova*.cs`, `Enemies/GoldenGoblin.cs`,
`Player/GoldOnDeathCollector.cs`, and `Config/Reincarnate.csv` for the code side.

- [x] **Create SO asset instances** (each already has a `[CreateAssetMenu]` entry):
  - [x] `DeathNovaSO` — tune `baseCharges` (leave 0), `baseCooldownSeconds`, `baseRevivePercent` (leave 0).
  - [x] A `DamageTriggerSO` + `DamageSO` pair for the nova hitbox (radius/damage/duration/maxHits,
        plus the new `knockbackForce` field on the `DamageTrigger` component itself, not the SO).
  - [x] A second `SkillTreeSO` instance pointed at `Assets/Bladehold/Config/Reincarnate.csv`
        (`hasHeaderRow` on, same as the existing gold-tree `SkillTreeSO.asset`).

- [x] **Player prefab** (`Assets/Bladehold/Bladehold Prefabs/Player.prefab`):
  - [x] Add a child GameObject `DeathNovaHitbox` with a `DamageTrigger` component, `readsPlayerStats`
        off, wired to the nova `DamageTriggerSO`/`DamageSO` from above.
  - [x] Add a `DeathNova` component on the player root; assign `health`, `novaHitbox` (the child
        above), `config` (the `DeathNovaSO`).
  - [x] Add a `GoldOnDeathCollector` component on the player root; assign `health` (wallet/stats can
        stay unassigned — they default to `Player.Instance`).

- [x] **Goblin prefab** (`Assets/Bladehold/Bladehold Prefabs/Goblin Enemy.prefab`):
  - [x] Add a `GoldenGoblin` component; assign the same `EnemySO`/`Coin` prefab `CoinDropper`
        already uses.
  - [x] Assign a gold-glowing `Material` to `goldenMaterial` and the goblin's body renderer(s) to
        `bodyRenderers`.
  - [x] Assign a gold-burst VFX prefab to `deathVfxPrefab` and an SFX clip to `deathSfx` (both
        optional — the gold bonus still applies without them).

- [x] **Scene**: add a `ReincarnateService` component (alongside where `SkillTreeService` lives),
      pointed at the Reincarnate `SkillTreeSO` from above.

- [x] **Death-screen UI**:
  - [x] Duplicate the existing `SkillTreeView` + `SkillNodeView` hierarchy for the Reincarnate tree;
        assign the new view's `serviceBehaviour` to the `ReincarnateService`.
  - [x] Set `costSuffix` to `" pts"` on the duplicated node-view prefab (blank/default for the gold
        tree's prefab).
  - [x] Wire `DeathScreen`'s new `reincarnateButton` (+ optional `reincarnatePreviewLabel`) to the
        new Reincarnate UI panel.

- [ ] **Balance pass**: tune the placeholder point costs in `Assets/Bladehold/Config/Reincarnate.csv`
      and the nova's cooldown/damage/radius/knockback values to taste.

## Manual verification (after the wiring above)

- [ ] Buy a Reincarnate node in Play mode; confirm the modifier applies (behavior or a debug view).
- [ ] Take lethal damage with Death Nova unlocked but revive not purchased → blast fires, player still dies normally.
- [ ] Buy the revive tier, repeat → player survives at the expected % HP, no death animation/screen.
- [ ] Let goblins spawn with Golden Goblin chance > 0 → visual swap, VFX/SFX on death, bonus coin.
- [ ] Die with gold on the ground and Grave Robber owned → wallet gets the right %, ground coins disappear.
- [ ] Click Reincarnate on the death screen → points banked, gold skill tree empty next run, wave back
      to 1, Reincarnate-tree upgrades still applying.

## Sword combat overhaul — Unity Editor wiring

The C# for blade-sweep hit detection, the range/cut-through skills, and hit/charge/swing feedback is
done. The following can only be done in the Unity Editor (asset creation, prefab/animator/clip wiring,
art/audio) — Claude Code can't safely author these headlessly. See
`Assets/Bladehold/Bladehold Scripts/DamageSystem/DamageTrigger.cs`, `DamageSystem/SwordHitFeedback.cs`,
`Player/SwordChargeFeedback.cs`, `Player/AnimationEvents.cs`, `Player/PlayerAttack.cs`, and
`Config/SkillTree.csv` for the code side.

- [x] **Animator controller** (`Assets/Third Party/Synty/AnimationBaseLocomotion/Animations/Sidekick/AC_Sidekick_Masculine.controller`
      — the one vendored-asset exception here; there's no code-only way to add a reachable state):
  - [x] Add a `Blocked` trigger parameter, a blocked/parry state, and transitions in/out of it (e.g.
        from any attack state, back to locomotion). Until this exists, `SwordHitFeedback.OnBlocked`
        calls `Animator.SetTrigger("Blocked")` on a parameter that doesn't do anything yet.

- [x] **Sword prefab** (`Wep_Sword_01`, nested under `Assets/Bladehold/Bladehold Prefabs/Player.prefab`):
  - [x] Add empty child transforms `BladeBase` (at the hilt/guard) and `BladeTip` (at the point).
  - [x] On the existing `DamageTrigger` component: set `Detection Mode` to `Blade Sweep`; assign
        `Blade Base`/`Blade Tip`; tune `Base Point Count` (~5) and `Hit Layers` if the sweep ever
        clips the player's own hurtbox.
  - [x] Add a `SwordHitFeedback` component: assign an `AudioSource`, the player rig's `Animator`, hit/crit
        `AudioClip[]`s and woosh `AudioClip[]` (sound effects to be provided), and blood/crit particle
        prefabs + damage-scaling tunables (`Damage For Max Particles`, `Min/Max Particles`,
        `Min/Max Speed Multiplier`).
  - [x] Add a `SwordChargeFeedback` component (can live on the sword or the player root): assign
        `PlayerAttack` and a `MMF_Player[]` — create one child `MMF_Player` per charge stage (start
        with 4, matching the range skill's 25/50/75/100% tiers) with an increasingly bigger spark
        particle + louder/more satisfying SFX per stage.

- [x] **Attack animation clip**: add a `PlaySwordWoosh` animation-event marker earlier in the swing
      (before the existing hit-frame event that calls `OneHandedSwordAttack`), and wire
      `AnimationEvents.swordHitFeedback` to the new `SwordHitFeedback` component.

- [x] **Damage numbers**: create an alternate DamageNumbersPro prefab variant for crits (bigger/colored
      text) and assign it to `DamageNumberSpawner.critPopupPrefab` on the player and/or goblins.

- [x] **Particle prefabs**: author a blood-particle prefab (and optional distinct crit variant) —
      any `ParticleSystem` works, `SwordHitFeedback` sets `startSpeedMultiplier` and calls `Emit`
      manually at runtime, so emission-over-time modules should be off/minimal.

- [ ] **Balance pass**: tune the placeholder costs in the new `Extended Blade` (`range_ext_*`) and
      `Like Butter` (`butter_*`) rows in `Assets/Bladehold/Config/SkillTree.csv`, and reposition their
      canvas `x`/`y` if you want them visually closer to related nodes (they're currently in two fresh
      columns to the right of the existing tree so nothing needed to move).

## Manual verification (sword combat overhaul)

- [ ] Swing at a single goblin — confirm it still dies as before (no regression from Sphere→BladeSweep
      on the sword).
- [ ] Fire the Death Nova (still Sphere mode) — confirm its blast radius/behavior is unchanged.
- [ ] Swing into 2+ goblins standing close together at the base cut-through cap (1) — confirm the first
      is hit/damaged and the second triggers the Blocked reaction with no damage.
- [ ] Buy Like Butter tiers, repeat — confirm the cap rises and more goblins get hit before a block.
- [ ] Buy Extended Blade tiers — confirm the blade visibly lengthens and reaches further goblins, and
      that hit detection still registers correctly at each tier.
- [ ] Hold an attack to charge — confirm charge stages play in order as the hold progresses, resetting
      cleanly if released and re-charged.
- [ ] Land several hits of varying damage (e.g. crit vs normal, charged vs not) — confirm cutting sound
      and blood particle count/speed visibly scale with damage and cap out on big hits; confirm crits
      are audibly/visually distinct and use the crit damage-number prefab.
- [ ] Confirm the woosh plays on every swing, charged or not.

## Skill icons + new skill lines (Vampiric/Solid/Sprinter/Amplified Knockback) — Unity Editor wiring

The C# for node icons (CSV `icon` column resolved through the `SkillTreeSO.icons` list), the
Skill Tree Editor window, and the four new skill lines is done. See
`Assets/Bladehold/Bladehold Scripts/Editor/SkillTreeCsvEditorWindow.cs`, `Player/VampiricBlade.cs`,
`Player/DamageBlocker.cs`, `Player/AttackCancelsSprint.cs`, `DamageSystem/Health.cs`
(`TryBlockDamage`/`Heal`), `DamageSystem/DamageTrigger.cs` (charge knockback), and the new rows in
`Config/SkillTree.csv` for the code side.

- [x] **SkillNode prefab** (`Assets/Bladehold/Bladehold Prefabs/SkillNode.prefab`): add a child
      `Image` for the icon and assign it to the new `icon` field on `SkillNodeView` (hidden
      automatically when a node has no icon).

- [x] **Player prefab** (`Assets/Bladehold/Bladehold Prefabs/Player.prefab`):
  - [x] Add a `VampiricBlade` component on the player root; assign `swordTrigger` to the sword's
        `DamageTrigger` explicitly (no auto-wire — the Death Nova hitbox is also a `DamageTrigger`;
        health/stats default to `Player.Instance`).
  - [x] Add a `DamageBlocker` component on the player root (next to `Health`); optionally assign a
        `blockFeedback` `MMF_Player` (shield flash/clank SFX) so blocks are readable.
  - [x] Add an `AttackCancelsSprint` component on the player root (auto-finds the `InputReader` and
        `SamplePlayerAnimationController` via `OnValidate`).

- [x] **Icons**: open **Bladehold > Skill Tree Editor**, pick each `SkillTreeSO`, and drag sprites
      onto the new rows (`sprint_*`, `ampknock_*`, `vamp_*`, `solid_*` currently have blank icons);
      dropping a sprite adds it to the tree's `icons` list and sets the node's icon name in one step.

- [ ] **Balance pass**: tune the placeholder costs on the new rows and reposition their `x`/`y` if
      wanted (Sprinter chains off `move_2`, Amplified Knockback off `knock_1`; Vampiric Blade and
      Solid are fresh root columns at x=10/x=12).

## Impulse skill line — Unity Editor wiring

The C# for the Impulse buff/skill line (Impulse Goblin variant, Impulse Orb pickup, Impulse Buff,
the sword's impulse-stamped hits, and the enemy-side ragdoll fling reaction) is done.
**`ImpulseReceiver` now also ragdolls every plain kill** (not just Impulse-flung ones) — on
`Health.OnDied` with no fling/knockdown already in progress, it disables the AI/animator and calls
`EnemyRagdoll.EnterRagdoll` with zero launch velocity (just a small random tumble spin) instead of
letting the Death animation play, subject to the same `EnemyRagdoll.MaxActive` cap (see the pause
menu section above for the new Max Ragdolls setting) — kills beyond the cap still just play the
Death animation as before. The following
can only be done in the Unity Editor (asset creation, prefab/scene/animator wiring, physics layers,
art/audio) — Claude Code can't safely author these headlessly. See
`Assets/Bladehold/Bladehold Scripts/Player/ImpulseSO.cs`, `Player/ImpulseBuff.cs`,
`Enemies/ImpulseGoblin.cs`, `Economy/ImpulseOrb.cs`, `DamageSystem/ImpulseConfigSO.cs`,
`DamageSystem/ImpulseReceiver.cs`, `DamageSystem/ImpulseHitFeedback.cs`, `DamageSystem/EnemyRagdoll.cs`,
`DamageSystem/RagdollConfigSO.cs`, and the `impulse_unlock`/`impdur_*`/`impchance_*`/`imppower_*` rows
already in `Config/SkillTree.csv` for the code side.

- [ ] **Create SO asset instances** (each already has a `[CreateAssetMenu]` entry):
  - [x] `ImpulseSO` — leave `baseOrbDurationSeconds`/`basePower` at 0 (locked until skill nodes are
        bought); tune `baseImpulseForce`/`forcePerPower`/`powerPerChargeLevel`/
        `forcePerExtraStackPercent`/`damagePerExtraStackPercent`.
  - [x] `ImpulseConfigSO` — tune `defaultResistance`, launch/landing/recovery timings. (The old
        `maxSimultaneousRagdolls` field is gone — it's the player-facing Max Ragdolls setting now,
        see the pause menu section above.)
  - [x] `RagdollConfigSO` — tune mass/damping/joint-limit values (defaults are reasonable starting
        points).

- [x] **Physics layer**: add a `Ragdoll` layer in **Edit > Project Settings > Tags and Layers** (must
      match `RagdollConfigSO.ragdollLayerName`), then in **Physics > Layer Collision Matrix** disable
      Ragdoll×Ragdoll and Ragdoll×(the enemy/character layer), keeping Ragdoll×Default enabled so
      flung bodies still land on the ground.

- [x] **Animator controller** (the same Synty controller already touched for the `Blocked` state):
  - [x] Add a `Knockdown` trigger parameter plus enter/exit states for the animation-only knockdown
        reaction (`ImpulseReceiver.knockdownTrigger`).
  - [x] Add a `GetUp` state (played directly by name, not a trigger — the animator is disabled
        mid-flight so a trigger would be lost) for standing up after a landed fling
        (`ImpulseReceiver.getUpStateName`).
  - [x] Confirm the existing `Cheer` trigger is reachable from any state — a recovering enemy
        re-fires it if the player died while it was airborne.

- [x] **ImpulseOrb prefab**: trigger `Collider` + `ImpulseOrb` component; assign `pickupPopup` (a
      `DamageNumber` prefab), `pickupFeedback` (optional `MMF_Player`), tune `lifetime`.

- [x] **Player prefab** (`Assets/Bladehold/Bladehold Prefabs/Player.prefab`):
  - [x] Add an `ImpulseBuff` component on the player root; assign `config` (the `ImpulseSO`);
        optional `activationFeedback`/`deactivationFeedback` `MMF_Player`s and an `auraVisual` child
        object.
  - [x] Add an `ImpulseHitFeedback` component; assign `damageTrigger` to the sword's `DamageTrigger`
        explicitly (same precedent as `VampiricBlade`), plus `burstPrefab` (`ParticleSystem`) and
        `pulseLightPrefab` (a `Light` at rest intensity 0).
  - [x] Optionally assign the sword `DamageTrigger`'s `impulseBuff` field explicitly (it auto-finds
        the player's `ImpulseBuff` via `GetComponentInChildren` if left blank).

- [x] **Goblin prefabs** (`Goblin Enemy.prefab` and the brute variant — Impulse can fling either):
  - [x] Add an `EnemyRagdoll` component; assign `animator` (auto-wires via `OnValidate`), `config`
        (the `RagdollConfigSO`).
  - [x] Add an `ImpulseReceiver` component; most references (`health`/`agent`/`ragdoll`/`animator`/
        `rootCollider`/`aiMovement`/`aiAnimation`/`aiAttack`) auto-wire via `OnValidate` — assign
        `config` (the `ImpulseConfigSO`); optional `landingVfxPrefab`/`landingSfx`.
  - [x] Add an `ImpulseGoblin` component; assign `health`, `orbPrefab` (the `ImpulseOrb` prefab),
        `bodyRenderers`, `impulseAuraMaterial`, optional `deathVfxPrefab`/`deathSfx`.
  - [x] Confirm the existing `KnockbackReceiver`'s `impulseReceiver` field picked up the new
        `ImpulseReceiver` via `OnValidate` (so the two reactions don't fight over the same hit).

- [ ] **Balance pass**: tune the placeholder costs in the `impulse_unlock`/`impdur_*`/`impchance_*`/
      `imppower_*` rows of `Config/SkillTree.csv` to taste. Their icons (`Warriorskill_05_nobg`,
      `Push_nobg`, `IncreaseStrength_2_nobg`/`_3_nobg`/`_4_nobg`) are already registered in the
      `SkillTreeSO`'s icon list from earlier skill lines, so no new icon drag-and-drop should be
      needed.

## Manual verification (Impulse)

- [ ] Buy `Impulse`, hit a goblin at or above its resistance while the buff is active — confirm it
      launches skyward, tumbles, lands, re-seats on the NavMesh, and stands back up (or joins the
      corpse pipeline if the hit was lethal).
- [ ] Hit a goblin exactly one resistance point below the fling threshold — confirm an
      animation-only knockdown instead (no ragdoll).
- [ ] Hit a goblin further below resistance — confirm only the normal `KnockbackReceiver` slide
      happens, no knockdown/fling.
- [ ] Buy `Impulse Power` tiers — confirm goblins that previously only knocked down now fling, and
      brutes eventually fling too.
- [ ] Pick up multiple Impulse Orbs back-to-back — confirm buff duration stacks and stack count
      increases (extra force/damage per stack).
- [ ] Let more enemies get flung than the Max Ragdolls setting at once — confirm the overflow
      degrades to knockdowns instead of flinging.
- [ ] Kill the player (or let the run end) while an enemy is airborne/recovering — confirm it
      doesn't get stuck mid-air/mid-recovery and still ends up a normal corpse.
- [ ] Kill a goblin with a plain (non-impulse) sword hit — confirm it now ragdolls and collapses
      physically instead of playing the canned Death animation.
- [ ] Set Max Ragdolls to 0 in Settings — confirm kills fall back to the normal Death animation with
      no ragdoll at all.
- [ ] Kill a wave's worth of goblins at once with Max Ragdolls set low (e.g. 3) — confirm only that
      many ragdoll simultaneously and the rest play the Death animation instead.
- [ ] Raise Max Ragdolls back up mid-run — confirm subsequent kills ragdoll again without a restart.

## Storm Witch enemy + Chain Lightning skill line — Unity Editor wiring

The C# for the Storm Witch enemy (ranged lightning-ball attack, storm-zone hazard), her Lightning
Orb drop/pickup, and the Chain Lightning buff/skill line is done. The following can only be done in
the Unity Editor (asset creation, prefab/scene wiring, art/audio) — Claude Code can't safely author
these headlessly. See `Assets/Bladehold/Bladehold Scripts/Enemies/LightningBallAttack*.cs`,
`Enemies/LightningStormAttack*.cs`, `Enemies/LightningOrbDropper.cs`, `Economy/LightningOrb.cs`,
`Player/ChainLightning*.cs`, and `Config/Enemies.csv`/`Config/SkillTree.csv` for the code side.

- [x] **Create SO asset instances** (each already has a `[CreateAssetMenu]` entry):
  - [x] `LightningBallAttackSO` — tune `attackRange`/`damage`/`ballSpeed`/`ballLifetime`/
        `windupToApex`/`attackCooldown`.
  - [x] `LightningStormAttackSO` — tune `castRange`/`castCooldown`/`stormRadius`/`stormDuration`/
        `strikeInterval`/`strikeDamage`.
  - [x] `ChainLightningSO` — leave `baseBounces`/`baseDamagePercent`/`baseOrbDurationSeconds` at 0
        (locked until skill nodes are bought); tune `chainRadius`/`damagePerExtraStackPercent`.

- [x] **LightningBall prefab**: kinematic `Rigidbody` + trigger `SphereCollider` + `LightningBall`
      component; optional impact VFX/SFX.

- [x] **LightningStormZone prefab**: just a `LightningStormZone` component (uses `OverlapSphere`, no
      collider needed) + optional strike VFX/SFX.

- [x] **LightningOrb prefab**: trigger `Collider` + `LightningOrb` component — mirrors the existing
      `ImpulseOrb` prefab (`DamageNumber` popup, pickup `MMF_Player`, lifetime).

- [x] **Storm Witch prefab**: build with `Health`, `AIMovement`, `AIAnimation`,
      `LightningBallAttack` (assign `firePoint`, `ballPrefab`, the `LightningBallAttackSO`),
      `LightningStormAttack` (assign `stormZonePrefab`, the `LightningStormAttackSO`),
      `LightningOrbDropper` (assign `orbPrefab`), plus `ImpulseReceiver`/`KnockbackReceiver` like
      other enemies.
  - [x] Register the prefab in `WaveSpawner`'s `enemyPrefabs` list under id `storm_witch` (row
        already in `Config/Enemies.csv`).

- [x] **Player prefab** (`Assets/Bladehold/Bladehold Prefabs/Player.prefab`): add a
      `ChainLightningBuff` component (assign the `ChainLightningSO`) and a `ChainLightning`
      component (assign the sword's `DamageTrigger` explicitly — same precedent as `VampiricBlade` —
      and set `enemyLayers`) on the player root.

- [ ] **Balance pass**: tune the placeholder costs/values in the new `Config/SkillTree.csv` rows
      (`lightning_unlock`, `lightdur_*`, `lightbounce_*`, `lightdmg_*`, `lightcrit_*`) and the
      `storm_witch` row in `Config/Enemies.csv` to taste.

## Manual verification (Storm Witch + Chain Lightning)

- [ ] Let a Storm Witch spawn (wave 6+) — confirm she approaches, fires lightning balls at range, and
      periodically casts a storm at the player's current position.
- [ ] Get hit by a lightning ball / stand in the storm — confirm damage applies, and the storm keeps
      striking on its interval while something stays inside it.
- [ ] Kill a Storm Witch — confirm she always drops a Lightning Orb.
- [ ] Pick up a Lightning Orb before buying `Chain Lightning` — confirm nothing happens (feature
      locked, base stats at 0).
- [ ] Buy `Chain Lightning`, pick up an orb, hit an enemy standing near others — confirm the hit
      chains to a nearby enemy for the expected damage.
- [ ] Buy bounce/damage/crit tiers — confirm chains reach more enemies, hit harder, and crit at the
      expected rate.
- [ ] Stack multiple orbs — confirm buff duration extends and bounce damage gets the stack bonus.

## Pause menu, settings, and Photo Mode — Unity Editor wiring

The C# for the pause menu, settings (audio/sensitivity/invert/button remapping), and Photo Mode
(free-fly camera, sun/post-processing tweaks, screenshot capture) is done. Esc is handled by a
code-built Input System action (`MenuInputActions`), not a hand-authored `.inputactions` asset or the
vendored Synty `Controls` asset, so there's no input-asset wiring needed. See
`Assets/Bladehold/Bladehold Scripts/Settings/`, `Player/InputSettingsBinder.cs`,
`Player/ScreenshotFlyCamera.cs`, and `UI/PauseMenuView.cs`, `UI/SettingsPanelView.cs`,
`UI/RebindButtonView.cs`, `UI/ConfirmDialog.cs`, `UI/ScreenshotModePanel.cs` for the code side.

**Run `Bladehold > Generate Settings Menu` first** (`Assets/Bladehold/Bladehold Scripts/Editor/SettingsMenuGenerator.cs`) —
it builds and wires the `GameMenu` scene object (`PauseMenuController`/`ScreenshotModeController`/
`GameSettingsService`), the whole `PauseMenuCanvas` hierarchy (main buttons, Settings panel with all
sliders/toggles/rebind list/Delete Save + confirmation dialog, Photo Mode panel with all its sliders),
and adds `InputSettingsBinder` to the Player instance in the scene — everything below is what it
can't do for you.

- [x] **Regenerate the menu** (Photo Mode feedback round): delete **`PauseMenuCanvas` and `GameMenu`**
      from the scene, then re-run `Bladehold > Generate Settings Menu`. This picks up: the layout fix
      (rows were 100px tall, pushing Take Photo off-screen), the Photo Mode panel now stretching full
      screen height with the sliders in a scroll view and Take Photo/Exit pinned at the bottom, a
      per-setting **reset button** (new `MenuIconButton` prefab + generated `ResetIcon.png`), a **Sun
      Yaw** slider + Sun Pitch narrowed to -10..90, `hideOnCapture` now actually wired (screenshots no
      longer include the UI), and the pause backdrop wired so it hides during Photo Mode (it was
      graying out the shot and would eat click-drag input). Camera look in Photo Mode is now
      click-and-drag (left mouse held) instead of always-on mouse delta.
- [x] **Regenerate the menu again** (always-ragdoll kills): the Settings panel now has a **Max
      Ragdolls** slider (0-50, whole numbers, default 12) alongside Sensitivity —
      `GameSettingsService.SetMaxRagdolls` applies it to the new `EnemyRagdoll.MaxActive` cap, which
      both the Impulse fling and the new always-ragdoll-on-kill reaction (see the Impulse section
      below) check before starting a ragdoll. If `PauseMenuCanvas`/`GameMenu` already exist from an
      earlier generation, delete and regenerate to pick up the new row (or add a `MenuSlider` instance
      by hand and wire it to `SettingsPanelView.maxRagdollsSlider`). `ImpulseConfigSO.maxSimultaneousRagdolls`
      is gone — it's a player-facing setting now instead of a designer-tuned asset value; if any
      `ImpulseConfigSO` asset shows the old field as "missing" in the inspector that's expected and
      harmless.
- [ ] **Reskin the shared control prefabs** it generated under `Assets/Bladehold/Bladehold Prefabs/UI/`
      (`MenuButton`, `MenuLabel`, `MenuSlider`, `MenuToggle`, `MenuIconButton` + its `ResetIcon.png`,
      `MenuValueInput`) to match the game's look — every button/label/slider/toggle/value-field in the
      generated menu is an instance of one of these, so restyling the prefabs restyles the whole menu
      at once. Then lay out/resize
      the panels (`PauseMenuCanvas` > `PauseMenuView` > `MainButtonsPanel`/`SettingsPanel`/
      `PhotoModePanelRoot`) to taste — the generator only gives them functional placeholder
      sizes/positions.
- [x] **Player prefab**: the generator added `InputSettingsBinder` to the Player *instance* in the open
      scene only (a prefab override) — select it and **Overrides > Apply All** onto
      `Assets/Bladehold/Bladehold Prefabs/Player.prefab` to make it permanent.
- [x] **Settings mixer routing**: the generator already assigned `GameSettingsService.mixer` to
      `MMSoundManagerAudioMixer.mixer` (exposed params `MasterVolume`/`MusicVolume`/`SfxVolume`), but
      **Music/SFX sliders only have an audible effect once sources are routed through its groups** —
      assign the Output Audio Mixer Group on `SwordHitFeedback`'s/`ImpulseHitFeedback`'s
      `AudioSource`s, `Coin`'s pickup `AudioSource`, and any MMF_Player "Sound" feedbacks, to the
      mixer's Sfx group (or Music, for anything music-like). Master volume works regardless via
      `AudioListener.volume`.
- [x] If the generator logged warnings (no Main Camera/AudioMixer/Player found, etc.), assign those
      `ScreenshotModeController`/`GameSettingsService`/`InputSettingsBinder` fields by hand.
- [x] **Balance pass**: tune default sensitivity/volume values and `ScreenshotFlyCamera`'s
      `moveSpeed`/`boostMultiplier`/`lookSensitivity` to taste.
- [x] **Regenerate the menu again** (FOV setting + bow aim FOV as a percentage): the Settings panel
      now has a **Field of View** slider (30-100, default 40 — matches the rig's authored FOV) below
      Invert Y — `GameSettingsService.SetFieldOfView` applies it via the new
      `BowAimCamera.SetRestingFieldOfView`, which the bow's aim-zoom blends away from and back to
      (`BowSO.aimFieldOfViewPercent`, default 1 = unchanged, replaces the old absolute
      `aimFieldOfView` override — re-tune it on `BowSO.asset` if the bow's aim should still
      zoom/widen the view). The **Sensitivity** slider's range also changed from 1-15 to 0-10 so the
      existing default of 5 sits at the middle, with room to go lower than before. If
      `PauseMenuCanvas`/`GameMenu` already exist from an earlier generation, delete and regenerate to
      pick up the new row and range (or add a `MenuSlider` instance by hand, wire it to
      `SettingsPanelView.fieldOfViewSlider`, and edit the existing Sensitivity slider's Min/Max to
      0/10).
- [x] **Regenerate the menu again** (typeable slider values): every Settings-panel slider row (Master/
      Music/SFX Volume, Sensitivity, Max Ragdolls, Field of View) now also gets a small text field next
      to it — a new `MenuValueInput` prefab (a `TMP_InputField`) kept in sync with the slider by the new
      `UI/SliderValueField.cs`, so exact numbers can be typed in instead of only dragging. Volume shows
      2 decimals, Sensitivity 1, Max Ragdolls/Field of View whole numbers. Photo Mode's sliders are
      unchanged (still drag-only) — regenerate only touches the Settings panel. If
      `PauseMenuCanvas`/`GameMenu` already exist from an earlier generation, delete and regenerate to
      pick up the new fields (or add a `MenuValueInput` instance next to each existing slider by hand,
      add a `SliderValueField` to the row, and wire its `slider`/`inputField`).

## Manual verification (pause menu, settings, Photo Mode)

- [ ] Press Esc mid-run — game freezes (enemies/animations stop, character stops responding), cursor
      unlocks, pause menu appears; press Esc again (or click Resume) — everything resumes cleanly with
      no camera snap.
- [ ] Move the mouse while paused, then resume — confirm the camera doesn't jump from input that
      accumulated while frozen.
- [ ] Adjust Master/Music/SFX sliders — Master audibly changes volume immediately; Music/SFX do too
      once routed through the mixer (see wiring above); all three persist across a restart.
- [ ] Adjust sensitivity and invert X/Y — camera look responds immediately and correctly in both axes;
      settings persist across a restart. Sensitivity now ranges 0-10 (0 = no look) with 5 — the
      default — in the middle.
- [ ] Adjust the Field of View slider — the gameplay camera's zoom changes immediately (even while not
      aiming the bow) and persists across a restart. Draw the bow while at a non-default FOV — the aim
      zoom blends from and back to *that* FOV, not the old authored default.
- [ ] Each Settings slider shows a matching number in its text field, updating live as you drag; type an
      exact number into a field and press Enter (or click away) — the slider jumps to that value and the
      setting applies exactly as if it had been dragged. Type something out of range — it clamps to the
      slider's min/max; type garbage (letters) — the field reverts to the current value instead of
      accepting it.
- [ ] Click a rebind row, press a new key — the row updates to the new binding and the new key actually
      controls that action in gameplay; pressing Esc while "Press any key..." is showing cancels the
      rebind without closing the pause menu; the new binding survives a restart.
- [ ] Click Delete Save — a confirmation dialog appears; Cancel does nothing; Confirm wipes gold/
      upgrades/settings and reloads to a fresh save.
- [ ] Click Quit — the game/Editor Play session actually stops.
- [ ] From the pause menu, click Photo Mode — the pause dim/backdrop disappears, the camera detaches
      and flies with WASD/QE/Shift boost, and **click-and-drag** (hold left mouse on empty scene, not
      on the panel) rotates the look; dragging a slider does not rotate the camera.
- [ ] Photo Mode panel fills the right edge of the screen with the sliders scrolling and the
      **Take Photo**/Exit buttons always visible at the bottom; sliders open showing the scene's
      *actual* current values (e.g. Field of View matches the gameplay FOV).
- [ ] Sun Pitch/Sun Yaw give fine control over the light direction (pitch is -10..90 now, not a full
      0..360 sweep); each setting's ↺ reset button puts just that setting back to the value it had
      when Photo Mode was entered.
- [ ] Take Photo writes a PNG under `persistentDataPath/Screenshots/` with no UI visible in it; Exit
      (button or Esc) returns to the pause menu with the camera, sun, and post-processing back exactly
      as they were before entering.

## Manual verification (skill icons + new skill lines)

- [x] Nodes with an icon show it on the death screen; icon-less nodes look unchanged; console shows
      a line-numbered error for a typo'd icon name.
- [x] Edit a node and Save in the Skill Tree Editor → CSV file updates on disk and the tree renders
      the change on next death screen.
- [ ] Buy Vampiric Blade, hit goblins → health visibly refills by ~1% of damage dealt per tier
      (crit/charged hits heal more); no healing while dead.
- [ ] Buy Solid, take a goblin hit → first hit negated (no damage number/feedback), next hits land
      normally until the cooldown elapses; higher tiers shorten the window.
- [ ] Buy Sprinter tiers → sprint is visibly faster; pressing attack while sprinting drops the player
      out of sprint (works even with no Sprinter nodes owned).
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

## Cinemachine camera conversion — Unity Editor wiring

The C# is done: the camera is now Cinemachine-driven (`com.unity.cinemachine` 3.1.7, already
installed). New `Player/PlayerCameraPivot.cs` replaces the look-input/rotation half of the vendored
`SampleCameraController` — it accumulates yaw/pitch from the Synty `InputReader` mouse delta (same
raw-delta × sensitivity scale, so saved sensitivity values are unchanged), clamps pitch, sticks to
`SyntyPlayer_LookAt`, and owns the gameplay cursor lock. Positioning/framing/damping/collision move
to a `CinemachineCamera` with **Third Person Follow** tracking the pivot. `Player/BowAimCamera.cs`
was rewritten to blend the follow component's public `CameraDistance`/`ShoulderOffset.x` and the
vcam lens FOV (reflection gone); `Player/InputSettingsBinder.cs` now writes plain
`PlayerCameraPivot` properties for sensitivity/invert (its only remaining reflection is the
`InputReader._controls` rebinding access, and its old per-frame `_mouseDelta.x` invert flip is
gone). `PauseMenuController` docs/tooltip and `SettingsMenuGenerator` wiring updated. The vendored
`SampleCameraController` is **not** edited — it stays on the rig disabled as a passive facade,
because `SamplePlayerAnimationController` still calls its `GetCamera*()` getters, which only read
the serialized `_mainCamera` transform.

All wiring below is in `Player.prefab` (open in prefab mode; the gameplay scene is
`Demo_01_Sidekick.unity`). Current authored rig values to reproduce: distance **2.5**, height/
horizontal offset **0**, tilt offset **15°**, tilt bounds **±70°**, FOV **90**, lag 0.2 (which the
old `1/(lag/20)` math made effectively rigid — so damping 0 matches today's feel).

- [x] **Main camera** (`PF_SyntyCamera` → its `MainCamera` child): add a `CinemachineBrain`.
      Existing components (AudioListener, URP camera data, `MMCameraShaker`, `ScreenshotFlyCamera`)
      stay put.
- [x] **Disable (don't remove) the `SampleCameraController`** component on the `PF_SyntyCamera`
      root. Keep its `_syntyCharacter`/`_mainCamera` references assigned — the movement controller's
      camera-relative getters read through them. Disabled means its `Start`/`Update` (old cursor
      lock + boom driving) never run.
- [x] **CameraPivot**: new child GameObject of the Player root (sibling of `PF_SyntyCamera`), add
      `PlayerCameraPivot`. `inputReader` and `followTarget` (`SyntyPlayer_LookAt`) auto-wire via
      `OnValidate`. Defaults already match the authored rig (sensitivity 0.5, tilt bounds ±70,
      hideCursor on).
- [x] **GameplayCamera**: new child GameObject of the Player root, add `CinemachineCamera`:
  - [x] Tracking Target = the CameraPivot; Lens FOV = **90**.
  - [x] Position Control = **Third Person Follow**: Camera Distance **2.5**, Shoulder Offset
        **(0, 0, 0)**, Vertical Arm Length **0**, Camera Side **1** (right — the bow's aim shoulder
        offset is authored positive-right), Damping **(0, 0, 0)** to match the current rigid feel
        (raise to taste later).
  - [x] Rotation Control = none (the camera matches the pivot's rotation).
  - [x] Add a **CinemachineRecomposer** extension with Tilt = **15** (the old `_cameraTiltOffset`,
        which tilted the camera down without moving the boom).
  - [x] Recommended (new capability): on Third Person Follow, enable **Avoid Obstacles**, set the
        collision filter to the environment/ground layers (exclude Player, layer 6), Camera Radius
        ~0.15 — the old rig clipped through walls.
- [x] **BowAimCamera** (on the Player root): its old `cameraController`/`aimCamera` fields are gone;
      the new `aimCamera` field wants the GameplayCamera vcam (auto-found via
      `GetComponentInChildren<CinemachineCamera>` — verify it resolved).
- [x] **InputSettingsBinder** (on the Player root): assign the new `cameraPivot` field (auto-finds
      in children; the old `cameraController` field is gone).
- [x] **PauseMenuController** (`GameMenu` object in the scene): in `componentsToDisable`, replace
      the `SampleCameraController` entry with **both** the `PlayerCameraPivot` and the
      `CinemachineBrain` (keep `InputReader`). Disabling the brain is what lets Photo Mode's
      detached fly camera work. (Alternatively delete `PauseMenuCanvas` + `GameMenu` and re-run
      **Bladehold > Generate Settings Menu** — the generator now wires all three.)

## Manual verification (Cinemachine conversion)

- [ ] Look around, run, sprint — camera orbits with the same feel (rigid follow, ±70° pitch clamp,
      15° down-tilt) and camera-relative movement/strafing is unchanged (the Synty controller's
      getters still work through the disabled facade).
- [ ] Settings menu: sensitivity slider and invert X/Y toggles take effect immediately and persist
      across restarts (the saved values are the same scale as before).
- [ ] Aim the bow: framing blends over `aimBlendSeconds` to distance 2.75 / shoulder 0.7 / FOV 50
      and back on release; `BowAimLook`'s spine bend and `CombatFacing` still track correctly
      (both read `Camera.main`, which the brain drives).
- [ ] Die mid-aim: framing snaps back (BowAimCamera `OnDisable`), death screen frames normally and
      frees the cursor; restarting re-locks it (pivot `Start`).
- [ ] Pause (Esc): camera freezes completely, mouse movement while paused causes no snap on resume.
      Enter Photo Mode: fly camera moves freely (brain disabled); exit restores the gameplay camera
      cleanly.
- [ ] If Avoid Obstacles was enabled: back the camera into a wall — it slides in instead of
      clipping.
- [ ] Hit feedbacks that shake the camera (`MMCameraShaker` on the main camera): confirm shakes are
      still visible. The brain rewrites the camera transform after `LateUpdate`, so if shakes
      stopped showing, swap to Feel's Cinemachine shaker (add the `MM_CINEMACHINE3` scripting
      define and use `MMCinemachineCameraShaker`/Cinemachine Impulse on the vcam) — flag it and
      we'll wire that variant.
