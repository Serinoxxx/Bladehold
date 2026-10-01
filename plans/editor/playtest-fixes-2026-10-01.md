# Editor to-do: playtest fixes (2026-10-01)

Lance's bug list from a playtest: tutorial, mount, campaign map, wave cards, rout, enemy waypoints. Unity MCP was connected, and everything compiles clean (Unity + `dotnet build`). Tick items off as you go and delete the file when it's empty.

**Done in code:**
- **Tutorial hint overlap:** `TutorialHintUI` labels size to their text up to `maxLabelWidth` (760), then wrap. The HUD prefab's hint rows now control child size, so a long line pushes the rows apart instead of spilling over them.
- **Tutorial arena:** `TutorialEncounter.countPerPoint` / `secondsBetweenRounds` / `spawnScatter`. The arena runs 3 per side, 3 s apart, so 6 goblins.
- **Tutorial gate (T3):**
  - 3 waves (10/15/20 kills) via `TutorialRoundPacingConfig`.
  - New `RoundPacingConfigSO.draftWaveCards` (off here), so every wave is the fixed kill-quota card, with no draft and no captain.
  - New steps: `Step10_Wave1` (a `WaveEventStep`, new `WaveCleared` trigger), `Step11_Mount` (new `MountStep`, SummonMount glyph), `Step12_Ready2`, then `Step13_Wave` (victory).
  - `SceneAbilityRules.allowMount` is on in T3.
  - `BuildDefenseStep` limits the build wheel to the Arrow Tower while it runs (`BuildWheelUI.OnlyAllowed`).
- **Mount:** two summon systems both answered keyboard X, so you got two horses and a bar tracking the wrong timer.
  - `PlayerMount` is now the only summoner. It owns the cast, horse, duration and cooldown from the equipped `MountDefinitionSO`, and its Dismount action only dismounts.
  - `PlayerSummonMount` is now just the SummonMount input plus the HUD events, forwarding PlayerMount's real timers.
  - `TryStartMountCast` checks `SceneAbilityRules.MountAllowed`. The Fishing Pond could summon through the old path before.
  - `StatType.SummonMountDuration/Cooldown` are no longer set or read. Duration and cooldown come from the mount definitions.
- **HUD mount slot:**
  - `Cross_Pivot/Mount` and `Item_01` are active. It hides with a CanvasGroup when the scene blocks the mount.
  - `Keybind/ICON` has an `InputGlyph` (SummonMount, follows rebinds and devices) with a dark overlay letter.
  - `SummonMountUI` found its ability on the wrong object (`Player.Instance` is the Synty child), so it never subscribed.
- **Campaign map:**
  - No backtracking: clearing a node closes everything except its `nextNodes`.
  - `LastCompletedNodeId` / `CurrentLocationNode` drive a bobbing "you are here" flag (`LocationMarker` in the map scene).
  - The graph is re-laid into 3 forward lanes that split after the gate and rejoin at tier 4, tier 7 and the Crypt, with no crossing edges and wider columns (x = 140 + 260 per tier). Both the node assets and `BuildDefaultGraph()` were updated.
  - Frozen Pass and Ancient Garden are in `allNodes` at last.
  - New `Bypassed` node status (faded), padlocks only on demo-locked nodes, paths trimmed to the node edges, and thicker lit paths for the travelled and open routes.
  - Type icons, tier column headers (`UI/CampaignTierHeader.prefab`), a darker vignetted background and icon currency chips.
- **Wave cards:** a new `WaveChoiceConfigSO.cardsDelaySeconds` (4 s, was a hardcoded 2 s `GameLoopManager.rewardPopupSeconds`). The cards also wait for the 20 s rout now.
- **Rout:** `routDurationSeconds` 4 → 20, with a "Hunt them down: Ns" countdown in the objective panel (`wave.status.rout_hunt`, localised).
- **Skull waypoints:** a new `EnemySkullWaypoints` on the HUD's waypoint overlay.
  - During a wave: one orange skull per group of enemies within 20 m of each other, hidden when the group is within 12 m of you.
  - During the rout: a red skull on every straggler.

**Verified in Play mode (driven from code):**
- Outer Gate: one horse per summon, the 30 s ride timer, the mount slot showing time left and the X glyph, 3 group skulls on 10 goblins, 20 red straggler skulls and the countdown.
- Tutorial Gate: the wheel shows only the Arrow Tower, then wave 1 → rout → mount step (no cards) → mounted → Ready2 → wave 2 (15 kills).
- Campaign map: after clearing Outer Gate then North Ramparts, only Frozen Pass and Castle Rest Area are open, the other branches fade, and the flag sits on North Ramparts.

## Play it yourself

- [ ] **Campaign map UI review:** node colours, icons (swords, tavern, fish, skull, magic, dragon), tier headers, the flag, background vignette, currency chips. Screenshot-tuned only.
- [ ] **Campaign map with a gamepad:** does focus navigation still feel right with the wider columns? Faded (bypassed) nodes are still focusable but not deployable.
- [ ] **Mount slot art:** the keycap overlay letter on `Cross_Pivot/Mount/Keybind/ICON`. Also check it in the binary castle scenes, in case one overrides the HUD's `Mount` object inactive.
- [ ] **Skull markers:** is the orange group tint readable against snow and grass? Tunables are on `EnemySkullWaypoints` (Bladehold HUD → Objective Waypoints Overlay).
- [ ] **Tutorial arena:** 6 goblins, 3 s apart. Too many for a first fight?
- [ ] **Tutorial gate:** the Ready prompt while mounted, and whether 10/15/20 kills feels right.
- [ ] **Rout length:** 20 s, ending early once every straggler is dead. Do stragglers idle at the spawn points too long?
- [ ] `Editor/SetupCampaignPrefabsAndAssets` still rebuilds an older node-button layout. Don't run it without porting the prefab's current look into it: the type icon fields are wired there, but not the hidden tier text or the icon placement.
