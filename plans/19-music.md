# 19: Music

**Goal:** every scene has music, including the main menu. Scene changes fade instead of cutting. The Fishing Pond and Rest Area are peaceful. Battles switch between calm "prep" music and battle music as waves start and end, with special music when a captain spawns, a fanfare when an objective is completed, and stings for victory and defeat. Music ducks while the game is paused.

**Status (2026-10-05):** C# and MCP wiring complete and verified in Play mode. Listening checks moved to [`plans/editor/19-music.md`](editor/19-music.md).

**Skills to load first:** `/find-and-import-assets` (step 4 import snippet), `/feel-integration`, `/compile-check`, `/unity-editor-mcp`, `/editor-wiring-todo`, and `/changelog` before committing.

## What exists today (verified 2026-10-05)

- **No music system.** The only music in the project is Feel demo audio, plus a disabled `Music` GameObject in `Bladehold Survivors Scene.unity` and `Demo Scene.unity`. Delete both.
- **Feel's sound manager is already in scenes.** A `MMSoundManager_AutoCreated` object (MMSoundManager component, Feel's own settings asset) is saved in MainMenu, Meta Area, Rest Area, Survivors and Ancient Garden. `MMSoundManager` is an `MMPersistentSingleton`: whichever copy wakes first survives scene loads, and later copies destroy themselves in `Awake`. **If no copy exists, `MMSoundManager.Instance` creates a bare one with no settings asset**, so the Music track has no mixer group. The bootstrap below fixes this.
- **The Music slider does nothing yet.**
  - `GameSettingsService.SetMusicVolume` writes the `MusicVolume` parameter on `MMSoundManagerAudioMixer.mixer`, but nothing plays through that mixer's Music group.
  - Feel's settings asset auto-loads its own saved volumes (Music = 0.2) into the same mixer, so two systems write those volumes.
  - `GameSettingsService` is a per-scene singleton. Its `mixer` field is only set where `GameMenu.prefab` is placed, and when the service auto-creates itself `mixer` is null.
- **The loading screen has no events.** `LoadingScreenManager` (persistent, `UI/Transitions/`) does: 0.35 s fade-in → async load (`allowSceneActivation = false`) → at least 1.0 s on screen → activate → wait on `HoldFadeOut` holds (up to 8 s) → 0.35 s fade-out. Static `IsTransitioning` exists.
- **`EnemyPrewarmer` sets `AudioListener.volume = 0`** while it prewarms after activation, then restores it. Any music still fading out at that moment cuts out abruptly. **The fade-out must finish before scene activation.**
- **Combat events (all instance events on scene singletons, so re-subscribe every scene):**
  - `GameLoopManager`: `OnWaveStarted(int)`, `OnWaveCleared(int, string)`, `OnVictory`, plus `IsWaveActive` / `IsPrepPhase`.
  - `SurvivorsObjectiveManager`: `OnObjectiveCompleted`, `OnPhaseChanged(SurvivorsObjectivePhase)` (`Active` / `Cleanup` / `Intermission`).
  - `PauseMenuController.OnPauseChanged(bool)`.
  - Static: `Gate.OnAnyGateDestroyed`.
- **There is no captain-spawned event.** Captains spawn in `GameLoopManager.SpawnCaptainForWave` (~line 818), which ends with `EnemyIntroUI.ShowIntro("A clan captain has arrived", ...)` (~878).
- **`DeathScreen` has no events.** All outcomes meet in its private `ShowRunOver(title, subtitle, isVictory)` (~line 283). Boss victories call `DeathScreen.ShowVictory` directly and never raise `GameLoopManager.OnVictory`.
- **Bosses play their own victory music** through MMF feedbacks: `BossVictoryMMF.prefab` plays `Triumphant Victory.wav` on the Music track, and `NecromancerBossController.victoryFeedback` / `PrincessBossController`'s victory-music feedback do the same. The director takes this job over.

## Design

**Feel's `MMSoundManager` plays the audio. A new `MusicDirector` decides what plays.** Don't use Feel's playlist manager (`MMSMPlaylistManager`): it plays songs in order, and this game needs music driven by game state.

### New files (`Assets/Bladehold/Bladehold Scripts/Audio/`)

