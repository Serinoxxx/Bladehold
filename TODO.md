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

- [ ] **Gate defense objective** — castle gates as a second loss condition alongside player death:
  - [ ] A `Gate` is just another `Health`/`IDamageable` object; reuse `AIAttack` for goblins attacking
        it (attack range/damage against the gate rather than the player).
  - [ ] `AIMovement` needs a target-selection layer: path toward the nearest/assigned gate by default,
        but switch to engaging the player if the player comes within engage range.
  - [ ] Level 1: one gate. Level 2+: multiple gates, with mini-waves spawning on a fixed interval
        (~30s) alternating which gate they target — the player must clear the goblins at one gate and
        reach the next before the next mini-wave lands, so travel time/spacing between gates becomes
        a real tuning lever.
  - [ ] Round ends (loss) if any gate's `Health.OnDied` fires, same as it currently ends on the
        player's `Health.OnDied` — likely both route through the same "run over" path `DeathScreen`
        already owns.
  - [ ] Keep the alternation pattern predictable/learnable (fits the mastery-over-time goal); scale
        difficulty by goblin count/composition per mini-wave and gate HP, not by randomizing timing.

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
  - [ ] `ImpulseSO` — leave `baseOrbDurationSeconds`/`basePower` at 0 (locked until skill nodes are
        bought); tune `baseImpulseForce`/`forcePerPower`/`powerPerChargeLevel`/
        `forcePerExtraStackPercent`/`damagePerExtraStackPercent`.
  - [ ] `ImpulseConfigSO` — tune `defaultResistance`, launch/landing/recovery timings,
        `maxSimultaneousRagdolls`.
  - [ ] `RagdollConfigSO` — tune mass/damping/joint-limit values (defaults are reasonable starting
        points).

- [ ] **Physics layer**: add a `Ragdoll` layer in **Edit > Project Settings > Tags and Layers** (must
      match `RagdollConfigSO.ragdollLayerName`), then in **Physics > Layer Collision Matrix** disable
      Ragdoll×Ragdoll and Ragdoll×(the enemy/character layer), keeping Ragdoll×Default enabled so
      flung bodies still land on the ground.

- [ ] **Animator controller** (the same Synty controller already touched for the `Blocked` state):
  - [ ] Add a `Knockdown` trigger parameter plus enter/exit states for the animation-only knockdown
        reaction (`ImpulseReceiver.knockdownTrigger`).
  - [ ] Add a `GetUp` state (played directly by name, not a trigger — the animator is disabled
        mid-flight so a trigger would be lost) for standing up after a landed fling
        (`ImpulseReceiver.getUpStateName`).
  - [ ] Confirm the existing `Cheer` trigger is reachable from any state — a recovering enemy
        re-fires it if the player died while it was airborne.

- [ ] **ImpulseOrb prefab**: trigger `Collider` + `ImpulseOrb` component; assign `pickupPopup` (a
      `DamageNumber` prefab), `pickupFeedback` (optional `MMF_Player`), tune `lifetime`.

- [ ] **Player prefab** (`Assets/Bladehold/Bladehold Prefabs/Player.prefab`):
  - [ ] Add an `ImpulseBuff` component on the player root; assign `config` (the `ImpulseSO`);
        optional `activationFeedback`/`deactivationFeedback` `MMF_Player`s and an `auraVisual` child
        object.
  - [ ] Add an `ImpulseHitFeedback` component; assign `damageTrigger` to the sword's `DamageTrigger`
        explicitly (same precedent as `VampiricBlade`), plus `burstPrefab` (`ParticleSystem`) and
        `pulseLightPrefab` (a `Light` at rest intensity 0).
  - [ ] Optionally assign the sword `DamageTrigger`'s `impulseBuff` field explicitly (it auto-finds
        the player's `ImpulseBuff` via `GetComponentInChildren` if left blank).

- [ ] **Goblin prefabs** (`Goblin Enemy.prefab` and the brute variant — Impulse can fling either):
  - [ ] Add an `EnemyRagdoll` component; assign `animator` (auto-wires via `OnValidate`), `config`
        (the `RagdollConfigSO`).
  - [ ] Add an `ImpulseReceiver` component; most references (`health`/`agent`/`ragdoll`/`animator`/
        `rootCollider`/`aiMovement`/`aiAnimation`/`aiAttack`) auto-wire via `OnValidate` — assign
        `config` (the `ImpulseConfigSO`); optional `landingVfxPrefab`/`landingSfx`.
  - [ ] Add an `ImpulseGoblin` component; assign `health`, `orbPrefab` (the `ImpulseOrb` prefab),
        `bodyRenderers`, `impulseAuraMaterial`, optional `deathVfxPrefab`/`deathSfx`.
  - [ ] Confirm the existing `KnockbackReceiver`'s `impulseReceiver` field picked up the new
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

- [ ] **Create SO asset instances** (each already has a `[CreateAssetMenu]` entry):
  - [ ] `LightningBallAttackSO` — tune `attackRange`/`damage`/`ballSpeed`/`ballLifetime`/
        `windupToApex`/`attackCooldown`.
  - [ ] `LightningStormAttackSO` — tune `castRange`/`castCooldown`/`stormRadius`/`stormDuration`/
        `strikeInterval`/`strikeDamage`.
  - [ ] `ChainLightningSO` — leave `baseBounces`/`baseDamagePercent`/`baseOrbDurationSeconds` at 0
        (locked until skill nodes are bought); tune `chainRadius`/`damagePerExtraStackPercent`.

- [ ] **LightningBall prefab**: kinematic `Rigidbody` + trigger `SphereCollider` + `LightningBall`
      component; optional impact VFX/SFX.

- [ ] **LightningStormZone prefab**: just a `LightningStormZone` component (uses `OverlapSphere`, no
      collider needed) + optional strike VFX/SFX.

- [ ] **LightningOrb prefab**: trigger `Collider` + `LightningOrb` component — mirrors the existing
      `ImpulseOrb` prefab (`DamageNumber` popup, pickup `MMF_Player`, lifetime).

- [ ] **Storm Witch prefab**: build with `Health`, `AIMovement`, `AIAnimation`,
      `LightningBallAttack` (assign `firePoint`, `ballPrefab`, the `LightningBallAttackSO`),
      `LightningStormAttack` (assign `stormZonePrefab`, the `LightningStormAttackSO`),
      `LightningOrbDropper` (assign `orbPrefab`), plus `ImpulseReceiver`/`KnockbackReceiver` like
      other enemies.
  - [ ] Register the prefab in `WaveSpawner`'s `enemyPrefabs` list under id `storm_witch` (row
        already in `Config/Enemies.csv`).

- [ ] **Player prefab** (`Assets/Bladehold/Bladehold Prefabs/Player.prefab`): add a
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
