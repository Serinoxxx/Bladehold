---
name: add-skill-line
description: Use when adding a new skill line, upgrade node, or player mechanic to Bladehold's gold or Reincarnate skill trees — new StatTypes, passive components, buffs, procs, or CSV-only stat nodes.
---

# Add a skill / player mechanic

You are adding draft skills to `Assets/Bladehold/Resources/DraftUpgrades.csv` or permanent meta perks (`MetaPerkDefinitionSO`), backed by `StatType` and player components. **Reuse the existing patterns below — do not invent a parallel system.**

## Ground truth first (do these before writing anything)

1. For in-run draft skills, read `Assets/Bladehold/Resources/DraftUpgrades.csv` and `Upgrades/DraftUpgradeDefinition.cs`. Columns:
   `id,displayName,category,weapon,element,isUltimate,maxLevel,description,upgradeText,effects,iconName,targetSlot,isDuo,prerequisiteElements`
   Categories are `Weapon`, `Fortress`, or `Elemental`.
2. For permanent meta upgrades, see `MetaUpgradesUI.cs` and `MetaPerkDefinitionSO` (purchased via Goblin Blood and Orcish Metal in the Meta Area).
3. Read your chosen exemplar file (table below) end to end before writing the new component.
4. Grep `Assets/Bladehold/Bladehold Scripts/` before assuming a mechanic/helper doesn't exist — AGENTS.md is a map, not an inventory.

## Step 1 — Pick the shape (copy the exemplar, don't improvise)

| Wanted mechanic | Copy this exemplar |
|---|---|
| Pure stat bump, no new behaviour | CSV rows only — e.g. `sword_dmg` (Flat, per-level `\|` amounts) or `move_1` (Percent on a base-1.0 multiplier stat). No code needed if the stat already exists. |
| Passive on the player's melee hits | `Player/VampiricBlade.cs` — subscribes to the sword `DamageTrigger.OnHit` |
| Negate/shape incoming damage | `Player/DamageBlocker.cs` (`Health.TryBlockDamage`, cooldown), `Player/Parry.cs` (chance + facing + `unparryable`/`elemental` exclusions), `Player/RageBuff.cs` (`Health.ScaleDamageTaken` multiplier) |
| React to another skill's success | `Player/Counterstrike.cs` — listens to `Parry.OnParried` (add an `event Action<...>` to the source skill; it must not know its listeners) |
| Timed, stackable buff + pickup orb | `Player/ImpulseBuff.cs` + `Economy/ImpulseOrb.cs` (orb is a deliberate `Coin` sibling, not a subclass) |
| On-hit proc (chance-rolled) | `Player/ChainLightning.cs` |
| Cheat-death / on-death ability | `Player/DeathNova.cs` (`Health.TryPreventDeath`), `Player/GoldOnDeathCollector.cs` (real `OnDied`) |
| Spawn-marked enemy variant | `Enemies/GoldenGoblin.cs` / `Enemies/ImpulseGoblin.cs` (marked by `WaveSpawner` right after `Instantiate`, before `Start`) |
| Weapon-side behaviour (crit, charge, range) | Extend `DamageSystem/DamageTrigger.cs` / `Player/PlayerAttack.cs` — read how `ChargeDamageBonus` / `MaxHitsPerSwing` flow through first |

`Health`'s only behaviour-altering hooks are `TryBlockDamage`, `ScaleDamageTaken`, and `TryPreventDeath`. Everything else must be a plain `OnDamaged`/`OnDied` listener. Never modify `Health` for a skill.

## Step 2 — StatType plumbing

1. Add the enum member(s) to `Stats/StatType.cs`.
2. The system that **owns** the number registers the base in its `Start` via `Player.Instance.Stats.SetBase(...)` — never hardcode a literal a node might later modify. **Base 0 = locked** is the unlock convention (see `DeathNovaCharges`, `ParryChance`): the component early-returns while the stat is 0, and the CSV unlock node sets it nonzero.
3. Consumers call `GetValue(...)` fresh each use (never cache across purchases). Multiplier-style stats (`MoveSpeed`, `SwordRange`, `GoldDropMultiplier`) use base 1.0.
4. Add a label/format entry to the table in `Stats/StatDisplay.cs` (`StatFormat.Number/Integer/Percent/Multiplier/Seconds`) so dynamic tooltips render before→after values. Skipping this degrades to an auto-split name — acceptable but sloppy.

