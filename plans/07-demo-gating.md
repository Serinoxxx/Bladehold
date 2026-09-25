# 07: Demo gating

**Goal:**
- Players get the full loop (Meta → map → sectors → die → Meta) with restricted content.
- They're cut off before the final battles, roughly halfway through the map.
- **Needs Lance's scope decisions first** (00 §A).

## Tasks

- [ ] One **`DemoConfigSO`** (single source of truth, easy to flip off for the full game), holding:
  - Allowed weapons, armours, perk tiers (tier 1 only), and the mount state (shown but locked).
  - The campaign cutoff (node flag or tier from plan 02).
- [ ] **Pedestals:**
  - Weapon/armour pedestals: show non-demo items as "Locked in demo" (replacing the current `isLockedForDemo` special case on wand/staff with the SO).
  - Mount pedestals: the variants are shown and locked. `MountPedestal.cs` exists but isn't placed or implemented, so that part is display only. The basic mount summon is available from the start (plan 12).
- [ ] **Meta perks:** tiers 2/3 visible but locked with a demo message (`UI/Meta/MetaUpgradesUI.cs`).
- [ ] **Campaign cutoff:** nodes past the cutoff are visible but locked on the map. Reaching the cutoff shows a "Thanks for playing / Wishlist on Steam" end screen, reusing plan 02's campaign-end path, then returns to Meta.
- [ ] **Drafts:** exclude cards for non-demo weapons (should fall out of the equipped-weapon filter; verify ultimates and duos).
- [ ] Log UI mockups (locked badges, demo end screen) for human review.

## Loose data bugs found during the Sep 2026 audit (fix while in the pedestal/weapon data)

- [ ] Sword `WeaponDefinitionSO` pedestal ultimate label says "STALLION" (the old mount ult); the real ult is Blade Tempest.
- [ ] Mace ult label typo: "SIESMIC SMASH".
- [ ] Staff is authored as a Ranged slot, but the ultimate lookup treats "staff" as a melee id. Fix it before the staff is ever un-demo-locked.

## Acceptance

A clean save in a player build can only reach demo content, and flipping `DemoConfigSO` off restores everything.
