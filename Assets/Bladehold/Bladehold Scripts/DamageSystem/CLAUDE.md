# DamageSystem: the combat core

## Health (the hub)

- **`Damageable.cs`** holds `IDamageable` (`ReceiveDamage(Damage)`), the `Damage` payload (value, `DamageType` sharp/blunt/elemental, crit, knockback, `sourcePosition`, `source` = the attacker's own `IDamageable` when known, `unparryable`), and the type enum.
- **`Health`** implements `IDamageable` with its max value from a `HealthSO`. It raises:
  - `OnDamaged(Damage)` on every hit.
  - `OnDied` once, latched via `IsDead`.
  - `OnHealthChanged`.
  - The static `OnAnyHealthDamaged`.
- Per-instance overrides: `SetMaxHealth(value[, preserveFraction])`, `ScaleMaxHealth`. Spawners call these right after `Instantiate`, before `Start`.
- **The only three hooks that change what Health does** (all are invocation-list events):
  - `TryBlockDamage` (`Func<Damage,bool>`): checked first; `true` negates the hit entirely (no loss, feedback or events).
  - `ScaleDamageTaken` (`Func<Damage,float>`): every handler's multiplier is multiplied together (clamped ≥ 0) and applied to `Damage.value` in place, so listeners see the mitigated value. Used for damage-reduction buffs.
  - `TryPreventDeath` (`Func<bool>`): checked before a lethal hit latches; `true` cancels the death (the handler calls `Revive` itself). Second Wind is the intended user.
- Everything else is a reactive listener that unsubscribes in `OnDestroy`.

## Hitboxes

- **`DamageTrigger`**: an activatable hitbox, usually opened by an animation event.
  - Two modes: `Sphere` (overlap around its position) and `BladeSweep` (raycasts sampled blade points from their previous to current position each tick, so fast swings can't tunnel).
  - Dedupes targets and never hits its owner.
  - With `readsPlayerStats`, damage, range, crit and the cut-through cap come from `PlayerStats`. Hitting the cap and then meeting one more target ends the activation and raises `OnBlocked`.
  - Raises `OnHit(IDamageable, Damage, hitPoint)`.
- **`IShieldBlocker`**: shields (Bulwark, bubble shields) that intercept melee.
- **`KnockbackReceiver`** reacts to `OnDamaged` and shoves the `NavMeshAgent`.

## Feedback and cleanup

- `SwordHitFeedback` / `BowHitFeedback` / `ImpulseHitFeedback`: play `MMF_Player`s from `DamageTrigger.OnHit` / `PlayerBow.OnArrowImpact`, with intensity = damage (or knockback) over a cap. `KnockbackReceiver` (knockdown/fling/wall pin) and `EnemyRagdoll` (bone impacts) do the same with players under the enemy prefab's `Feedbacks` child (on `Goblin Enemy (Base)`, so variants inherit them).
- `MMF_PooledParticleBurst`: the house MMF feedback for blood and hit bursts. It takes the system from `ParticlePool`, and intensity (0-1) picks emit count, speed and scale from ranges. When a burst needs a direction (bow spray, wall normal, ragdoll contact), the caller rotates the MMF player's GameObject just before `PlayFeedbacks` and the burst uses the owner's rotation, so keep such players on their own GameObject.
- `DamageNumberSpawner`: DamageNumbersPro popups from `OnDamaged`.
- `BloodDecal*`.
- `HitstopFeedback`: hitstop was removed from the sword feel; check before re-adding.
- `DisableCollidersOnDeath`.
- `CorpseDespawner` + `CorpseManager` + `CorpseConfigSO`: freeze, then sink and destroy corpses long after `OnDied`, with a cap on how many linger. No logic may depend on a corpse being destroyed.
- `EnemyRagdoll` + `RagdollConfigSO`: a lazily built runtime ragdoll on Humanoid rigs.
