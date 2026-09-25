# 11: Test strategy

**Problem:** `Editor/WeaponReachBenchmark.cs` is a ~2,900-line editor script acting as the whole regression suite, run from a menu and reporting via `Debug.Log`. `AGENTS.md` made adding to it mandatory for every change. It's hard to run headless, hard to read, and slow to grow.

## Proposal (confirm with Lance)

- [ ] Add test assemblies:
  - `Assets/Bladehold/Tests/EditMode/` (`.asmdef` referencing `Assembly-CSharp` isn't possible directly, since there's no game asmdef). Pick one of two:
    - (a) Add a `Bladehold.Runtime.asmdef` for first-party scripts. This is a big change: third-party refs, Synty, Feel, DamageNumbersPro, LeanTween all need asmdef refs.
    - (b) Keep Editor-folder tests using the Unity Test Framework `[Test]` attribute inside an Editor assembly that sees `Assembly-CSharp`.
  - **Recommend (b) now**, and (a) only if compile times hurt.
- [ ] Migrate benchmark sections into real `[Test]`/`[UnityTest]` cases gradually, starting with pure logic:
  - Draft CSV parsing, level formulas, `RunSession` state transitions, buff-fish idempotency, campaign node unlocking, supply refunds.
  - Keep `WeaponReachBenchmark` as the physics/reach benchmark it was named for.
- [ ] Make tests runnable headless: `Unity.exe -batchmode -runTests -testPlatform EditMode -testResults …`. Document the command in `/CLAUDE.md`.
- [ ] **Policy:**
  - Tests for logic with rules or formulas, and for every bug fixed (a regression test).
  - No mandatory tests for pure feel/visual changes.
  - Lance runs the suites himself unless he asks otherwise.
