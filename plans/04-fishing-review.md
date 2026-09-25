# 04: Fishing minigame review

**Scope:** `Fishing/` (FishingManager, FishController, FishingBowController, FishingBowArrow, FishingUpgradeManager, FishType/FishingUpgradeType, `UI/*`), `Economy/RunSession.cs` buff-fish code, `Bladehold Fishing Pond.unity`, spec in `docs/FishingMinigameSpec.md`.

## Review focus

- **Spec vs implementation:** 60s frenzy, start prompt / 3-2-1 / horn, 6 draft cards, 6 buff fish, cap of 3 per run, Diamond Fish after 30s with 20x HP.
- **Code-built visuals:** `FishingBowController`, `FishingManager` and `FishingTallyUI` all build visuals in code or load assets by path (flagged by grep). Replace them with prefabs and flag for human/agent mockup.
- **Audio/VFX:** must be MMF (countdown thump, horn, catches).
- **Scene exit:** `FishingManager` loads the map via plain `SceneManager` with no loading screen, and may not save HP/ult/ammo the way sector exits do. Unify on one "leave node" path in `CampaignManager`.
- **Rewards:** Gold/Blood/Metal/Diamond Bones payout (~L358-360); check against `SaveData` and `RunSession` (no double-grants on scene reload).
- **Buff fish:** idempotency (the compounding bug is fixed in plan 01; confirm it covers fishing) and cap enforcement.
- **Fishing draft upgrades:** do they leak into the main draft pool or persist wrongly across nodes?
- **Input:** fishing bow on controller, cursor lock state on entry and exit.
- **Diamond Fish Bones:** currently no spend. That's expected; it's for the fishing spear + fisherman's armour (not implemented). Just note it.

## Output

A findings list in this file (severity-ranked), then fix the high/medium ones in the same session if small; otherwise add tasks here for a follow-up session.

## Findings (2026-09-25, severity-ranked)

Reviewed from code and the scene YAML. There was no Unity MCP this session, so nothing was play-tested.

