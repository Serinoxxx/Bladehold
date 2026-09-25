# CLAUDE.md

**Source of truth for all coding agents on this repo.** `.agents/AGENTS.md` (Antigravity) points here. Update this file, not that one.

## Overview

Unity 6 game, codenamed **Bladehold**. A 3D action roguelite: one hero, a melee + ranged loadout, fighting goblin hordes across a branching castle campaign while building tower defences between waves. URP, new Input System, NavMesh AI. Editor version is pinned in `ProjectSettings/ProjectVersion.txt` (currently `6000.3.10f1`), open with exactly that.

**Target: Steam Next Fest, Feb 27 2027** (moved from Oct 2026; `STEAM_NEXT_FEST_PLAN.md` still has the old dates).

> This doc is a map, not an inventory. Grep `Assets/Bladehold/Bladehold Scripts/` before concluding something doesn't exist, and trust code over docs. Deeper notes live in nested `CLAUDE.md` files next to the code (see the list at the bottom).

## The game loop (current)

1. **Meta Area** (`Bladehold Meta Area Scene`) is the hub. Spend permanent currencies:
   - **Goblin Blood** on 3-tier perks at the Spirit NPC (`UI/Meta/MetaUpgradesUI.cs`, `MetaPerkDefinitionSO` assets in `Bladehold Config/MetaPerks/`). Tier 2/3 unlock with Orcish Metal.
   - **Orcish Metal** on weapon and armour pedestals (`UI/Meta/WeaponPedestal.cs`, `ArmourPedestal.cs`). Mount pedestals (`MountPedestal.cs`) are **not implemented yet**.
   - **Battle Portal** (`UI/Meta/BattlePortal.cs`) wipes run state, starts a fresh campaign run and opens the Campaign Map.
2. **Campaign Map** (`Bladehold Campaign Map Scene`, `Campaign/`): an 8-tier branching node graph. Node types: Combat sectors (castle scenes, each with a difficulty tier + named captain), Fishing Ponds, Rest Areas, a PreBoss sector, then the Crypt (Necromancer choice: Obey → Princess boss, Defy → Necromancer boss). See `Campaign/CLAUDE.md`.
3. **Combat sector** (any battle scene: the castle scenes and `Bladehold Survivors Scene`, which despite the name is just one of the battles). 5 waves, each preceded by a war-banner pick + tower-building prep phase. Clear wave 5 → towers are dismantled for supply (remaining supply + upgrade spend) → victory screen → back to the map. Towers never carry between sectors. See `Waves/CLAUDE.md` and `Fort/CLAUDE.md`.
4. **Between sectors**, run state rides in the static `Economy/RunSession.cs`: HP ratio, in-run gold, supply, ammo, draft levels, ultimate + charge, buff fish, campaign node state.
5. **Death** → defeat screen (one button) → back to the Meta Area; the run is wiped (`RunSession.ClearRun()`). Permanent currencies are kept. No retry, and no voluntary exit from the map: the only ways home are death or campaign end.

### Currencies

| Currency | Where | Persistence | Spent on |
|---|---|---|---|
| Gold | `RunSession.InRunGold` | Run only | Rest Area shop |
| Supply | `RunSession.InRunSupply` | Run only | Building/refilling/upgrading towers |
| Goblin Blood | `SaveData.goblinBlood` | Permanent | Meta perks |
| Orcish Metal | `SaveData.orcishMetal` | Permanent | Weapon/armour/mount unlocks, perk tiers |
| Diamond Fish Bones | `SaveData.diamondFishBones` | Permanent, rare (fishing) | Planned: fishing spear weapon + fisherman's armour set (not implemented) |

### Player kit

- **Loadout** (`Player/PlayerWeaponManager.cs`, `WeaponDefinitionSO` assets in `Bladehold Config/Weapons/`): 1 melee (sword, axe, mace) + 1 ranged (bow, throwing axe). Sword + bow are free; the rest cost Orcish Metal. Wand/staff are `isLockedForDemo`.
- Hold-to-charge attacks (`PlayerAttack`), dash with charges (`PlayerDodge`), shared ranged ammo pool (`PlayerAmmo`, synced through `RunSession.CurrentAmmo`), armour sets (`PlayerArmourManager` + `ArmourSetSO`).
- **Ultimates** (`PlayerUltimateController` + `IUltimateHandler` implementations): locked until the weapon's ultimate draft card is picked, one per run.
- **Mounts** (`Player/PlayerSummonMount.cs`, `Horse/MountDefinitionSO`): summon is gated by `StatType.SummonMountUnlocked`, which nothing raises yet, and it's bound to the Synty `Dismount` action (Q / pad East), not X.
- **Interaction** is `[E]` / gamepad west via `Player/PlayerInteraction.cs` + `IInteractable`.

