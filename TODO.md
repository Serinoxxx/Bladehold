# TODO

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
the sword's impulse-stamped hits, and the enemy-side ragdoll fling reaction) is done. The following
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
  - [x] `ImpulseConfigSO` — tune `defaultResistance`, launch/landing/recovery timings,
        `maxSimultaneousRagdolls`.
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
- [ ] Let more enemies get flung than `ImpulseConfigSO.maxSimultaneousRagdolls` at once — confirm
      the overflow degrades to knockdowns instead of flinging.
- [ ] Kill the player (or let the run end) while an enemy is airborne/recovering — confirm it
      doesn't get stuck mid-air/mid-recovery and still ends up a normal corpse.

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

- [ ] **Reskin the shared control prefabs** it generated under `Assets/Bladehold/Bladehold Prefabs/UI/`
      (`MenuButton`, `MenuLabel`, `MenuSlider`, `MenuToggle`) to match the game's look — every button/
      label/slider/toggle in the generated menu is an instance of one of these, so restyling the four
      prefabs restyles the whole menu at once. Then lay out/resize the panels (`PauseMenuCanvas` >
      `PauseMenuView` > `MainButtonsPanel`/`SettingsPanel`/`PhotoModePanelRoot`) to taste — the
      generator only gives them functional placeholder sizes/positions.
- [ ] **Player prefab**: the generator added `InputSettingsBinder` to the Player *instance* in the open
      scene only (a prefab override) — select it and **Overrides > Apply All** onto
      `Assets/Bladehold/Bladehold Prefabs/Player.prefab` to make it permanent.
- [ ] **Settings mixer routing**: the generator already assigned `GameSettingsService.mixer` to
      `MMSoundManagerAudioMixer.mixer` (exposed params `MasterVolume`/`MusicVolume`/`SfxVolume`), but
      **Music/SFX sliders only have an audible effect once sources are routed through its groups** —
      assign the Output Audio Mixer Group on `SwordHitFeedback`'s/`ImpulseHitFeedback`'s
      `AudioSource`s, `Coin`'s pickup `AudioSource`, and any MMF_Player "Sound" feedbacks, to the
      mixer's Sfx group (or Music, for anything music-like). Master volume works regardless via
      `AudioListener.volume`.
- [ ] If the generator logged warnings (no Main Camera/AudioMixer/Player found, etc.), assign those
      `ScreenshotModeController`/`GameSettingsService`/`InputSettingsBinder` fields by hand.
- [ ] **Balance pass**: tune default sensitivity/volume values and `ScreenshotFlyCamera`'s
      `moveSpeed`/`boostMultiplier`/`lookSensitivity` to taste.

## Manual verification (pause menu, settings, Photo Mode)

- [ ] Press Esc mid-run — game freezes (enemies/animations stop, character stops responding), cursor
      unlocks, pause menu appears; press Esc again (or click Resume) — everything resumes cleanly with
      no camera snap.
- [ ] Move the mouse while paused, then resume — confirm the camera doesn't jump from input that
      accumulated while frozen.
- [ ] Adjust Master/Music/SFX sliders — Master audibly changes volume immediately; Music/SFX do too
      once routed through the mixer (see wiring above); all three persist across a restart.
- [ ] Adjust sensitivity and invert X/Y — camera look responds immediately and correctly in both axes;
      settings persist across a restart.
- [ ] Click a rebind row, press a new key — the row updates to the new binding and the new key actually
      controls that action in gameplay; pressing Esc while "Press any key..." is showing cancels the
      rebind without closing the pause menu; the new binding survives a restart.
- [ ] Click Delete Save — a confirmation dialog appears; Cancel does nothing; Confirm wipes gold/
      upgrades/settings and reloads to a fresh save.
- [ ] Click Quit — the game/Editor Play session actually stops.
- [ ] From the pause menu, click Photo Mode — camera detaches and flies freely with WASD/QE/mouse/Shift
      boost; sun and post-processing sliders visibly change the scene; Capture writes a PNG under
      `persistentDataPath/Screenshots/` with no UI visible in it; Exit (button or Esc) returns to the
      pause menu with the camera, sun, and post-processing back exactly as they were before entering.

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