| File | What it is |
|---|---|
| `MusicCueSO.cs` | `[CreateAssetMenu(menuName = "Scriptable Objects/Audio/Music Cue")]`. Fields: `AudioClip[] clips` (a pool), `float volume = 1`, `bool loopClip = true` (true for seamless `LOOP` files; false means "crossfade to another pool pick `rotateCrossfade` seconds before the clip ends", used for `FULL` tracks), `float rotateCrossfade = 4`. |
| `MusicSettingsSO.cs` | One asset holding the defaults: the default cues (`prep`, `battle`, `captain`, `boss`, `victoryBed`, `objectiveSting`, `victorySting`, `campaignCompleteSting`, `defeatSting`) and the timings (below). |
| `SceneMusic.cs` | Goes in each scene, like `SceneAbilityRules`. Fields: `MusicCueSO sceneCue` (required) and optional `MusicCueSO prepOverride`, `battleOverride`. A static `Current` is set in `Awake` and cleared in `OnDestroy`. If a scene has no `SceneMusic`, the current music keeps playing. |
| `MusicDirector.cs` | Persistent manager. Logic below. |

**Timings** (on `MusicSettingsSO`, all unscaled seconds): `sceneFadeOut 1.2`, `sceneFadeIn 2.0`, `toBattle 1.5`, `toPrep 3.0`, `toCaptain 1.0`, `toBoss 1.5`, `stingDuckTo 0.25`, `stingDuckIn 0.3`, `stingDuckOut 1.5`, `pauseDuckTo 0.4` (about −8 dB), `pauseDuckFade 0.25`, `defeatFadeOut 1.5`.

### Bootstrap

- Add `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` on `MusicDirector`. It instantiates `Resources/Audio/MusicSystem.prefab`, which holds:
  - an `MMSoundManager` whose `settingsSo` is the new Bladehold settings asset (see Volume ownership);
  - the `MusicDirector`, referencing `MusicSettingsSO`.
- `BeforeSceneLoad` makes this copy wake first, so it wins the singleton and the copies saved in scenes destroy themselves. Delete those copies anyway (Editor checklist).
- Mark the director `DontDestroyOnLoad` and guard against duplicates, following the `RunTelemetry` pattern.
- Validate references in `Start` (house `anyError` pattern).

### Playback: the director owns volume

- **Allocation:** play each clip through `MMSoundManager.Instance.PlaySound(clip, options)` with track `Music`, `Loop = cue.loopClip`, `Persistent = true` (so Feel's scene-load cleanup doesn't free it), and `Volume = 0`. This routes the source to the Music mixer group, which makes the settings slider work.
- **Volume:** the director sets every source's volume itself, every frame in `Update`, from `Time.unscaledDeltaTime`:
  `source.volume = cue.volume × envelope × duck`
  - `envelope` is that source's own fade value (0..1).
  - `duck` = pause duck × sting duck, shared by all music sources.
  Don't use `MMSoundManager.FadeSound`: its tweens would fight the ducking.
- **Freeing:** when a source's envelope reaches 0 and it isn't being kept for a scene dip (below), call `MMSoundManager.Instance.FreeSound(source)`.
- **Crossfade:** start the new source at envelope 0 and ramp it to 1, while the old one ramps to 0. Allow any number of fading-out sources.
- **Clip choice:** pick at random from the cue's pool, never repeating the last pick for that cue. For `loopClip = false` cues, crossfade to a new pick `rotateCrossfade` seconds before the clip ends.

### Picking the music: derive it, don't stack it

Keep a little state and recompute the wanted cue whenever any of it changes. If the wanted cue is the one already playing, do nothing; otherwise crossfade using the timing for that kind of change.

```
if (terminalCue != null)           return terminalCue;       // victory bed; null music after defeat
if (bossFightActive)               return settings.boss;
if (phase == Battle && captainsAlive > 0) return settings.captain;
if (phase == Battle)               return scene.battleOverride ?? settings.battle;
if (phase == Prep)                 return scene.prepOverride   ?? settings.prep;
return scene.sceneCue;
```

