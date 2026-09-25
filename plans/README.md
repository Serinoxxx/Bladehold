# Bladehold plans: index and roadmap

**One plan per agent session.** Open a fresh session, say "execute `plans/NN-*.md`", and let it finish, compile-check and update the plan's checkboxes. Review, playtest, commit, then move to the next one. Don't run two plans that touch the same files at once.

Every plan follows `/CLAUDE.md` (source of truth). Key rules: no visuals or fallback UI built in code, MMF for all feedback, prefab-based data-driven UI mockups with Synty art + Texturina/Grenze, anything needing the Editor goes in `plans/editor/NN-<topic>.md` (one checklist per plan, linked from the plan) and the session summary (never `TODO.md`, which is Lance's own list).

## The plans

| # | Plan | Kind | Depends on |
|---|---|---|---|
| 00 | [Editor checklist (Lance)](00-editor-checklist-human.md) | Human | Runs alongside everything |
| 01 | [Run flow fixes](01-run-flow-fixes.md) | Fix | none |
| 02 | [Campaign Map: review + fix](02-campaign-map-review.md) | Review + fix | 01 |
| 03 | [Elemental draft system: review + fix](03-elemental-draft-review.md) | Review + fix | none |
| 04 | [Fishing minigame: review](04-fishing-review.md) | Review + fix | 01 |
| 05 | [Balance Tree Editor: review + usability](05-balance-tree-editor-review.md) | Review + tooling | 03 |
| 06 | [Sector difficulty + enemy roster in waves](06-sector-difficulty-and-roster.md) | Feature fix | 01, 02 |
| 07 | [Demo gating](07-demo-gating.md) | Feature | 02, 06 |
| 08 | [Legacy code removal](08-legacy-cleanup.md) | Cleanup | 01, 02 |
| 09 | [Code-built visuals + MMF audit](09-visuals-and-mmf-audit.md) | Audit + cleanup | 08 |
| 10 | [Skills refresh](10-skills-refresh.md) | Tooling | none |
| 11 | [Test strategy](11-test-strategy.md) | Tooling | 08 |
| 12 | [Mount from start + fishing restrictions](12-mount-and-fishing-restrictions.md) | Fix | none |

## Roadmap to Next Fest (Feb 27 2027)

About 22 weeks at roughly 10 hrs/week, so around 220 hours of your time. AI multiplies the code side, but your time goes on playtesting, taste and decisions. The phases matter more than the dates.

**Rule #1: feature freeze starts now.** Put new ideas in `plans/PARKING_LOT.md`, not into the game. Content (cards, shop items, tower upgrades) is allowed in Phase 4 only.

1. **Stabilise** (plans 01, 02, 03, 04, 08, 10, 11, 12). Make the loop work end to end with no broken systems. Exit test: you play Meta → map → 3 sectors → die → Meta, and nothing is broken or confusing.
2. **Demo slice** (plans 05, 06, 07). Demo gating, difficulty curve, the full enemy roster in waves. Exit test: a demo build (a real player build, not the Editor) where a friend can play 30-45 min unassisted.
3. **Readability pass** (plan 09 plus new UI plans). UI polish with human review, text readability, controller support, localization plumbing. Exit test: whole demo playable on a controller, every string localized, UI signed off by you.
4. **Content + balance.** Draft cards, shop items, tower upgrades/synergies, enemy and pacing balance using the `balance-sim` skill plus real playtests. Exit test: 5+ external playtesters, balance changes driven by their data (RunTelemetry CSVs).
5. **Ship prep.** Store page, trailer, capsule art, press/creator outreach, Steam build pipeline, final bug bash. Check Valve's Feb 2027 Next Fest dates for the registration, press-preview and final-asset cutoffs; they land weeks before the event, so this phase starts no later than early January.

## Who does what

- **Give to AI:**
  - Bug fixes, refactors, code reviews, legacy removal.
  - Tests, data plumbing (CSV/SO/`RunSession`), balance-sim runs.
  - Draft first versions of card/shop/upgrade *text and numbers* for you to edit.
  - UI mockups (prefab-based, flagged for review).
  - Editor wiring via Unity MCP when connected.
- **Do yourself:**
  - Design calls: what's in the demo, what an upgrade should feel like.
  - Game feel and juice tuning (MMF timings, screenshake strength).
  - Final UI sign-off, art/audio selection, animation work.
  - Playtesting and watching others play.
  - Steam/marketing voice.
- **Your routine:** each session, pick the next plan, hand it to an agent, and while it runs, do an item from `00-editor-checklist-human.md` or playtest. End each session by committing and writing down anything that felt bad (that's your real backlog).
