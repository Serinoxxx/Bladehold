# Editor to-do: plan 04 (Fishing Pond)

From [plan 04](../04-fishing-review.md), 2026-09-25. Tick items off as you go and delete the file when it's empty.

## Done by an agent via MCP (2026-09-26)

Play-checked in `Bladehold Fishing Pond`: T → countdown → 30 fish in the pond with outlines → catches pay out with a popup → a level-up opens the draft → picking a card resumes → time's up opens the tally. Your terrain, rocks and environment are untouched.

- [x] **Arrows vs the dome.** New layers `Fish` (12) and `PlayerBarrier` (13). `PondWater` and `WaterBarrierCollider` are on `PlayerBarrier`: the player still bumps into them, arrows fly through. `PlayerBow` strips `PlayerBarrier` from its hit mask in `Start`, so real arrows and the aim ray fly through it. Resize the colliders as you like; keep them on `PlayerBarrier`.
- [x] **Fish follow the pond.** You moved the water to (3.6, −5.7), but fish orbited (0, 0, 0) out to 13 m, under the terrain. `FishingManager` has a new **Pond Center Anchor** (set to `PondWater`) and orbit radius 1.5–6.5 m. Retune the radii if you resize the pond.
- [x] **Slower fish.** Orbit speed is a tunable range on `FishingManager` (**Fish Orbit Speed Range**, now 8–16°/s, was 20–35) plus **Speedy Fish Speed Multiplier** (2).
- [x] **Type outline/glow.** Each fish gets a Highlight Plus outline in its type colour, with a glow for buff and Diamond fish. See-through, so it reads under the water. Profile: `Bladehold Highlight Profiles/Fish Type HPP.asset`. Colours: **Fish Type Highlight & Popups** on `FishingManager`. The hit flash now uses Highlight Plus HitFX, which also stops the `_Color` warning spam.
- [x] **Catch popups (DNP).** "+12 Gold", "+1 Orcish Metal", "+2 Goblin Blood", "Speedy Fish! +15 Gold", "DIAMOND FISH! +1 Bone, +100 Gold", tinted by type. Uses the DNP demo `Gold.prefab` for now (`FishingManager.catchPopupPrefab`).
- [x] **Tally modal** (`FishingTallyUI`): parchment panel, 5 reward lines, buff-fish status + button list (`UI/FishingBuffFishButton.prefab`, a MenuButton variant), Continue. Gamepad focus lands on Continue.
- [x] **Draft modal** (`FishingDraftUI`): header + 3 instances of `UI/FishingDraftCard.prefab`, with a click sound. Gamepad focus lands on card 1.
- [x] **HUD** (`FishingHUDUI`): stats panel top left with level, XP bar and the 4 resource counters. The canvas now scales at 1920×1080 like the rest of the game, and the old texts use Texturina/Grenze.
- [x] **Fish prefab:** on the `Fish` layer, with a `HighlightEffect` wired.
- [x] **Real bow in the pond (2026-09-26).** `FishingBowController`/`FishingBowArrow`/`FishingArrow.prefab` are gone (it fired a second, fake arrow on every click), along with `Player.prefab`'s `FishingArrowSpawn` and `FishingBowShootMMF`. The pond now uses `PlayerBow` with `PlayerAmmo.InfiniteAmmo` on; Fish Skewer adds to `BowPierceCount`, Bounce Shot calls `PlayerBow.BounceFrom` on fish hits (`FishingManager`). Fish take the bow's real damage.
- [x] **Pond = base kit + fishing cards (2026-09-27).** `FishingManager` sets `RunSession.RunUpgradesSuspended` for the scene: the bow is forced (ranged cycling locked, save untouched), and draft cards, elemental slots, the ultimate, backstab/executioner/agility/second-wind perks, armour stats and eaten buff fish all stay off the pond player. `RunSession` keeps them, so they return in the next sector (play-checked: run cards, fire slot and 42 ultimate charge survived a pond visit). Deep Quiver still counts: arrows are free anyway, and dropping it would clip the run's ammo to 20.
- [x] **MMF:** `CatchMMF` (splash sound) and `TimeUpMMF` (the horn at 0.8 pitch) on `FishingMinigameManagers`.
- [x] **EventSystem:** the legacy `StandaloneInputModule` was swapped for `InputSystemUIInputModule`, which fixes the `UnityEngine.Input` exception.

