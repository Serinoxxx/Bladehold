---
name: add-captain
description: Use when adding a new named Clan Captain to Bladehold (like Captain Kombusta or Captain Fraglob) or assigning a captain to a campaign combat node — controller + SO, roster row and generated prefab, GameLoopManager spawn routing, tier scaling, death rewards, and the map/intro UI that names it.
---

# Add a Clan Captain

A captain is a named elite who arrives mid-sector. **Template: Captain Kombusta** (`Enemies/Captain/CaptainKombustaController.cs` + `CaptainKombustaSO.cs` + the `captain_kombusta` manifest entry and CSV row). Don't copy Captain Fraglob (`CaptainEnemyController`): its tunables are inspector fields rather than an SO, it has unused `AudioClip` fields, and it has no prefab or roster row.

## Ground truth first

Read these before starting:
- `Waves/GameLoopManager.cs`: the captain block in `StartWave`, plus `SpawnCaptainForWave`.
- `CaptainKombustaController.cs` (`Initialize`, `ApplyDifficultyScaling`, `HandleDeath`).
- `Waves/Banners/WarBannerDifficulty.cs` (`BannerDifficultyHelper`).
- `Campaign/CampaignNodeSO.cs`.

There is **no captain registry** (no SO list, no CSV). Captain data is spread over four places:

| Piece | Where |
|---|---|
| Identity on the map | `CampaignNodeSO.captainName` (string) + `captainIcon` (sprite) on the node assets in `Assets/Bladehold/Resources/CampaignNodes/` (text YAML) |
| Which prefab spawns | Hard-coded name match in `GameLoopManager.SpawnCaptainForWave` |
| Behaviour + tunables | `<Name>Controller` + `<Name>SO` under `Enemies/Captain/` |
| Prefab + CSV-overridable stats | `EnemyManifest` entry + `Config/Enemies.csv` row `captain_<name>` |

## How it works today

- **When**: `StartWave` always spawns a captain on wave 5 (`totalWaves`), at the banner's tier but at least **Enraged**. On waves 1-4 it spawns one only if the picked war banner is Enraged or higher.
- **Which**: `preferredCaptainName` = the current campaign node's `captainName`. If that contains `"Kombusta"` you get Kombusta, anything else gets Fraglob, and blank is a coin flip. Fraglob's `captainPrefab` slot is empty, so it logs an error and falls back to Kombusta.
- **Spawn path**: `captainKombustaPrefab` is empty on the prefab and in the scene, so Kombusta spawns through `SurvivorsSpawner.DebugSpawnEnemyType("captain_kombusta")`. That applies the CSV row, puts it in the spawner's alive set (so the `KillRemainingEnemiesObjective` cleanup and wave clear wait for it), and **doesn't** count it toward the kill quota. It spawns at a random spawn point; `captainSpawnPoint` is only used on the direct-`Instantiate` path.
- **Scaling**: `Initialize(tier, name)` → `ApplyDifficultyScaling`. HP = `SO.baseMaxHealth × GetStatMultiplier(tier)` (1 / 1.25 / 1.5 / 2). Speed is scaled from `SO.baseMoveSpeed`. Melee damage = (`SetDamage` override ?? `SO.baseMeleeDamage`) × multiplier. The `HighlightEffect` outline takes the tier colour. **This overwrites the CSV `health`/`speed` columns**; only `damage` survives (via `damageOverride`).
- **Death** (`Health.OnDied` → `HandleDeath`): Morale Break (pauses every `AIMovement` within 15 m for 2 s), then `RunSession.AddInRunGold(50 × reward)` and `RunSession.AddGoblinBlood(3 × reward)`. `reward` is `GetRewardMultiplier` = 1/2/4/8, and Blood goes straight to `SaveData`, so it's permanent. `OnCaptainDied` has no subscribers yet.
- **UI that names it**:
  - Map node badge: `CampaignNodeButtonUI` shows node difficulty skulls + `captainName` in the tier colour.
  - Map tooltip: `CampaignTooltipUI` shows name, tier, skulls, `captainIcon` and clan buff.
  - In sector: `EnemyIntroUI.ShowIntro("<name> has arrived!", skulls, "<TIER> - NX REWARDS")`.
  - Node `difficultyTier`/`bountyType`/`clanBuff` are **display-only**. `GameLoopManager` reads only `captainName`; the captain's real tier comes from the wave's banner.

## Assign an existing captain to a node

1. Set `captainName` (exactly "Captain Kombusta" / "Captain Fraglob") and optionally `captainIcon`/`difficultyTier` on the node asset in `Resources/CampaignNodes/`. Use the inspector, MCP, or a text edit of the YAML.
2. Mirror it in `CampaignGraphSO.BuildDefaultGraph()` (the `captainName:` argument), otherwise **Bladehold/Campaign/Setup All Campaign Prefabs & Scene** regenerates it away.
3. Update the flavour text if it names a captain (`description` in both places, and `UI/RestArea/AreaDatabase.cs`).