- `phase` is `None` / `Prep` / `Battle`. In scenes without a `GameLoopManager` it stays `None`.
- **Reset per scene:** `phase`, `captainsAlive`, `bossFightActive`, `terminalCue` and the "defeated" flag.
- **Defeat:** `terminalCue` is a "silence" marker, i.e. wanted music is nothing.
- **Stings** are separate one-shot sources (`loop = false`, `Persistent = true`), not cues. A sting ramps the sting duck to `stingDuckTo` over `stingDuckIn`, holds for the clip's length, then ramps back over `stingDuckOut`. The duck doesn't apply to the sting itself.

### Scene transitions

1. **Add to `LoadingScreenManager`:** `public static event Action OnTransitionStarted` (raise at the top of `TransitionRoutine`) and `public static event Action OnSceneRevealed` (raise just before each `FadeOut(...)` call, both the ~192 and ~247 paths). Clear both in the existing `ResetStatics`.
2. **When a transition starts:** ramp every music source to 0 over `sceneFadeOut`, and stop any sting. Don't free the current cue's source yet; remember it as `dipped`.
3. **`SceneManager.sceneLoaded`:**
   - Reset state, then re-subscribe to the new scene's singletons (`GameLoopManager.Instance`, `SurvivorsObjectiveManager.Instance`, `PauseMenuController.Instance`, `Player.Instance.Health`, `DeathScreen` (see Combat hooks)), unsubscribing from the old ones first.
   - Set `phase`: `GameLoopManager.Instance == null` → `None`; else `IsWaveActive` → `Battle`; else `Prep`.
   - If `LoadingScreenManager.IsTransitioning` is false (Editor play start, or a plain `SceneManager.LoadScene`), apply the wanted cue now with `sceneFadeIn`.
4. **On `OnSceneRevealed`:** if the wanted cue is the `dipped` cue, ramp the same source back up so the track carries on where it was (Meta Area ↔ Campaign Map share nothing today, but overrides might). Otherwise free `dipped` and start the wanted cue over `sceneFadeIn`.
5. **Route Quit → Main Menu through the loading screen:** change `PauseMenuView.cs:182` from `SceneManager.LoadScene` to `LoadingScreenManager.LoadScene(...)`, using the same call shape as `MainMenuManager`/`DeathScreen`. Before calling it, set `timeScale = 1`, which `TransitionRoutine` already does. Leave `DevConsole`, `DraftTester` and `SettingsPanelView`'s reload alone; step 3's sceneLoaded path covers them.

### Combat hooks

| Moment | Hook (add where marked **new**) | Director does |
|---|---|---|
| Wave starts | `GameLoopManager.OnWaveStarted` | `phase = Battle`, crossfade over `toBattle` |
| Wave over / prep | `SurvivorsObjectiveManager.OnPhaseChanged(Intermission)` | `phase = Prep`, `captainsAlive = 0`, crossfade over `toPrep` |
| Captain spawns | **new** `public event Action<Health> OnCaptainSpawned` on `GameLoopManager`, raised at the end of `SpawnCaptainForWave` with the captain's `Health` (the method already has `captainHealth`) | `captainsAlive++`, subscribe to that `Health.OnDied` → `captainsAlive--`, crossfade over `toCaptain` (back to battle uses `toBattle`). Unsubscribe on scene change. |
| Objective completed | `SurvivorsObjectiveManager.OnObjectiveCompleted` | Play `objectiveSting`. Prep music follows from the phase change. |
| Run over | **new** `public static event Action<bool> OnRunOver` on `DeathScreen` (`bool isVictory`), raised in `ShowRunOver`, so it covers sector wins, boss wins, player death and gate fall | Victory: play `campaignCompleteSting` if `CampaignManager.Instance?.CurrentNodeEndsCampaign` else `victorySting`, then `terminalCue = victoryBed`. Defeat: play `defeatSting`. |
| Player dies / gate falls | `Player.Instance.Health.OnDied`, `Gate.OnAnyGateDestroyed` | Set terminal "silence" right away and fade out over `defeatFadeOut`, so music stops at the moment of death, before the screen appears. Skip in scenes with `TutorialDirector` (the Arena reloads the room on death; the reload brings the music back). |
| Boss fight | `NecromancerBossController.StartBossFight` and the Princess fight start call **new** `MusicDirector.SetBossFight(true)`; their defeat handlers call `SetBossFight(false)`. Static, like the existing `DeathScreen.ShowVictory` call. | Crossfade over `toBoss` |
| Pause | `PauseMenuController.OnPauseChanged(bool)` | Pause duck to `pauseDuckTo` / back to 1 over `pauseDuckFade` (unscaled) |