## Step 3 — Author the CSV rows

- One node per skill, leveled via `maxLevel` + `growth` (cost of level N = `cost × growth^(N-1)`; `SkillTreeService.GetCost` computes it — UI must never read `node.cost` directly).
- `stat`/`kind`/`amount` accept `;`-separated lists for multi-effect nodes (equal lengths required); `amount` accepts `|`-separated per-level values (e.g. `1|2|3` = +1 then +2 then +3). Combined: `1;0.5|0|0` (see `charge_unlock`).
- `prereqs` is `;`-separated and **symmetric** — buying either end of a link reveals the other. It does NOT mark roots.
- `root` (last column, truthy `1`) is the only thing that makes a start-visible node. Every new branch needs exactly one root or a prereq path to the existing tree.
- `icon` names a sprite in the tree's `SkillTreeSO.icons` list. Blank = no icon (degrades gracefully). New icon sprites can only be registered in the Editor (**Bladehold > Skill Tree Editor**) — put that in the TODO entry, don't try to edit the `.asset` file.
- `x,y` are layout coords — look at neighbouring rows and pick an empty region; flag the positions as placeholder in the TODO entry.

## Step 4 — Component conventions (non-negotiable)

- New scripts go in `Assets/Bladehold/Bladehold Scripts/Player/` (or `Economy/` for pickups). No `.asmdef`, no namespace changes — everything compiles into `Assembly-CSharp`.
- `OnValidate` auto-wires sibling refs (`GetComponent<...>()`); `Start` null-checks each, `Debug.LogError`s, and sets an `anyError` flag; handlers early-return `if (anyError)`.
- **Exception**: the melee `DamageTrigger` is always an explicit serialized assignment, never auto-wired — the player carries several triggers (nova hitbox etc.). The `VampiricBlade` precedent. Also note `PlayerWeaponManager` re-points shared listeners at the active melee weapon's trigger via setters (`OnMeleeChanged` / `PlayerWeaponManager.Instance.ActiveMeleeDefinition`).
- Always unsubscribe from events in `OnDestroy`.
- Juice = optional serialized `MMF_Player` fields, null-safe. Any feedback that can play during the frozen intermission (`Time.timeScale = 0`) must use MMF Unscaled time mode — note it in the TODO entry.
- Tunables that aren't upgradeable stats go on a `ScriptableObject` (`*SO`, `[CreateAssetMenu(menuName = "Scriptable Objects/...")]`); the TODO entry tells the user to create the asset instance.
- Never modify vendored code (`Assets/Third Party/`, LeanTween, Feel, DamageNumbersPro). To reach Synty controller internals, use cached reflection — the `Player/PlayerMoveSpeedBinder.cs` / `AttackCancelsSprint.cs` precedent.

## Finish protocol

1. Run `/compile-check` (new files must be added to `Assembly-CSharp.csproj` first — that skill explains).
2. **Automated Behavioral Test (Mandatory)**: Use `/maintain-mechanic-tests` to append an automated behavioral assertion to `WeaponReachBenchmark.cs` (or Unity Test Runner) verifying that the new skill, perk, or stat modifier behaves as designed, and verify the test passes.
3. If the node touches combat/economy stats, `/balance-sim` — prototype it with a `node.<id>=<level>` override to see the pacing effect, and re-run the baseline once the CSV lands.
4. Write the wiring + manual-verification entry via `/editor-wiring-todo` (component additions to `Player.prefab`, SO assets, icons, balance pass); with the Editor open, `/editor-wire` executes the MCP-doable items.
5. Commit directly to `main` and push (no branches/PRs — solo project rule in AGENTS.md).
