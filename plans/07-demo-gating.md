# 07: Demo gating

**Goal:**
- Players get the full loop (Meta → map → sectors → die → Meta) with restricted content.
- They're cut off before the final battles, roughly halfway through the map.
- Scope decided 2026-09-25: Sword + Bow free, Mace unlockable, other weapons "Locked for demo"; Hero + Iron Vanguard armour; basic warhorse only; tier-1 perks only (that's 4 perks: Agility, Backstab, Deep Quiver, Regeneration); the demo ends after tier 4.

## Tasks

- [x] One **`DemoConfigSO`** (single source of truth, easy to flip off for the full game), holding:
  - Allowed weapons, armours, perk tiers (tier 1 only), and the mount state (shown but locked).
  - The campaign cutoff (node flag or tier from plan 02).
- [x] **Pedestals:**
  - Weapon/armour pedestals: show non-demo items as "Locked in demo" (replacing the current `isLockedForDemo` special case on wand/staff with the SO).
  - Mount pedestals: the variants are shown and locked. `MountPedestal.cs` exists but isn't placed or implemented, so that part is display only. The basic mount summon is available from the start (plan 12).
- [x] **Meta perks:** tiers 2/3 visible but locked with a demo message (`UI/Meta/MetaUpgradesUI.cs`).
- [x] **Campaign cutoff:** nodes past the cutoff are visible but locked on the map. Reaching the cutoff shows a "Thanks for playing / Wishlist on Steam" end screen, reusing plan 02's campaign-end path, then returns to Meta.
- [x] **Drafts:** exclude cards for non-demo weapons (should fall out of the equipped-weapon filter; verify ultimates and duos).
- [x] Log UI mockups (locked badges, demo end screen) for human review.

## Loose data bugs found during the Sep 2026 audit (fix while in the pedestal/weapon data)

- [x] Sword `WeaponDefinitionSO` pedestal ultimate label says "STALLION" (the old mount ult); the real ult is Blade Tempest.
- [x] Mace ult label typo: "SIESMIC SMASH".
- [x] Staff is authored as a Ranged slot, but the ultimate lookup treats "staff" as a melee id. Fix it before the staff is ever un-demo-locked.

## Acceptance

A clean save in a player build can only reach demo content, and flipping `DemoConfigSO` off restores everything.

## What was built (2026-09-25)

- `Demo/DemoConfigSO.cs` + `Resources/DemoConfig.asset`. Static helpers (`IsWeaponLocked`, `IsArmourLocked`, `IsMountLocked`, `IsMetaTierLocked`, `IsCampaignNodeLocked`, `IsDemoCutoffNode`) all return "unlocked" when `demoEnabled` is off or the asset is missing. `WeaponDefinitionSO.isLockedForDemo` is removed; the Balance Tree Editor reads the SO instead.
- Pedestals (weapon/armour/mount) show `lockedLabel` and refuse interaction. Saved loadouts that aren't allowed in the demo fall back to sword/bow, Hero armour and the basic warhorse.
- Meta perks: demo-locked tiers keep their rows and unlock buttons visible but disabled; `UnlockTier`/`PurchasePerk` also refuse. Perks already bought on an old save still apply (`RunSession.HasMetaPerk` isn't gated), which is fine for clean demo saves.
- Campaign: nodes past `campaignCutoffTier` never become available and render as `NodeVisualStatus.DemoLocked`. Clearing any node on the cutoff tier returns to the map (so combat, pond and rest exits all behave the same), where `CampaignMapUI` shows `Demo/DemoEndScreenUI`; its Continue calls `CampaignManager.EndCampaign()`. `CampaignNodeSO.endsCampaign` stays a hard end flag and isn't used for the demo. `DeployToNode` (DevConsole) can still jump past the cutoff.
- Drafts: already filtered to equipped weapons (ultimates included; duos are weapon-agnostic); added an explicit demo-lock skip as a backstop.
- Data: sword ult label → BLADE TEMPEST, mace → SEISMIC QUAKE (matches its draft card), staff ultimate lookup moved to the ranged slot.

## Rescope for a shorter demo (2026-10-02)

A full demo clear was running past an hour (up to 4 battles × 5 waves at ~4 min each), against a 20–30 min target, so it was cut back:
- **Cutoff tier 4 → 3** (`DemoConfig.campaignCutoffTier`). Every route is now Outer Gate, then a tier-2 node, then a tier-3 node: 1–3 battles, with pond nodes on the map in tiers 2 and 3. Great Hall / Captain Fraglob becomes a locked teaser.
- **3 waves per sector in the demo** (`DemoConfig.maxWavesPerSector`, read through `RoundPacingConfigSO.WavesPerSector` by `GameLoopManager` and `SurvivorsSpawner`'s prewarm). 0 turns the cap off. Trade-off: anything with `unlockWave` ≥ 4 in `Enemies.csv` (the Bomber; the Troll is threat-gated out anyway) never appears in the demo.
- **First-run gift** (`Tutorial/FirstRunGift.cs`): the first time a run ends (defeat via `DeathScreen.ReturnToMetaScene`, or the campaign/demo end via `CampaignManager.EndCampaign`) the save gets 30 Goblin Blood + 10 Orcish Metal (`TutorialConfig.firstRunGift*`), once per save (`SaveData.firstRunGiftGranted`). That's the Mace plus three tier-1 perks. Winning without dying pays it too, so a skilled player still gets the meta loop.
- **Gift hint:** `FirstTimeHints.MetaGift` (`hint.meta_gift`) shows in the Meta Area with a waypoint on the first affordable weapon pedestal (`WeaponPedestal.All` / `CanAffordUnlock`), then the existing Spirit hint follows. Meta hints now wait for any showing hint instead of replacing it.

## Needs Lance in the Editor

Moved to its own checklist: [`plans/editor/07-demo-gating.md`](editor/07-demo-gating.md).
