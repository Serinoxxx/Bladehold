# Impulse Fling feature — handoff (code complete; manual Editor steps remain)

Full approved plan: `C:\Users\lance\.claude\plans\rustling-gliding-panda.md`. This file records exactly where implementation stopped and what remains.

> ✅ **All code/config work (steps 1-5 below) is done and compiles.** What's left is the manual Unity Editor setup in step 6 (layer, animator states, SO assets, prefab wiring) and the in-Editor verification pass in step 7 — neither can be scripted from outside the Editor.

## Already implemented (code complete, unverified in Editor)

- `DamageSystem/Damageable.cs` — `Damage.impulsePower` + `Damage.impulseForce` fields.
- `Stats/StatType.cs` — `ImpulseGoblinChance`, `ImpulseOrbDuration`, `ImpulsePower` (with XML docs stating the `>= r` fling / `>= r−1` knockdown contract).
- `DamageSystem/RagdollConfigSO.cs` — new; ragdoll build tunables (layer name, mass 40, damping, collider sizing ratios, joint limits).
- `DamageSystem/ImpulseConfigSO.cs` — new; gameplay tunables (defaultResistance 0, launch 60°, settle/timeout, recovery retries, getUp 2.2s, knockdown 2.5s, max 12 simultaneous ragdolls).
- `DamageSystem/EnemyRagdoll.cs` — new; lazy runtime ragdoll builder (Humanoid bones via `GetBoneTransform`, 11 bodies/CharacterJoints, local-space sizing so brute scale works, kinematic+colliders-off idle, `ActiveCount` cap, `EnterRagdoll`/`AddImpulse`/`ExitRagdoll`/`FreezeCorpse`, subscribes `CorpseDespawner.OnDespawnStarted`).
- `DamageSystem/ImpulseReceiver.cs` — new; the state machine (Normal/KnockedDown/Airborne/Recovering/Corpse): threshold check, fling sequence (AI off → agent off → capsule off → animator off → ragdoll), settle detection, NavMesh recovery (`SamplePosition` retries → `Warp` → `animator.Play("GetUp")` + `animator.Update(0f)`), off-mesh force-kill via `ReceiveDamage(999999)`, corpse handoff, cheer re-fire, landing VFX/SFX hooks, `SetResistance`, `WouldIncapacitate`.
- `DamageSystem/KnockbackReceiver.cs` — optional auto-wired `ImpulseReceiver` ref; skips the slide when the same hit knocks down/flings.
- `DamageSystem/CorpseDespawner.cs` — `public event Action OnDespawnStarted` invoked at top of `SinkAndDestroy()`.
- `DamageSystem/DisableCollidersOnDeath.cs` — comment documenting that the Start-time cache intentionally excludes runtime ragdoll colliders.
- `Player/ImpulseSO.cs` — new; buff bases (duration 0 = locked, power 0, force 10 m/s, forcePerPower 0.15, powerPerChargeLevel 0.25, stack bonuses 0.15 force / 0.10 damage).
- `Player/ImpulseBuff.cs` — new; timed stacking buff (registers `ImpulseOrbDuration`/`ImpulsePower` bases, `CollectOrb()`, Update countdown, activation/deactivation MMF + aura toggle, exposes `IsActive`/`StackCount`/`DamageMultiplier`/`CurrentImpulsePower`/`CurrentImpulseForce`/`PowerPerChargeLevel`/`OnChanged`).
- `Economy/ImpulseOrb.cs` — new; Coin-pattern pickup gated on `GetComponentInParent<ImpulseBuff>()`, popup shows seconds granted, 30s lifetime.
- `Enemies/ImpulseGoblin.cs` — new; GoldenGoblin mirror (`MarkImpulse()` pre-Start, aura material swap, orb drop + optional VFX/SFX on `OnDied`).
- `Enemies/GoldenGoblin.cs` — guard at top of `ApplyGoldenVisual()`: impulse aura wins when both marks land.
- `Waves/WaveSpawner.cs` — `ImpulseGoblinChance` base registered in Start; independent impulse roll after the golden roll in `SpawnEnemy`; `ApplyDefinition` calls `ImpulseReceiver.SetResistance` (**this is the line that needs step 1**).
- `DamageSystem/DamageTrigger.cs` — optional `impulseBuff` field (falls back to `Player.Instance.GetComponentInChildren<ImpulseBuff>()`); `BuildDamage()` stamps stack damage multiplier + `impulsePower` (power + chargeLevel × powerPerChargeLevel) + `impulseForce` (buff force × (1 + chargeLevel × ChargeKnockbackBonus)).

## Remaining work

### 1. ✅ `Enemies/EnemyRosterSO.cs` — FIXES THE COMPILE BREAK (done)
- Add to `EnemyDefinition`: `/// <summary>Overrides the prefab ImpulseReceiver's impulse resistance. Blank = prefab default.</summary> public float? impulseResistance;`
- Bump `const int ColumnCount = 12` → `13`.
- In `ParseRow`, add `impulseResistance = ParseOptionalFloat(f[12], lineNumber, "impulseResistance"),` (match the exact `ParseOptionalFloat` signature used by the other optional columns).
- Update the class doc's column list to include `impulseResistance`.

