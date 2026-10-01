# Editor to-do: enemy spawn/death prewarm

Not from a plan; 2026-10-01. Unity MCP was **not** connected, so nothing has run in the Editor yet: the code compiles (`dotnet build`), the prefab/asset wiring was written as YAML. Tick items off as you go and delete the file when it's empty.

What it is: `Waves/EnemyPrewarmer` (on `Bladehold Prefabs/Waves/EnemySpawner.prefab`, config `Bladehold Scripts/Waves/EnemyPrewarmConfig.asset`) holds the loading screen after a battle scene activates, stages one of each spawnable enemy ~22 m in front of the camera, renders it, kills each crowd type (goblin / skeletons) once into a ragdoll and once into a baked fall, tops up `ParticlePool` for their blood bursts, then clears it all. Audio is muted while it runs.

## Verify first

- [ ] **Unity imports cleanly.** Focus the Editor and check the console: no "missing script" or YAML errors on `EnemySpawner.prefab` / `EnemyPrewarmConfig.asset`. Select the prefab: **Enemy Prewarmer** sits under **Survivors Spawner**, with **Config** = `EnemyPrewarmConfig` and **Spawner** filled in (it also auto-wires in `OnValidate`). *(MCP-able)*
- [ ] **Scenes picked it up.** In each battle scene (Outer Gate, Desert Gate, Frozen Pass, Graveyard, Ancient Garden, Tutorial Gate), the `EnemySpawner` instance shows the component. A scene that unpacked the prefab won't have it. *(MCP-able)*

## Wiring

- [ ] **Re-collect the shader variant collection.** `Assets/Settings/BladeholdPrewarmVariants.shadervariants` was last saved 2026-08-29 and is missing `BakedCrowdLit` (the crowd shader added 2026-09-30). Project Settings → Graphics → Shader Loading: **Clear**, play a sector through a few waves (goblins and skeletons, kills, ragdolls), then **Save to asset…** over that file. The prewarmer covers the gap at runtime anyway; this makes the main-menu warm-up complete again.

## Playtest

- [ ] **No first-spawn / first-death hitch.** Use a **build** if you can: Editor shader compiles are async and Mono JIT adds hitches a build doesn't have. Enter a sector from the Campaign Map, start wave 1 and watch the first spawn and first kills with the Profiler (or the Stats overlay). If a hitch remains, take a Profiler capture of that frame. It will show what's left. The likeliest remaining cost is per spawn rather than first-time: every goblin's `AttackHitMMF` (Particles Instantiation, Mode **Pool**, size 5, `FX_Slash_01`) instantiates 5 slash FX in `Start`, and all 10 MMF players initialise there too. Turning on **Mutualize Pools** on that feedback in `Goblin Enemy (Base).prefab` is the cheap fix to try.
- [ ] **The loading screen stays up a moment longer** (about 1–1.5 s) and you never see the rehearsal enemies after it fades out.
- [ ] **Playing a battle scene directly in the Editor** (no loading screen) shows a black screen for about 1 s instead.
- [ ] **Must never happen:** gold, kills, ultimate charge or a powerup at sector start; the game staying muted after the load; a corpse or goblin left in front of the camera; the player taking a hit during the load.
