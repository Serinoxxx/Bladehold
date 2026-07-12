# Baked Enemy Animations + Baked Ragdolls (horde scaling to 300–500)

## Context

Enemy cost today is dominated by per-enemy **Animator evaluation + ~18 animator param writes** (`AIAnimation.cs:153-195`, `LocomotionAnimator.cs:403-427`) and **skinned mesh skinning**, plus live PhysX ragdolls capped at 12 (`EnemyRagdoll.MaxActive`). The existing mitigations (far-frame-skip, `CullUpdateTransforms`, repath throttling) top out well below the target. Goal: **300–500 concurrent enemies**, with **all enemies** on baked playback (no hybrid LOD) and **ragdolls fully baked** (lethal flings, plain-death collapses, and survivable fling→land→get-up), removing PhysX ragdolls and the 12-cap from the runtime path.

Decisions confirmed with the user: target 300–500; all enemies baked (player keeps the real Synty Animator); bake everything ragdoll-wise.

Key enablers already in the codebase:
- All enemies share one Synty Humanoid rig (`SidekickSyntyCharacter.fbx`, 1 SkinnedMeshRenderer, 2 materials) + one controller (`AC_Sidekick.controller`).
- Enemies only use forward locomotion (Idle/Walk/Run/Sprint) + one-shots (Death/Attack/Cheer/Slam/Knockdown/GetUp).
- **Attack damage timing is wall-clock** (`AIAttack.cs:187-214` windupToApex; `TrollSlamAttack` telegraphSeconds) — baked visuals cannot break combat.
- Once the Animator is removed, live ragdolls can't work anyway (`EnemyRagdoll.TryBuild` needs `Animator.GetBoneTransform`) — ragdoll baking is the replacement, not optional.
- Mounted Knight + horse (different rig, max 1 concurrent) stay on the real Animator — out of scope.

## Architecture decisions

