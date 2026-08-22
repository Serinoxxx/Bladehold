# Bladehold Unity Editor Wiring

## Slayer Gate-Assault Objective, Generic Enemy Intro Cinematic, Boss Health Bar & Damage Retaliation — Unity Editor Wiring

### The C# is done
Implemented a modular special enemy introduction system, objective integration, gate-charging AI, and boss health bar:
- **`AITargetSelector.cs`**: Added `ignorePlayer` (when true, beelines gate regardless of player distance) and `SetPlayerTargetOverride(float durationSeconds)` (temporarily overrides targeting to player for retaliation).
- **`EnemyDamageRetaliation.cs`**: Subscribes to `Health.OnDamaged`. When cumulative damage received reaches a configurable threshold (`damageFractionPerTrigger`, default 25% max HP), triggers `targetSelector.SetPlayerTargetOverride(10f)` and optional `retaliationFeedback`, turning the boss around to chase and attack the player for 10s before resuming gate assault.
- **`SlayerDashAttack.cs`**: Updated to resolve attack range and dash trajectory toward `AITargetSelector.TargetPosition` (gate or player) rather than hardcoded player transform, damaging both gates and players in the swept lane.
- **`SpecialEnemyIntro.cs`**: Component on boss/special enemies configuring display name (`"SLAYER"`), taunt trigger (`"Taunt"`), and camera focus point.
- **`EnemyIntroController.cs`**: Scene singleton managing cinematic intro sequences: pauses timescale (`Time.timeScale = 0`), locks player movement/attack/camera pivot components, sets enemy animator to `UnscaledTime` and fires the taunt trigger, switches Cinemachine to `Enemy Intro Camera` (priority 30), triggers letterbox bars and title, holds for 3.0s unscaled time, then restores player controls, resets camera priority, and unpauses.
- **`EnemyIntroUI.cs`**: Cinematic overlay with top and bottom black bars that rapidly slide in (0.3s) and slowly drift horizontally during the 3s hold, plus white enemy name text sliding in at the top.
- **`BossHealthBarUI.cs`**: Screen-space top-center HUD health bar modeled after `PlayerHealthBarUI`, with `MMProgressBar`, text values, and smooth canvas group fade in/out on spawn/death.
- **`DefeatSlayerObjective.cs`**: `ISurvivorsObjective` implementation spawning Slayer, configuring gate assault AI and retaliation, triggering cinematic intro, binding boss health bar, and tracking completion.
- **`EnemyManifest.cs`**: Added `SpecialEnemyIntro` and `EnemyDamageRetaliation` to Slayer's manifest entry.

### Wiring checklist
- [x] **Scene GameObject — Enemy Intro Camera**:
  - [x] In `Bladehold Test Scene.unity` and `Bladehold Survivors Scene.unity`, created a child GameObject named `Enemy Intro Camera` with `CinemachineCamera`.
  - [x] Set `Priority` to 0 (default resting priority).
  - [x] Added `CinemachineThirdPersonFollow` framing component configured to frame enemy intros.
- [x] **Scene GameObject — EnemyIntroController**:
  - [x] Created `EnemyIntroController` GameObject in both `Bladehold Survivors Scene.unity` and `Bladehold Test Scene.unity`.
  - [x] Attached `EnemyIntroController.cs`.
  - [x] Wired `introCamera` to the `Enemy Intro Camera` GameObject.
  - [x] Wired `introUI` to the `EnemyIntroUI` instance in the HUD Canvas.
  - [x] Wired `bossHealthBar` to the `Boss Health Bar` instance in the HUD Canvas.
- [x] **HUD Canvas — EnemyIntroUI Overlay**:
  - [x] Under `Bladehold HUD.prefab` > `ScreenSpace`, created `Enemy Intro UI` with `RectTransform` (stretched full screen) and `CanvasGroup`.
  - [x] Added child `Top Bar` (Image, black, anchored top stretch: Min 0,1 / Max 1,1 / PosY 0 / Height 110).
  - [x] Added child `Bottom Bar` (Image, black, anchored bottom stretch: Min 0,0 / Max 1,0 / PosY 0 / Height 110).
  - [x] Added child `Name Container` under Top Bar with `TextMeshProUGUI` (White, bold, centered, size 38, Grenze font).
  - [x] Attached `EnemyIntroUI.cs` to `Enemy Intro UI` and wired `canvasGroup`, `topBar`, `bottomBar`, `enemyNameText`, and `nameContainer`.
- [x] **HUD Canvas — Boss Health Bar**:
  - [x] Under `Bladehold HUD.prefab` > `Screen_HUD_Adventure_01/ScreenSpace/Top/`, created `Boss Health Bar`.
  - [x] Followed `Player Health Bar` structure: `CanvasGroup`, frame/background image, `MMProgressBar` with `Fill` Image (`Filled` horizontal) and `DelayedBar` image, `TextMeshProUGUI` for `bossNameText` ("SLAYER") and `healthText` ("200 / 200").
  - [x] Attached `BossHealthBarUI.cs` and wired `canvasGroup`, `progressBar`, `bossNameText`, and `healthText`.
- [x] **Slayer Enemy Variant Prefab**:
  - [x] Regenerated via `EnemyPrefabGenerator.GenerateAll()`.
  - [x] Verified `AITargetSelector`, `SpecialEnemyIntro` (name: `"SLAYER"`, tauntTrigger: `"Taunt"`), and `EnemyDamageRetaliation` (threshold: `0.25`, duration: `10`) are attached and wired.
- [x] **Objective Setup**:
  - [x] In `Bladehold Survivors Scene.unity`, added `DefeatSlayerObjective` to `SurvivorsObjectives` and added to `SurvivorsObjectiveManager`'s `repeatingObjectiveComponents` list.
  - [x] Wired `slayerPrefab` to `Assets/Bladehold/Bladehold Prefabs/Slayer Enemy Variant.prefab`.

### Manual verification (Slayer Objective & Intro Cinematic)
- [x] Headless C# compilation verified with `dotnet build Assembly-CSharp.csproj` (0 errors).
- [x] Headless Editor compilation verified with `dotnet build Assembly-CSharp-Editor.csproj` (0 errors).
- [ ] Playtest: Trigger Slayer spawn (via `DefeatSlayerObjective` or scene spawn).
- [ ] Playtest: Verify game pauses (enemies/projectiles freeze) while Slayer plays his taunt animation unscaled.
- [ ] Playtest: Verify Cinemachine smoothly transitions to the Enemy Intro Camera framing Slayer.
- [ ] Playtest: Verify top and bottom letterbox bars slide in rapidly (0.3s) and slowly drift horizontally during the 3s hold.
- [ ] Playtest: Verify "SLAYER" title text slides in in white at the top.
- [ ] Playtest: Verify after 3.0s, camera blends back to player, controls unlock, and gameplay unpauses.
- [ ] Playtest: Verify the Boss Health Bar appears at the top center of the HUD displaying Slayer's HP.
- [ ] Playtest: Verify Slayer ignores the player by default and charges straight at the castle gate.
- [ ] Playtest: Attack Slayer until he takes 25% max HP damage -> verify he enrages/retaliates, turning around to chase and attack the player for 10s.
- [ ] Playtest: After 10s, verify Slayer turns back to the gate and resumes charging it.
- [ ] Playtest: Defeat Slayer -> verify objective completes, boss health bar fades out smoothly, and run proceeds normally.
