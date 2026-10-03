# Editor to-do: Bulwark feel + "halts mounts" attribute

Not from a plan. 2026-10-03. Tick items off as you go and delete the file when it's empty.

**Done in code + assets (via MCP):**
- **Turning.** The Bulwark has its own `Enemies/Bulwark/AIMovementSO Bulwark.asset`: `stoppedTurnSpeed` 75 (was the goblin's 240 °/s) and the new `AIMovementSO.agentAngularSpeed` 70 (overrides the NavMeshAgent's 120 while moving; 0 keeps the prefab value for every other enemy).
- **Attacks.** It has its own `Enemies/Bulwark/AIAttackSO Bulwark.asset` instead of sharing the goblin's. Its swing clip (`Standard Axe Attack`, 2.4 s) connects at about 0.9 s, but the goblin data put damage at 0.4 s with the axe still behind its head and a 2 s cooldown shorter than the clip. Now: wind-up 0.9, cooldown 2.6, range 2.7 (was 2, the same as its stopping distance, so it often stopped just out of range), cone 40°, 0.15 s pre-delay. `BulwarkAttack.HandleShieldBlocked` no longer fires `BlockHit` mid-swing (that Any State transition was cancelling the swing animation while the damage still landed).
- **Mount stopper.** New `Enemies/MountStopper` + `MountStopperSO` (`MountStopperSO Bulwark.asset`: front 90°, needs the shield up, 0.9 s rear lock), added to `Bulwark Enemy Variant.prefab`. `HorseMotor.UpdateCrowd` finds one squarely ahead (`HorseSO.mountStopReach` / `mountStopHalfWidth`). If the horse arrives at ≥ `mountStopMinRearSpeed` it rears (speed 0, charge/trample cut, no move or turn for the lock). Either way it can't push forward into the Bulwark. A broken shield or an approach from behind rides straight through.

## Wiring

- [ ] **Optional: mount-stop feedback.** `HorseMotor.mountStopFeedback` on the Horse prefab is empty. Add an `MMF_Player` with a shield-impact thud, a whinny and a small screenshake.

## Playtest (EnemyZoo / DevConsole spawn a `bulwark`)

- [ ] **You can get behind it.** On foot, circle-strafe a Bulwark: you should be able to outpace its turn and land back hits, especially during its 0.9 s wind-up (turning is frozen then). If it's now too easy, raise `stoppedTurnSpeed` on its movement SO a little at a time.
- [ ] **Swings are readable.** Damage arrives when the axe visibly connects, and every swing you see that reaches you hurts. Hitting its shield mid-swing doesn't cancel the swing.
- [ ] **Horse hits a wall.** Ride or charge into its front: the horse rears in place and stops dead. Holding W keeps you stopped against it; steering lets you go round it, and S backs you off.
- [ ] **From behind / shield broken, ride on through.** From behind, or once its shield is broken, the horse tramples it like any other enemy.
- [ ] **Other enemies unchanged.** Goblins still get shouldered aside by the horse, and other enemies still turn at their usual rate.
