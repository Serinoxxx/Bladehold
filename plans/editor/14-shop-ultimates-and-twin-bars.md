# Editor to-do: shop ultimates and one bar per weapon

From a chat request on 2026-09-27 (no plan file). Tick items off as you go and delete the file when it's empty.

**Done in C#:**
- **Draft:** ultimates are never drafted. The `isUltimate` rows in `Resources/DraftUpgrades.csv` are now the shop's catalog, and the ranged ones say "Aim and press Q".
- **Run state:** `RunSession` holds `MeleeUltimateId` / `RangedUltimateId` and one charge per slot. `MaxUltimateSlots` is 1, or 2 with the `second_ultimate` meta perk.
- **Shop:** `UI/RestArea/ShopUI.cs` builds ultimate offers at runtime from `DraftUpgradeService.GetShopUltimates()`. They sit on top of the 3 item slots, are priced by the new `UltimateShopConfigSO` and use the `ShopItemEffectType.UnlockUltimate` effect.
- **Controller:** `Player/PlayerUltimateController.cs` handles charging and firing:
  - Melee hits fill the melee bar, and ranged weapon hits fill the ranged bar.
  - All other damage to enemies fills both at 50%.
  - Q fires the ranged ultimate while aiming, and the melee one otherwise.
  - One ultimate runs at a time.
- **HUD:** `UI/UltimateBarUI.cs` has a `slot` field, and each bar hides through its `CanvasGroup` until that slot is owned.

**Done by an agent via MCP (2026-09-27):** Unity compiled clean, with no console errors.
- **Price config:** created `Bladehold Config/ShopItems/UltimateShopConfig.asset` (100 / 400) and assigned it on `ShopUI.prefab`.
- **Meta perk:** created `Bladehold Config/MetaPerks/second_ultimate.asset` ("Twin Fury", tier 3, 50 Goblin Blood, `ult_unlock` icon) and added it to `MetaUpgradesUI.allPerks` in `Bladehold Meta Area Scene`.
- **Second bar:** in `Bladehold HUD.prefab`, "Ult Meter" moved to x −260 (melee), and a copy "Ult Meter Ranged" sits at x +260 (ranged). Both got a `CanvasGroup`.
- **HUD scene cleanup:** removed a stray scene-added `UltimateBarUI` override with no references from `HUD.unity`.
- **Logic check in edit mode:**
  - With no ultimate, the shop offers Blade Tempest (melee) and Arrow Stream (ranged).
  - The weapon draft pool contains no ultimates.
  - After owning one ultimate without the perk, the shop offers nothing.

## 1. Fix first

- [ ] **`SwordBladeTempestUltimate` is not on `Player.prefab`.** The other handlers are. The sword ultimate falls back to `AddComponent` at runtime with no serialized refs (MMF feedbacks, config SO). Add it to the Player root, disabled, and wire it (see `/add-ultimate-handler` step 4). This was already the case before this change.
- [ ] **Mace ultimate icon:** `mace_earthshaker_ult` uses icon `shockwave`, which isn't in `SkillTreeIcons.asset`, so its shop card has no icon.

## 2. UI review (human)

- [ ] **HUD:** the two round meters at x ±260 don't overlap the crosshair, mount bar or attack charge bar. Only owned bars show, so a single-ultimate run shows one meter off-centre. Decide whether a lone meter should re-centre.
- [ ] **Shop row:** with ultimates, the row can hold 5 cards (3 items + 2 ultimates), or 6 with Deep Pockets, all in one horizontal `SlotsContainer`. Check it fits, or give ultimates their own row by assigning `ShopUI.ultimateSlotsContainer`.
- [ ] **Twin Fury** reads well on the Meta Area perk card. The name and description are placeholders.

## 3. Playtest

- [ ] **First Rest Area of a run:** the shop shows both of your weapons' ultimates at 100 gold, next to the normal 3 items. Buy one: the other disappears, the gold is spent once, and only that weapon's bar appears on the HUD.
- [ ] **Charging:** sword hits fill only the melee bar; arrows fill only the ranged bar (when owned). Tower kills and burns fill owned bars at half rate. Nothing charges while an ultimate is running.
- [ ] **Firing:** with only a melee ultimate, Q fires it anywhere. With only a ranged ultimate, Q fires it even without aiming. With both, Q while aiming fires the ranged one, and Q otherwise fires the melee one.
- [ ] **Survives scene changes:** the owned ultimate(s) and both bars' charge carry into the next sector and back from the Rest Area.
- [ ] **Twin Fury:** buy it in the Meta Area and start a run. Buy the first ultimate at 100 and the other weapon's ultimate shows at 400. After buying both, both bars show and work.
- [ ] **Demo build:** Twin Fury is hidden (tier 3), so demo runs stay at one ultimate.
- [ ] **Elemental cards:** Inferno Burst and Eye of the Storm only appear in the draft once you own an ultimate, and they trigger on either ultimate.
- [ ] **Fishing Pond:** both bars are hidden and Q does nothing. Charges are unchanged afterwards.
- [ ] **DevConsole:** Unlock Ult fills the matching slot (a bow ultimate goes to ranged), and Fill Charge fills both owned bars.