| # | Sev | Finding | Status |
|---|---|---|---|
| 1 | Critical | **The pond scene is a skeleton.** `FishingDraftUI` and `FishingTallyUI` have every field unassigned, and `FishingManager` has no fish prefab, materials or SFX. There's no `FishingBowController` on the Player prefab either. | Code now logs each missing ref. Scene/prefab work is task A |
| 2 | Critical | **The first level-up froze the game.** `OpenDraft` set `timeScale = 0` and then threw a NullReferenceException on the unwired card slots, so time never came back (about 4 kills in). | Fixed: the draft checks its wiring in `Start`, pauses only after filling the cards, and skips (with an error) when unwired |
| 3 | Critical | **No way out of the pond.** The tally modal and Continue button are unwired, so nothing shows after 60s. | Logged in `Start`. Needs task A |
| 4 | High (verify) | **Arrows probably can't reach the fish.** `PondWater` and `WaterBarrierCollider` are cylinders scaled 26×0.1×26 and 25.5×2×25.5. A CapsuleCollider can't scale like that and becomes a **sphere of radius ~13 m**, so the player (spawn z=15, outside it) shoots into a solid dome. Arrows cast against every layer (`hitLayers = ~0`) and die on the first non-fish hit. | Task A: box/mesh colliders or a ring barrier, fish on their own layer, arrows ignore the water |
| 5 | High | **Normal weapons still work in the pond.** Nothing disables `PlayerAttack`/`PlayerWeaponManager`, so a click also swings the sword. Aiming the real bow fires from the shared `RunSession.CurrentAmmo`, so fishing drains ammo you carry into the next sector. | Moved to plan 12 (rules component) |
| 6 | Medium | **Level-ups in one frame lost cards.** A Fishsploshion chain called `OpenDraft` once per level, and each call re-rolled over the last. | Fixed: drafts queue, one pick per level |
| 7 | Medium | **Kills after time's up still counted.** Bleed and Fishsploshion kept killing after the tally opened: they added gold behind it and could open a draft over it (pausing time). Fish kept swimming (the spec says freeze). | Fixed: `OnFishKilled` ignores kills outside the frenzy, fish and bleed stop at Finished, and pending drafts are cancelled |
| 8 | Medium | **Double grant.** Clicking Continue twice before the scene unloaded paid everything twice (Blood/Metal/Bones are permanent `SaveData`). | Fixed: a `rewardsCommitted` guard, plus Continue goes non-interactable |
| 9 | Medium | **Cursor stayed locked** on the draft and tally modals, and nothing selected a button, so gamepads couldn't use them. | Fixed: `CursorLockManager` unlock requests plus first-button selection in both |
| 10 | Medium | **Exit didn't save HP** (it matters after an Armored feast). | Fixed with one path: `CampaignManager.CompleteCurrentNodeAndContinue` now calls `RunSession.CapturePlayerHealthRatio()`. Ult charge and ammo already write through live. Loading-screen exit landed with plan 02 |
| 11 | Medium (design) | **Fire/Frost/Spark buff fish barely do anything.** They add +10% *Percent* to `MageFireDamagePercent` (the Mage class is gone), `IceBreakerDamageBonus` (base 0, so +10% of 0 = 0) and `ChainLightningDamagePercent` (the legacy storm buff). None feed the drafted element system (`WeaponFireExplosionDamagePercent`, `WeaponLightningDamagePercent`, …). | Task B, needs a Lance decision |
| 12 | Low | **Start is `T` only** (raw `Keyboard.current`), with no gamepad. `FishingBowController` also polls the mouse directly alongside `InputReader`; the cooldown hides the double-fire. | Task C |
| 13 | Low | **Arrows spawned at the player's feet.** `arrowSpawnPoint` defaulted to `transform`, so the +1.2 m fallback never ran. | Fixed. A real spawn point belongs in task A |
| 14 | Low | **Spec drift:** weights 55/18/15/12 (spec 60/20/15/5), gold 8-15 (spec 5-15), Diamond always spawns at 30s (spec: "a chance"), Fishsploshion 3.5 m (spec 3 m), buff fish also pay 15 gold. | Lance: pick code or spec. The spec's exit step is updated |
| 15 | Low | **Code-built visuals/feedback (plan 09):** `CreatePrimitive` fish and arrow fallbacks, `AddComponent<FishingBowController>`/`<AudioSource>`, `Resources.Load` SFX, `PlayOneShot`, the `material.color` hit-flash, `Instantiate(deathVfxPrefab)` and the coroutine countdown punch. The tally's code-built buff button is **removed** (it now logs an error). | Rest goes with task A, so the pond isn't left empty before the prefabs exist |

**Checked and OK:**
- Fishing upgrades live only in the scene-local `FishingUpgradeManager` (reset in `Awake`): no leak into the main draft pool or across nodes.
- Buff fish: `ConsumedBuffFish` is the single record, so plan 01's idempotency fix covers fishing. The cap of 3 is enforced (the tally now respects `TryConsumeBuffFish`'s result), and so is one per visit.
- Currencies: Blood/Metal/Bones go to `SaveData`, and Gold to `InRunGold`. They're granted only on Continue, so a reload before then grants nothing.
- Diamond Fish Bones have no spend yet, as expected (fishing spear + fisherman's armour, not implemented).

## Follow-up tasks

- [ ] **A. Build the pond for real** (agent mockup via a temporary editor build script or Unity MCP, then human UI review):
  - Fish prefab: Synty `SM_Item_Meat_Fish_0x` + collider on a `Fish` layer + `FishController`, one material per type.
  - Arrow prefab with `hitLayers` = Fish + shore.
  - `FishingBowController` with a real spawn point.
  - Replace the two sphere-ified capsule colliders.
  - Prefab-based draft card (×3 from data) and tally modal with a buff-fish button prefab (Synty UI, Texturina/Grenze).
  - MMF players for thump/horn/time-up/catch/shoot/hit-flash/death/countdown punch.
  - Then delete every fallback in finding 15.
- [ ] **B. Buff fish stats:** Lance picks what Fire/Frost/Spark boost in the draft element system; point `RunSession.ApplyBuffFishBonus` at those stats.
- [ ] **C. Controller:** move the pond start onto an `InputReader` action (or an `[E]` `IInteractable` start post), and drop the raw mouse polling in `FishingBowController`.

## Needs Lance in the Editor

Moved to its own checklist: [`plans/editor/04-fishing.md`](editor/04-fishing.md).
