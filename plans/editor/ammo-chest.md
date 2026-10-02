# Editor to-do: Ammo chest + Hold the Gate top-up

Not from a plan: a direct request on 2026-09-29. Unity MCP was connected and the scripts compile clean, but no prefab or scene work was done (both castle scenes had your unsaved or uncommitted edits). Tick items off as you go, and delete the file when it's empty.

**Update 2026-10-02 (plan 17):**
- The prefab now exists at `Assets/Bladehold/Bladehold Prefabs/Economy/AmmoChest.prefab`, saved from the Tutorial Gate's hand-built chest (feedbacks wired), and the tutorial chest is now an instance of it.
- The defense-scene generator places one outside the main gate at spec `ammoChestPosition` (8, 5) in Outer Gate, Desert Gate and Graveyard.
- Only the castle scenes are left.

## Prefab

- [x] ~~**Build `Assets/Bladehold/Bladehold Prefabs/Powerups/AmmoChest.prefab`**~~ Done 2026-10-02 at `Economy/AmmoChest.prefab` (from the tutorial chest).: a chest or crate mesh (Synty) with the `AmmoChest` component at the root. *(MCP-able)*
  - `goldCost` 50 and `arrowsPerPurchase` 5 are the defaults. `interactionRadius` is 3.
  - `interactionAnchor` is optional (it defaults to the root). Nothing auto-wires in `OnValidate`.
  - **`purchasePopup`**: use the same DamageNumber prefab that `AmmoPickup.prefab`'s `pickupPopup` uses. The code sets its text to "+5 arrows".
  - **`purchaseFeedback`**: a child `MMF_Player` with a coin clink or lid rattle.
  - **`deniedFeedback`**: a child `MMF_Player` with a dull thunk. It plays when you don't have enough gold or the quiver is full.
  - Any feedback left unassigned logs an error in `Start` but doesn't block buying.

## Scene

- [ ] **Place one chest just inside the gate** in `Bladehold Castle Dungeons.unity`, plus any other non-generated sector scene with a gate. (The generated defense scenes and the Tutorial Gate have one; move it with the spec's `ammoChestPosition` if it's in the wrong spot.) *(MCP-able once your scene edits are saved)*
  - It needs no collider, since interaction is distance-based through `InteractableRegistry`.
  - Put it where you can reach it mid-wave without walking through the horde.

## Playtest

- [ ] **Hold the Gate keeps spawning until the quota is killed.** Let a bomber or powder keg kill some goblins; those deaths don't count as kills.
  - Once the quota has spawned, the spawner tops up in normal batches by the kills still owed, until the counter reaches N/N.
  - The field should never sit empty with the counter short.
- [ ] **Chest prompt:**
  - "[E] Buy 5 arrows (50g)" normally.
  - "… · not enough gold" when you have under 50g.
  - "Quiver full · 20/20" when full.
  - It shows during prep and mid-wave, but never in the Fishing Pond (infinite ammo).
- [ ] **Buying:** each press takes 50g and adds up to 5 arrows, with a "+5 arrows" popup over the chest. When you're 1-4 arrows short of full, it still costs 50g and the popup shows the real amount (e.g. "+3 arrows").
- [ ] **Denied:** a press with under 50g or a full quiver spends nothing and plays the denied feedback.
- [ ] **Ground ammo pickup** now pops "+5 arrows" instead of "5".
