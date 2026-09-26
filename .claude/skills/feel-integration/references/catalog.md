# Feel feedback catalog (Bladehold subset)

Bladehold is URP + Cinemachine 3 on PC, uGUI + TMP. HDRP, Post Processing v2, UI Toolkit, 2D-light and Nice Vibrations (mobile haptics) feedbacks are left out on purpose. Full list: the MMF_Player "Add new feedback" menu, or `Assets/Third Party/Feel/MMFeedbacks/`.

**Used in the project** (count of authored instances, 2026-09): MMSoundManager Sound 334, Particles Instantiation 122, Sound 57, Flicker 57, Scale Spring 43, Bladehold/Pooled Particle Burst 26, Cinemachine Impulse 24, Scale 13, Instantiate Object 7, Image Alpha 6, TMP Text Reveal 5, Timescale Modifier 4, Particles Play 4. Prefer these unless you need something else.

## Audio
- **MMSoundManager Sound**: house default. Plays a clip through `MMSoundManager` (auto-created if missing) with track (SFX/UI/Music), 2D/3D, random pitch/volume. Pitch can be set per play from code.
- **Sound**: standalone clip (Cached mode if no sound manager). Older prefabs use it; new work uses MMSoundManager Sound.
- **AudioSource**: play/stop a preexisting AudioSource. Use for a looping bed you start and stop.
- **MMSoundManager Track/Sound Fade/Control**: duck or fade music tracks (boss, victory).

## Particles / spawning
- **Particles Instantiation**: one-shot VFX (house settings in SKILL.md; looping sources need an `MMTimedDestruction` variant).
- **Particles Play**: play/stop a particle system already on the prefab.
- **Bladehold/Pooled Particle Burst**: `ParticlePool` burst whose count, speed and scale follow intensity; for blood and impact bursts.
- **Instantiate Object**: spawn any prefab (e.g. `VFX/KnockbackFlashLight`).

## Camera / time
- **Cinemachine Impulse**: screenshake. Needs a `CinemachineImpulseListener` extension on the Cinemachine camera; impulses are distance-based from the player's position.
- **Camera Shake**: needs an `MMCameraShaker` (or `MMCinemachineCameraShaker`) rig. Avoid in favour of Impulse.
- **Field of View / Camera Zoom**: need `MMCinemachineFieldOfViewShaker` / `MMCinemachineZoom` on the camera.
- **Timescale Modifier / Freeze Frame**: hitstop and slow-mo. Need an `MMTimeManager` (`Bladehold Prefabs/Managers/MMTimeManager.prefab`).
- **Flash / Fade**: need an `MMFlash` / `MMFader` UI element in the scene.

## Renderer / transform
- **Flicker**: flash a renderer's material colour (hit flash). Set the property name if the shader doesn't use `_Color`.
- **Material Set Property / Shader Controller**: drive a shader value.
- **Light**: pulse a light's intensity/colour (e.g. `ImpulseHitMMF`). The light must be active.
- **Scale / Scale Spring / Position / Rotation / Squash And Stretch**: bumps and pops on transforms.
- **Position / Rotation / Scale Shake**: need the matching `MM*Shaker` on the target.

## UI (uGUI + TMP)
- **Scale Spring / Scale**: button and banner pops.
- **CanvasGroup / Image Alpha / Image / Image Fill**: fades, tints, fills.
- **RectTransform Anchor / Offset / Size Delta**: slide panels in and out.
- **TMP Text Reveal / TMP Count To / TMP Color / TMP Font Size**: text effects (`TMP Count To` for currency count-ups).
- **Floating Text**: floaty text via a spawner. Damage numbers use DamageNumbersPro instead.

## Flow / misc
- **Pause / Holding Pause / Looper**: sequence control inside one player.
- **Feedbacks Player / MMF Player Chain**: trigger or sequence other players.
- **Set Active / Enable Behaviour / Collider**: toggle objects as part of a sequence.
- **Unity Events**: call a method at a point in the sequence (keep gameplay logic in code, not here).
- **Debug Log / Debug Comment**: notes and logging while tuning.
