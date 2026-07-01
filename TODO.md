# TODO

## Reincarnate system — Unity Editor wiring

The C# for the Reincarnate meta-progression system (Death Nova, Golden Goblin, Grave Robber) is
done. The following can only be done in the Unity Editor (asset creation, prefab/scene wiring,
art/audio) — Claude Code can't safely author these headlessly. See
`Assets/Bladehold/Bladehold Scripts/Reincarnate/`, `Player/DeathNova*.cs`, `Enemies/GoldenGoblin.cs`,
`Player/GoldOnDeathCollector.cs`, and `Config/Reincarnate.csv` for the code side.

- [ ] **Create SO asset instances** (each already has a `[CreateAssetMenu]` entry):
  - [ ] `DeathNovaSO` — tune `baseCharges` (leave 0), `baseCooldownSeconds`, `baseRevivePercent` (leave 0).
  - [ ] A `DamageTriggerSO` + `DamageSO` pair for the nova hitbox (radius/damage/duration/maxHits,
        plus the new `knockbackForce` field on the `DamageTrigger` component itself, not the SO).
  - [ ] A second `SkillTreeSO` instance pointed at `Assets/Bladehold/Config/Reincarnate.csv`
        (`hasHeaderRow` on, same as the existing gold-tree `SkillTreeSO.asset`).

- [ ] **Player prefab** (`Assets/Bladehold/Bladehold Prefabs/Player.prefab`):
  - [ ] Add a child GameObject `DeathNovaHitbox` with a `DamageTrigger` component, `readsPlayerStats`
        off, wired to the nova `DamageTriggerSO`/`DamageSO` from above.
  - [ ] Add a `DeathNova` component on the player root; assign `health`, `novaHitbox` (the child
        above), `config` (the `DeathNovaSO`).
  - [ ] Add a `GoldOnDeathCollector` component on the player root; assign `health` (wallet/stats can
        stay unassigned — they default to `Player.Instance`).

- [ ] **Goblin prefab** (`Assets/Bladehold/Bladehold Prefabs/Goblin Enemy.prefab`):
  - [ ] Add a `GoldenGoblin` component; assign the same `EnemySO`/`Coin` prefab `CoinDropper`
        already uses.
  - [ ] Assign a gold-glowing `Material` to `goldenMaterial` and the goblin's body renderer(s) to
        `bodyRenderers`.
  - [ ] Assign a gold-burst VFX prefab to `deathVfxPrefab` and an SFX clip to `deathSfx` (both
        optional — the gold bonus still applies without them).

- [ ] **Scene**: add a `ReincarnateService` component (alongside where `SkillTreeService` lives),
      pointed at the Reincarnate `SkillTreeSO` from above.

- [ ] **Death-screen UI**:
  - [ ] Duplicate the existing `SkillTreeView` + `SkillNodeView` hierarchy for the Reincarnate tree;
        assign the new view's `serviceBehaviour` to the `ReincarnateService`.
  - [ ] Set `costSuffix` to `" pts"` on the duplicated node-view prefab (blank/default for the gold
        tree's prefab).
  - [ ] Wire `DeathScreen`'s new `reincarnateButton` (+ optional `reincarnatePreviewLabel`) to the
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

- [ ] **Animator controller** (`Assets/Third Party/Synty/AnimationBaseLocomotion/Animations/Sidekick/AC_Sidekick_Masculine.controller`
      — the one vendored-asset exception here; there's no code-only way to add a reachable state):
  - [ ] Add a `Blocked` trigger parameter, a blocked/parry state, and transitions in/out of it (e.g.
        from any attack state, back to locomotion). Until this exists, `SwordHitFeedback.OnBlocked`
        calls `Animator.SetTrigger("Blocked")` on a parameter that doesn't do anything yet.

- [ ] **Sword prefab** (`Wep_Sword_01`, nested under `Assets/Bladehold/Bladehold Prefabs/Player.prefab`):
  - [ ] Add empty child transforms `BladeBase` (at the hilt/guard) and `BladeTip` (at the point).
  - [ ] On the existing `DamageTrigger` component: set `Detection Mode` to `Blade Sweep`; assign
        `Blade Base`/`Blade Tip`; tune `Base Point Count` (~5) and `Hit Layers` if the sweep ever
        clips the player's own hurtbox.
  - [ ] Add a `SwordHitFeedback` component: assign an `AudioSource`, the player rig's `Animator`, hit/crit
        `AudioClip[]`s and woosh `AudioClip[]` (sound effects to be provided), and blood/crit particle
        prefabs + damage-scaling tunables (`Damage For Max Particles`, `Min/Max Particles`,
        `Min/Max Speed Multiplier`).
  - [ ] Add a `SwordChargeFeedback` component (can live on the sword or the player root): assign
        `PlayerAttack` and a `MMF_Player[]` — create one child `MMF_Player` per charge stage (start
        with 4, matching the range skill's 25/50/75/100% tiers) with an increasingly bigger spark
        particle + louder/more satisfying SFX per stage.

- [ ] **Attack animation clip**: add a `PlaySwordWoosh` animation-event marker earlier in the swing
      (before the existing hit-frame event that calls `OneHandedSwordAttack`), and wire
      `AnimationEvents.swordHitFeedback` to the new `SwordHitFeedback` component.

- [ ] **Damage numbers**: create an alternate DamageNumbersPro prefab variant for crits (bigger/colored
      text) and assign it to `DamageNumberSpawner.critPopupPrefab` on the player and/or goblins.

- [ ] **Particle prefabs**: author a blood-particle prefab (and optional distinct crit variant) —
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
