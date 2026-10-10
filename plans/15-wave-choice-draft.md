# 15: Wave choice draft (replaces war banners)

**Decided (Lance, 2026-09-27):** between waves the player picks one of **3 wave cards**. Each card bundles an **objective**, a **stance** (Defence = fight at the gate under your towers; Offence = leave tower cover), a **difficulty** (1-3 skulls, with a named enemy modifier) and a **reward bundle**. The card replaces the war banner, the random objective roll and the reward chest in one decision. Offence and more skulls pay more. The player chooses based on their HP, build and playstyle.

This is a deliberate exception to the feature freeze in `README.md`: it *simplifies* the loop (three systems become one choice) and fixes difficulty tiers that currently do nothing.

Design calls (all answered 2026-09-27):

> **Superseded by plan 22 Phase 7 (2026).** Wave cards now choose the *enemies* you fight (a rolled composition), and objectives become optional non-gating bonuses. Specifically: **stance is removed** (Defence/Offence no longer exist); **objectives no longer fail the wave** — a wave ends when its card's enemy composition is cleared (survival = reward), the gate is still the only run-failure state. The "Failure" and "Card mix" rows below (stance split, offence fail path) no longer apply; skulls, the clan modifier, the reward bundle and the gate-as-failure principle carry over. See `plans/22-playtest-feedback-oct10.md` Phase 7.

| Question | Decision |
|---|---|
| Reward | **Bundle per card:** gold + supply + one bonus (draft pick / Goblin Blood / Orcish Metal / Troll Heart), all scaled by stance and skulls |
| Skulls | **Named clan modifier + scaling:** every card rolls a clan (reusing the 5 `WarBannerClanSO`s and the unused `EnemyBuffController`), skulls scale enemy HP, count and modifier strength, and 3 skulls adds a captain |
| Failure | **The gate is the failure state.** If the gate falls, the whole run is lost (`Gate.OnAnyGateDestroyed`, as today). Gate HP carries across sectors (`RunSession.FortressGateCurrentHealth`), so damage taken is a run-long cost. **Defence objectives have no separate fail condition:** their risk is gate damage. **Offence objectives can fail** (timer or escape), which loses the card reward and ends the wave. Kill gold, kill supply and the flat wave-clear supply are always kept. |
| Wave 1 / 5 | **Wave 1 fixed** 1-skull Hold the Gate (no draft). **Waves 2-4 drafted.** **Wave 5 fixed captain wave** (no draft). 3 drafts per sector. |
| Order | **Pick card → build towers → Ready.** Knowing the objective drives where you build. |
| Collection | **Auto-grant + popup** on success, with no chest. A draft-pick bonus opens the existing skill-card modal. |
| Card mix | **≥1 Defence and ≥1 Offence** per draw, skulls rolled per card and ramping with wave and sector threat, no duplicate objective in a draw, and last wave's objective down-weighted |

## The new sector loop

```
Sector start ─► prep (build) ─► Ready ─► W1 Hold the Gate (1 skull, fixed)
   ┌──────────────────────────────────────────────────────────────┐
   ▼                                                              │
 objective resolves ─► rout stragglers ─► reward popup (if success)
   ─► WAVE DRAFT (3 cards) ─► prep: build towers, objective preview markers
   ─► hold Ready ─► 3-2-1 ─► wave N (2-4) ────────────────────────┘
 after W4 ─► prep ─► Ready ─► W5 Captain Assault ─► victory
```

## The card

This is what the card has to communicate, in order of glance priority:

