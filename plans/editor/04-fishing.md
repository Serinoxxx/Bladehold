# Editor to-do: plan 04 (Fishing Pond)

From [plan 04](../04-fishing-review.md), 2026-09-25. Unity MCP wasn't connected, so nothing was play-tested. **Until the first two sections are done, the pond can't be finished:** the tally never shows, so there's no Continue button and no exit. Tick items off as you go and delete the file when it's empty.

## 1. Verify first (5 min)

- [ ] **Dome collider.** Enter Play mode in `Bladehold Fishing Pond`, press T, and shoot at a fish from the spawn point. If arrows vanish at the water's edge, it's confirmed.
  - Why: `PondWater` (26×0.1×26) and `WaterBarrierCollider` (25.5×2×25.5) are cylinders with CapsuleColliders, and a capsule scaled like that becomes a sphere of radius ~13 m.
  - Fix: swap them for a thin BoxCollider/MeshCollider under the water and a ring (or several boxes) for the barrier.

## 2. Wire the scene (could be done by an agent via MCP or an editor build script)

- [ ] **Tally modal** on `FishingTallyUI`: `modalPanel`, the 5 reward texts, `buffFishStatusText`, `buffFishButtonContainer`, a **buff-fish button prefab** (Button + TMP label), and `continueButton`. Without these there's no exit.
- [ ] **Draft modal** on `FishingDraftUI`: `modalPanel` and 3 card slots (root, title, level, desc, selectButton). Ideally one card prefab, placed ×3. Until it's wired, level-ups are skipped with an error.
- [ ] **HUD** on `FishingHUDUI`: `levelText`, `xpSlider`, and the 4 resource counters are unassigned (prompt/countdown/timer/fish count are wired).
- [ ] **Fish prefab** → `FishingManager.fishBasePrefab`:
  - Synty `SM_Item_Meat_Fish_0x`, a collider on a new `Fish` layer, and `FishController`.
  - Plus the 10 per-type materials on `FishingManager`. Today every fish is an identical grey capsule, so you can't spot buff or Diamond fish.
- [ ] **Arrow prefab** with `FishingBowArrow`, `hitLayers` = Fish + shore only.
- [ ] **Fishing bow.** Add `FishingBowController` to the pond's player (it's added at runtime today) with `arrowPrefab` and an `arrowSpawnPoint` at hand height.
- [ ] **MMF feedback.** MMF_Players for countdown thump, horn, time-up, catch, shoot, fish hit-flash/death and the countdown punch. Once they're in, an agent can remove the code fallbacks (plan 04 finding 15).

## 3. UI review (after section 2)

- [ ] Draft and tally modals: Synty art, Texturina headers / Grenze body, readable at 1080p, and gamepad focus lands on the first card / first buff fish / Continue.

## 4. Decisions

- [ ] **Fire/Frost/Spark buff fish** currently boost dead or legacy stats (Mage fire %, Ice Breaker with base 0, legacy chain lightning), so they do almost nothing. Pick what they should boost in the draft element system, e.g. `WeaponFireExplosionDamagePercent`, `WeaponLightningDamagePercent`, and something for Ice.
- [ ] **Spec vs code, pick one of each:**

  | Setting | Code | Spec |
  |---|---|---|
  | Spawn weights | 55/18/15/12 | 60/20/15/5 |
  | Gold per fish | 8-15 | 5-15 |
  | Diamond Fish | always spawns at 30s | "a chance" |
  | Fishsploshion radius | 3.5 m | 3 m |
  | Buff fish | also pay 15 gold | no gold |

- [ ] **Start button.** Start is `T` only, with no gamepad. Keep it as a key, or make it an `[E]` interact post at the shore?

## 5. Playtest (once 1-3 are done)

- [ ] Map → Fishing node → T → 3-2-1 + horn → fish for 60s. Level-ups give a draft (and several at once each give one).
- [ ] Time's up: fish freeze and bleed stops. The tally shows correct totals; eat one buff fish; Continue once → back on the map with Blood/Metal/Bones added **once**.
- [ ] Eat an Armored fish: max HP +10 carries into the next sector.
- [ ] Known issue (plan 12): clicking also swings your sword, and the real bow spends ammo here.