## Add a new captain

1. **SO**: `Enemies/Captain/Captain<Name>SO.cs` with `[CreateAssetMenu(menuName = "Scriptable Objects/Enemies/Captain <Name> SO")]`. Put base HP/damage/speed and every ability number here, with tooltips. Telegraphs and projectiles are **authored prefab** fields (like `telegraphPrefab`/`dynamitePrefab`).
2. **Controller**: copy Kombusta's shape:
   - `captainName`/`difficultyTier` fields and `Initialize(BannerDifficultyTier, string)` → `ApplyDifficultyScaling`.
   - `SetDamage(float)` storing a `damageOverride`, because the CSV override lands before `Start`.
   - Auto-wire in `Awake`, subscribe `health.OnDied` in `Start`, unsubscribe in `OnDestroy`.
   - Stop acting when you or the player is dead.
   - Attacks stamp `Damage.source`/`sourcePosition`, and wide AoEs are `unparryable`.
   - Add a `Start` null-check with `anyError` (Kombusta doesn't have one yet).
   - **Feedback via serialized `MMF_Player`s only** (`/feel-integration`), e.g. Kombusta's `igniteFeedback`. Don't copy `IgniteSelf`'s `Instantiate(fireAuraVfxPrefab)`.
   - Death rewards: reuse the `RunSession.AddInRunGold`/`AddGoblinBlood` × `GetRewardMultiplier` pattern. Put the base amounts on the SO rather than the literals 50/3.
3. **Override routing**: add `enemy.GetComponent<Captain<Name>Controller>()?.SetDamage(...)` to `Enemies/EnemyDefinitionApplier.cs`.
4. **Roster row** in `Config/Enemies.csv`: `captain_<name>`, `enabled` `TRUE`, **`minThreat` 0** so it never joins normal waves. `health`/`speed` are overwritten by the SO scaling (leave them matching the SO). `damage`, `minGold`/`maxGold`, `scale` and `knockbackResistance` do apply. Kombusta's row: `350,20,50,100,3.8,1.35,...,6,TRUE,0`.
5. **Prefab**: a manifest entry modelled on `captain_kombusta` (`soFolder = "Captain"`, `removeComponents` golden/impulse, fire-point child, controller wiring incl. `highlightEffect`), then run the generator (`/generate-enemy-prefabs`). Keep `rootScale` equal to the CSV `scale`.
6. **Spawn routing**: `SpawnCaptainForWave` only knows two captains by substring. Add a branch for the new name that spawns via `spawner.DebugSpawnEnemyType("captain_<name>")`. Keep that path so cleanup tracking and CSV overrides work, then call `Initialize(tier, captainName)`.
   - Check `Roster.Find(id)` first. It ignores `enabled` and map presence, and `DebugSpawnEnemyType` silently spawns a **goblin** for an unknown id.
   - With a third captain, consider replacing the substring branches with a name → roster-id lookup plus a shared captain interface. That's a design change, so confirm it with Lance first.
7. **Assign** it to nodes (section above). Nodes past `DemoConfigSO.campaignCutoffTier` (4) are never reached in the demo. If a captain must be demo-gated, add a static helper on `Demo/DemoConfigSO` (like `IsCampaignNodeLocked`), never a per-asset flag.
8. **Test**: add a `WeaponReachBenchmark` section per `/test-mechanic`. Section 13 (Kombusta: SO values, `Initialize` name/tier, ability states) and 8D (Fraglob tier HP scaling + shield absorb) are the models. Lance runs the suite himself unless he asks.

## Test in Play mode

Open a battle scene (no campaign run means threat 1 and a coin-flip captain), or go through Meta Area → Battle Portal → a node with your captain. Use the DevConsole (backquote) wave/objective controls to reach wave 5 fast, or pick an Enraged+ banner. The spawn-type picker spawns `captain_<name>` directly, but without `Initialize` (no tier scaling or intro).

## Finish protocol

1. `/compile-check` (both csprojs if you touched the manifest/benchmark).
2. Editor work via `/unity-editor-mcp` when connected: run the generator, set node `captainName`/`captainIcon`, and do a Play-mode wave-5 check.
   - Everything else goes in the plan's `plans/editor/NN-<topic>.md` via `/editor-wiring-todo`: MMF players on the prefab, animator triggers, telegraph/projectile prefabs, the captain icon sprite, a balance pass, and a playtest (name on map badge + tooltip + intro, tier outline, cleanup waits for the captain, Blood/gold paid once).
   - Agents never write to `TODO.md`.
3. Commit directly to `main` and push.