### 2. ✅ `Config/Enemies.csv` — append 13th column (done)
```
id          ,displayName ,health,damage,minGold,maxGold,speed,scale,unlockWave,spawnChance,minSpawn,maxConcurrent,impulseResistance
goblin      ,... (blank impulseResistance)
goblin_brute,... ,3
```
Goblin blank → prefab/SO default (0). Brute = 3. Progression math: power 0–1 → brutes knockback only; 2 (or 1 + full 4-level charge) → knockdown; 3 → full fling.

### 3. ✅ `Config/SkillTree.csv` — append 11 rows (done; icons and grid cells verified free)
```csv
impulse_unlock,Impulse,"Impulse Goblins (10% of spawns) drop Impulse Orbs: collecting one supercharges your sword for 3s, flinging goblins skyward",40,ImpulseGoblinChance;ImpulseOrbDuration,Flat;Flat,0.1;3,range_ext_2,-3,1,Warriorskill_05_nobg,
impdur_1,Lingering Impulse,Impulse buff lasts 4s per orb,25,ImpulseOrbDuration,Flat,1,impulse_unlock,-4,1,Warriorskill_05_nobg,impdur
impdur_2,Lingering Impulse,Impulse buff lasts 5s per orb,35,ImpulseOrbDuration,Flat,1,impdur_1,-5,1,Warriorskill_05_nobg,impdur
impdur_3,Lingering Impulse,Impulse buff lasts 6s per orb,50,ImpulseOrbDuration,Flat,1,impdur_2,-6,1,Warriorskill_05_nobg,impdur
impdur_4,Lingering Impulse,Impulse buff lasts 7s per orb,70,ImpulseOrbDuration,Flat,1,impdur_3,-6,2,Warriorskill_05_nobg,impdur
impdur_5,Lingering Impulse,Impulse buff lasts 8s per orb,95,ImpulseOrbDuration,Flat,1,impdur_4,-6,3,Warriorskill_05_nobg,impdur
impchance_1,Impulse Scent,+10% Impulse Goblin spawn chance,30,ImpulseGoblinChance,Flat,0.1,impulse_unlock,-4,0,Push_nobg,impchance
impchance_2,Impulse Scent,+10% Impulse Goblin spawn chance,50,ImpulseGoblinChance,Flat,0.1,impchance_1,-5,0,Push_nobg,impchance
imppower_1,Impulse Power,+1 Impulse Power: flings launch 15% harder,35,ImpulsePower,Flat,1,impulse_unlock,-3,0,IncreaseStrength_2_nobg,
imppower_2,Impulse Power,+1 Impulse Power: enough to knock Goblin Brutes off their feet,55,ImpulsePower,Flat,1,imppower_1,-2,0,IncreaseStrength_3_nobg,
imppower_3,Impulse Power,+1 Impulse Power: enough to fling Goblin Brutes skyward,80,ImpulsePower,Flat,1,imppower_2,-1,0,IncreaseStrength_4_nobg,
```
(Branch roots off `range_ext_2` at (−3,2), grows into the free top-left band; icons reuse names already in the gold `SkillTreeSO`; verify those icon names exist in the asset and cells are still free before committing.)

### 4. ✅ `DamageSystem/ImpulseHitFeedback.cs` — new file (done)
Sibling of `SwordHitFeedback` (use it as the template — its `SpawnBlood` is the burst-scaling idiom):
- Serialized: `DamageTrigger damageTrigger` (**explicit assignment, NO OnValidate auto-wire** — the VampiricBlade precedent, the player has other DamageTriggers), `ParticleSystem burstPrefab`, `Light pulseLightPrefab` (point-light prefab, intensity 0), `float peakIntensity = 8f`, `float pulseInSeconds = 0.05f`, `float pulseOutSeconds = 0.3f`, `float pulseRange = 8f`, `float forceForMaxPulse = 25f`, plus min/max particle counts + cleanup delay like SwordHitFeedback.
- Start: error if damageTrigger null (anyError pattern); subscribe `damageTrigger.OnHit += HandleHit`; unsubscribe in OnDestroy. Signature: `OnHit(IDamageable, Damage, Vector3 hitPoint)`.
- `HandleHit`: early-return unless `damage.impulseForce > 0f` (per-hit correct even if the buff expires mid-swing). Scale factor = `Mathf.Clamp01(damage.impulseForce / forceForMaxPulse)` — charge scaling comes free since force folds it in. Instantiate burst at hitPoint, `Emit(scaled count)`, delayed `Destroy`. Instantiate the light prefab at hitPoint, animate `light.intensity` 0 → `peakIntensity × factor` → 0 with `LeanTween.value` (vendored, usable) chained in/out, set `light.range = pulseRange`, `Destroy(go, pulseInSeconds + pulseOutSeconds + 0.1f)`.
- Fires on every impulse-stamped hit (knockdown-vs-fling is decided enemy-side); note in code that `ImpulseReceiver` could expose an `OnFlung` event later to gate the big pulse.

