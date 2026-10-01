# Editor to-do: Troll sliding / warping

Not from a plan. 2026-10-01, Unity MCP not connected. Tick items off as you go and delete the file when it's empty.

**Done in code:** `Bladehold Scripts/Enemies/EnemyDefinitionApplier.cs` no longer multiplies `NavMeshAgent.radius`/`height` by the roster `scale` (the agent already applies the transform scale, so the 4.8x Troll had an ~11.5 m avoidance radius). This also shrinks the agent size of every scaled enemy: `golden_goblin` 0.9, `goblin_brute` 1.25, `big_ork` 1.5, `slayer` 2, `bulwark` 1.4, `captain_kombusta` 1.35, `captain_mogra` 1.3.

## Verify

- [ ] **Agent gizmo matches the body.** Play mode, spawn a Troll (DevConsole / EnemyZoo), select it: the NavMeshAgent cylinder should hug the model (about 2.4 m radius in world space), not dwarf it. *(MCP-able: read `agent.radius * transform.lossyScale.x`.)*

## Wiring

- [ ] **Sync the Troll's walk to its speed.** `Bladehold Prefabs/Troll AC.controller` → Base Layer → `Locomotion` blend tree (`IdleRunBlend`, param `MoveSpeed` in m/s): `Troll Walk` sits at threshold 1, so it plays at 1x from 1 m/s up whatever the agent's real speed (2.5 m/s in `Enemies.csv`, lower when slowed). Measure the walk's stride speed at scale 4.8 (watch the feet against the ground at a known agent speed), then either set the Walk threshold to that speed (turn off *Automatic Thresholds*) or add a speed multiplier on the state. Failure mode if skipped: feet slide/moonwalk. *(An agent can do the controller edit once you give the stride speed.)*

## Playtest

- [ ] **Troll walks smoothly through a crowd.** T5 sector or EnemyZoo with goblins around it: no sideways sliding, no jitter, and goblins aren't pushed away from it from ~10 m out.
- [ ] **Other scaled enemies still look right.** Big Ork, Slayer, Bulwark and the captains: they shouldn't clip into each other or into the player much more than before. They'll pack a bit tighter now; that's expected.
- [ ] **Slam still lands where telegraphed.** The Troll stays put during the wind-up and the circle stays under it.
