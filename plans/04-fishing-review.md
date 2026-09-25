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
