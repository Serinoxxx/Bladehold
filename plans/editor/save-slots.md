# Editor to-do: Save slots (5-slot save picker)

Not from a numbered plan. Built 2026-09-30 with Unity MCP connected: prefabs built, scene wired and play-checked by an agent. Tick items off as you go and delete the file when it's empty.

**Your existing save was migrated.** The first launch moved `save.json` into `save_slot1.json` and split settings out to `settings.json` (both in `%USERPROFILE%/AppData/LocalLow/DefaultCompany/Bladehold/`). A backup of the old file is in the agent session's scratchpad (`save-backup/`).

## Verify first

- [ ] **Main menu opens the slot screen.** `MainMenu.unity` > Play > Start. You should get 5 cards. Slot 1 shows your migrated save: Created is the old file's creation date, Time Played starts near 0 (older saves never tracked it).
- [ ] **Continue / New Game.** A used card goes to the Meta Area. An empty card starts a fresh save and goes to the tutorial, because `tutorialCompleted` is false on a new save.
- [ ] **Replay Tutorial** now asks for a slot first, then plays the tutorial on that slot.
- [ ] **Settings are shared by every slot.** Change the volume, switch slots, and the volume should stay the same.

## UI review (agent mockup)

- [ ] **Save slot screen: `Bladehold Prefabs/UI/SaveSlotsScreen.prefab` (instance under `UI_Canvas` in MainMenu.unity) + `SaveSlot.prefab`.** Check:
  - The Synty art: cards use `Box_Medium_ParchmentGradient_01` with a `Frame_Box_Medium_01` tinted `#8C7A62`, the empty slot uses a fleur-de-lis emblem, and delete is a skull.
  - Texturina for headers and Grenze for body text.
  - Layout at 16:9 and ultrawide.
  - The background is a flat dark dimmer. You may want the title-screen backdrop showing behind it instead.
- [ ] **The delete button is small** (92px skull, top-right of the card). Decide whether it's findable enough, or whether it should become a labelled button under the card.
- [ ] **Hover feel.** Hovering or focusing a card springs it to 1.07x, fades in the gold glow and plays the draft cards' paper sounds. Leaving springs it back to 1.0 with the same sound. Clicking plays the draft select sound ("Legendary Tier Item") plus a bump. These are MMF_Players on `SaveSlot.prefab` (`HoverEnterFeedback`, `HoverExitFeedback`, `SelectFeedback`) copied from `Card.prefab`. Tune the scale in their `MMF_ScaleSpring` (MoveTo 1.07 / 1.0).
- [ ] **Delete warning: `SaveSlotsScreen.prefab` > `DeleteDialog`.** You have to hold "Hold to Delete" for 1.5s (`HoldToConfirmButton.holdDuration`). Releasing early drains the fill. **Human intervention:** approve the sounds, which are the hold-start charge `Weapon Charge/magic_flame_of_light_01` and the confirm `GORE_Blade_Chop_mono`.
- [ ] **Gamepad.** Focus starts on slot 1. Left and right move between cards, up reaches the delete skull, and B goes back to the title. In the dialog, focus starts on Cancel, B cancels, and holding A on "Hold to Delete" deletes.

## Decisions

- [ ] **Settings > Delete Save on the main menu** now does nothing, because no slot is active there. In game it still wipes the active slot's progress. Options: hide it on the main menu, or remove it now that the slot screen can delete saves.
- [ ] **Translations.** The `saveslots.*` rows in `Strings.csv` are English only.

## Playtest

- [ ] Play for a few minutes, then quit. Time Played and Last Played on that card should have gone up.
- [ ] **Must never happen:** changing settings on the main menu makes an empty slot show as used. Deleting one slot changes any other slot.
- [ ] **Editor note:** entering Play mode straight into a gameplay scene uses the last slot you played. If no slot exists yet, progress isn't written to disk, so go through the main menu once.
