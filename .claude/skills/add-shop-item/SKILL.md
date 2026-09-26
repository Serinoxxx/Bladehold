---
name: add-shop-item
description: Use when adding or changing an item in Bladehold's Rest Area shop (bought with in-run gold) — the ShopItemSO asset, its effect type, how the effect is applied and carried between sectors in RunSession, and the shop pool.
---

# Add a shop item

Items are `ShopItemSO` assets (`UI/RestArea/ShopItemSO.cs`) in `Assets/Bladehold/Bladehold Config/ShopItems/`: `itemId`, `displayName`, `description`, `goldCost`, `icon`, `effectType`, `effectValue`, `durationWaves`.

`UI/RestArea/ShopUI.cs` rolls 3 random items from its `itemPool` (4 with the `deep_pockets` perk) each time the shop opens, charges `RunSession.TrySpendInRunGold`, and runs the effect in a `switch` on `ShopItemEffectType`. Slots are `slotPrefab` instances (`ShopSlotUI`).

| Effect type | Template | How it's applied / carried |
|---|---|---|
| `HealInstant` | `maggoty_bread` | `Health.Heal(effectValue)` now. Carried via the HP ratio. |
| `MaxHealthRun` | `troll_heart` | `RunSession.PlayerBonusMaxHealth += effectValue`, re-applied in `RestoreInRunUpgrades`. |
| `WaveEndHealTemporary` | `special_herbs` | Sets `RunSession.SpecialHerbsWavesRemaining`; `GameLoopManager` heals at wave end; `RunSession.OnWaveCompleted` counts it down. |
| `AmmoRefill` | `ammo_bundle` | `RunSession.AddInRunAmmo` + `PlayerAmmo.AddAmmo`. |
| `MoveSpeedTemporary` | `crystal_water` | **Broken:** sets `CrystalWaterWavesRemaining`, but nothing applies the speed bonus. Fix this before copying it. |

## Steps

1. **Reuse an effect type** if one fits: then it's asset-only.
2. **New effect type:** append a value to `ShopItemEffectType` (never reorder; assets serialize the int), add its `case` in `ShopUI`'s apply switch, and store anything that must outlive the Rest Area scene in `RunSession` (a property, reset in `StartNewRun`, re-applied in `RestoreInRunUpgrades` or counted down in `OnWaveCompleted`). For a stat buff, prefer a `StatType` modifier through `PlayerStats` over a bespoke field. Purchase feedback goes through MMF (`/feel-integration`), with unscaled time because the shop pauses the game.
3. Create the asset (Editor): `Create > Scriptable Objects > ShopItemSO` in `Bladehold Config/ShopItems/`, filename = `itemId`, and an icon from `Resources/SkillTreeIcons.asset` or `/generate-sprite-variants`.
4. Add it to `ShopUI.itemPool` in `Bladehold Rest Area Scene`.
5. There's no demo gating for shop items today. If an item must be hidden in the demo, add a static helper on `Demo/DemoConfigSO` and filter in `GenerateStock`; never a per-asset flag.
6. Test in Play mode: the DevConsole grants gold. Add a `WeaponReachBenchmark` check for non-trivial effects (`/test-mechanic`).

## Finish

1. `/compile-check` if you touched C#.
2. The asset and pool entry via `/unity-editor-mcp` if connected; otherwise put them in the plan's `plans/editor/NN-<topic>.md` via `/editor-wiring-todo` with a playtest line (item appears in stock, gold is spent once, the effect lasts as long as it should across the next sector, and it's cleared on a new run). Agents never write to `TODO.md`.
3. `/changelog`, then commit to `main` and push.
