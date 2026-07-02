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

- [ ] **Player prefab** (`Assets/Bladehold/Bladehold Prefabs/Player.prefab`):
  - [ ] Add a `VampiricBlade` component on the player root; assign `swordTrigger` to the sword's
        `DamageTrigger` explicitly (no auto-wire — the Death Nova hitbox is also a `DamageTrigger`;
        health/stats default to `Player.Instance`).
  - [ ] Add a `DamageBlocker` component on the player root (next to `Health`); optionally assign a
        `blockFeedback` `MMF_Player` (shield flash/clank SFX) so blocks are readable.
  - [ ] Add an `AttackCancelsSprint` component on the player root (auto-finds the `InputReader` and
        `SamplePlayerAnimationController` via `OnValidate`).

- [ ] **Icons**: open **Bladehold > Skill Tree Editor**, pick each `SkillTreeSO`, and drag sprites
      onto the new rows (`sprint_*`, `ampknock_*`, `vamp_*`, `solid_*` currently have blank icons);
      dropping a sprite adds it to the tree's `icons` list and sets the node's icon name in one step.

- [ ] **Balance pass**: tune the placeholder costs on the new rows and reposition their `x`/`y` if
      wanted (Sprinter chains off `move_2`, Amplified Knockback off `knock_1`; Vampiric Blade and
      Solid are fresh root columns at x=10/x=12).

## Manual verification (skill icons + new skill lines)

- [ ] Nodes with an icon show it on the death screen; icon-less nodes look unchanged; console shows
      a line-numbered error for a typo'd icon name.
- [ ] Edit a node and Save in the Skill Tree Editor → CSV file updates on disk and the tree renders
      the change on next death screen.
- [ ] Buy Vampiric Blade, hit goblins → health visibly refills by ~1% of damage dealt per tier
      (crit/charged hits heal more); no healing while dead.
- [ ] Buy Solid, take a goblin hit → first hit negated (no damage number/feedback), next hits land
      normally until the cooldown elapses; higher tiers shorten the window.
- [ ] Buy Sprinter tiers → sprint is visibly faster; pressing attack while sprinting drops the player
      out of sprint (works even with no Sprinter nodes owned).
- [ ] Buy Amplified Knockback with Heavy Strike owned → a fully charged swing shoves goblins visibly
      further than an uncharged one, scaling per tier.
