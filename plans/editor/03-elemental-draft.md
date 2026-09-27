# Editor to-do: plan 03 (Elemental drafts)

From [plan 03](../03-elemental-draft-review.md), 2026-09-25. Tick items off as you go and delete the file when it's empty.

## Done by an agent via MCP (2026-09-26)

- [x] **Benchmark:** 110 passed, 2 failed. Section 26 is all PASSED. The two failures are the old Bulwark-attack and Bannerman-rig checks.
- [x] **Play-mode test:** `Draft Weapon Charges (Play Mode)` → PASS, 26 cases.
- [x] **Chain Dash bolt:** `Player.prefab` → `PlayerDodge.lightningDashVfxPrefab` is now `FX_Electricity_02_NoLoop` (the sky strike `FX_LightningStrike_01` is kept in the project for a possible "Thunder Step" card).
- [x] **Blazing Trail VFX:** `PlayerDodge.fireTrailSegmentVfxPrefab` = `FX_Fire_Small_01`.

## Your test notes: what was wrong

- **Combustion never showing:** bad luck, not a bug. An elemental draft picks 3 of 20 cards at random, so any one card shows about 1 draft in 7. 9 of the 20 are tower cards. **Decision:** weight the pool by slot, or trim the tower cards?
- **Chain Dash gave a bolt:** the bolt was just the dash VFX (a sky strike). The real effect, "next melee hit chains lightning", never ran: it looked for `PlayerDodge` from the child `Player`, but it's on the prefab root. Fixed; the blade now glows Lightning after a dash until the next hit arcs.
- **No Blazing Trail:** the trail segments copied a VFX that only emits while moving, so a segment on the ground was invisible (damage and burn worked). Segments now use their own VFX field.
- **"Broken in general": yes.** 13 cards (6 elemental, 7 weapon) used `Percent` on stats whose base is 0, so they always came out as 0 and did nothing: Kindling, Fortress Pyre, Frost Step, Shatter, Fire/Lightning Arrows, Lunge Mastery, ShieldBreaker, Vampire Blade's drawback, Power Dash, First Strike, Armor Shatter, Colossal Force. All are `Flat` now. The same child-vs-root lookup bug also broke the ultimate slot's element, the Lunge window and ultimate setup on pick; all fixed.
- **Icon colours:** Fire orange, Ice light blue, Lightning purple on the draft cards and the sidebar. Set on `Resources/SkillTreeIcons.asset` (Elemental Icon Tints).

## Still yours

- [ ] **Playtest.**
  - Chain Dash: dash, the blade glows Lightning, the next hit arcs to nearby enemies, then the glow clears.
  - Blazing Trail: dashing leaves small burning patches for about 3 s.
  - Frost Step, Kindling, Shatter, Fortress Pyre and the 7 weapon cards above now do something. Balance them: they've never been live.
  - Combustion: the sword glows Fire and hits ignite. Then Static Edge: the card shows `[Overwrite]`, the glow turns Lightning, +25 gold.
- [ ] **UI review:** the icon tints on the parchment cards and the sidebar.
- [ ] *(Optional)* `ChainLightning.chainDashChargedFeedback`: an MMF crackle when the blade charges. Empty = silent.
- [ ] **Decision: Colossal Force** says "massive knockback", but level 1 only adds 6 units of impulse (level 4: 24). Raise the amounts if it feels weak.
- [ ] **Design call (content, Phase 4).** Only Ice has a ranged card, so Fire/Lightning can never make the bow glow. Add a Fire and a Lightning ranged card?
