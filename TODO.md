# Bladehold HUD Setup

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