Shop, meta-upgrades, fishing-draft and enemy-intro freezes also set `timeScale = 0`. They get **no** duck. Only the pause menu ducks.

### Volume ownership

- **New settings asset:** create `Assets/Bladehold/Config/Audio/BladeholdSoundManagerSettings.asset` by duplicating Feel's `MMSoundManagerSettings.asset` (don't edit the vendored one). Keep the same mixer and groups, set **AutoLoad = false, AutoSave = false**, and set every track volume to 1. The `MusicSystem` prefab uses it.
- **`GameSettingsService` is the single source of truth.** If its `mixer` field is null, fall back to `MMSoundManager.Instance.settingsSo.TargetAudioMixer` (check the field name in `MMSoundManagerSettingsSO.cs`). The slider then works in every scene.
- **Fix the stale path** in `Editor/SettingsMenuGenerator.cs:42`: `Assets/Feel/...` → `Assets/Third Party/Feel/...`.
- Routing SFX through the mixer is **out of scope** (separate TODO).

## Tracks (pre-approved)

Import from Asset Inventory package **Ultimate Game Music Collection** (asset id 192) with the `/find-and-import-assets` step 4 snippet, into `Assets/Bladehold/Audio/Music/<Folder>/`.

**Import settings for every file:**
- Music beds: Load Type **Streaming**, Compression **Vorbis**, Quality **60**, Preload Audio Data **off**, Load In Background **on**.
- Stings (the `Stings/` folder): **Compressed In Memory** instead of Streaming, so they start without lag.

Apply the settings with an `AudioImporter` loop through `execute_code`, then reimport.

| Cue asset | Folder | Clips (FileID: name) | `loopClip` |
|---|---|---|---|
| `Music_Menu` | Menu | 221376 Nordic Title, 221320 Fantasy Title | false |
| `Music_MetaHub` | Hub | 221201 Tavern LOOP SLOW, 221635 Medieval Market LOOP | true |
| `Music_CampaignMap` | Hub | 221574 Open Exploring, 221466 Nordic Landscape AMBIENT EDIT | false |
| `Music_RestArea` | Peaceful | 221674 Tavern LOOP LIVELY, 221633 Upbeat City LOOP | true |
| `Music_FishingPond` | Peaceful | 221457 Calm Ambient, 221685 Meadow, 221655 Moonlit Forest - Main Loop | false |
| `Music_Prep` (default) | Combat | 221336 Enemy Territory LOOP, 221626 Solemn City LOOP | true |
| `Music_Prep_Dark` | Combat | 221267 Scary Dungeon LOOP, 221605 Murky Dungeon LOOP, 221332 Ambient Dungeon LOOP | true |
| `Music_Prep_Desert` | Combat | 221589 Middle Eastern Market LOOP without melody | true |
| `Music_Prep_Castle` | Combat | 221215 Church LOOP MEDIUM | true |
| `Music_Battle` (default) | Combat | 221647 Epic Combat LOOP, 221479 Close Combat LOOP, 221612 Frantic Battle LOOP, 221642 Enemies LOOP, 221495 Desperate Battle LOOP | true |
| `Music_Captain` | Combat | 221596 Boss Battle 2 Loop, 221436 Boss Battle 5 Loop, 221514 Barren Boss LOOP | true |
| `Music_Boss` | Combat | 221525 Boss LOOP, 221693 Boss Battle 1 Loop | true |
| `Music_VictoryBed` | Combat | (reuses 221626 Solemn City LOOP) | true |
| sting: objective | Stings | 221246 Fanfare Win | n/a |
| sting: victory | (existing) | `Assets/Bladehold/Bladehold Audio/Triumphant Victory.wav`, already imported, don't re-import | n/a |
| sting: campaign complete | Stings | 221529 Fanfare WORLD WIN | n/a |
| sting: defeat | Stings | 221238 Dramatic Defeat SHORT | n/a |

That's 28 new files. Put the cue assets in `Assets/Bladehold/Config/Audio/Music/`, beside `MusicSettingsSO` (`Assets/Bladehold/Config/Audio/MusicSettings.asset`).

### Scene → `SceneMusic` wiring

