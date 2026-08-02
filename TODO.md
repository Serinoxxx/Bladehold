# Bladehold HUD Setup

## Summon Mount — Unity Editor wiring

### The C# is done
Added `PlayerSummonMount` to handle summoning a mount on demand, and `SummonMountUI` for the HUD icon and cooldown display. Modified `PlayerMount` to unlock riding by default (`HorseRidingUnlocked` base is 1f). Replaced the "Saddle Up" node in `SkillTree.csv` (and Berserker/Mage variants) with "Summon Mount", which unlocks the ability via `SummonMountUnlocked`, and added stats for `SummonMountDuration` and `SummonMountCooldown` to `StatType.cs`. This uses the `add-skill-line` conventions, intercepting the `Dismount` action in `InputReader` to spawn the horse and auto-mount.

### Wiring checklist
- [ ] On `Assets/Bladehold/Bladehold Prefabs/Player.prefab`:
  - Add `PlayerSummonMount` component.
  - Set `horsePrefab` to `Assets/Bladehold/Bladehold Prefabs/Horse/Horse.prefab`.
  - Create three child objects for `MMF_Player` feedbacks: `SpawnSmoke`, `DespawnSmoke`, `ErrorSound`. Assign them to the script.
  - In `SpawnSmoke` and `DespawnSmoke`, add a particle effect (e.g. `FX_Smoke_White_Large_01.prefab` via `MMF_ParticlesInstantiation`) and a sound effect (`MMF_AudioSource`).
  - In `ErrorSound`, add an `MMF_AudioSource` with an error/buzz sound.
- [x] On `Assets/Bladehold/Bladehold Prefabs/UI/Bladehold HUD.prefab`:
  - Create a new UI button object in the bottom section near the weapon icons (or duplicate an existing one).
  - Add `SummonMountUI` component.
  - Assign the visual components: `skillIcon` (the main sprite image), `radialFillImage` (set image type to Filled, Radial 360), `timerText` (TextMeshProUGUI), and `keybindIcon` (the small prompt image).
  - Set `keyboardSprite` to the Synty 'X' key sprite, and `gamepadSprite` to the Synty 'B/Circle/East' sprite.
  - Add two `MMF_Player` child objects for `cooldownFinishedFeedback` (e.g., `MMF_PunchScale`) and `activatedFeedback`, and assign them.
  - Create the `SummonCastBar` in the middle of the screen using `MMProgressBar`.
  - Attach `SummonCastBarUI` and wire the progress bar, label ("Summoning Mount"), and CanvasGroup. Add MMF_Players for cast start/finish/cancel juice.
- [ ] **HUMAN:** In **Bladehold > Skill Tree Editor**, drag the horse/summon mount icon sprite to ensure it's registered in `SkillTreeSO`'s icons array under the name `Warriorskill_43_nobg` (or whichever sprite you choose).

### Manual verification (Summon Mount)
- [x] Playtest: Ensure the summon mount icon appears in the HUD only if you purchase the skill or give yourself `SummonMountUnlocked = 1`.
- [x] Playtest: Pressing X while unmounted begins a 2s cast.
- [x] Playtest: The Cast Bar appears, fills up, says "Summoning Mount", and the player plays the Cheer animation.
- [x] Playtest: If you move during the cast, or take damage, the cast is cancelled (bar hides, error sound).
- [x] Playtest: When cast finishes, it spawns a horse, plays smoke/sound, and automatically mounts the player.
- [x] Playtest: The HUD icon shows the active duration (cyan color, decreasing timer).
- [x] Playtest: When the duration expires, the player is automatically dismounted, the horse vanishes in smoke, and the ability goes on cooldown.
- [x] Playtest: Pressing X early dismounts the player. The horse stays alive until its duration expires, then it despawns.
- [x] Negative case: Cannot summon a new horse while one is already active or while on cooldown.

## C# Implementation complete
- [x] `WaveSpawner.cs`: Added `WaveGoblinTotal` and `KilledThisWave` to allow tracking wave progress in real time.
- [x] `WaveClearedBannerUI.cs`: Created to handle the `Quest Complete / Wave Cleared` UI elements (tracking and animating Gold/Kill counts with `MMF_Player`).
- [x] `ObjectiveTrackerUI.cs`: Created to track the objective string ("Slay all enemies: x/y") by reading `WaveSpawner`.

## Unity Editor wiring
- [x] In `Bladehold Test Scene`, delete the old `HUD Canvas` / HUD root.
- [x] Instantiate the `Bladehold HUD` prefab into the scene.
- [x] Add `WaveClearedBannerUI` script to the `Top Left -> Quest Complete / Wave Cleared` object in the new HUD.
    - [x] Assign `Wave Cleared Text`, `Gold Earned Text`, and `Enemies Killed Text` from its children.
    - [x] Assign the `Banner Animation Feedback` (add an `MMF_Player` if one isn't already present for popping the banner in/out).
- [x] Add `ObjectiveTrackerUI` script to the `Objective_List` object (or `HUD_FantasyWarrior_Objectives_02`).
    - [x] Assign the `Objective Header Text` ("HOLD THE GATE: WAVE X").
    - [x] Assign the `Objective Progress Text` ("Slay all enemies...").
- [x] Wire the `Ult Meter`: add `UltimateBarUI.cs` to it (or configure if already present) and point it to the relevant fill Image/Text.
- [x] Wire the `Player Health Bar`: add `PlayerHealthBarUI.cs` / `HealthBarUI.cs` as appropriate and ensure it finds `Player.Instance.Health`.
- [x] Wire the `Currency` text: add `CoinUI.cs`.
- [x] Wire the `Fortress Gate Health` (Top Right): add `HealthBarUI.cs` and ensure its `MMHealthBar` component points to the scene's Gate object.
- [x] Wire the `Horse Health/Stamina`: add `HorseBarGroupUI.cs`, `HorseHealthBarUI.cs`, and `HorseStaminaUI.cs` to the corresponding elements if the player is mounted.

## Manual verification
- [ ] Enter Play Mode.
- [ ] Verify HUD is visible and all bars (Health, Gate, Ult) display correctly.
- [ ] Complete Wave 1.
- [ ] Verify the wave banner pops in correctly, displays accurate gold and kill counts, and animates out.
- [ ] Check that the objectives progress updates with each kill.
