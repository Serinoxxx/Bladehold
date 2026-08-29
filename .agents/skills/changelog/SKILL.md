---
name: changelog
description: Use when reading, writing, or updating Bladehold's consumer-facing changelog (CHANGELOG.md) — handles build version detection from ProjectSettings.asset, categorizing fixes, new features, balance changes, and general changes in concise, minimal language without fluff or jargon.
---

# Consumer-Facing Changelog (CHANGELOG.md)

Bladehold tracks all changes in a concise, player-facing changelog at the project root: **`CHANGELOG.md`**. Every commit must maintain this document so players and testers know what changed in minimal text without fluff or technical jargon.

---

## 1. Structure & Required Categories

Every build section is headed by the build version (matching `bundleVersion` in `ProjectSettings/ProjectSettings.asset`) and release date:

```markdown
## [0.1.12] - 2026-08-29

### New Features
- Added meta progression grid in main menu
- Added 4 periodic elemental weapon imbuements (Fire, Ice, Lightning, Poison)

### Fixes
- Fixed Goblin Brute weapon collision box hitting outside swing arc
- Fixed Fire Imbuement flame zones spawning below ground

### Balance Changes
- Capped active weapon/ability slots to 4
- Rebalanced enemy health scaling across waves 5 through 15

### General Changes
- Updated meta upgrades screen with dark parchment theme and unique icons
- Improved death screen animations and gold tally
```

### The 4 Mandatory Categories:
1. **`### New Features`**: Additions to gameplay, game modes, bosses, abilities, socket systems, UI screens, weapons.
2. **`### Fixes`**: Bug fixes, collision repairs, animation loop corrections, UI desync resolutions, audio cutoffs.
3. **`### Balance Changes`**: Stat adjustments, damage multipliers, cooldowns, gold costs, wave difficulty curves, XP thresholds.
4. **`### General Changes`**: Quality-of-life enhancements, visual/audio polish, UI layout tuning, performance optimizations.

---

## 2. Writing Style: Concise & Minimal (No Fluff, No Jargon)

- **No marketing fluff**: State what changed directly in minimal words. Never use marketing phrasing or dramatic filler (e.g. avoid "Be on the lookout for the elusive...", "Harness 4 epic elements to dominate the battlefield...").
- **No technical jargon**: Never expose internal code variable names, class names, method signatures, or stack traces (e.g. avoid `NullReferenceException`, `FireImbuement.cs:42`, `statType GoldenGoblinChance`).
- **Concise bullet points**:
  - **Bad (fluffy)**: "Experience a dramatic cinematic entrance for the Slayer boss, complete with an epic overhead health bar and distinct encounter phases."
  - **Bad (technical)**: "Fixed NullReferenceException in FlameZone.cs when floorLayerMask is not assigned in prefab."
  - **Good (concise & direct)**: "Added Slayer boss intro and boss health bar"
  - **Good (concise & direct)**: "Fixed Fire Imbuement flame zones spawning below ground"
- Start bullets with simple past-tense verbs: *Added*, *Fixed*, *Adjusted*, *Reduced*, *Increased*, *Updated*.

---

## 3. How Build Versioning Works

In Bladehold, build versions are managed automatically:
- Unity builds trigger `Assets/Bladehold/Bladehold Scripts/Editor/AutoVersionIncrementer.cs` (`IPreprocessBuildWithReport`).
- The patch number in `PlayerSettings.bundleVersion` (`ProjectSettings/ProjectSettings.asset`) increments automatically on each build (e.g. `0.1.11` -> `0.1.12`).
- To inspect the current version:
  ```powershell
  pwsh .agents/skills/changelog/scripts/changelog.ps1 GetVersion
  ```

---

## 4. Reading Changes

### View the Latest Build Notes:
```powershell
pwsh .agents/skills/changelog/scripts/changelog.ps1 ReadLatest
```

### View a Specific Build Version:
```powershell
pwsh .agents/skills/changelog/scripts/changelog.ps1 Read -Version 0.1.11
```

### List All Recorded Versions:
```powershell
pwsh .agents/skills/changelog/scripts/changelog.ps1 List
```

Or view `CHANGELOG.md` directly via `view_file`.

---

## 5. Writing & Updating Changes (Commit Workflow)

Whenever preparing to commit changes:
1. **Identify the current version**:
   Run `pwsh .agents/skills/changelog/scripts/changelog.ps1 GetVersion` or check `bundleVersion` in `ProjectSettings/ProjectSettings.asset`.
2. **Review your changes**:
   Run `git status` / `git diff` to understand all modified components and gameplay effects.
3. **Add bullet points to `CHANGELOG.md`**:
   - Either edit `CHANGELOG.md` directly under the active build heading.
   - Or use the helper script:
     ```powershell
     pwsh .agents/skills/changelog/scripts/changelog.ps1 AddEntry -Category "Fixes" -Message "Fixed fire imbuement VFX failing to attach to weapon blades"
     ```
4. **Stage and commit**:
   Include `CHANGELOG.md` in the commit.