### In-run progression

- **Drafts** (`Upgrades/DraftUpgradeService.cs`, `Assets/Bladehold/Resources/DraftUpgrades.csv`): 3-card picks. Categories are `Weapon` (only cards for your equipped weapons, including `isUltimate` cards), `Elemental`, and `Fortress` (Fortress cards are excluded from drafts; towers replaced them). Triggered by the war-banner wave bounty and the Rest Area Draft Station. The XP level-up draft (`SurvivorsLevelSystem`) is legacy and its prompt is disabled.
- **Rest Area** (`Bladehold Rest Area Scene`, `UI/RestArea/`): Well, Shop (`ShopUI`, items in `Bladehold Config/ShopItems/`), Draft Station, gate back to the map.
- **Fishing Pond** (`Fishing/`, spec in `docs/FishingMinigameSpec.md`): 60s Fishing Frenzy with its own draft cards; pays currencies and lets you eat buff fish (max 3 per run).

## Design direction (decided, not yet built)

Build towards these; don't "fix" code back to the old behaviour.

- **Sectors get harder deeper into the campaign.** Stronger enemy types spawn in later sectors, but basic goblin fodder keeps spawning alongside them for the power fantasy. Today every sector resets to waves 1-5 with the same pacing asset.
- **The newer enemies belong in sector waves**: Bulwark, Bannerman, Powder Keg and co. Today `SurvivorsSpawner` only admits goblin/brute/big_ork/bubbler/bomber via each wave's `allowedEnemyIds`.
- **Mount summon is available from the start** on **X**, keeping the cast time, ride duration and cooldown. The *variants* (Frost Strider etc.) show on pedestals but are locked for the demo.
- **Fishing Pond is just you and your bow:** no mount summon and no ultimate there.
- **Demo scope (not final):** the full loop, but restricted to limited weapons/armours/mounts and **tier-1 meta perks only**, with the campaign cut off before the final battles (roughly halfway through the map). The aim is for players to die a few times and go round the meta loop.

## Building, running, testing

