---
name: add-meta-perk
description: Use when adding or changing a Bladehold permanent meta perk (bought with Goblin Blood from the Spirit NPC in the Meta Area) — the MetaPerkDefinitionSO asset, its tier, the code that applies it during a run, and demo tier gating.
---

# Add a meta perk

Perks are `MetaPerkDefinitionSO` assets (`UI/Meta/MetaPerkDefinitionSO.cs`) in `Assets/Bladehold/Bladehold Config/MetaPerks/`: `id`, `displayName`, `description`, `tier` (1–3), `goblinBloodCost`, `icon`, `prerequisites`. Existing perks are the templates:

| Tier | Cost | Perks |
|---|---|---|
| 1 | 10 | `agility`, `backstab`, `deep_quiver`, `regeneration` |
| 2 | 25 | `executioner`, `greed`, `second_wind` |
| 3 | 50 | `deep_pockets`, `master_tactician`, `war_chest`, `second_ultimate` (Twin Fury: the shop can sell a second ultimate, `RunSession.MaxUltimateSlots`) |

## How perks work

- **Buying** (`UI/Meta/MetaUpgradesUI.cs`): tiers 2 and 3 unlock for 5 / 10 Orcish Metal (`SaveData.unlockedMetaTier`). Buying a perk spends Goblin Blood and adds its `id` to `SaveData.purchasedMetaPerks`. The UI shows every perk in `Resources/MetaPerkCatalog` (`MetaPerkCatalogSO`) grouped by `tier`, one `perkCardPrefab` per perk.
- **`prerequisites` is not enforced**: `PurchasePerk` ignores it. Don't rely on it without changing that code.
- **Applying**: there is no generic effect system. Game code checks `RunSession.HasMetaPerk("<id>")` at the point it matters, so the `id` string is the contract. Pick the hook that matches your effect:

| Effect | Precedent |
|---|---|
| Starting run value | `RunSession.StartNewRun` (`war_chest` gold, `master_tactician` reroll, `deep_quiver` ammo) |
| Stat modifier re-applied every scene | `RunSession.RestoreInRunUpgrades` (`agility` → `DodgeMaxCharges`, `deep_quiver` → `MaxAmmo`) |
| Damage rule | `DamageSystem/DamageTrigger.cs` (`backstab`, `executioner`) |
| Cheat death | `second_wind` → `Health.TryPreventDeath` hook wired in `RestoreInRunUpgrades` |
| Wave-end effect | `Waves/GameLoopManager.cs` (`regeneration`) |
| Shop/economy | `UI/RestArea/ShopUI.cs` (`deep_pockets`), `RunSession` (`greed`) |

For a stat effect, prefer an existing `StatType` modifier via `PlayerStats.AddModifier` in `RestoreInRunUpgrades` over a new hard-coded branch. Put any new tuning number on an SO rather than as a literal next to the `HasMetaPerk` check.

## Steps

1. Write the effect code at the right hook (the table above). `SaveData` needs no new field: the perk is just an id in `purchasedMetaPerks`.
2. Create the asset (Editor): `Create > Scriptable Objects > MetaPerkDefinitionSO` in `Bladehold Config/MetaPerks/`, filename = `id`, with the tier cost matching its row above unless Lance says otherwise, and an icon.
3. Add it to the `perks` list of `Assets/Bladehold/Resources/MetaPerkCatalog.asset`. The Meta Area window and `RunSession.GetMetaPerkValue` both read that catalog; there is no per-scene perk list. Ranked perks set `rankCosts` (tier 1: 10/15/20/25) and `rankValues`; read the value with `GetMetaPerkValue`, not `HasMetaPerk`.
4. **Demo:** the demo shows tier-1 perks only (`DemoConfigSO.IsMetaTierLocked`, already enforced by the UI). A new tier-2/3 perk is hidden in the demo automatically; never add a per-asset demo flag.
5. Test: add a `WeaponReachBenchmark` check for the effect (the `backstab`/`executioner`/`second_wind` sections are the model; `/test-mechanic`). In Play mode, the DevConsole grants currencies.

## Finish

1. `/compile-check`.
2. The asset and scene list via `/unity-editor-mcp` if connected; otherwise put them in the plan's `plans/editor/NN-<topic>.md` via `/editor-wiring-todo` with a playtest line (buy it in the Meta Area, start a run, the effect applies and survives a scene change, it's gone after deleting the save). Agents never write to `TODO.md`.
3. `/changelog`, then commit to `main` and push.
