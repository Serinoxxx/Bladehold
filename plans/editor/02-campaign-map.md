# Editor to-do: plan 02 (Campaign Map)

From [plan 02](../02-campaign-map-review.md), 2026-09-25. Unity MCP wasn't connected, so all of this is yours. Tick items off as you go and delete the file when it's empty.

- [ ] **DeathScreen in the boss scenes.** Add `Bladehold Prefabs/UI/DeathScreen.prefab` to `Bladehold Necromancer Crypt` and `Bladehold Princess Sanctuary`. Wire its Next Stage / Return To Meta buttons the same way as the castle scenes.
  - Why: neither scene has one, so dying in a boss fight shows nothing, and a boss kill ends the campaign with no screen (it logs an error).
  - Verify: die in each boss scene and you get the defeat screen.
- [ ] **Crypt cleanup.** Delete the `ConfrontationTrigger` GameObject; its script was removed, so it shows as Missing Script. Optionally drag the Necromancer into `NecromancerConfrontationUI.boss` (it's auto-found otherwise).
- [ ] **Player build check.** Crypt → the monologue opens → Obey → Princess → kill → "CAMPAIGN COMPLETE!" → Meta with the run cleared. Repeat with Defy (Necromancer boss).
- [ ] **UI review: `CampaignNodeButton.prefab` / `CampaignPathLine.prefab`.** One label uses LiberationSans instead of Grenze, there's no Texturina header, and the completed badge is a text "[V]".
- [ ] **Map check.** 19 nodes, statuses right after clearing a sector, and a single click deploys once.