1. **Bake format: bone-matrix animation texture** (not vertex-VAT). One RGBAHalf `Texture2D`; each frame row stores per used bone the 3 rows of `rootLocal(bone) × bindpose` = **3 texels/bone** (≤64 bones → ≤192 px wide). Vertex shader skins with 4 bone indices/weights baked into mesh UV1/UV2. All clips incl. ~26 ragdoll clips ≈ **~5 MB total** (vertex-VAT would be ~400 MB). Crossfade = sample twice + lerp. Fallback if matrix-lerp thinning ever offends: pos+quat texels (2/bone, NLERP) — isolated in baker + one HLSL function. Per-type uniform scale rides through `unity_ObjectToWorld`; big root travel (ragdolls) is NOT in the texture — see root-motion split below.
2. **Rendering: per-enemy MeshRenderer + GPU-instanced shader properties** (not a central `RenderMeshInstanced` manager). Playback params live in `UNITY_INSTANCING_BUFFER`; materials have GPU Instancing on; set via MaterialPropertyBlock. Instanced props keep same-material renderers batching into a handful of instanced draws; engine frustum culling, shadows, Forward+ lights, and the golden/impulse *material swap* keep working unchanged. Playback is **time-anchored** (shader computes frame from `_Time` + anchor), so MPB writes happen only on state changes, never per frame.
3. **Shader: hand-written URP HLSL** (not Shader Graph — SG can't put custom props in the instancing buffer). Passes: ForwardLit (Forward+), ShadowCaster, DepthOnly, DepthNormals — all sharing one skinning vertex function. Instanced props: `_ClipA(startRow, frameCount, fps×rate, anchorTime)`, `_ClipB(same)`, `_Blend(blendStart, blendDur, flagsA, flagsB)`, `_FlashColor`.
4. **`BakedAnimator` playback model**: 2 clip slots A/B. Locomotion = quantized gait (same halfway thresholds as `LocomotionAnimator.CalculateGait`, 0.2s hysteresis) + per-instance rate `speed2D / clip.nativeSpeed` (clamped 0.5–1.75) + 0.2s crossfades. One-shots (Attack, Death, Cheer, Slam×3, Knockdown, GetUp, ragdoll) are **full-body** overrides in slot B — the avatar-masked Attack layer is dropped for v1 (baked mesh carries an upper-body weight in UV3.x from day one so a masked-attack shader variant can land later with **no rebake**). Lean/incline additive layers are lost — accepted, verified in the side-by-side stage.
5. **Ragdoll root-motion split**: bone texture stays root-relative (±2 m, half-precision-safe); each ragdoll clip carries a CPU-side **root path** (per-frame `Vector3`, launch-local: +Z = launch dir, origin = launch point, full float) that the driver applies to the transform. Root rotation is constant (= launch yaw); all tumble lives in bone data. So damage numbers/targeting/corpse-sink keep tracking the root.
6. **Ragdoll landing is pre-validated, not recovered**: before playing a fling clip, transform its `endDisplacement` to world and check wall SphereCast along launch dir (capped at `maxHorizontalReach`) + (survivable only) `NavMesh.SamplePosition`. No fit → try other variants/shorter buckets → degrade to Knockdown (stays in place). This **deletes** the post-hoc NavMesh retry loop and the `ReceiveDamage(999999)` force-kill (`ImpulseReceiver.cs:345-378`).
7. **Get-up seam**: survivable clips get ~0.5s of synthetic frames appended at bake time, blending the settled pose into a **canonical downed pose = frame 0 of the baked GetUp clip** — so `fling → GetUp` chains seamlessly (replaces today's hard snap + pose discard, `ImpulseReceiver.cs:380-394`). Lethal clips hold their raw settled frame as the corpse pose.
8. **One ragdoll library at scale 1.0**; `BakedRagdollDriver` multiplies the root path by `transform.localScale.x` (pose scales with the root automatically — paths must scale too or ground contact breaks). Heavies (troll) are gated by roster `impulseResistance`, not separate bakes.
9. **Simplifications accepted**: mid-air `AddImpulse` top-ups are ignored (damage still applies; mid-air death flips a survivable clip into a corpse hold); bodies can clip small props mid-arc (arena is open; wall SphereCast prevents through-wall corpses); root capsule collider **stays enabled during flight** (root follows the path, so airborne enemies stay sword-hittable — old code disabled it, `ImpulseReceiver.cs:308`).

## New files

Runtime — `Assets/Bladehold/Bladehold Scripts/Enemies/BakedAnimation/`:
- `BakedAnimationSetSO.cs` — texture + baked-mesh refs, boneCount, `BakedClip[] { name, hash, startRow, frameCount, fps, loop, holdLast, nativeSpeed }`; hash lookup.
- `BakedAnimator.cs` — playback state machine; API: `PlayLocomotion(clipId, rate)`, `PlayOneShot(hash)`, `CancelOneShot()`, `SetSpeedMultiplier(f)` (SlowStatus), `Freeze()/Resume()`, `IsPlayingOneShot`, `IsFinished`, `CurrentFrame`, `SetFlash(color, amount)`; owns the renderer's single MPB (state-change writes only).
- `BakedHitFlash.cs` — `Health.OnDamaged` → flash decay via `BakedAnimator.SetFlash` (replaces MMF_Flicker, which would stomp the MPB).

Runtime — `Assets/Bladehold/Bladehold Scripts/DamageSystem/`:
- `RagdollClipLibrarySO.cs` — `RagdollClipEntry { clipId, kind (LethalFling/SurvivableFling/DeathCollapse), launchSpeed, variantSeed, frameCount, rootPath[], endDisplacement, maxHorizontalReach, impactFrame, settleFrame }`; selection API `TrySelectFling(force, lethal, scale, endpointValid, out entry)` (nearest force bucket, shuffled variants, fall through to shorter buckets) + `PickCollapse()`.
- `RagdollBakeConfigSO.cs` — bake tunables: force buckets, variants/bucket, launch angle/spin/limb-kick + settle params (moved from `ImpulseConfigSO` — bake-time-only now), physics Hz, clip fps, blend-to-downed seconds.
- `BakedRagdollDriver.cs` — dumb playback of one entry: `Play(entry, origin, launchYaw, scale)` → `BakedAnimator.PlayOneShot` + per-frame root = `origin + launchYaw * (rootPath[frame] * scale)` indexed off the BakedAnimator playhead (no drift); raises `OnImpact` (landing VFX/SFX at `impactFrame`) and `OnFinished`.

Editor — `Assets/Bladehold/Bladehold Scripts/Editor/`:
- `BakedAnimationBakeManifestSO.cs`, `BakedAnimationBaker.cs`, `BakedAnimationBakerWindow.cs` (**Bladehold > Baked Animation Baker**). Samples humanoid clips via **PlayableGraph on the real rig's Animator** (never `clip.SampleAnimation` — humanoid retargeting), `graph.Evaluate(1/fps)`, writes `root.worldToLocal × bone.localToWorld × bindpose`. `isReadable:0` is a player-build restriction only — editor reads mesh data fine. 30 fps default.
- `RagdollBaking/RagdollClipBakerWindow.cs` (**Bladehold > Ragdoll Clip Baker**), `RagdollSimulationRunner.cs`, `RagdollClipPostProcessor.cs`. Runner: empty in-memory scene (`EditorSceneManager.NewScene`), procedural ground plane at y=0 (flat-arena assumption — stated limit), instantiate rig, **reuse `EnemyRagdoll.BuildIfNeeded()` verbatim** (single source of physical truth), `Physics.simulationMode = Script` + `Physics.Simulate` stepping at 60 Hz, capture all ~55 bone world transforms per recorded frame, `Random.InitState(variantSeed)` before launch (deterministic re-bakes), restore scene setup in `finally`. PostProcessor: low-pass root-path extraction (final frames pinned to y=0), root-relative re-expression, downed-pose blend append, standard `AnimationClip` asset output (scrubbable in the Animation window) + library metadata. Window: Bake All / Bake Selected / reroll-seeds toggle; post-bake report table (duration, end displacement, max height/reach) to spot outlier variants.

Shader — `Assets/Bladehold/Bladehold Materials/BakedAnimation/`:
- `BakedBoneAnimation.hlsl` (frame-row fetch, frame lerp, A/B blend, 4-weight skin), `BakedEnemyLit.shader`.

Dev — `Assets/Bladehold/Bladehold Scripts/Debug/BakedAnimationScrubber.cs` (clip scrubber, DevConsole idiom).

Generated assets → `Assets/Bladehold/Bladehold Animation/`: `SidekickBoneAnim.asset` (Texture2D RGBAHalf, point, no mips, linear), `SidekickBakedMesh.asset` (UV1=bone indices, UV2=weights, UV3.x=upper-body mask, both submeshes, generous fixed bounds ~3×3×3 m), `SidekickBakedSet.asset`, ragdoll `.anim` clips + `RagdollClipLibrarySO` asset.

Clip list (sources GUID-resolved from AC_Sidekick): Idle, Walk, Run, Sprint (loop, nativeSpeed from `AnimationClip.averageSpeed.z`), Attack, Death (holdLast), Cheer (loop), SlamStart/Hold(loop)/End, Knockdown (holdLast), GetUp, + ragdoll matrix: **3 force buckets (9/14/20 m/s) × 4 lethal + × 3 survivable variants + 5 zero-velocity collapses ≈ 26 clips**.

## Integration changes (existing files)

- **`AIAnimation.cs` — rewrite in place**: drop Animator/LocomotionAnimator; Update maps `agent.velocity` → gait → `PlayLocomotion` (no-op when unchanged); `OnDied` → `PlayOneShot(Death)` + disable self; player death → `PlayOneShot(Cheer)`. All frame-skip/culling logic deleted.
- **`AIAttack.cs` / `TrollSlamAttack.cs` / `LightningBallAttack.cs` / `LightningStormAttack.cs` / `BomberAttack.cs`**: `Animator` field → `BakedAnimator` (OnValidate `GetComponentInChildren`); `SetTrigger` → `PlayOneShot` (serialized names stay = old trigger names; Slam becomes SlamStart→SlamHold(loop)→SlamEnd on the existing wall-clock telegraph).
- **`ImpulseReceiver.cs` — rewrite of fling/death paths**: states become `Normal/KnockedDown/Airborne/GettingUp/Corpse`. `FlingRoutine` → pre-validate + `driver.Play(...)`; `OnFinished` → Corpse (hold last frame) or GettingUp (`agent.Warp(worldEnd)` + `PlayOneShot(GetUp)` + wait `IsFinished` — `getUpSeconds` deleted, clip length is truth). `HandleDied` → `PickCollapse()` for every plain death (no cap, no Death-trigger fallback). Knockdown path keeps its shape (baked Knockdown clip + `knockdownSeconds`) and is now the universal degrade tier. Delete: `UprightYaw`, NavMesh retry loop, force-kill, settle polling, `FreezeCorpse` calls, `animator.Play(getUpStateHash)`.
- **`SlowStatus.cs`**: `animator.speed` → `SetSpeedMultiplier`. **`CorpseDespawner.cs`**: `Animator` field → `BakedAnimator`, `animator.enabled=false` → `Freeze()` (sink moves the root; pose follows for free).
- **`GoldenGoblin.cs` / `ImpulseGoblin.cs`**: code unchanged — wiring only (baked-shader material duplicates).
- **`EnemyRagdoll.cs`**: demoted to **bake-time builder only** — component removed from prefabs; strip `ActiveCount/MaxActive/HasCapacity/AddImpulse/ExitRagdoll/FreezeCorpse` + corpse subscription. `RagdollConfigSO` becomes bake-time config (tweaks require re-bake).
- **`ImpulseConfigSO`**: keep `defaultResistance`, `knockdownSeconds`; move launch/settle fields to `RagdollBakeConfigSO`; delete recovery fields; add `landingNavSampleDistance` (1 m), `wallCheckRadius` (0.3), `wallCheckMask`.
- **Max Ragdolls player setting deleted end-to-end**: `SaveData.maxRagdolls`, `GameSettingsService.MaxRagdolls/ApplyMaxRagdolls`, `SettingsPanelView.maxRagdollsSlider`, `SettingsMenuGenerator` row, slider rows in both scene canvases (Editor wiring).
- **`AIMovementSO`**: delete `cullOffscreenAnimators`, `animationFullRateDistance`, `animationFarFrameInterval` (obsolete); avoidance/repath knobs stay (they're the remaining CPU levers). **`LocomotionAnimator.cs`**: delete once unreferenced.
- **Untouched**: `KnockbackReceiver` (still consults `WouldIncapacitate`), `WaveSpawner`/`EnemyZoo` (scale flows through object-to-world), `Health`, `CoinDropper`, `DamageNumberSpawner`, Player/MountedKnight/horse, corpse cap + sink, `CorpseDespawner.OnDespawnStarted` event (only the ragdoll subscription dies).

## Stages (commit-sized; each playable/testable)

- **A — Animation bake pipeline** (editor-only, zero gameplay risk): baker SO/window/logic + `BakedAnimationSetSO`; bake the 12 base clips; commit generated assets.
- **R1 — Ragdoll bake infrastructure** (parallel with A/B — outputs plain `AnimationClip`s previewable on the still-Animator'd rig): `RagdollBakeConfigSO`, `RagdollClipLibrarySO`, Runner/PostProcessor/Window; smoke-bake one lethal clip, scrub it, confirm root path ends at y=0.
- **B — Shader + BakedAnimator + scrubber** (nothing swapped yet): materials; a BakedBody test object next to an Animator goblin in the EnemyZoo scene; side-by-side verify shadows/depth/Forward+ lights/brute scale/crossfades/hold-loop/hit flash/golden swap. **This is the lean-loss acceptance checkpoint.**
- **R2 — Bake the full ragdoll library**: Bake All, review report table, iterate `RagdollConfigSO` (deterministic re-bakes).
- **C — Enemy integration (the swap)**: AIAnimation rewrite + all field/call swaps; prefab edits (add BakedBody child, disable SkinnedMeshRenderer GO + Animator, add BakedAnimator/BakedHitFlash, re-point `bodyRenderers`, remove MMF_Flicker — keep MMF sound). **Interim seam**: all would-be flings route through the existing knockdown degrade path, deaths play the baked Death clip, until R4 lands.
- **R3 — Texture-bake ragdoll clips** through the Stage A pipeline (GetUp frame-0 = canonical downed pose requirement).
- **R4 — Runtime ragdoll playback**: `BakedRagdollDriver` + `ImpulseReceiver` rewrite + `ImpulseConfigSO` migration; `EnemyRagdoll` leaves prefabs.
- **R5/D — Deletions + perf verification**: strip `EnemyRagdoll` to bake-only, delete Max Ragdolls setting, obsolete `AIMovementSO` fields, `LocomotionAnimator.cs`; update CLAUDE.md blurbs. Perf protocol below; record numbers in TODO.md.
- **E (optional)** — masked upper-body Attack using the already-baked UV3.x weight (shader change only, no rebake) — only if full-body attacks read badly.

## Verification

- **Stage B**: EnemyZoo side-by-side baked vs Animator goblin — every clip scrubbed; shadows, DepthNormals (SSAO), Forward+ point lights, brute 1.25× scale, golden/aura material swap, hit flash.
- **Stage C**: full wave run — gaits at all roster speeds, attack timing (windupToApex unchanged), troll slam telegraph, witch cast, knockdown→get-up, death, cheer on player death, golden/impulse drops, corpse sink, SlowStatus, damage numbers, coins, stuck arrows (anchor falls back to root — accepted).
- **R-stages**: EnemyZoo — fling every roster type lethal + survivable + collapse; kill mid-air (survivable→corpse hold); kill mid-knockdown; fling into the arena wall (expect shorter variant or knockdown, never a through-wall corpse); brute/troll scale.
- **Perf (Stage D)**: DevConsole `DebugSpawnBurst(300)` then `(500)`; Profiler — `Animators.Update`/`MeshSkinning.Update` should vanish; Frame Debugger — a handful of instanced draws per pass; **fling 50+ enemies in one Impulse swing** (previously capped at 12) with flat frame time; before/after ms recorded in TODO.md. Compile check via `dotnet build` on the generated csprojs (per memory: add new files to the csproj first).

## TODO.md Editor-wiring items (recorded per stage, project convention)

Create bake manifest + `RagdollBakeConfigSO`/`RagdollClipLibrarySO` assets and run bakes; create 4 GPU-instanced materials (`M_GoblinBaked_Body`, `M_GoblinBaked_Accessory`, golden + aura duplicates); base-prefab edits (BakedBody child, component adds/removes, MMF_Flicker removal, `bodyRenderers` re-point) + spot-check variants; wire `clipLibrary`/`driver` on enemy prefabs; remove Max Ragdolls slider rows from `Bladehold Test Scene.unity` and `Enemy Zoo.unity` (or regenerate via `SettingsMenuGenerator`); manual verification checklists per stage.

## Known losses (accepted)

Lean/incline additive polish; avatar-masked attack-while-running (Stage E escape hatch); mid-flight physics interaction (wall raycast + pre-validation cover the visible cases); carried momentum / mid-air nudges quantized away by force buckets; corpse pose vocabulary = 17 poses (config bump + re-bake to grow).