- No command-line build. Work in the Editor; enter Play mode from `Bladehold Meta Area Scene` for the real flow, or open any battle scene directly.
- All live scenes are in `Assets/Bladehold/Bladehold Scenes/` and registered in `ProjectSettings/EditorBuildSettings.asset`. New scenes go there. The castle, crypt and sanctuary scenes are **binary-serialized** (can't be grepped); they were generated by `Editor/BuildCastleLevels.cs`, `BuildNecromancerCryptScene.cs` and `BuildPrincessSanctuaryScene.cs`.
- `.csproj`/`.slnx` are generated by Unity. Never hand-edit them.
- **Compile check**: run `dotnet build` after C# changes (the `compile-check` skill covers the new-file registration trap). Lance runs tests himself unless he asks.
- **Mechanic regression suite**: `Editor/WeaponReachBenchmark.cs`, menu **Bladehold/Benchmarks/Run Weapon Reach & Damage Benchmark**, logs `N PASSED | M FAILED`.
- **DevConsole** (`Debug/DevConsole.cs`, backquote key): draft cards, currencies, loadout cycling, wave/objective controls, enemy spawns, scene loads, combat scenarios.
- **Unity MCP** (CoplayDev unity-mcp) can drive the live Editor when connected. See the `unity-editor-mcp` skill.

## Source control

- **Commit and push directly to `main`.** Solo project, no feature branches or PRs unless asked. This overrides the global "never work on main" rule.
- Commit as **`serinoxxx <lancemclachlan@gmail.com>`** (set in the repo's local git config).
- `CHANGELOG.md` holds player-facing release notes per `bundleVersion` (`ProjectSettings/ProjectSettings.asset`), grouped as New Features / Fixes / Balance Changes / General Changes. Plain language, no internal names.
- **Pushing headless**: GitHub auth routes through the `gh` CLI (Git Credential Manager pops a GUI and hangs). `gh` is installed at `C:\Program Files\GitHub CLI\gh.exe`; a session started before the install won't have it on PATH, so restart or use the full path. Setup/repair: `gh auth login --hostname github.com --git-protocol https --web` (as **Serinoxxx**), then `gh auth setup-git`.

## Unity Editor wiring tasks

**`TODO.md` is Lance's personal list. Agents never write to it.** When a change needs Editor-only work (SO assets, prefab/scene wiring, animator/clip work, art/audio, UI review), do it via Unity MCP if connected. Anything left over goes in a **"Needs Lance in the Editor"** section at the end of your session summary, and in the active `plans/` file if you're working from one. Keep it short: what, where, and how to verify.

## Code layout

- First-party code lives **only** in `Assets/Bladehold/Bladehold Scripts/` (note the space). Key folders: `Campaign/`, `Waves/` (incl. `Banners/`), `Objectives/`, `Fort/`, `Economy/`, `Player/`, `Horse/`, `Enemies/`, `Bosses/`, `DamageSystem/`, `Upgrades/`, `Stats/`, `Fishing/`, `UI/` (incl. `Meta/`, `RestArea/`, `Transitions/`), `Save/`, `Debug/`, `Editor/`.
- Designer data: `Assets/Bladehold/Config/Enemies.csv`, `Assets/Bladehold/Resources/DraftUpgrades.csv`, SO assets under `Assets/Bladehold/Bladehold Config/`.
- No `.asmdef` for game code; everything compiles into `Assembly-CSharp`.
- Vendored, don't modify: `Assets/Third Party/` (Synty incl. the active player controller, StarterAssets (unused), Wingman, Kevin Iglesias), `Assets/LeanTween/`, `Assets/DamageNumbersPro/`, `Assets/Feel/` (`MMF_Player`), `Assets/AssetInventory/`.
- **Player controller** is Synty's `SamplePlayerAnimationController` + `InputReader`, not StarterAssets. New gameplay inputs go in the Synty map (`Assets/Third Party/Synty/AnimationBaseLocomotion/Samples/Scripts/InputSystem/Controls.inputactions`), then satisfy the new interface methods in `InputReader.cs`. Don't use `Assets/InputSystem_Actions.inputactions` for gameplay.

## Conventions

- **`Health` is the hub; dependencies point inward.** `Health` raises `OnDied` (once) and `OnDamaged(Damage)` and knows nothing about listeners. Reactions (death anims, loot, scoring, UI, wave tracking) are separate components that subscribe, and unsubscribe in `OnDestroy`. Only three hooks may alter what `Health` does: `TryPreventDeath`, `TryBlockDamage`, `ScaleDamageTaken`. Details in `DamageSystem/CLAUDE.md`.
- **Death is signalled, not destruction.** Detect death via `Health.OnDied` / `IsDead`, never via `OnDestroy` or object counts. Corpses despawn much later.
- **Validate dependencies in `Start`.** Auto-wire in `OnValidate`/`Awake`, null-check in `Start`, `Debug.LogError` + set an `anyError` flag, early-return from `Update`/handlers. No silent failures.
- **Don't assume hierarchy.** Check the actual prefab/scene hierarchy before `GetComponent*` calls. On the Player prefab, `Player.cs` (`Player.Instance`) is on the child `SidekickSyntyCharacter`, while `PlayerWeaponManager`/`PlayerUltimateController` are on the root, so `Player.Instance.GetComponentInChildren<T>()` won't find root components. Use `transform.root.GetComponentInChildren<T>(true)` or explicit serialized refs.
- **Never build visuals in code, and no runtime asset fallbacks.** Meshes, indicators, UI elements and VFX are authored prefabs wired via serialized fields/SOs. No `AddComponent<MeshRenderer>`-style assembly, no `EnsureVisuals()`/`CreateProceduralX()` fallback methods, no `AssetDatabase.LoadAssetAtPath` fallbacks in gameplay code. If a prefab or reference is missing, `Debug.LogError` and flag it as **human intervention** or **agent mockup** under "Needs Lance in the Editor". Don't paper over it in production code. About 40 older scripts still do this (tracked in `plans/`); don't copy them.
- **UI work: AI mockups are OK, humans sign off.** Agents may build a simple UI mockup, either live through Unity MCP or with a temporary editor build script that gets deleted afterwards, never with runtime code. Mockups must:
  - Be **prefab-based and data-driven**: one prefab per repeated element, populated from data. Never copy-paste a UI element and tweak the copies.
  - Use **Synty UI assets as placeholder art** (`Assets/Synty/InterfaceFantasyWarriorHUD/`, `Assets/Synty/InterfaceCore/`) to stay in style.
  - Use the house fonts: **Texturina** for headers, **Grenze** for all other text (TMP assets in `Assets/Synty/InterfaceFantasyWarriorHUD/Fonts/`).
  - Get **flagged for human UI review** under "Needs Lance in the Editor".
- **All feedback goes through MMF (Feel).** Audio/SFX, particles/VFX, screenshake, flashes, hitstop and tweens are `MMF_Player` feedbacks on the prefab, triggered from code with `PlayFeedbacks()`. No direct `AudioSource.PlayOneShot`, `Instantiate(vfxPrefab)` or camera-shake calls in gameplay code. Older scripts that do this get migrated when touched.
- **Tunables go on a `ScriptableObject`** (`*SO`, `[CreateAssetMenu]` under `Scriptable Objects/…`).
- **Upgrade-able numbers are `StatType` bases**, registered by the owning system via `PlayerStats.SetBase` and read via `GetValue`, even when the base is 0 ("locked"). `final = (base + Σflat) × (1 + Σpercent)`.
- **Scene singletons** (`Player.Instance`, `GameLoopManager`, `GameStats.Instance`, …) are set in `Awake` and cleared in `OnDestroy`. `CampaignManager` and `LoadingScreenManager` are `DontDestroyOnLoad`.
- **Persistence tiers**: permanent progress → `Save/SaveData.cs` via `SaveSystem` (a single cached instance; add fields, old saves load defaults). Run state → static `RunSession`. Campaign progress is **not** saved to disk.
- **Scene loads go through `Bladehold.UI.LoadingScreenManager`** when present.

## Legacy / dead code (don't build on it)

Still in the codebase, not reachable in live build scenes:
- Old `Waves/WaveSpawner.cs` endless loop (only Demo/Test scenes). `SurvivorsSpawner` still calls its static `ApplyDefinition`, and many systems still null-check `WaveSpawner.Instance`.
- Gold skill tree (`SkillTreeService`, `SkillTreeView`) and Reincarnate (`Reincarnate/`), plus `SaveData.totalGold`/`purchasedNodeIds`/`reincarnatePoints`.
- `HoldTheLineBonus`, `WaveIntermissionUI`.
- The 3-waves-then-Rest-Area gate path in `GameLoopManager`, `RunSession.RestVisitsCount` formulas, the stage-select fields (`highestUnlockedStage`, `selectedStage`).
- `SurvivorsGameManager`'s 20-minute siege timer / endgame boss.
- Old socket-based `FortDefense`/`FortDefenseManager`/`FortDefenseSocket` (still applies some draft effects; plots replaced it).
- `ClassDefinitionSO`/`PlayerClassController` are **deleted**; only stale comments remain.
- `Bladehold Supply Room`, `Frozen Pass`, `Ancient Garden` scenes have no campaign node.

## Project skills

Recipes in `.claude/skills/`; invoke the matching one before starting: `add-enemy-type`, `generate-enemy-prefabs`, `balance-sim`, `compile-check`, `unity-editor-mcp`. (`editor-wire` and `editor-wiring-todo` are TODO.md-based and due for retirement; `add-player-class` and `add-skill-line` target removed/dead systems and are due for retirement or rewrite.) `.agents/skills/` has more (changelog, maintain-mechanic-tests, test-mechanic, add-ultimate-handler, …) that haven't been ported yet.

## Plans

`plans/` (tracked in git) holds the work queue. `plans/README.md` is the index and roadmap; each numbered plan is sized for one agent session. `plans/PARKING_LOT.md` collects new ideas during the feature freeze.

## Nested docs

- `Assets/Bladehold/Bladehold Scripts/Campaign/CLAUDE.md`: graph, nodes, map UI, boss routing.
- `Assets/Bladehold/Bladehold Scripts/Waves/CLAUDE.md`: sector phases, banners, spawning, objectives, rewards.
- `Assets/Bladehold/Bladehold Scripts/Fort/CLAUDE.md`: tower plots, build wheel, supply.
- `Assets/Bladehold/Bladehold Scripts/DamageSystem/CLAUDE.md`: combat core.
- `Assets/Bladehold/Bladehold Scripts/Enemies/CLAUDE.md`: AI, roster CSV, captains.

## Key packages

URP 17.3, Input System 1.18, AI Navigation 2.0 (baked NavMesh required per battle scene), Cinemachine 3.1, Timeline, Visual Scripting. Full list in `Packages/manifest.json`.