| Scene | `sceneCue` | `prepOverride` | `battleOverride` |
|---|---|---|---|
| MainMenu | Music_Menu | | |
| Bladehold Meta Area Scene | Music_MetaHub | | |
| Bladehold Campaign Map Scene | Music_CampaignMap | | |
| Bladehold Rest Area Scene | Music_RestArea | | |
| Bladehold Fishing Pond | Music_FishingPond | | |
| Bladehold Survivors Scene, Ancient Garden, Frozen Pass, Outer Gate | Music_Prep | | |
| Bladehold Desert Gate | Music_Prep_Desert | Music_Prep_Desert | |
| Bladehold Graveyard, Necromancer Crypt, Castle Dungeons | Music_Prep_Dark | Music_Prep_Dark | |
| Bladehold Tutorial Dungeon / Arena / Gate | Music_Prep_Dark | Music_Prep_Dark | |
| Castle Ramparts, Castle Armory, Great Hall, Castle Conservatory, Throne Antechamber, Princess Sanctuary | Music_Prep_Castle | Music_Prep_Castle | |

For combat scenes, `sceneCue` only plays while `phase == None`, which barely happens. Set it to the scene's prep cue so it never sounds wrong. `DefenseSceneGenerator` regenerates Outer Gate and Desert Gate: add a `MusicCueSO sceneMusic` (+ prep override) field to `DefenseBiomePaletteSO` and have the generator place `SceneMusic` from it (Arid → Prep_Desert, Alpine / Kingdom → default Prep, Graveyard → Prep_Dark), the same way `worldEventIds` works. **Never hand-edit those two scenes.**

## Steps

1. Import the 28 tracks and apply the import settings. `refresh_unity`, then `read_console`.
2. Write `MusicCueSO`, `MusicSettingsSO`, `SceneMusic`, `MusicDirector`.
3. Add the `LoadingScreenManager` events, `GameLoopManager.OnCaptainSpawned`, `DeathScreen.OnRunOver`, the boss `SetBossFight` calls, the `GameSettingsService` mixer fallback, the `PauseMenuView` quit route, the `SettingsMenuGenerator` path fix, and the `DefenseBiomePaletteSO` field plus generator hook. `/compile-check`.
4. Through Unity MCP:
   - Create the settings asset, `MusicSettings.asset`, the cue assets, and `Resources/Audio/MusicSystem.prefab`.
   - Place `SceneMusic` in each hand-built scene and save each scene.
   - Regenerate Outer Gate and Desert Gate.
   - Delete the `MMSoundManager_AutoCreated` objects and the two dead `Music` objects.
   - Remove the music feedback from `BossVictoryMMF.prefab`, the Necromancer's `victoryFeedback` and the Princess's victory-music feedback (or remove just the Music-track sound entry if the feedback also does other things).
   - Anything that can't be done goes in `plans/editor/19-music.md`.
## Needs Lance in the Editor

Moved to its own checklist: [`plans/editor/19-music.md`](editor/19-music.md).

## Manual verification

- [x] Main menu music starts on launch, and a fresh Editor Play in any scene starts that scene's music.
- [x] Every transition (menu → meta → map → sector → rest area → fishing → death → meta) fades out on the loading screen and in on reveal, with no cut and no blip when `EnemyPrewarmer` mutes and unmutes.
- [x] Quit to Main Menu from the pause menu fades through the loading screen.
- [x] A wave start goes to battle within about 1.5 s; the wave clearing eases back to prep.
- [x] A captain spawn switches to captain music in time with the banner, and killing it drops back to battle.
- [x] Objective complete: the fanfare plays over ducked music, which comes back afterwards.
- [x] Sector victory: sting, then the calm bed under the victory screen. A campaign-ending node plays the World Win fanfare.
- [x] Death and gate fall: music fades as it happens, then the defeat sting plays.
- [x] Necromancer and Princess: boss music in the fight, and only one victory sting at the end (no double music).
- [x] Pause ducks the music and unpause restores it. Shop and draft screens don't duck.
- [x] The Music slider changes music volume in every scene, including ones without `GameMenu.prefab`, and its saved value survives a restart (no snapping to Feel's 0.2).
- [x] `FULL` tracks (menu, map, fishing) rotate with a crossfade instead of stopping.
- [x] Console is clean, and the build size goes up by a sane amount (Vorbis streaming).
