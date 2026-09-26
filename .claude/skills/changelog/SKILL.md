---
name: changelog
description: Use when a Bladehold change is player-visible and you're about to commit, or when asked to read or write release notes — adds plain-language entries to CHANGELOG.md under the current bundleVersion.
---

# Player-facing changelog (CHANGELOG.md)

`CHANGELOG.md` at the repo root holds release notes, newest version first. It is **CRLF, UTF-8 without BOM**. The build copies it into `StreamingAssets/` (`Editor/AutoVersionIncrementer.cs`), so players can read it.

## Format

```markdown
## [0.1.28] - 2026-09-13

### New Features

- Added ...

### Fixes

- Fixed ...

### Balance Changes

### General Changes
```

- The heading version is `bundleVersion` in `ProjectSettings/ProjectSettings.asset`. `AutoVersionIncrementer` bumps the patch number on every Unity build, so never edit the version by hand.
- New entries go under the section for the **current** `bundleVersion`. Create it (all four categories, blank line after each heading) if it doesn't exist yet.
- Categories, in this order: **New Features** (new content or systems), **Fixes** (bugs), **Balance Changes** (numbers: damage, costs, cooldowns, spawn rates, quotas), **General Changes** (QoL, polish, UI layout, removals).

## Style

- Only what a player could notice. Skip benchmarks, editor tools, refactors and MMF migrations with no visible effect.
- Plain language, no internal names: no class, file, SO, CSV, `StatType` or scene-file names, no "prefab", no exception text.
- One line each, starting with a past-tense verb: *Added*, *Fixed*, *Increased*, *Reduced*, *Removed*, *Updated*. No marketing filler.
  - Bad: `Fixed NullReferenceException in FlameZone.cs when floorLayerMask is unassigned`
  - Bad: `Experience a dramatic cinematic entrance for the Slayer boss...`
  - Good: `Fixed fire zones spawning below the ground`

## Helper script

`scripts/changelog.ps1` (run with `pwsh` from the repo root). It is line-based and keeps the file's CRLF endings. `-Version` defaults to the current `bundleVersion`.

```powershell
$cl = ".claude/skills/changelog/scripts/changelog.ps1"
pwsh $cl GetVersion                  # current bundleVersion
pwsh $cl ReadLatest                  # newest section
pwsh $cl Read -Version 0.1.20
pwsh $cl List                        # all versions + dates
pwsh $cl NewBuild                    # empty section for the current version
pwsh $cl AddEntry -Category Fixes -Message "Fixed ..."   # creates the section if missing
```

Editing by hand is fine too. Afterwards `git ls-files --eol CHANGELOG.md` must still show `w/crlf`, not `w/mixed`.

## Workflow

1. `git diff` to see what actually changed for the player.
2. Add entries (script or by hand) under the current version.
3. Commit `CHANGELOG.md` in the same commit as the change, straight to `main`.
