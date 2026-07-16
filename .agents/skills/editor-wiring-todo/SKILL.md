---
name: editor-wiring-todo
description: Use when finishing any Bladehold feature whose C# is done but which needs manual Unity Editor work (SO assets, prefab/scene wiring, animator or clip work, icons, art/audio) — writes the TODO.md entry in the house format.
---

# Write the Unity Editor wiring entry in TODO.md

Anything that can't be done by editing files headlessly — creating `ScriptableObject` asset instances, wiring prefab/scene references, animator/clip work, icon registration, art/audio — is recorded in **`TODO.md` at the project root**. Never a scratch/plan file, never only in chat: TODO.md is the durable cross-session record of what's done in C# vs. what still needs the Editor.

## Format (copy an existing entry — the **Bomber** and **Parry + Counterstrike** sections are the cleanest templates)

Read 1–2 existing entries in `TODO.md` first, then **prepend** the new entry near the top (newest entries go first — matching the existing file order), with these parts:

### 1. Header
```
## <Feature name> — Unity Editor wiring
```

### 2. "The C# is done" blurb
A dense paragraph: what was built, **file pointers** (`Folder/File.cs`), the key design decisions and which existing precedent each follows ("the `VampiricBlade` precedent", "the `MarkGolden` timing trick"), new `StatType`s, new CSV rows by id. This is what lets a future session pick the feature up cold — don't skimp.

### 3. Wiring checklist (`- [ ]` items)
Ordered roughly: assets → prefabs → scene → cosmetics → balance.
- **SO asset instances** with their create-menu paths (`menu Scriptable Objects/FooSO`) and the default values to set.
- **Prefab/scene component additions** — which object, which refs **auto-wire via `OnValidate`** vs. which must be **hand-assigned** (call both out explicitly; hand-assigned refs are the ones that get missed).
- **Animator work** — new triggers/params/states, clips, and any **animation events that must be baked on clip import settings with exact names** (bold the failure mode, e.g. "a missing event = silent no-hit swings").
- **Skill icons** — blank-icon node ids to fill via **Bladehold > Skill Tree Editor** (drag-and-drop registers the sprite in the SO's `icons` list).
- **Registration** — e.g. new enemy prefab into `WaveSpawner.enemyPrefabs` under its CSV id.
- **Balance pass** — every placeholder number (CSV costs/positions, SO tunables) named in one line.
- Note any MMF feedback that plays during the frozen intermission must use **Unscaled time mode**.

### 4. Manual verification checklist
```
## Manual verification (<feature>)
```
`- [ ]` items describing **observable in-game behaviours**, written so a person in Play mode can tick them:
- The happy path, step by step ("Reach wave 5 → a bomber spawns and…").
- **Negative cases** — things that must *never* happen ("the explosion is never parried", "a chest is never flung"). These catch the subtle regressions.
- Interactions with existing systems (death/restart, Reincarnate wipe, save/load, Hold the Line, class switch).
- How to reach the state fast: DevConsole cheats (`DebugSetNextWave`, `DebugWipeWave`, `DebugSpawnBurst`, class picker), the `EnemyZoo` gallery scene, `SkillTreePreview.unity` for tree/tooltip checks.

## Rules

- Don't delete or reorder existing entries; completed items get `- [x]` in place.
- Keep ids/paths/values **exact** — the reader executes this in the Editor without the code open.
- If the feature lands in stages (the Berserker A–E pattern), one blurb + checklist per stage under the same header.