## Needs you

- [ ] **Your Environment group brought gameplay objects in with it:** `SurvivorsObjectives` (that's the "Objectives: Destroy Siege Engines" tracker at the pond), a `Bladehold HUD` (shows a "Fortress Gate 400/400" bar), a `DeathScreen` and a second `WeatherControl`. I left them because you said to leave the environment alone. **Delete at least `SurvivorsObjectives` and the extra `WeatherControl`.** Keep the HUD/DeathScreen if you want the health bar and a defeat screen at the pond, but hide the gate bar.
- [ ] **UI review (agent mockup):** draft cards, tally panel and HUD stats panel. Synty art, Texturina headers / Grenze body, readable at 1080p, gamepad focus. The white parchment cards are bright against the dim backdrop.
- [ ] **Catch VFX (human pick):** `CatchMMF` has a sound only. `FX_WaterRipple_01` / `FX_Water_Splash_01` both loop, so they'd need a timed variant like `VFX/BuildDustSmall`. A proper catch popup prefab (instead of the DNP demo Gold) is an art pick too.
- [ ] **Fish materials:** the 10 per-type material slots on `FishingManager` are still empty, so every fish is the raw fish mesh. The outlines carry the type now; add materials only if you want tinted bodies.

## Decisions

- [ ] **Fire/Frost/Spark buff fish** currently boost dead or legacy stats (Mage fire %, Ice Breaker with base 0, legacy chain lightning), so they do almost nothing. Pick what they should boost in the draft element system, e.g. `WeaponFireExplosionDamagePercent`, `WeaponLightningDamagePercent`, and something for Ice.
- [ ] **Spec vs code, pick one of each:**

  | Setting | Code | Spec |
  |---|---|---|
  | Spawn weights | 55/18/15/12 | 60/20/15/5 |
  | Gold per fish | 8-15 | 5-15 |
  | Diamond Fish | always spawns at 30s | "a chance" |
  | Fishsploshion radius | 1 m (was 3.5, chained through the pond) | 3 m |
  | Buff fish | also pay 15 gold | no gold |

- [ ] **Start button.** Start is `T` only, with no gamepad. Keep it as a key, or make it an `[E]` interact post at the shore?

## Playtest

- [ ] Map → Fishing node → T → 3-2-1 + horn → fish for 60s. Aim at a fish over the water: arrows no longer stop at the dome. Level-ups give a draft (several at once each give one).
- [ ] Time's up: fish freeze and bleed stops. The tally shows correct totals; eat one buff fish; Continue once → back on the map with Blood/Metal/Bones added **once**.
- [ ] Eat an Armored fish: max HP +10 carries into the next sector.
- [ ] Elemental fish (+25% each): eat a Fire fish, then in the next sector with a Fire-charged weapon the charge explosion, burn ticks and Blazing Trail numbers are ~25% higher. Spark: lightning chain and Static Edge ~25% higher. Frost: every hit on a chilled or frozen enemy ~25% higher. The tally shows "+25% Fire Damage" / "+25% Damage vs Chilled" / "+25% Lightning Damage".
- [ ] Real bow: bow is out even with the axe/wand saved, hold aim + click fires one arrow (not two), the ammo counter doesn't drop, your run's cards/elements don't show, Fish Skewer pierces, Bounce Shot hops fish, Fishsploshion blasts only catch fish right next to the kill.
- [ ] Clicking without aiming does nothing (plan 12 blocks melee in the pond; checked in [12-mount-and-fishing.md](12-mount-and-fishing.md)).
