# 16: Tutorial level (first launch onboarding)

**Goal:** a first-time player learns move → quick attack → heavy attack → bow → melee fight → build a tower → survive a wave in **about 5 minutes**, then drops into the normal loop. Everything else (elements, draft cards, other towers, special enemies, bosses, wave choice cards) is learned in play. **First-encounter hints** cover gate damage, the Meta Area and gate repair / tower restock (Phase I).

This is a deliberate exception to the feature freeze: onboarding is a demo requirement (Phase 2's exit test is "a friend can play unassisted").

**Unity MCP is allowed for execution** (copying rooms out of the dungeon demo, placing prefabs, wiring, the NavMesh bake, screenshots). Follow `/unity-editor-mcp`. Only human-taste items (MMF timing and shake strength, UI sign-off) go to the editor checklist.

## The flow

```
Main Menu "New Game" ─► tutorialCompleted? ──yes──► Meta Area (as today)
                              │ no
                              ▼
 T1 Dungeon (Bladehold Tutorial Dungeon.unity)
   1 Move        walk to waypoint
   2 Quick atk   tap LMB, cut down 3 hanging banners blocking an archway
   3 Heavy atk   hold + release LMB, smash the locked door (charged hit only)
   4 Bow         hold RMB + LMB, shoot 3 ropes → platform drops over the gap
                 (free ammo crate in the room in case arrows run out)
   5 Exit        walk through the door ─► T2
 T2 Arena (Bladehold Tutorial Arena.unity)
   6 Fight       6 goblins rush in (3 rounds of 2); kill them all  (death = reload T2)
   7 Exit        cage gate slides open ─► T3
 T3 Outer field (Bladehold Tutorial Gate.unity, generated once)
   8 Build       walk to the marked plot, [E], build an Arrow Tower
   9 Ready       hold [T] to start the wave
  10 Wave 1      10 goblins, gate + tower + you
  11 Mount       summon the horse (X)
  12 Waves 2-3   hold Ready, then 15 and 20 goblins (no wave cards)
      death ─► Meta Area (as today)      victory ─► Campaign Map (as today)
```

## Decisions (Lance, 2026-09-30)

| Question | Decision |
|---|---|
| Heavy door threshold | **80% of a full charge**: 8 damage on the stock sword (5 base × 1.6), instead of >6, which a slow click could hit. See Numbers. |
| Loadout | **No override.** Blade + bow is the default, and the tutorial runs before the Meta Area where weapons change. On a replay after unlocking other weapons the player still has *a* ranged weapon, and the door check is charge-based (works with any melee weapon). |
| Out of arrows | A **free ammo crate** (`AmmoChest` with `goldCost = 0`) in the chasm hall. |
| When is it "done"? | `SaveData.tutorialCompleted` is set on **entering T3**. |
| Skip / replay | **Skip Tutorial** in the pause menu (sets the flag, loads the Meta Area). A **Replay Tutorial** button in Settings. |
| Run state in T3 | `RunSession.StartNewRun()` runs on entering T3, with no campaign. Victory goes through `DeathScreen`'s no-campaign branch → fresh campaign → map. Gold and supply don't carry over. Goblin Blood does. |
| Mount / ultimate | Ultimate off in all three scenes (`SceneAbilityRules`). **Mount on in T3** (2026-10-01 playtest): after wave 1, a `MountStep` teaches summoning the horse before waves 2-3. |
| Cobwebs | **Hanging banners** instead: no Synty pack has cobweb art. |
| T3 scene | **Generate once with `/generate-defense-scene`, then hand/MCP-edit it like any other scene.** Never regenerate it: add a warning to the spec asset's name/notes and to the skill's stock-scenes list. |
| Bow charge time | **Full draw = 1 s.** See Numbers: this is a global change. |
| Enemies in T2 | A small `TutorialEncounter` spawner (see Code). Not `SurvivorsSpawner`. |

## Progress

**Session 1 (2026-09-30): Phases A-I done** (Unity MCP connected; Unity and `dotnet build` both clean). The whole T1 → T2 → T3 → Campaign Map flow was play-tested by driving it from code. The human-judgement leftovers are in [`plans/editor/16-tutorial.md`](editor/16-tutorial.md).

**Code:**
- `Bladehold Scripts/Tutorial/`:
  - Core: `TutorialDirector`, `TutorialStep` + `ReachAreaStep` / `DestroyTargetsStep` / `KillEnemiesStep` / `BuildDefenseStep` / `WaveEventStep`.
  - Pieces: `TutorialEncounter`, `TutorialBreakable`, `TutorialDropPlatform`, `TutorialGateOpener`, `TutorialSceneExit`, `TutorialFallRespawn`.
  - UI and state: `TutorialHintUI`, `TutorialHint`, `TutorialRun`, `TutorialConfigSO`, `TutorialTelemetry`.
  - First-time tips: `FirstTimeHints` + `FirstTimeHintsWatcher`.
- `UI/IWaypointSource`.

**Hooks into existing code:**
- `SaveData.tutorialCompleted` / `seenHints`, with the migration in `SaveSystem.Load`.
- `MainMenuManager` routing + `OnReplayTutorialClicked`.
- `DeathScreen` reload branch and tutorial victory → `StartNewRun`.
- `TowerPlot.OnBuilt`.
- `ObjectiveWaypointTrackerUI.RegisterSource`.
- `RoundPacingConfigSO.rollWaveClans`.
- `PauseMenuView.skipTutorialButton`.
- Accessors: `AmmoChest` free prompt, `GameLoopManager.IsChoosingCard`, `GateRepairStation.Instance`, `SpiritNPC.Instance`, `Gate.Health`.
- `DefenseSceneDefaults` gets a Kingdom palette + the tutorial spec.

**Deviations from the plan above:**
- **Save migration:** `runsAttempted` is never incremented (it's always 1), so the plan's migration by run count wouldn't work. Instead, any save file whose JSON has no `tutorialCompleted` key loads as completed.
- **No `forceFodderOnly` flag:** `fodderShare = 1` already forces every spawn to be the fodder goblin (`SectorSpawnRules.MustSpawnFodder`).
- **Fixed waves could still roll a clan modifier**, so `RoundPacingConfigSO.rollWaveClans` (off on the tutorial pacing) strips it.
- **No `SpawnEnemyAt` overload:** `TutorialEncounter` instantiates from the prefab map + roster itself.
- **T1/T2 are kit-built shells dressed with props copied from the demo rooms**, not whole copied demo rooms. The demo's rooms overlap across levels, so they don't lift out cleanly.
- **T2 walks and bakes on a flat `NavFloor` slab with the tile colliders stripped.** The Synty tiles' bevelled edges split the NavMesh at every 5 m seam, which left goblins with partial paths.
- **`BuildDefenseStep` also completes if the wave starts without a build**, so holding Ready early can't soft-lock the tutorial.

## Numbers (checked against current code)

- **Sword:** base `SwordDamage` 5 (`DamageSO.asset`), sword `chargeTimePerLevel` 0.33 (`Bladehold Config/Weapons/sword.asset`).
  - `PlayerAttack.RecomputeMultiplier` = `0.1 + 1.9 × min(t / 0.33, 1)`. A tap is ≈ 0.1-0.3× (0.5-1.5 damage), and a full charge is 2.0× (10 damage).
  - **Door check:** `PlayerAttack.AttackDamageMultiplier` (latched on release) must be **≥ 1.6** (80% charge, ≈ 0.27 s of hold on the sword). This is the same as "≥ 8 damage" on the stock sword.
  - Checking the multiplier instead of raw damage makes it weapon-agnostic (mace/axe on a replay) and immune to meta-perk damage.
- **Banners:** 0.3 HP each, so any tap cuts one. Hang them across the archway at chest height, with a blocker collider that goes when the last one falls.
- **Bow:** Player.prefab uses `DamageSystem/BowSO.asset`: `baseDamage` 2, 3 charge levels, and **`chargeTimePerLevel: 0`**.
  - Today, 0 means **every arrow is an instant full-power draw**.
  - Set it to **0.333** (3 levels = 1 s full draw), as decided.
  - **This changes bow feel and DPS game-wide, not just in the tutorial.** Playtest a normal sector afterwards and note it in the CHANGELOG.
  - `Bladehold Config/Weapons/bow.asset`'s `chargeTimePerLevel: 0.25` is not read for the bow (only melee definitions feed `PlayerAttack.SetChargeTimePerLevel`). Leave it, or delete the field's value to avoid confusion.
- **Ropes:** 0.1 HP. A quick-release arrow does 2 × 0.1 = 0.2, so any arrow cuts one. Place them across the gap, **≥ 5 m from any standable point**, so only a ranged weapon reaches.
- **Ammo:** the quiver starts at 20 (`RunSession.CurrentAmmo`). The crate gives 5 per press, free. If ammo drops below the ropes still standing, the step hint adds a line: "Out of arrows? Grab more from the crate" (the crate gets the waypoint).
- **Arena:** 2 stock goblins (10 HP). That's 2 full charges or ~8-10 taps each.
- **T3:** 60 starting supply, and the Arrow Tower costs 30. The player can build 2 arrow towers. The waypoint marks one plot and the second is free to find. The wave is 20 goblins, with at most 20 alive.

## Code (new files under `Bladehold Scripts/Tutorial/` unless noted)

Follow CLAUDE.md house rules: `Start` validation with `anyError`, tunables on an SO, MMF for all feedback (one `<Event>MMF` child per event, `[SerializeField]`), `Loc.Get` for text, no code-built visuals.

### Core
- [ ] **`TutorialDirector`** is a scene singleton.
  - It holds an ordered `List<TutorialStep>`, runs one step at a time, and raises `OnStepStarted/Completed`.
  - It pushes the step's hint to `TutorialHintUI` and its waypoint to the tracker.
  - Field: `reloadOnDeath` (T2 only). When the final step completes, it triggers the scene exit.
- [ ] **`TutorialStep`** (abstract MonoBehaviour):
  - Fields: `waypointTarget`, `hintKey` + `englishHint`, `hintAction` (InputActionReference for the glyph), `startMMF` / `completeMMF` (optional), `completeDelay`.
  - Subclasses:
    - `ReachAreaStep`: a trigger volume, player only.
    - `DestroyTargetsStep`: a list of `Health`, done when all are `IsDead`, with a counter in the hint. It has an optional `ammoHelper` (an `AmmoChest` plus a low-ammo hint key) for the rope step.
    - `KillEnemiesStep`: drives a `TutorialEncounter` and completes when it reports all dead.
    - `BuildDefenseStep`: completes on `TowerPlot.OnBuilt`.
    - `ReadyStep`: completes on `GameLoopManager.OnWaveStarted`.
    - `SurviveWaveStep`: completes on `OnVictory`, then hands off to the normal flow.
- [ ] **`TutorialConfigSO`** (`Scriptable Objects/Tutorial/`) holds the scene names, `heavyMinChargeMultiplier` (1.6), the arena enemy id + count, and the T3 wave size (20).
- [ ] **`TutorialRun`** static (session state): `Active`. The director sets it; Skip, T3 victory and T3 death clear it.

### Enemies in T2: `TutorialEncounter`
Don't put a `SurvivorsSpawner` in T2. Its `Start` auto-runs `StartWave(1)` when there's no `GameLoopManager` (`SurvivorsSpawner.cs:239`), which would start a real wave.

`TutorialEncounter` repeats the core of `SurvivorsSpawner.SpawnEnemyForType` (`:837`) for a fixed list:
- Look up the prefab and definition for `"goblin"` in the same `EnemyPrefabMap` / `EnemyRosterSO` assets the spawner uses. Serialize them, validated in `Start`.
- For each spawn point: `NavMesh.SamplePosition`, then `Instantiate`, then `EnemyDefinitionApplier.Apply(enemy, def)`. There's no `ApplyWaveModifiers`, since there's no wave.
- Subscribe to each `Health.OnDied` and raise `OnAllDead` when the count reaches 0.
- Optional `introMMF` per spawn (a roar/bark and a dust puff at the alcove).
- Targeting needs no change: with no `Gate` and no objective manager in the scene, `AITargetSelector` falls through to `Player.Instance` (`AITargetSelector.cs:247-256`).
- **Verify in Play mode** that nothing else on the goblin prefab assumes `GameLoopManager`/`SurvivorsSpawner` exists. Its kill gold goes to the `RunSession` statics, which T3's `StartNewRun` resets, so that's harmless.
- If a shared helper falls out of this naturally, extract `SurvivorsSpawner.InstantiateEnemy(prefab, def, pos)` and use it in both. Otherwise keep the duplicate small.

### Interactables
- [ ] **`TutorialBreakable`**: `[RequireComponent(Health)]`.
  - `requireChargedMelee` (bool) + `minChargeMultiplier`. It uses `Health.TryBlockDamage` to block hits where `!damage.isProjectile` and `Player.Instance`'s `PlayerAttack.AttackDamageMultiplier < minChargeMultiplier`. Arrows never break the door either.
  - A blocked hit plays `blockedMMF` (a thunk, a small spark, a tiny impulse). After 2 blocks it swaps the hint to "Hold to charge a heavy attack!".
  - On `OnDied` it plays `breakMMF`, swaps the intact root for the broken root (both authored), and turns colliders off.
  - Used for the banners, the door and the ropes.
- [ ] **`TutorialDropPlatform`**: watches the rope `Health`s. When all are dead:
  - It plays `dropMMF`: Position/Rotation feedbacks swing the platform down, then a creak, a thud, `FX_Dust_Big_01` and a strong impulse.
  - Then it turns on the platform's walk collider and turns off the gap barrier.
- [ ] **`TutorialGateOpener`**: `openMMF` (the portcullis slides up, chain rattle, falling dust, medium impulse), then turns off the blocker.
- [ ] **`TutorialSceneExit`**: an `Interactable` modelled on `RestAreaGate`, locked until its step is active. It loads the next scene with `LoadingScreenManager.LoadScene`, falling back to `SceneManager.LoadScene`.
- [ ] **`TutorialFallRespawn`**: a pit trigger. It fades and returns the player to the last waypoint, so T1 can't be failed.
- [ ] **`AmmoChest`** (existing, `Economy/AmmoChest.cs`): when `goldCost == 0`, the prompt reads "Take {0} arrows" (new key `ammo_chest.prompt_free`) instead of "Buy 5 arrows (0g)", and skips the gold check.

### Hooks into existing code (small, additive)
- [ ] **Waypoints:** a static `IWaypointSource` registry that `ObjectiveWaypointTrackerUI` also polls, beside the objective and preview sources, which stay unchanged. `TutorialDirector` implements it. Add a `Tutorial` icon type.
- [ ] **`TowerPlot.OnBuilt`** (static `Action<TowerPlot>`), raised at the end of `BuildDefense`.
- [ ] **`DeathScreen.HandlePlayerDied`:** if `TutorialDirector.Instance?.ReloadOnDeath`, skip run-over and play a short fade, then reload the active scene.
- [ ] **`SaveData.tutorialCompleted`** (default false). Treat saves with `runsAttempted > 0` as completed. Include it in `ResetProgress()`.
- [ ] **`MainMenuManager`** (:152, where `sceneToLoad` is picked): if `!tutorialCompleted`, load T1.
- [ ] **Pause menu:** Skip Tutorial (only when `TutorialRun.Active`). **Settings:** Replay Tutorial.
- [ ] **T3 wave, goblins only:** a tutorial `RoundPacingConfigSO` (`wavesPerRound = 1`, quota 20, `fodderShare = 1`) with a new `forceFodderOnly` flag honoured by `SurvivorsSpawner`, because `SectorSpawnRules` can still roll other T1 types.
  - Verify `WaveChoiceConfigSO` (`finalWaveNumber = 5`) opens no draft or captain wave when `TotalWaves = 1`. Wave 1 should be last, so `ClearActiveWave` → `TriggerVictory`.
- [ ] **Bow:** `DamageSystem/BowSO.asset` `chargeTimePerLevel` 0 → 0.333.

### UI
- [ ] **`TutorialHintUI`**: a top-centre panel with an `InputGlyph` (switches keyboard/pad and honours rebinds), a hint line, and an optional counter and second line.
  - Built as a prefab via `/ui-mockup`, reusing `HintEntryView`.
  - Animations are MMF: slide-in, a complete tick + chime (unscaled), and a counter punch.
  - It lives in the HUD prefab and stays hidden unless a director or first-encounter hint is active.
- [ ] **Strings:** `tutorial.*` keys in `Resources/Localization/Strings.csv`, English only for now:
  - move, banners, heavy, heavy_nudge, bow, bow_aim, bow_ammo, fight, dodge, build, ready, wave, skip, replay.
  - The Phase I hint keys.

### Telemetry
- [ ] Send a GameAnalytics progression event per step start/complete and on skip (`/telemetry-analytics`). That gives the Next Fest drop-off funnel.

## Scenes

### T1: Tutorial Dungeon (rooms copied from the Synty dungeon demo via MCP)
Source: `Assets/Synty/PolygonDungeon/Scenes/Demo.unity`.

**Method:**
1. Open the demo additively.
2. Screenshot it from above to pick sections.
3. Copy the chosen root objects/groups into `Bladehold Scenes/Bladehold Tutorial Dungeon.unity` with `execute_code`. Use `Object.Instantiate`, or `PrefabUtility.InstantiatePrefab` + copy transforms, so the prefab links survive.
4. Offset them into one linear path of about 60-80 m, and join the gaps with `SM_Env_Wall_*` / `SM_Env_Tiles_*` modules.
5. Close the demo **without saving**.

Also copy the demo's lighting settings and post-process volume.

| Section | Source idea | Gameplay pieces |
|---|---|---|
| Cell (spawn) | a cell room with `SM_Env_Door_Bars_01` open | Player spawn, step 1 waypoint at the cell door |
| Corridor + archway | a corridor + `SM_Env_Wall_Archway_01` | 3 hanging banner `TutorialBreakable`s filling the arch (a banner/cloth prefab from Dungeon or Kingdom; the broken variant is a torn stub + a fallen cloth), a blocker collider |
| Guard room | ends in `SM_Env_Door_Large_Wood_01` in `SM_Env_Door_Frame_01` | Door `TutorialBreakable` (`requireChargedMelee`), broken = `SM_Env_Rubble_Plank_*` + `SM_Prop_Plank_*` |
| Chasm hall | a room with a pit; the platform is `SM_Env_Wood_Bridge_01` or `SM_Env_Wood_Platform_*`, held up by 3 ropes (`SM_Gen_Prop_Rope_*`, PolygonGeneric) on the far side | 3 rope breakables with fat capsule triggers so arrows register; `TutorialDropPlatform`; `TutorialFallRespawn`; torches lighting the ropes; a **free `AmmoChest`** (goldCost 0) by the gap edge |
| Exit | `SM_Env_DoorDouble_Round_01` | `TutorialSceneExit` → T2 |

**Scene contents:** the Player prefab, the HUD, the Cinemachine rig (match the gameplay scenes), `SceneAbilityRules` (mount/ultimate off), and `TutorialDirector`. No NavMesh is needed.

### T2: Tutorial Arena
- One open room (`Bladehold Tutorial Arena.unity`) from the same demo pieces.
- An entry door that slams shut behind the player at step 6 (MMF slam + impulse).
- 2 alcove spawn points (`TutorialEncounter`) and `SM_Env_Portcullis_01` on the exit (`TutorialGateOpener`).
- A **baked Humanoid NavMesh**, `SceneAbilityRules` (mount/ultimate off), and `TutorialDirector` with `reloadOnDeath = true`.
- Step 6's second hint line: "[Space] Dodge".

### T3: Tutorial Gate (generate once, then edit as a normal scene)
- **Spec** `Config/SceneGen/TutorialGate_DefenseSpec.asset`, duplicated from `OuterGate_DefenseSpec`:
  - Smaller valley, **no ravines**, **2 plots** near the gate, 1-2 spawns up the valley, and no objective band.
  - Player spawn inside the gate with the step 8 plot in view.
- **Palette** `Kingdom_DefensePalette.asset`, green and friendly:
  - Build `Kingdom_Grass_01/02.terrainlayer` from `PolygonFantasyKingdom/Textures/Ground/Grass_0x` (as `Arid_RockWall` is built), with the PNB Alpine `Dirt_01` for roads.
  - Kingdom trees, bushes/hedges and fences. Kingdom castle pieces if they fit the wall roles; otherwise the Alpine ones.
  - `propClusters` harvested from `Demo_ExteriorOnly_Optimized.unity`.
  - Add a `Create or Refresh Tutorial Gate Assets (Kingdom)` menu item in `DefenseSceneDefaults`.
- **Generate once**, read the validator and take screenshots. After that, **don't run the generator on this spec again.**
  - Name the spec `TutorialGate_DefenseSpec_GENERATED_ONCE` or add a header note.
  - List it in the skill under "hand-edited after generation".
- Then place the tutorial layer by hand/MCP:
  - `TutorialDirector` + steps, the waypoint anchors on the chosen plot and gate.
  - A priced `AmmoChest` just inside the gate (the normal one, learned in play).
  - The tutorial pacing config on `GameLoopManager`, and `SceneAbilityRules` (mount/ultimate off).
- Add it to Build Settings. It's not a campaign node.

## MMF feedback list (authoring checklist)

| Event | Sound | Particles | Impulse |
|---|---|---|---|
| Step complete | soft chime (UI, unscaled) | n/a | n/a |
| Banner cut | cloth tear | small dust / fabric scrap | n/a |
| Door blocked (weak hit) | dull wood thunk | `FX_Impact_Wood_01` small | tiny |
| Door smashed | heavy wood crash | `FX_Impact_Wood_01` + `FX_Dust_Big_01` | strong, short |
| Rope snap (×3) | rope twang | small debris | tiny |
| Platform drop | creak → thud | `FX_Dust_Big_01` at landing | strong |
| Free ammo crate | lid rattle | n/a | n/a |
| Arena door slam | iron slam | dust | medium |
| Goblin spawn-in | existing goblin bark | dust at alcove | n/a |
| Portcullis open | chain rattle + grind | `FX_Dust_Small_01` falling | medium |
| Scene exit | whoosh into the loading screen | n/a | n/a |

The T3 build, wave start and victory already have their feedback.

## First-encounter hints (Phase I)

These use the same `TutorialHintUI`, driven by a `HashSet<string> seenHints` in `SaveData` and a small `FirstTimeHints` service: `Show(id)` does nothing if the hint was already seen, then marks it seen and shows it for N seconds or until dismissed.

| Trigger | Hint (English fallback) | Hook |
|---|---|---|
| The gate takes damage for the first time | "The gate is taking damage! Gate damage carries across the whole run. If it falls, the run is over." | `Gate` health `OnDamaged` (first time), plus a waypoint pulse on the gate |
| First prep phase with gate HP below max | "Repair the gate with supply at the gate during prep. Supply spent here can't build towers." | `GameLoopManager` prep start + `RunSession.FortressGateCurrentHealth < Max` |
| First prep phase with a tower low on ammo | "Towers run out of ammo. Restock them with supply at the plot during prep." | Prep start + any `DefenseStructure` below its refill threshold (use the existing refill check) |
| First arrival in the Meta Area after a run | "Spend Goblin Blood with the Spirit for permanent upgrades." | The Meta Area scene loads with `runsAttempted ≥ 1`. The waypoint targets the Spirit NPC |
| Wave choice cards open for the first time | "Defence keeps you at the gate. Offence pays more but leaves your towers." | `SurvivorsCardSelectUI.OpenWaveChoice` |

Left to play (no hint needed): draft cards, elements, other towers, special enemies (`SpecialEnemyIntro`), bosses (confrontation UI). The mount gets a one-line control prompt the first time it's available.

## Phases

- [ ] **A. Hooks + fixes:**
  - `tutorialCompleted` + migration, `TutorialRun`, `TowerPlot.OnBuilt`, `IWaypointSource`, the `DeathScreen` reload branch, MainMenu routing, `forceFodderOnly`.
  - The free-crate prompt and the `BowSO` charge time.
  - `/compile-check`.
- [ ] **B. Tutorial components:**
  - Director, steps, `TutorialEncounter`, breakable, drop platform, gate opener, exit, fall respawn, config SO, strings.
  - `/compile-check`.
- [ ] **C. Hint UI** prefab via `/ui-mockup` (flagged for human review).
- [ ] **D. T3:**
  - Kingdom palette + spec, generate once, validator, screenshots.
  - Hand-place the tutorial layer.
  - Play-test 20 goblins vs 1-2 arrow towers.
- [ ] **E. T1 + T2** via MCP: copy rooms from the demo, place the gameplay prefabs, bake the T2 NavMesh, take screenshots. Close the demo unsaved.
- [ ] **F. Feel pass:** author the MMF players in the table. Lance tunes the timings and shakes.
- [ ] **G. Telemetry** step events. `/changelog` (tutorial + bow draw time).
- [ ] **H. Editor checklist** → `plans/editor/16-tutorial.md` (`/editor-wiring-todo`): human-taste leftovers only.
- [ ] **I. First-encounter hints:** `seenHints`, the `FirstTimeHints` service, the 5 triggers above.

**Manual verification:**
- [ ] A fresh save → New Game → T1. A save with `runsAttempted > 0` → Meta Area.
- [ ] Taps and arrows can't open the door. A ~0.3 s charged swing does. The nudge appears after 2 blocked hits.
- [ ] The sword can't reach the ropes. A quick-release arrow cuts each one. The platform drops only after the third.
- [ ] Empty the quiver on purpose → the low-ammo hint + crate waypoint appear. The crate gives 5 free arrows with a "Take 5 arrows" prompt.
- [ ] Falling into the pit respawns at the last waypoint.
- [ ] T2: goblins spawn only when step 6 starts, and no real wave starts. Death reloads T2. Both dead → the portcullis opens.
- [ ] T3: entry sets the flag. A build completes step 8. Hold T → exactly 20 goblins, no other types.
- [ ] T3 death → Meta Area. Victory → Campaign Map, fresh run.
- [ ] Skip Tutorial from each scene → Meta Area, flag set. Replay Tutorial from Settings works with a non-default loadout.
- [ ] The bow takes 1 s to reach full draw. A normal sector still feels OK with the bow.
- [ ] Gamepad: every glyph switches. Waypoints are visible and edge-clamped for every step.
- [ ] Each first-encounter hint shows once only, and never again after a restart.
