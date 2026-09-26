---
name: feel-integration
description: Use when adding or changing any audio, VFX, screenshake, flash, hitstop or UI tween in Bladehold gameplay code — the house MMF_Player pattern (serialized player, PlayFeedbacks, Start validation, unscaled time) that replaced direct PlayOneShot/Instantiate/shake calls.
---

# Feedback goes through MMF (Feel)

CLAUDE.md rule: audio, particles/VFX, screenshake, flashes, hitstop and tweens are `MMF_Player` feedbacks **authored on the prefab**, triggered from code with `PlayFeedbacks()`. Feel is vendored at `Assets/Third Party/Feel/` (namespace `MoreMountains.Feedbacks`); don't modify it.

**Banned in gameplay code:** `AudioSource.PlayOneShot`, `AudioSource.PlayClipAtPoint`, `MMSoundManagerSoundPlayEvent.Trigger`, `MMCameraShakeEvent.Trigger`, moving `Camera.main` for shake, one-shot `Instantiate(vfxPrefab)` + `Destroy(vfx, t)`, and `AudioClip`/VFX-prefab fields on SOs. Plan 09 migrated almost everything; when you touch an older script that still does this, migrate it.

**Not feedback** (leave as-is): state visuals parented for a duration (status auras, dash trails, `EnemyStatusManager` fire/ice), gameplay spawns (damage zones, projectiles, telegraphs), looping audio shaped per frame (`HorseHoofbeatAudio`, battering ram roll).

## Code pattern

Precedents: `Fort/DefenseStructure.cs` (required players, `protected virtual ValidateFeedbackReferences()` so subclasses add theirs), `UI/WaveClearedBannerUI.cs`, `Enemies/SpecialEnemyIntro.cs` (optional roar), `Bosses/NecromancerConfrontationUI.cs` (unscaled UI).

```csharp
using MoreMountains.Feedbacks;

[Header("Feedback")]
[Tooltip("Boom + debris burst at the impact point.")]
[SerializeField] private MMF_Player impactFeedback;          // required
[Tooltip("Optional: windup hiss. Leave empty for silence.")]
[SerializeField] private MMF_Player windupFeedback;          // optional

private void Start()
{
    if (impactFeedback == null) Debug.LogError($"{name}: impactFeedback is not assigned.", this);
}

private void Impact(Vector3 point)
{
    if (impactFeedback != null) impactFeedback.PlayFeedbacks(point);   // positional one-shot
}
```

- **One `MMF_Player` per event**, a child GameObject named `<Event>MMF` (often under a `Feedbacks` child), wired to a serialized field. Never `GetComponentInChildren<MMF_Player>()`: on the Player it finds the damage flicker (a real bug plan 09 fixed).
- **Required vs optional:** a required player that's missing `LogError`s in `Start` but **doesn't set `anyError` or disable gameplay** (a silent sound must not break combat). Optional ones say "Optional … leave empty" in the tooltip and are just null-checked at the call.
- **Position:** `PlayFeedbacks(worldPos)` for anything spatial; `PlayFeedbacks()` for 2D/UI. Pass intensity with `PlayFeedbacks(pos, intensity01)` when a feedback scales (see `MMF_PooledParticleBurst`).
- **Runtime-added components** (`AddComponent<T>()` at runtime, e.g. `BubbleShield`, `CryptSkeletonAI`) can't own authored players: the spawner holds them and passes them in.
- **Per-play tweaks** are fine from code (e.g. `SurvivorsStatsPanelUI` sets the tick sound's pitch before each play); building feedback lists at runtime is not.
- **Looping feedback** (a channel hum): `PlayFeedbacks()` to start, `StopFeedbacks()` to end (`PrincessBossController.channelFeedback`).
- **Shared tuning:** if several scenes hold the same feedback, make it a prefab and nest it (`Waves/GateDestructionMMF`, `Bladehold Prefabs/Bosses/*MMF`); editor scene builders nest the same prefab.

## Unscaled time (paused / frozen phases)

Anything that plays while `Time.timeScale == 0` or during a freeze (draft picks, shop, pause, enemy intro cinematic, death/victory screens, confrontation UI) must run unscaled, or delays and tweens stall:

- Author it on the player: **Settings → Force Timescale Mode = on, Forced Timescale Mode = Unscaled** (`NecromancerDefyLaughMMF`, the Slayer roar, gate/ram death).
- Or force it in code in `Start` when the prefab can't be trusted (`UI/SurvivorsCardUI.cs` `ForceUnscaledTime`: sets `ForceTimescaleMode`, `ForcedTimescaleMode` and `PlayerTimescaleMode`).
- Particle systems spawned during a freeze need an `*Unscaled` VFX variant (`VFX/FireExplosionUnscaled`, `ImpactLargeUnscaled`), not code that flips `useUnscaledTime`.

## Authoring the player (house recipe)

Do it through Unity MCP when connected (see `/unity-editor-mcp`); otherwise list it in the plan's `plans/editor/` checklist via `/editor-wiring-todo`.

- **Sound:** `MMSoundManager Sound` (the house default; `MMSoundManager` auto-creates if the scene lacks one). 2D for player-side/UI sounds, 3D for world sounds. Volume caps at 1.
- **One-shot VFX:** `Particles Instantiation`, Mode `OnDemand`, `CachedRecycle` off, `PositionMode = Script`, `NestParticles` off, `ForceStopAction = Destroy`. If the source prefab **loops**, it never stops: make a variant with `MMTimedDestruction` (e.g. `VFX/FireBurst` 2 s) or it stays in the level forever.
- **Pooled / damage-scaled / directional bursts:** the house `Bladehold/Pooled Particle Burst` feedback (`DamageSystem/MMF_PooledParticleBurst.cs`, uses `ParticlePool`). For `UseOwnerRotation`, rotate the player's GameObject just before playing.
- **Screenshake:** `Cinemachine Impulse` (the camera is Cinemachine 3; copy the mace slam's impulse settings as a starting point). Plain `Camera Shake` needs an `MMCameraShaker` rig.
- **Hit flash on meshes:** `Flicker`. **UI pops:** `Scale Spring` / `Scale`, `CanvasGroup`, `Image Alpha`, `TMP Text Reveal`. **Hitstop:** `Timescale Modifier` (needs an `MMTimeManager` in the scene: `Bladehold Prefabs/Managers/MMTimeManager.prefab`).
- Building players from editor code: a fresh `AddComponent<MMF_Player>()` has a null `FeedbacksList`, so create it first; clone an authored feedback with `JsonUtility` and give it a new `UniqueID`. Keep such helpers in a temporary `Editor/` script and delete it afterwards.

Pick feedback types from `references/catalog.md` (URP-relevant subset, with the scene components each one needs).

## Verify

- Grep your files: `PlayOneShot|PlayClipAtPoint|MMSoundManagerSoundPlayEvent|MMCameraShakeEvent|Instantiate\(.*(vfx|Vfx|VFX|particle)` should return nothing new.
- `/compile-check`, then play the event in Play mode (or the MMF_Player inspector's Play button) and read the console for the `LogError`s above.
- Don't save a scene after running the mechanic benchmark in it (it leaves spawned junk and MMF bursts behind).
