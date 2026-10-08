# Editor to-do: Enemy roles (Hunter / Guard / Assault)

From playtest feedback ("enemies should split between chasing player, defending object, and attacking wall"), 2026-10-08. Built headlessly; Unity MCP was not used. Tick items off as you go and delete the file when it's empty.

## Verify first

- [ ] **Asset imports cleanly**: `Assets/Bladehold/Resources/EnemyRoleConfig.asset` (hand-written YAML, script `Enemies/EnemyRoleConfigSO.cs`). Select it: the inspector shows Hunter 40 / Guard 25 / Assault 35, ring 3–7 m, engage 12 m, patrol 6°/s, refresh 0.5 s. If it shows "missing script", re-create it via Create > Scriptable Objects > Enemies > Enemy Role Config, named `EnemyRoleConfig`, in `Assets/Bladehold/Resources/`. Without it the code defaults apply and the console warns once ("No asset at Resources/EnemyRoleConfig").
- [ ] **Console clean after compile** (no errors from `AITargetSelector`, `EnemyRoleConfigSO`, `SurvivorsSpawner`).

## Playtest

- [ ] **Hold the Gate (wave 1)**: roughly half the horde comes for you wherever you stand, the rest march on the gate. Before, all of them went for the gate unless you were within 8 m.
- [ ] **Free Prisoners / Destroy Siege Engines**: about a quarter of each batch rings the nearest cage/engine (3–7 m out, slowly circling) and doesn't attack it. Walk within ~12 m of it: they come at you; walk away: they go back to their posts.
- [ ] **Protect the Wagon**: guards ring the wagon and move with it; they engage you when you're near it.
- [ ] **Battering Ram**: Assault goblins (and Guards, who fall back to Assault) still push the ram; Hunters come straight for you. Check the ram still gets enough pushers to roll.
- [ ] **Walls**: an Assault goblin that reaches a shut wall attacks it. A Hunter whose path is blocked by a wall also gets claimed and attacks it. Standing on the enemy side of a wall within 8 m still pulls them off it.
- [ ] **Offence waves (Goblin Rush, Defeat Captain…)**: the Assault share now walks to the gate too (before, every enemy chased you). **Decision**: is gate pressure during offence waves wanted? If not, lower `assaultWeight` or ask for per-stance weights.
- [ ] **Specials unchanged**: sapper/hexer still go for towers, troll/captains/siege still walk straight in, the Slayer still charges the gate, the golden goblin still flees, Dome Warden escorts hold formation, the rout still sends everything home.
- [ ] **Big crowd perf**: 200+ baked goblins (DevConsole burst) shows no new spike in `AITargetSelector` in the Profiler.
- [ ] **Tune**: weights and guard ring/engage radius on the asset.

## Failed objective: survivors storm the gate (2026-10-08)

**Done in code:**
- When an objective **fails** and a gate is standing, `GameLoopManager.RoutRoutine` no longer routs the survivors. `GateAssaultRoutine` sets every survivor to `EnemyRole.Assault`, fires `OnGateAssaultStarted`, and keeps the wave open until they're all dead.
- The objective panel shows "They're storming the gate! Cut them down: N left".
- `WaveClearedBannerUI` follows the OBJECTIVE FAILED banner with "THEY STORM THE GATE!".
- `WaveChoiceConfigSO.gateAssaultMaxSeconds` (120) despawns anything still stuck after that.
- A successful objective still routs as before. If no gate is alive, a failure also routs.

**Manual verification:**
- [ ] Let Destroy Siege Engines time out: the survivors (Guards included) turn on the gate. The banner shows, and the panel counts them down.
- [ ] Kill them all: the draft follows about 1.25 s later.
- [ ] The gate falling mid-assault ends the run as usual.
- [ ] Nobody flees and no hunt countdown appears on a failure. A success still shows the flee banner and countdown.
