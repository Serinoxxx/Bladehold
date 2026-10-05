# Editor to-do: plan 19 (Music)

From [plan 19](../19-music.md), 2026-10-05. Tick items off as you go and delete the file when it's empty.

## Done by an agent via MCP (2026-10-05)

All C#, ScriptableObject assets, prefabs, scene placements, and regenerations are complete and play-checked:

- [x] **Track imports & compression:** 28 tracks from Ultimate Game Music Collection imported to `Assets/Bladehold/Audio/Music/` with Vorbis streaming, Quality 60, Load In Background on. Stings imported with Compressed In Memory for zero-latency playback.
- [x] **Scriptable Objects:**
  - `Assets/Bladehold/Config/Audio/BladeholdSoundManagerSettings.asset` (independent Feel settings asset with AutoLoad/AutoSave off, track volumes defaulted to 1.0).
  - `Assets/Bladehold/Config/Audio/MusicSettings.asset` (references default cues and timings).
  - 13 `MusicCueSO` assets in `Assets/Bladehold/Config/Audio/Music/` (`Music_Menu`, `Music_MetaHub`, `Music_CampaignMap`, `Music_RestArea`, `Music_FishingPond`, `Music_Prep`, `Music_Prep_Dark`, `Music_Prep_Desert`, `Music_Prep_Castle`, `Music_Battle`, `Music_Captain`, `Music_Boss`, `Music_VictoryBed`) and 4 sting cues (`Sting_Objective`, `Sting_Victory`, `Sting_CampaignComplete`, `Sting_Defeat`).
- [x] **Bootstrap Prefab:** `Assets/Resources/Audio/MusicSystem.prefab` created with `MMSoundManager` and `MusicDirector`. Initialized before first scene via `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`.
- [x] **Scene Wiring:** `SceneMusic` component placed in all hand-built scenes.
- [x] **Defense Scene Generator:** `sceneMusic` and `prepMusicOverride` added to `DefenseBiomePaletteSO` and wired into `DefenseSceneGenerator`. Outer Gate and Desert Gate regenerated and passed validation.
- [x] **Boss cleanups:** `BossVictoryMMF.prefab` cleaned of its duplicate `Music` track feedback so the director solely owns victory music. `SetBossFight(true/false)` hooked into Necromancer and Princess boss controllers.
- [x] **Legacy audio cleanup:** deleted `MMSoundManager_AutoCreated` and disabled `Music` game objects from scenes.
- [x] **Mixer volume slider:** `GameSettingsService` falls back to Feel's mixer so the settings slider works in every scene.
- [x] **Hooks & Transitions:**
  - `LoadingScreenManager`: `OnTransitionStarted` and `OnSceneRevealed` events added. Music fades out before scene activation and fades in on reveal.
  - Pause ducking: music ducks to 0.4 (−8 dB) when paused and fades back on unpause.
  - Wave hooks: prep → battle on wave start, battle → prep on intermission.
  - Captain hooks: battle → captain music when captain arrives, captain → battle when captain dies.
  - Objective completed: fanfare sting plays over ducked music.
  - Death & gate destruction: stops music on death/destruction and plays defeat sting.

## Listening checks (Needs Lance)

- [ ] **Music balance:** listen to the default balance between SFX and Music across a battle in `Outer Gate` or `Survivors Scene`. Adjust cue volumes on `MusicCueSO` assets or `MusicSettings.asset` if any specific cue is too loud or quiet relative to combat sounds.
- [ ] **Crossfade feel:** check if the 1.5s battle transition and 3.0s prep transition feel natural to you. Timings can be adjusted directly on `Assets/Bladehold/Config/Audio/MusicSettings.asset`.
- [ ] **Full tracks rotation:** in `MainMenu`, `Campaign Map`, or `Fishing Pond`, let the track play to the end to confirm the 4s crossfade rotation to the next track in the pool sounds seamless to your ears.

## Playtest

- [ ] **Full flow test:** Main Menu (title music) → Meta Area (tavern/market hub music) → Campaign Map (ambient exploration music) → Outer Gate (prep music) → Wave starts (battle music) → Captain arrives (boss battle music) → Captain killed (battle music resumes) → Objective completed (win fanfare over ducked music) → Victory screen (triumphant victory sting then calm Solemn City bed) → Rest Area (lively tavern/city music) → Fishing Pond (calm ambient/forest music).
- [ ] **Pause test:** press Esc in any scene and confirm music ducks down smoothly without cutting, and returns to full volume on unpause.
- [ ] **Settings slider:** open Settings > Audio, move the Music slider, and verify the volume changes in real-time. Close and reopen to verify the saved value persists.
- [ ] **Defeat test:** let player die or gate fall; verify music cuts to silence immediately and plays the dramatic defeat sting.