```
┌──────────────────────────────┐
│ ⚔ OFFENCE   · leaves towers  │  1. Stance tag: colour + icon (red sword / blue shield).
│                              │     Offence also shows a "leaves tower cover" chip.
│    FREE THE PRISONERS        │  2. Objective title
│  Break 3 cages before time   │  3. One-line rule
│  runs out.                   │
│  ☠ ☠ ☐   MEDIUM             │  4. Skulls + label, tier-coloured
│  [clan] Ironfang: goblins    │  5. Enemy modifier (clan icon + concrete effect,
│         take 20% less damage │     magnitude already scaled by skulls)
│  [crown] Captain Kombusta    │     3 skulls only: captain line
│ ──────────────────────────── │
│  ⏱ 3:00 time limit           │  6. Timer, or "No time limit"
│  ✖ Fail: any cage locked     │  7. Fail condition, in plain words
│     when time runs out       │
│ ──────────────────────────── │
│  REWARD            ×2.25     │  8. Reward bundle with icons and amounts, plus the
│  [g] 180 gold                │     total multiplier chip
│  [s]  90 supply              │
│  [b]   2 Goblin Blood        │
│  ✦ Fresh: +10%               │  9. Variety bonus when this objective wasn't played
└──────────────────────────────┘     in the last wave (tunable, can be 0)
```

Also shown alongside the cards:
- **The sidebar** (existing `Sidebar_PlayerInfo`): add **gate HP** next to player HP and supply, so a health-based choice is informed at a glance.
- **Objective preview:** after a pick, the prep phase shows where the objective will happen (ghost cages, catapult markers, ram start and route line, via the existing waypoint tracker), so towers can be placed for it.

## Objective roster

Every objective exists today; this plan adds stance, fail rules and card data.

