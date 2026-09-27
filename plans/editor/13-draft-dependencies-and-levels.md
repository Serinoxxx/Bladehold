# Editor to-do: draft card dependencies and levels

From a chat request on 2026-09-27 (no plan file). Tick items off as you go and delete the file when it's empty.

**Done in C#:**
- `Resources/DraftUpgrades.csv` has a new last column, `requires`, holding `;`-separated tokens: `ultimate`, `slot:<Element>` (that element is imbued on a slot) and `card:<id>` (that card is owned).
- `Upgrades/DraftUpgradeService.cs`: `ParseRow` reads and validates the tokens. `MeetsRequirements` filters the draft pool, and `ParseCsv` logs an error if a `card:` token names an unknown id. `Debug/DiegeticDraftTester.cs` shows the missing requirement as the reason.
- The requirements are:
  - Kindling needs `slot:Fire`.
  - Deep Freeze needs `slot:Ice`.
  - Shatter needs Deep Freeze.
  - Inferno Burst and Eye of the Storm need an `ultimate`.
  - Thermal Shock needs `slot:Fire;slot:Ice`.
  - Plasma Overload needs `slot:Fire;slot:Lightning`.
  - Superconductor needs `slot:Lightning` plus Deep Freeze, because it only fires on Frozen enemies.
- Level 1 to 4 was added to 10 cards:
  - Changed in the CSV only: Lunge Mastery, ShieldBreaker, Vampire Blade (the heal scales, the penalty stays at 50%), Fire Arrows and Lightning Arrows.
  - Also needed code, because their on/off stat now scales the effect:
    - Inferno Burst damage (`PlayerUltimateController.TriggerInfernoBurst`).
    - Permafrost radius (`TowerPlotManager`).
    - The three duos' damage (`EnemyStatusManager`).
- Left ungated on purpose: tower cards (you can build that tower in any sector), Tesla Spire and Permafrost (the fortress is always there) and Ice Shards (you always carry a ranged weapon).

**Done by an agent via MCP (2026-09-27):** Unity compiled clean. All 49 rows parse with no errors. `MeetsRequirements` was checked in edit mode:
- With no slots set, all 8 gated cards were blocked.
- With Fire on melee and Ice on dash: Kindling, Deep Freeze and Thermal Shock were allowed, while Plasma Overload and Shatter were still blocked.

## 1. Playtest

- [ ] **Fresh run, first elemental draft:** Kindling, Deep Freeze, Shatter, Inferno Burst, Eye of the Storm and all duos never appear until their requirement is met.
- [ ] **Pick a Fire slot card:** Kindling can now appear. **Pick an Ice slot card:** Deep Freeze and Thermal Shock can appear. **Own Deep Freeze:** Shatter can appear, and Superconductor too once Lightning is on a slot.
- [ ] **Pick any weapon ultimate:** Inferno Burst and Eye of the Storm can appear.
- [ ] **Levelled cards:** draft Lunge Mastery (or another of the 10) again and the card shows its upgrade text and level 2/4. The effect is visibly stronger (Inferno Burst blast damage numbers, Permafrost reaching towers further away, duo burst numbers).
- [ ] **Survives a scene change:** levels and requirements still hold after moving to the next sector and after visiting the Rest Area Draft Station.
- [ ] **Balance Tree Editor (F1):** opens with no parse errors, and saving an edit keeps the `requires` column intact.
- [ ] **Decide:** are 4 levels right for the duos, and are the per-level numbers right? They are in the CSV and easy to retune in the Balance Tree Editor.
