# Editor to-do: Campaign map diorama

The campaign map is now a generated 3D diorama (no plan file). Built 2026-10-05 with Unity MCP connected. Generator, the Play-mode overlay and all 13 level previews were run and checked by screenshot. Tick items off as you go and delete the file when it's empty.

## Verify first

- [ ] **Close the Synty Sidekick "Modular Character" window** before saving any scene. While it's open it drops a `Combined Character` object at the origin of whatever scene is active. It got saved into the map scene once this session and was removed. Check the map scene's root objects are only `Main Camera`, `EventSystem`, `CampaignMapCanvas`, `DraftUpgradeService` and `CampaignDiorama`.
- [ ] **Play the map in a real Game view** (the MCP game-view capture washes colours out, so the screenshots were rendered off-screen). Check the bloom, vignette and the tilt-shift depth of field in `Config/Campaign/CampaignDioramaVolume.asset` look right at 16:9 and at 16:10 or ultrawide. *(MCP-able tuning)*

## UI review

- [ ] **Node plaques** (`Bladehold Prefabs/UI/CampaignNodeButton.prefab`, rebuilt by `CampaignDioramaUIRig`): each label hangs under its castle and the whole castle is the hover/click area. Check that labels don't cover the castle behind them, the type icon and title fit, and the lock icon (top right) and completed tick read clearly. Also check Synty art, Texturina headers / Grenze body, and gamepad focus (WASD/d-pad moves between castles, the camera follows, the right stick pans).
- [ ] **Tooltip level preview** (`CampaignTooltip/Preview`): 16:9 screenshot at the top of the tooltip. Night levels (Graveyard, Frozen Pass) are brightened through `Config/Campaign/CampaignPreviewShots.asset` (`brightness`). The interior castle levels (Great Hall, Throne, Dungeons, Conservatory, Crypt, Princess Sanctuary) are still greybox, and their previews show that. Re-run **Bladehold > Campaign > Capture Level Previews** once they're dressed.
- [ ] **Top and bottom shades** (`MapOverlay/TopShade`, `BottomShade`) keep the title, tier headers and currency chips readable over bright snow.

## Decisions

- [ ] **Biome per node.** Picked automatically from the scene (`CampaignBiomes.Resolve`): Outer Gate = Snowfield, fishing ponds = enchanted Lake, rest areas, garden and conservatory = enchanted Forest, hall, throne and dungeon = CastleCourt (cobbles + townhouses). Override any node with `CampaignNodeSO.mapBiome`, then rebuild.
- [ ] **Look tuning** lives in assets, not code: biome colours, relief and scatter in `Config/Campaign/CampaignDioramaTheme.asset`; banner, ring, route colours and camera feel in `Config/Campaign/CampaignDioramaLook.asset`. After editing the theme, run **Bladehold > Campaign > Build Map Diorama**. The look asset applies live.

## Playtest

- [ ] Start a run from the Meta Area. The camera should open on the open sector(s), with gold dashes marching along the open routes and pulsing rings under the open castles.
- [ ] Clear a sector and return. That castle flies the blue banner and gets the tick, the flag marker moves onto it, routes you passed stay green, and castles you can no longer reach turn grey.
- [ ] Drag with any mouse button or scroll to pan. Letting go of a drag over a castle must **never** deploy. A plain click on an open castle deploys.
- [ ] Past the demo cutoff, castles are greyed with a padlock on the plaque and can't be deployed to.