### 5. ✅ `CLAUDE.md` updates (done)
- **Player**: `ImpulseBuff`/`ImpulseSO` bullet (orb-stacked timed buff, registers `ImpulseOrbDuration`/`ImpulsePower` bases, feedback pair + aura).
- **Damage system**: `Damage.impulsePower`/`impulseForce` on the Damage bullet; new bullets for `ImpulseReceiver` (thresholds, state machine, `SetResistance`), `EnemyRagdoll` (lazy runtime build, cap, corpse handoff), `ImpulseHitFeedback`, the two config SOs; note `CorpseDespawner.OnDespawnStarted` and the KnockbackReceiver coordination; extend the `DamageTrigger` bullet with the impulse stamping.
- **Enemies**: `ImpulseGoblin` bullet (GoldenGoblin mirror; both-marks rule: impulse aura wins); extend `EnemyRosterSO` column list with `impulseResistance`.
- **Economy**: `ImpulseOrb` bullet next to `Coin`.
- **Waves**: extend `WaveSpawner` (impulse roll beside golden, `ImpulseGoblinChance` base, `SetResistance` in ApplyDefinition).
- **Stats & upgrades**: add the 3 new StatTypes to the list.

### 6. ⏳ Editor manual steps (user does these in Unity; game degrades gracefully until then)
1. **Layer**: Tags & Layers → User Layer 8 = `Ragdoll`. Physics collision matrix: uncheck `Ragdoll×Ragdoll` and `Ragdoll×(layer 3 — the unnamed layer the player+enemies use)`; keep `Ragdoll×Default` (ground). Sword raycasts use `hitLayers` (everything), so flying bodies stay hittable.
2. **Animator**: duplicate vendored `Assets/Third Party/Synty/AnimationGoblinLocomotion/Animations/Sidekick/AC_Sidekick.controller` → `Assets/Bladehold/.../AC_GoblinEnemy.controller`. Add Trigger params `Knockdown`, `GetUp`. Add states: `KnockdownEnter` (clip `A_MOD_SWD_KnockDown_Enter_Neut` from AnimationSwordCombat/Sidekick/Hit/KnockDown — non-RM version), `KnockdownExit` (`..._Exit_Neut`), `GetUp` (same Exit clip, separate state — entered by `Animator.Play`). Transitions: AnyState→KnockdownEnter (trigger `Knockdown`, Can Transition To Self OFF), KnockdownEnter→KnockdownExit (exit time 1.0), KnockdownExit→Grounded (exit time, 0.25 blend), GetUp→Grounded (exit time, 0.25 blend). Assign to the model-child Animator on all 3 enemy prefabs (Goblin, Brute, Brute Variant).
3. **SO assets**: create `RagdollConfigSO` + `ImpulseConfigSO` assets (next to the DamageSystem scripts), `ImpulseSO` (next to `DeathNovaSO`).
4. **Enemy prefabs ×3**: add `ImpulseReceiver` + `EnemyRagdoll` + `ImpulseGoblin` components to the roots (auto-wire fills siblings); assign the SO assets, aura material, orb prefab, `bodyRenderers` (copy GoldenGoblin's list); optional landing dust VFX/SFX on ImpulseReceiver.
5. **Orb prefab**: clone the coin pickup prefab → swap `Coin` for `ImpulseOrb`, restyle (emissive orb), assign popup + MMF pickup feedback. Create the emissive impulse aura material (distinct hue from gold).
6. **Player/sword**: add `ImpulseBuff` to the Player root (assign ImpulseSO, activation/deactivation MMFs, optional looping aura child). Add `ImpulseHitFeedback` to the sword (assign the sword `DamageTrigger` explicitly, burst prefab, light prefab). Optionally wire `impulseBuff` on the sword DamageTrigger (falls back automatically).
7. Optional: proper icons for the 11 new nodes via Bladehold > Skill Tree Editor.

### 7. ⏳ Verification (in-Editor, from the plan)
Compile clean → tree renders new branch → DevConsole gold → buy `impulse_unlock` → kill aura'd goblin → orb → flings with burst/light pulse → landing dust + stand-up + resume chase → corpses stay ragdolled and despawn. Resistance: brutes slide at power 0–1, knock down at 2 (or 1 + full charge), fling at 3. Stacking extends timer + bigger flings. Horde: `DebugSpawnBurst` → cap degrades to knockdowns. Player dies while goblin airborne → lands, stands, cheers. Fling off NavMesh edge → force-dies after retry window, drops coins. Regression: Golden Goblin intact; golden+impulse shows impulse aura, drops both; Death Nova flings nothing.