| Objective (class) | Stance | Timer | Fail condition (new ones in **bold**) | Notes |
|---|---|---|---|---|
| Hold the Gate (`KillEnemiesObjective`) | Defence | none | none (gate falls = run over) | Kill target scales with skulls. It is wave 1's fixed objective and also draftable. |
| Stop the Battering Ram (`StopBatteringRamObjective`) | Defence | none | none (gate falls = run over) | Unchanged: the ram keeps hitting the gate (50 per 5 s) until destroyed. That *is* the pressure. |
| Defeat the Siegebreaker (`DefeatSlayerObjective`) | Defence | none | none (gate falls = run over) | Unchanged: charges the gate. Fix the "Seigebreaker" typo in the prefab and scene titles. |
| Goblin Rush (`GoblinRushObjective`) | Defence | 2:00 | none (gate falls = run over) | Survive the timer. |
| Free the Prisoners (`FreePrisonersObjective`) | Offence | 2:00 | Cages still locked at 0:00 (exists) | |
| Destroy the Siege Engines (`DestroySiegeEnginesObjective`) | Offence | 2:00 | Engines standing at 0:00 (exists) | **Remove the 100 gate-damage penalty** (failure = lose reward only). |
| Golden Goblin (`GoldenGoblinObjective`) | Offence | 0:30 | **It escapes** (today escape counts as complete) | Its own coin drip stays. Fix the bugs below. |
| Protect the Supply Wagon (`ProtectWagonObjective`) | Offence | **3:00** | **Wagon not delivered in time** | Its gold bags become part of the card reward (remove the separate payout, so rewards aren't doubled). |
| Captain Assault (**new**, `DefeatCaptainObjective`) | n/a | none | cannot fail | Wave 5 only, never offered in a draft. |

**How the stances trade off:**
- **Defence:** a guaranteed reward, but enemies (and the ram or Siegebreaker) are aimed at the gate, and every point of gate damage is permanent for the run.
- **Offence:** the player leaves the gate to the towers, and the reward can be lost on a failed timer. The pay is higher to make up for both.

**On the card:**
- **Defence cards** show "⚠ Gate at risk", plus a threat line where there is one ("Ram: 50 dmg every 5 s at the gate"), in place of a fail line.
- **Offence cards** show their fail line ("✖ Fail: any cage locked at 0:00").
- The sidebar shows **current gate HP** next to the cards, since it's the number the whole choice hinges on.

**Gate repair costs supply (decided, Lance 2026-09-27).**
- During prep, the player can spend tower supply to repair the gate at a tunable ratio, starting at **1 supply = 1 gate HP**.
- This is the intended tension: supply spent on the gate is supply *not* spent on towers, so repairing keeps the run alive but weakens it. Choosing Offence (more supply in the reward) partly pays for the repairs it causes.
- There is no separate repair reward. Supply is the only way to recover gate HP.

## Data

Following house rules, designer rows go in a CSV and tunables in an SO:

- **`Assets/Bladehold/Config/WaveObjectives.csv`**, one row per objective:
  `id,stance,locKey,icon,timerSeconds,failRule,failParam,weight,minWave,minThreat,draftable,demoEnabled`
  - `failRule` ∈ `Timer, Escapes, None`. Defence rows are `None`, since the gate itself is the loss condition.
  - `id` matches `ISurvivorsObjective.ObjectiveId`, which is how rows map to scene components.
- **`WaveChoiceConfigSO`** (`[CreateAssetMenu]` under `Scriptable Objects/Waves/`):
  - **Reward base per wave index:** gold, supply, and the bonus pool with weights and base amounts (draft pick, Goblin Blood 2, Orcish Metal 2, Troll Heart).
  - **Stance multiplier:** Defence 1.0, Offence 1.5.
  - **Skull multiplier:** reward 1.0 / 1.5 / 2.25. Enemy HP 1.0 / 1.15 / 1.3. Kill quota 1.0 / 1.15 / 1.3. Modifier magnitude 0.6 / 1.0 / 1.4 × clan base. Captain on 3 skulls.
  - **Skull roll table** by wave (2/3/4) and sector threat band. For example: wave 2 at threat 1-2 weights 60/35/5, and wave 4 at threat 5+ weights 15/45/40.
  - **Draft-pick bonus count:** 1, or 2 on 3-skull cards (two modal openings in a row).
  - **Variety bonus %:** default 10.
  - **Rout duration.**
- **Wave 1 and wave 5 definitions** (fixed objective, skulls and reward): fields on the same SO.

Starting numbers, to tune from telemetry. Base 80 gold / 40 supply: a 1-skull Defence card pays 80g/40s, and a 3-skull Offence card pays 270g/135s. Compare towers at 25-50 supply to build and 40 × level to upgrade. Treat 135 supply as the upper bound.

## Progress

**Session 1 (2026-09-28): Phases A+B done** (Unity MCP connected, Unity compiled clean). The flow is untouched: war banners still run until Phase C.
- **Data:** `Config/WaveObjectives.csv` plus `Waves/WaveChoice/`:
  - `WaveObjectiveCatalog` (parser + `WaveStance`/`WaveFailRule`);
  - `WaveChoiceConfigSO` (asset at `Resources/WaveChoiceConfig.asset`, loaded with `WaveChoiceConfigSO.Load()`);
  - `WaveCard` (model + `ClanModifierMath`);
  - `WaveCardGenerator` (`Roll`, `BuildFixed`, `Build`).
- **Deviations from the spec above:**
  - The CSV has four extra English text columns (`title,rule,failText,threatText`). They're the `Loc.Get(key, english)` fallbacks, the same way `DraftUpgrades.csv` carries its own English.
  - Captain Assault's id is `defeat_captain`.
  - `WarBannerClanSO.locKey` was added now, not in Phase D, because the card needs clan names.
  - The gate-repair tunables already sit on `WaveChoiceConfigSO`.
- **Skull scaling of clan magnitudes:** only the bonus part of Haste/Berserk multipliers scales (Haste 1.35 at 1 skull → 1.21). Armor is clamped to 0.9. This matches `EnemyBuffController`'s units, so Phase D can pass `card.modifierMagnitude` straight to `Initialize`.
- **UI:**
  - `UI/WaveCardUI.cs` + `Bladehold Prefabs/UI/WaveCard.prefab`.
  - `SurvivorsCardSelectUI.OpenWaveChoice(cards, onPicked)` instantiates cards into `WaveCardsRow` and hides the skill `CardsRow`.
  - `MenuFocusController.SetDefaultSelectable` gives pad focus to runtime-built rows.
  - Sidebar `gateHealthText` reads `RunSession.FortressGateCurrentHealth/MaxHealth`.
- **DevConsole:** a **Wave Choice preview** row (wave 2-4, then Show Wave Cards) opens a real draw and logs the pick. Phase E's `wavecard <id> <skulls>` force is still to do.
- **Benchmark:** section 27 of `WeaponReachBenchmark` checks:
  - CSV parse, and CSV ↔ prefab objective ids;
  - the mix rules over 1000 draws;
  - last-objective down-weighting and reward maths;
  - the fixed waves and clan scaling.

  Its checks were also run ad hoc via MCP (0 mix, duplicate or count violations; last objective 11% vs 41%). The full suite wasn't run.

**Session 2 (2026-09-28): Phases C-E done in code** (both csprojs build clean; not yet Play-tested). The editor checklist has the scene wiring and a full Play-mode pass.
- **Deviations:**
  - The objective resolving ends the wave; the kill quota only sizes spawns (Hold the Gate's target is set to the quota).
  - The Ready prompt and countdown show in the objective tracker via `GameLoopManager.StatusText`, since the countdown text fields are unwired.
  - DevConsole "commands" are buttons: the console has no text input.
  - Golden Goblin uses a new `ISuppressRegularSpawns` marker.
  - Gate max HP is **200** (`DoorSO`), so 1 supply / HP is on the pricey side, not too cheap.

## Tasks

### Phase A: Data + card model (no flow changes yet)
- [x] `WaveObjectives.csv` + loader (mirror `DraftUpgradeService`'s CSV load), `WaveChoiceConfigSO`, and a `WaveCard` model (objective row, stance, skulls, clan + scaled magnitude, captain flag, reward bundle, variety flag).
- [x] `WaveCardGenerator.Roll(wave, threat, lastObjectiveId, rng)`: enforces ≥1 Defence and ≥1 Offence, no duplicates, down-weights the last objective, and respects `minWave`/`minThreat`/`draftable`/demo gating. Cap offence cards at 2 when the pool is small.
- [x] Register the new files in the csproj and `/compile-check`.
- [x] Benchmark checks (`/test-mechanic`): the mix rule holds over 1000 rolls, reward maths matches the SO, and no Captain Assault appears in drafts.

### Phase B: Wave card UI (mockup, flagged for human UI review)
- [x] `/ui-mockup`: `WaveCard.prefab` (a variant of `Card.prefab` or a sibling) with a `WaveCardUI` component and a `SetData(WaveCard)` that fills every field in "The card" above. Use Synty art, Texturina/Grenze, and `Loc.Get(key, fallback)` for every string. Make no code-built visuals.
- [x] `SurvivorsCardSelectUI.OpenWaveChoice(cards, onPicked)`: reuse the pause, click guard, fade and callback plumbing. Header: "Choose your next battle". No banish. Add a reroll only if Lance asks (`DraftRerollsRemaining` is still unused).
- [x] Gate HP row in the sidebar.
- [x] Gamepad focus and navigation across the 3 cards.
- [x] MMF hover and select feedbacks (`/feel-integration`).

### Phase C: Flow rewrite in `GameLoopManager`
- [x] Replace the banner fields with `CurrentWaveCard` (drop `CurrentClanBuffSO`, `CurrentBannerRewardSO`, `CurrentWaveBuff`, `CurrentWaveBounty`, `CurrentWaveDifficultyTier`). Update the readers: `BannermanAura` (clan buff type now comes from the card's clan), `ObjectiveWaypointTrackerUI`, `ObjectiveTrackerUI`, captain spawn and the campaign seeding in `Start`.
- [x] New intermission: reward popup → `OpenWaveChoice` → prep phase with objective preview markers → **Ready** → 3-2-1 → `StartWave`. Wave 1 and wave 5 skip the draft.
  - **Ready input:** **T** / gamepad **D-pad Down** (decided, Lance 2026-09-27), held for 1 s. HUD prompt: "Hold [T] to start the wave".
    - Rename the dead `DraftSkills` action in the Synty `Controls.inputactions`, which is already bound to T + D-pad Down and has no subscribers, to `StartWave`. Update `InputReader` (`onDraftSkillsPerformed` becomes `onStartWavePerformed`, plus started/canceled for the hold). Hand-update `Controls.cs` the same way as plan 12, and check that the rebind list shows the new name.
    - Also switch `FishingManager.Update` (L228-231, a raw `Keyboard.current.tKey` read today with no gamepad support) to the same action, so "T / D-pad Down = start" is one consistent control everywhere.
  - Wire up the countdown text fields, which are empty today, so the 3-2-1 is actually visible.
- [x] `SurvivorsObjectiveManager.StartObjective(string id)`: a real entry point (not the Debug one) that the flow uses in place of `PickNextRandomObjective`. Keep the random pick only as a fallback when there's no card.
- [x] **Resolution:** on success, auto-grant the bundle (`ApplyBounty` logic reworked for bundles: gold → `AddInRunGold`, supply → `AddInRunSupply`, Blood → `AddGoblinBlood`, draft → `OpenDraft` chained, Troll Heart → max HP). Keep the heal perks (regeneration, Special Herbs) on wave clear. On failure: the fail banner (`WaveClearedBannerUI` already handles it) and no card reward.
- [x] **Rout the stragglers** on resolution (success or fail): remaining enemies flee to the nearest spawn point and despawn, reusing the captain morale-break stun then retreat. Kills during the rout still pay. This replaces the Cleanup phase wait, and the 45 s lightning backstop becomes a fallback only.
- [x] Remove the banner and chest runtime: `SpawnWarBannersRoutine`, `BannerTeardownRoutine`, `SpawnPowerupForCurrentBounty`, `HandlePowerupClaimed`, and the `warBannerPrefab`/`upgradePowerupPrefab` fields. Leave `WarBannerController`, `WaveUpgradePowerup` and the reward SOs in place for plan 08-style deletion after a playtest confirms (list them in the editor checklist).
- [x] `TowerPlot.IsPrepPhase` stays true through the new prep phase and false once Ready is pressed.

### Phase D: Objectives, fail rules and modifiers
- [x] Add the Offence fail conditions from the roster table: remove the Siege Engines gate penalty, make a Golden Goblin escape raise `OnFailed`, and add the Wagon timer. Defence objectives keep today's behaviour; the only loss is the gate falling, which ends the run.
- [x] **Gate repair with supply** (prep phase only, same `IsPrepPhase` gate as `TowerPlot`):
  - A `GateRepairStation` interactable at the gate (an `Interactable`, like the tower plots). **Hold [E]** pours supply into the gate at a steady rate, e.g. 20 HP/s, and stops at max HP or when supply runs out. The prompt shows "Repair gate: 1 supply / HP · 340/600 HP".
  - Heals through `Health` (add a public `Heal`/`Revive`-style call if one doesn't exist), and `RunSession.FortressGateCurrentHealth` follows as `Gate` already mirrors it.
  - Spend supply via `RunSession` (the same path `BuildWheelUI` uses).
  - Tunables on `WaveChoiceConfigSO`, or a small `GateRepairConfigSO`: `supplyPerHp` (default 1), `repairHpPerSecond`, and optionally a max repair per prep phase (default unlimited).
  - MMF feedback: hammering loop plus a gate-bar tick (`/feel-integration`).
  - Telemetry: supply spent on repair per wave, to see how often players choose repair over towers.
  - Gate max HP is set in the scene (not verified headlessly). Check it when tuning `supplyPerHp`: if the gate has thousands of HP, 1:1 may be too cheap compared with 25-50 supply towers.
- [x] Every objective reads `timerSeconds` and its fail parameters from its CSV row, so the card and the objective can't disagree.
- [x] **Golden Goblin bugs:**
  - add the missing `$` in `ProgressText` (L39);
  - remove the double completion via `DebugCompleteObjective` + `OnCompleted` (L128-133, L158-162);
  - make its `StopSpawning` stick (implement `IOverrideEnemySpawns` or a no-spawn marker that `StartWave` respects).
- [x] **Modifiers:** attach and initialise `EnemyBuffController` from the spawner's post-spawn hook (`SurvivorsSpawner.cs` ~L532, after `EnemyDefinitionApplier.Apply`) with the card's clan and scaled magnitude. Apply the skull HP multiplier at the same point. Skull quota multiplier in `StartWave`.
  - Check that all 5 buff types (Shield, Haste, Berserk, Regen, Armor) actually work. The component has never run.
  - Give each clan a `locKey` and a plain-language effect line for the card.
- [x] **Captain on 3-skull cards:** `SpawnCaptainForWave` with the tier mapped from skulls. Retire `BannerDifficultyTier`, or map skulls 1/2/3 to Standard/Enraged/Nightmare so the captain scaling code is untouched. Captain death rewards stay.
- [x] **`DefeatCaptainObjective`** for wave 5: spawns the node's captain at the sector-threat tier, ends the wave on its death, then victory. Fraglob's `captainPrefab` is empty and falls back to Kombusta, so either fix that or accept it for the demo.
- [x] **Campaign node:** `CampaignNodeSO.tierIndex` feeds the skull roll table, and `clanBuff` biases which clan shows up (×2 weight). Drop the node `bountyType` seeding. Update `CampaignTooltipUI` to say "Clan: Ironfang" and not show a buff that never applied.

### Phase E: Telemetry, docs, cleanup
- [x] `RunTelemetry`: a `wave_choice` row per draft: the 3 offered cards (id/stance/skulls), the pick, seconds to decide, and the player HP% and gate HP% at pick time. Add outcome (success/fail) and reward granted to the `wave_clear` row's `detail`. This is what the balance pass needs: pick rates and fail rates per stance and skull.
- [x] Update the `WeaponReachBenchmark` banner-tier tests (L711-777) and the wave simulations (L2542-2559, `SetupGameLoopAssets.cs:900-928`).
- [x] DevConsole: `wavecards` (reroll or show the draft now), `wavecard <objectiveId> <skulls>` (force a card).
- [x] Docs: `Waves/CLAUDE.md` "Sector sequence" (it also still mentions the removed `SpawnEndgameBoss`), `/CLAUDE.md` if conventions change, and the `add-objective` skill (objectives now need a CSV row, stance and fail rule), `/changelog`.
- [x] Editor checklist: `plans/editor/15-wave-choice.md` (`/editor-wiring-todo`) covers the SO asset, card prefab, Ready binding, wiring the countdown text, the objective preview markers, the sidebar gate row, placing the gate repair station in each battle scene, deleting banner and chest assets after the playtest, and a UI review.

## Needs Lance in the Editor

Moved to its own checklist: [`plans/editor/15-wave-choice.md`](editor/15-wave-choice.md).

## Out of scope / parking lot
- Rerolls, and card rarity beyond "2 draft picks at 3 skulls".
- New objectives. Six draftable objectives plus Hold the Gate is enough for the demo.
- Making towers able to shoot objective props (catapults and cages are layer 0 today). Offence is *meant* to be away from tower help.

## Acceptance
- New run → sector: build → Ready → wave 1 Hold the Gate. On clear: reward popup, then 3 cards, always with at least one Defence and one Offence, every card fully populated (stance, title, rule, skulls, clan line, timer, fail line, rewards).
- Picking an Offence card → prep shows where the objective will be → Ready → it plays with that objective and clan modifier. Enemies visibly tougher at 3 skulls, captain present.
- Failing a timed objective ends the wave with no card reward, and stragglers rout within a few seconds. Succeeding pays exactly what the card showed.
- Wave 5 is always the captain; killing it wins the sector.
- No banners or chests spawn anywhere. Telemetry CSV has `wave_choice` rows.
- Every card string shows `[xx]` in the pseudo-locale (i.e. it goes through `Loc`).

Suggested execution: two sessions (A+B, then C+D+E), with a playtest and commit between them. Phase C is the risky one, since it rewrites the core of `GameLoopManager`.
