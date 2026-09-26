---
name: add-draft-card
description: Use when adding or changing a Bladehold draft card (the 3-card picks from war banners and the Rest Area Draft Station) — a DraftUpgrades.csv row, any new StatType and the component that reads it, elemental slot/duo rules, icon, localization, a benchmark check and demo gating.
---

# Add a draft card

Cards are rows in `Assets/Bladehold/Resources/DraftUpgrades.csv`, parsed by `Upgrades/DraftUpgradeService.cs` (`ParseRow`) into `DraftUpgradeDefinition`s and shown to the UI as `SkillNode`s (`ConvertToSkillNode`; the name is left over from the deleted gold tree). Most cards are **CSV only**: they add modifiers to existing `StatType`s. Code is only needed when the card needs a stat nothing reads yet.

Fishing Frenzy cards are **not** in this CSV; they're `Fishing/FishingUpgradeType.cs` + `FishingUpgradeManager.cs`. Ultimate cards are covered by `/add-ultimate-handler`.

**Tool:** **Bladehold > Balance Tree Editor** (F1, `Editor/BalanceTree/`) edits the CSV losslessly, shows which scripts read each stat and validates rows. Prefer it (or a careful text edit) over rewriting the file.

## 1. The row

Header (the code wins if this drifts; read `ParseRow`):
`id,displayName,category,weapon,element,isUltimate,maxLevel,description,upgradeText,stat,kind,amount,icon,targetSlot,isDuo,prerequisiteElements`

| Column | Rules |
|---|---|
| `id` | Unique, snake_case, prefixed by weapon or element (`sword_lunge_mastery`). Ultimates route on the prefix. |
| `category` | `Weapon` or `Elemental` (`Upgrades/DraftCategory.cs`). Anything else skips the row. |
| `weapon` | A `WeaponDefinitionSO` id (`sword`, `axe`, `mace`, `bow`, `throwing_axe`), or empty. A Weapon card only enters the pool when that weapon is equipped and not demo-locked (`DemoConfigSO.IsWeaponIdLocked`). |
| `element` | `fire`, `lightning`, `ice`. Required on non-duo Elemental cards. |
| `isUltimate` | `1` = one per run; see `/add-ultimate-handler`. |
| `maxLevel` | Picks until the card leaves the pool (min 1). |
| `description` / `upgradeText` | Level 1 text / text shown when you already own it. Player-facing, plain language. Quote fields that contain commas. |
| `stat` / `kind` / `amount` | `;`-separated parallel lists, one entry per effect. `kind` is `Flat` or `Percent`. `amount` can hold `\|`-separated per-level values (`0.1\|0.2\|0.3`). **Per-level amounts are absolute, not cumulative**: level 2 swaps level 1's value for level 2's (`ApplyUpgrade` and `RunSession.RestoreInRunUpgrades` both work this way). An unknown `StatType` name skips that effect with an error. |
| `icon` | Sprite name in `Resources/SkillTreeIcons.asset` (`SkillTreeIconsSO.icons`). Blank = no icon. |
| `targetSlot` | Elemental only: `SLOT_MELEE`, `SLOT_RANGED`, `SLOT_MOBILITY`, `SLOT_ULTIMATE`, `SLOT_FORTRESS` (`RunSession.KnownElementalSlots`). Picking it imbues that slot; a different element already there is overwritten, and its cards are stripped for `ElementOverwriteGold` each. |
| `isDuo` / `prerequisiteElements` | Duo = `isDuo=1`, **no** `targetSlot`, and 2+ `\|`-separated elements (`fire\|ice`). It only enters the pool once all of those elements are active. |

Copy the closest existing row as your template: `sword_lunge_mastery` (multi-effect weapon card), a `fire_*` slot card, or an existing duo.

## 2. New stat (only if nothing already reads the number)

1. **Append** the member to `Stats/StatType.cs`. Never remove or reorder values: assets serialize the ints.
2. The owning system registers the base with `PlayerStats.SetBase` (in `Awake`/`Start`). **Base 0 = locked**: the component early-returns while the value is 0 and the card makes it non-zero. Multiplier-style stats use base 1.
3. Consumers read `GetValue` fresh each use; never cache across picks. `final = (base + Σflat) × (1 + Σpercent)`.
4. Add a row to `Stats/StatDisplay.cs` so tooltips show before → after.
5. Before writing a new mechanic, grep for one to reuse. The dormant stat-gated mechanics (`Parry`, `Counterstrike`, `DeathNova`, `StartMountedSpawner`) are live code that nothing grants; a card can switch one on.

New components follow CLAUDE.md: `Health` is only altered through `TryBlockDamage` / `ScaleDamageTaken` / `TryPreventDeath`; everything else listens to `OnDamaged`/`OnDied` and unsubscribes in `OnDestroy`. Validate refs in `Start`. Feedback is a serialized `MMF_Player` (`/feel-integration`), using unscaled time if it can play while paused. Non-upgradeable tunables go on an SO. Remember `Player.cs` is on the child `SidekickSyntyCharacter` while weapon/ultimate managers are on the prefab root.

## 3. Icon and text

- Icon: pick an existing sprite from `SkillTreeIcons.asset`, or make one with `/generate-sprite-variants` and add it to the `icons` array (Editor work).
- Localization: `SkillNode` supports `locKey` (`<key>.name/.desc/.upgrade` in `Resources/Localization/Strings.csv`), but **`ConvertToSkillNode` never sets it**, so draft cards are English-only today. Don't add `Strings.csv` rows for a card unless you're also wiring `locKey`; flag that as a decision for Lance.

## 4. Check it

- **Bladehold/Tests/Draft Catalog Loading (Edit Mode)** parses the whole CSV with the runtime rules and reports skipped rows.
- Add a `WeaponReachBenchmark` assertion for any new mechanic or non-trivial maths (`/test-mechanic`). Lance runs the suites himself unless he asks.
- Play mode: the DevConsole (backquote) can grant draft cards and set levels.

## Finish

1. `/compile-check` if you touched C#.
2. Editor work (icon registration, components on `Player.prefab`, SO assets): do it through `/unity-editor-mcp` if connected; otherwise list it in the plan's `plans/editor/NN-<topic>.md` via `/editor-wiring-todo`, with a playtest line (card appears only with its weapon/element, levels stack as absolute values, survives a scene change). Agents never write to `TODO.md`.
3. `/changelog` if players will notice, then commit to `main` and push.
