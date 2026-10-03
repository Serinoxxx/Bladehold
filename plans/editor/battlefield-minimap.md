# Editor to-do: Battlefield minimap

Not from a numbered plan; requested 2026-10-03. Built and play-checked by an agent with Unity MCP connected. Tick items off as you go and delete the file when it's empty.

## Done by an agent via MCP (2026-10-03)

Play-checked in `Bladehold Outer Gate`. Prep: routes fan from the 10 spawns through the centre bridge. Walling the centre and right bridges swings the left spawns over the left bridge straight away. Wave 1: enemy dots pop in at the spawns and march down the routes, the centre wall turns yellow while it's attacked, and the tower and wall tooltips work. Console stayed clean.

- [x] **Code** (`Bladehold Scripts/UI/Minimap/`): `MinimapUI` (controller), `MinimapConfigSO`, `MinimapPathGraphic` (marching chevrons), `MinimapDotsGraphic` (every enemy in one mesh), the `Minimap*Marker` views, `MinimapTooltip` and `MinimapLegendRow`. Supporting hooks: `AIMovement.Active`, `WallNavCost.ApplyTo(ref NavMeshQueryFilter)` and `SurvivorsSpawner.SpawnPoints`.
- [x] **Assets:** `Bladehold Scripts/UI/Minimap/MinimapConfig.asset`; prefabs in `Bladehold Prefabs/UI/Minimap/` (`Minimap`, `MinimapTowerMarker`, `MinimapWallMarker`, `MinimapGateMarker`, `MinimapSpawnMarker`, `MinimapLegendRow`) plus a generated `Minimap_Chevron.png`.
- [x] **HUD:** `Minimap` is nested in `Bladehold HUD.prefab` under `Screen_HUD_Adventure_01/ScreenSpace`. The old wall-icon row (`Bottom Right/Wall Status`) is **switched off, not deleted**, because the map's wall markers replace it.

## Verify

- [ ] **Other gate scenes:** open Desert Gate, Graveyard and Tutorial Gate and press **M**. The map fits itself to the gate, spawns, plots and NavMesh, and turns so the gate is at the bottom. Check that nothing important falls off the edge. If a scene's NavMesh is huge, untick **Fit To Nav Mesh** or lower **Max Map Meters** on `MinimapConfig`. Scenes without a `Gate` hide the map entirely.
- [ ] **Snapshot layers:** the terrain image is one top-down render taken on load, using `MinimapConfig` → **Snapshot Culling Mask**. That mask currently leaves out UI, Player, Enemy, Ragdoll, ClassPreview, UI-PP, Fish, PlayerBarrier, TransparentFX and Ignore Raycast. If FX or banners show up baked into the map, untick their layer.

## UI review: minimap *(agent mockup)*

- [ ] **Look and feel:** `Bladehold Prefabs/UI/Minimap/Minimap.prefab` (open it in game with **M** / gamepad **Back**). Judge:
  - the Synty art choices: `Minimap_Box_01` chrome (`SPR_FantasyWarrior_Frame_Box_11` border, glow shadow, vignette), parchment legend and tooltip;
  - Texturina headers and Grenze body text;
  - the corner size and position (600×600, sitting above the gate HP bar);
  - the expanded size (`MinimapConfig` → **Expanded Size** 1720, **Expanded Offset** (180, −20));
  - layout at 16:9 and ultrawide.
- [ ] **Animation feel:** `MinimapConfig` → **Expand Seconds** 0.32 / **Collapse Seconds** 0.24, plus the **Expand Curve** (overshoots to 1.035 and settles). The open/close sounds are empty: `MinimapUI` → **Expand Feedback** / **Collapse Feedback** are optional `MMF_Player` slots *(human intervention: pick sounds)*.
- [ ] **Colours:** routes are warm off-white chevrons on a dark band. Enemy dots are red, siege orange and captains purple, with size = NavMesh agent radius × 60 (Humanoid ≈ 30, Large Enemy ≈ 60 canvas units). Wall bars use green/yellow/red for HP, a gap while the door is open, and a red cross when breached.
- [ ] **Gamepad:** **Back** opens and closes the map, **B** closes it, and the **D-pad** moves the hover between markers (nearest marker in that direction). There's no virtual cursor. Decide whether the D-pad is enough.
- [ ] **Shared prefab bug, not fixed:** `Bladehold Prefabs/UI/Glyphs/HintEntry.prefab` draws an unmapped key's letter **white on the white keycap**, so it's invisible (it hits "M"). The minimap's two copies override the text to dark. Fix the shared prefab if other hint bars show blank keys *(MCP-able)*.

## Decisions

- [ ] **Player input while the map is open:** right now you can still move, and **B** also dodges as it closes the map. The camera and aiming freeze because the cursor is unlocked. The options are to keep this (a tactical glance while running), or to also suspend movement and attacks while the map is expanded.
- [ ] **One route set or two:** routes are predicted for the `Humanoid` agent with live wall costs, i.e. the horde. Siege units ignore wall costs and walk straight into walls, so their path isn't shown. An optional second dashed "siege route" is cheap to add.

## Playtest

- [ ] Prep phase: build a wall on the busiest bridge. The arrows should reroute within a second. Open and shut its door: the route should flip each time.
- [ ] During a wave: dots pop in at the spawns. Bigger agents (Bulwark, Troll) get bigger dots. Dead enemies vanish at once and never linger as corpses.
- [ ] Escape closes the map **without** opening the pause menu. Opening the build wheel, pausing or dying closes the map. **M** does nothing while the DevConsole is open.
- [ ] Hover each marker type (gate, empty/built tower, dry tower, wall standing/open/breached, spawn): the tooltip text should be right and stay on screen near the edges.
