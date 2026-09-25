# Editor to-do: plan 06 (Sector difficulty + roster)

From [plan 06](../06-sector-difficulty-and-roster.md), 2026-09-25. Unity MCP wasn't connected, so nothing was play-tested. The six new sector enemies were only checked statically (their prefabs have Health, NavMeshAgent, AIMovement, Enemy, CorpseDespawner, health bar and CoinDropper). Tick items off as you go and delete the file when it's empty.

## 1. Verify first (5 min)

- [ ] **Pacing asset loads clean.** Select `Assets/Bladehold/Bladehold Config/SurvivorsRoundPacingConfig.asset`. Expect 5 rounds with only `requiredKillsPerWave`, plus `Quota Growth Per Threat 0.1`, `Fodder Enemy Id goblin`, `Fodder Share 0.6`, `Objective Trickle Min Alive 6`, `Objective Trickle Interval 2`. The YAML was hand-edited, so check the console for import errors.
- [ ] **Roster parses.** Open the Enemy Manager and confirm the new **Min Threat** field shows on goblin (1), bannerman (2), bulwark (3), storm_witch (4) and troll (5), with 0 on golden_goblin and captain_kombusta. Any `EnemyRosterSO` parse error in the console = a bad CSV row.

## 2. Wiring

- [ ] **Captain Fraglob prefab.** None exists, so `GameLoopManager.captainPrefab` is empty in every battle scene. Every Fraglob spawn now logs an error and spawns Kombusta instead (it used to silently scale a brute). Build a Fraglob variant (the `generate-enemy-prefabs` skill can do it: an agent job) with `CaptainEnemyController` on the root, then assign it in `Bladehold Survivors Scene` and each castle scene. This replaces the Captain item in `00` §B.
- [ ] **Bannerman banner visual.** On `Bladehold Prefabs/Bannerman Enemy Variant.prefab`, the `DestructibleBanner` child has `bannerVisual` empty, so the banner mesh stays up after it's destroyed. `BannermanAura.auraVfx` is empty too, so the clan buff has no visual cue. Assign both, adding an MMF feedback if you want a sound on break.
- [ ] **Assassin audio.** `Assassin Enemy Variant.prefab` → `AssassinAttack`: `audioSource`, `windupFeedback` and `slashFeedback` are all empty, so the slash is silent and has no windup cue. Add MMF feedbacks.
- [ ] *(Optional)* **Bulwark is an unpacked prefab.** It isn't a variant of `Goblin Enemy (Base)`, so base-prefab fixes won't reach it. Consider regenerating it as a variant.

## 3. Playtest

Use the DevConsole's new **Sector Threat ▲/▼** row. It takes effect on the next wave start. Clear returns to the campaign node's tier.
- [ ] **Tier 1 feels like before.** Open `Bladehold Survivors Scene` directly (threat 1). Wave 1 is goblins only, brutes join on wave 2, bubblers and big orks on wave 3, bombers on wave 4. Goblins should be at least 60% of each wave, and no Bannerman/Bulwark etc. appears.
- [ ] **Tier 5 mixes elites in.** Set threat 5 before wave 1. Waves 1-2 match tier 1. From wave 3 you should see Bannermen, Powder Kegs, Bulwarks, Assassins and Storm Witches, and from wave 4 one Troll at a time, still inside goblin swarms. The wave 5 quota should be 49. Check each newcomer telegraphs, paths on the NavMesh, shows a health bar, and despawns its corpse.
- [ ] **Wagon/Ram trickle.** Force `ProtectWagon` or `StopBatteringRam` from the DevConsole objective picker and kill everything fast. At least ~6 enemies should keep arriving until the wagon arrives or the ram dies. The field must **never** sit empty with the wave stuck.
- [ ] **Goblin Rush** still spawns goblins only, at any threat.
- [ ] **Captain wave 5** with no Fraglob prefab: expect a red console error plus a Kombusta spawn. The error is intentional until the Fraglob item above is done.
