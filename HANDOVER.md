# Handover — Class Select Screen + trivial TODO wiring

Session context: Sonnet 5, working live against the Unity Editor via the UnityMCP MCP
server (project "My project", Unity 6000.3.10f1, scene `Bladehold Test Scene.unity`).
Picking this up in a fresh session: re-run `mcpforunity://editor/state` first and treat
every instance ID below as **stale** — Unity reassigns instance IDs on domain reload/scene
reload, so re-resolve everything via `find_gameobjects`/paths before touching it.

## What's actually done (committed, pushed to origin/main)

One commit landed and pushed (`48a9a084`): pre-existing uncommitted work from earlier in
the session (not mine to design, just committed as-is) —
- `LocomotionAnimator.cs`: strafe animator param changed from bool to float (blend tree fix)
- `MedusaGazeAura.cs`: added a `LightningSystemChain` effect wired to the gaze
- `Assets/Editor/FixBars.cs` + `SetupBars.cs`: untracked editor helper scripts for the
  screen-space HUD bars (were sitting uncommitted; committed, not reviewed/authored by me)
- Assorted regenerated prefab/material/scene YAML churn from the Editor being open

Working tree was clean and pushed as of this point. **Nothing else has been committed or
modified in the project since.**

## The ask

1. Build the **class-select screen** (TODO.md "Berserker class prototype — Stage B", the
   `ClassSelectPanel` under the death screen) using the Synty Fantasy Warrior HUD sprites
   and one of their sample button prefabs for the visual style.
2. Knock out a handful of other genuinely trivial TODO.md items (no art/model/animation
   dependency, just component wiring) alongside it.

Both were still **in progress, nothing built yet** when this session got interrupted —
see "Where it stopped" below. Two `manage_components`/`manage_gameobject` tool calls were
rejected by the user right at the point of actually creating the panel; the user did not
say why, so **check in with the user about what they wanted done differently before
resuming scene edits** — don't just retry the same calls.

## Task list (TaskCreate/TaskUpdate) — all still `pending`

1. Build ClassSelectPanel UI in death screen
2. Wire Parry + Counterstrike onto Player prefab
3. Wire CombatFacing onto Player prefab
4. Wire HoldTheLineBonus + re-enable gate
5. Fill enemy prefab map + assign to WaveSpawner/EnemyZoo

## Task 1 — Class select screen: research done, nothing built yet

### Key finding: no `ClassDefinitionSO` assets exist

`Assets/Bladehold/Bladehold Scripts/Player/ClassDefinitionSO.cs` exists (the class asset
type) but **zero asset instances** of it exist anywhere in the project. Before the
`ClassSelectPanel` can be wired with real options, two SO assets need creating via
`manage_scriptable_object` (action `create`, `type_name` = `ClassDefinitionSO`,
`menuName` folder `Scriptable Objects/ClassDefinitionSO`):

- **Swordsman**: `id="swordsman"`, `displayName="Swordsman"`, a short description blurb.
  Leave `animatorOverride`/`characterModelPrefab`/`skillTree`/`chargeTimePerLevel` at
  defaults (Swordsman = the authored baseline, null = keep as-is).
- **Berserker**: `id="berserker"`, `displayName="Berserker"`, a short description blurb.
  Leave the other fields null/default too — the full Berserker weapon/animator/skill-tree
  wiring (Stages A/C/D/E in TODO.md) is a **separate, much bigger, non-trivial task**
  (needs a new axe mesh, animator override clips, etc.) — **do not attempt that**, only the
  class-select UI needs these two SO assets to exist with id/displayName/description filled.

Do NOT scope-creep into wiring `PlayerClassController` slots on `Player.prefab` — that's
the separate Stage A wiring item in TODO.md, not part of "the class select screen."

### `ClassSelectPanel.cs` contract (read in full, `Assets/Bladehold/Bladehold Scripts/UI/ClassSelectPanel.cs`)

- `[SerializeField] ClassOption[] options`, each option = `{ ClassDefinitionSO definition,
  Button button, TMP_Text nameLabel (optional), TMP_Text descriptionLabel (optional),
  GameObject selectedHighlight (optional) }`.
- On `Start()`: validates every option has `definition` + `button` (else logs error and
  no-ops forever — must be filled in for the panel to work at all), fills labels from the
  SO, wires `button.onClick`, pre-selects whichever option matches
  `SaveSystem.Load().playerClassId`, toggles `selectedHighlight` per option on click.
- `SelectedClassId` (public getter) is read by `DeathScreen.HandleReincarnate()` and passed
  to `PlayerClassController.SetSavedClass(...)` right before the reincarnate reload.

### `DeathScreen.cs` wiring point

- `[SerializeField] ClassSelectPanel classSelectPanel` — optional; `Start()` hides it
  (`SetActive(false)`) until the player's first Reincarnate click banks points, then
  `HandleReincarnate()` shows it alongside `reincarnateTreePanel`. **This field on the
  `DeathScreen` component needs to be assigned** to whatever panel gets built.

### Scene hierarchy already mapped (re-resolve IDs, but paths are stable)

Path: `DeathScreen` (root Canvas) → `DeathScreen/GameObject` (holds the actual
`DeathScreen` component + `CanvasGroup`) → children include:
- `DeathScreen/GameObject/GoldSkillTree` — the gold tree panel (`SkillTreeView`), same
  size/position class as the Reincarnate tree below, toggled visibility between the two.
- `DeathScreen/GameObject/ReincarnateSkillTree` — a `SkillTreeView` + `ScrollRect`.
  **RectTransform**: anchors 0.5/0.5, pivot 0.5/0.5, `sizeDelta=(1759.58, 691.61)`,
  `anchoredPosition=(0, 8.0)` — i.e. it currently spans nearly the full DeathScreen width,
  centered. TODO.md says the class panel goes **"next to"** this — meaning it needs to be
  narrowed and docked to make room, not just dropped on top of it.
- `DeathScreen/GameObject/Reincarnate` (button), `Try Again`, `Wave 1` — the existing
  authored buttons. **This is the project's established button convention** — every button
  in this project follows the same recipe (see below), so any new button (including the
  class cards) should match it rather than inventing a new style.

### Established button recipe (reverse-engineered from the `Reincarnate` button)

Root: `Button` component (transition = **Animation**, not ColorTint) + `Image` (sprite
`Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Box_White_01.png`,
often tinted alpha=0 as an invisible hit-box — the *visible* art lives in children, not
this root Image) + `Animator` (controller
`Assets/Synty/InterfaceFantasyWarriorHUD/Samples/Animation/AC_Button_FantasyWarrior_Basic_01.controller`
— this is the Synty **sample** button-state animator the user pointed at; it already has
Normal/Highlighted/Pressed/Selected/Disabled trigger params, and Unity's `Button` +
`Animator` combo fires them automatically when transition=Animation).

Child `Content` (`HorizontalLayoutGroup` + `CanvasGroup`) holding:
- `Background` (Image, plain box art, the actual visible button shape)
- `ICON_Previous` / `ICON_Next` (Image + `LayoutElement`, decorative arrow icons —
  `ICON_Previous` is inactive on the `Reincarnate` button, only `ICON_Next` shows; these
  are leftover from a carousel-style sample button, reused here just for a bit of side
  ornament, not literal prev/next semantics)
- `Label_Button` (TMP text, the button's actual text)

### Sample prefab investigated and rejected as a direct base

Instantiated `Assets/Synty/InterfaceFantasyWarriorHUD/Samples/Prefabs/AssetDemo_FantasyWarrior_Button_ItemWide_01.prefab`
to inspect it (then deleted it — scene should be clean of this). Structure: `Button` +
`Image` + `Animator` root, children `Background` (containing `SPR_Background` /
`SPR_Background_Tracery` / `SPR_Background_Symbol` — layered decorative sprites, no text)
and `ICON` (with a `Frame` sibling) and a `Shadow` component. **No TMP text children at
all** — it's a pure icon-slot graphic (inventory-item style), not a name+description card.
None of the other sample button prefabs (`Button_Basic_01/02`, `Button_Icon_01/02`,
`Button_Item_01`, `Button_Title_01`) combine an icon with both a name label and a
description label either — every one is icon-only or icon+single-label.

**Conclusion reached (not yet executed)**: build the two class cards as a custom
composition rather than a single unmodified sample prefab, but reuse the *pieces* Synty
already ships and the *button recipe* above:
- Card root: `Button` (transition=Animation) + `Image` using a Synty frame sprite as the
  actual visible card background (e.g. one of
  `Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/FantasyWarrior/SPR_FantasyWarrior_Frame_Box_Medium_01_Variant_01.png`,
  Image type=Sliced) + `Animator` (`AC_Button_FantasyWarrior_Basic_01`) — this keeps the
  weight/border art *and* stays consistent with the existing button-state convention,
  instead of the transparent-hitbox-plus-separate-background split the `Reincarnate`
  button uses (not necessary here since we want the frame itself clickable/visible).
- Child `WeaponIcon` (Image): a weapon icon sprite from
  `Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/Icons_Weapons/` —
  `ICON_SM_Wep_Sword_XX.png` for Swordsman, `ICON_SM_Wep_Axe_XX.png` for Berserker (pick
  any single variant, there are ~12-18 of each).
- Child `NameLabel` (TMP text) → wire to `ClassOption.nameLabel`.
- Child `DescriptionLabel` (TMP text, wrapping) → wire to `ClassOption.descriptionLabel`.
- Child `SelectedHighlight` (Image, e.g.
  `Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/FX/SPR_FX_FantasyWarrior_Glow_01.png`
  tinted gold, stretched over the card, **inactive by default**) → wire to
  `ClassOption.selectedHighlight`.

Two of these cards stacked vertically inside a new `ClassSelectPanel` container GameObject
(with the `ClassSelectPanel` component on it), which sits in the freed-up strip beside the
narrowed `ReincarnateSkillTree`.

### Sizing math worked out (re-verify against current state — not yet applied except see below)

Original `ReincarnateSkillTree`: center x=0, width=1759.58 → left edge -879.79, right edge
+879.79 (all relative to its parent, `DeathScreen/GameObject`, anchors 0.5/0.5).

Planned split — keep the tree's **left edge** fixed at -879.79, shrink it to make room on
the right:
- `ReincarnateSkillTree` new `sizeDelta=(1200, 691.6)`, new `anchoredPosition=(-280, 8)`
  (new right edge lands at -280+600 = 320.2)
- New `ClassSelectPanel` container: `sizeDelta≈(560, 691.6)`, `anchoredPosition≈(600, 8)`,
  anchors/pivot 0.5/0.5, parented as a **sibling** of `ReincarnateSkillTree` under
  `DeathScreen/GameObject`.
- Two cards inside it, stacked: roughly `sizeDelta=(520, 330)` each, at local
  `anchoredPosition≈(0, +175)` and `(0, -175)`.

**⚠ The `manage_components.set_property` call to resize `ReincarnateSkillTree` and the
`manage_gameobject.create` call to add the `ClassSelectPanel` container were both rejected
by the user mid-session, right before this handover was requested.** No reason was given.
Do not assume the sizing math above is wrong — it might just be that the user wants to
weigh in on the layout approach first, or wants to see it happen more incrementally, or
wants a different panel location (not "next to", something else). **Ask before resuming
scene mutation**, rather than re-issuing the same calls.

### GameObject instance IDs seen this session (for reference only — re-resolve, don't reuse)

- `DeathScreen` root Canvas: was `481014`
- `DeathScreen/GameObject` (parent for everything below, holds the `DeathScreen` script):
  was `480994`
- `ReincarnateSkillTree`: was `481470`
- `GoldSkillTree`: was `479820`
- `Reincarnate` button: was `-1120600`, its `Content` child `-1120602`

## Task 2 — Parry + Counterstrike onto Player prefab (not started)

Add `Parry` and `Counterstrike` components to `Assets/Bladehold/Bladehold Prefabs/Player.prefab`'s
root (next to `Health`/`DamageBlocker`). `Counterstrike.parry` auto-wires via `OnValidate`
(`GetComponent<Parry>()`) — no manual reference needed. Feedback slots
(`parryFeedback`) are optional, skip them (no art). Tune `facingDotThreshold` only if it
feels wrong in testing (default 0.3 is fine to leave alone). See TODO.md "Parry +
Counterstrike skill lines" section for the full context.

## Task 3 — CombatFacing onto Player prefab (not started)

Add `CombatFacing` component to `Player.prefab` root — `inputReader`/`bow`/
`characterController` auto-wire via `OnValidate`, `facingCamera` defaults to `Camera.main`.
Also add `CombatFacing` to `PlayerDeath`'s inspector list of components it disables on
death (so a corpse holding attack doesn't keep turning to face the camera). See TODO.md
"Combat facing" section.

## Task 4 — HoldTheLineBonus + gate (not started)

Add `HoldTheLineBonus` component to a scene systems object in `Bladehold Test Scene`
(player root or a systems object — it self-wires to `Player.Instance.Stats`, no manual
refs needed). Also **re-enable the `Gate` object** in the scene — there's already a
`Gate_Test` GameObject found in the hierarchy dump above, currently `activeSelf: false`.
This is the second Hold-the-Line fail-state (`Gate.OnAnyGateDestroyed`); both
`HoldTheLineBonus` and the intermission UI already listen for it, nothing else to wire.
See TODO.md "Between-wave intermission" section.

## Task 5 — Enemy prefab map (not started, flagged as blocking in TODO.md)

`Assets/Bladehold/Bladehold Scripts/Enemies/EnemyPrefabMap.asset` exists (generator-created)
but is missing the **hand-built** mappings: `goblin` → `Goblin Enemy (Base)`,
`goblin_brute` → `Goblin Brute Enemy Variant`, `storm_witch` → `Storm Witch Enemy Variant`,
`troll` → `Troll Enemy Variant` (all four prefabs already exist — confirmed in the scene
hierarchy dump above as disabled template instances: `Goblin Enemy`, `Storm Witch Enemy`,
`Goblin Brute Enemy`, `Troll Enemy Variant`). Then **assign this map asset** on the
`WaveSpawner` component in `Bladehold Test Scene` (found in hierarchy, instance was
`479928`) and on the `EnemyZoo` component in the Enemy Zoo scene. TODO.md flags this as
**currently blocking all enemy spawning** — nothing spawns until it's done.

## Recommended next step

Given the two rejected tool calls right as scene mutation was about to start, the most
useful thing for whoever picks this up is to **ask the user what specifically they wanted
different** about the class-select panel approach (layout placement, sizing, whether to
touch `ReincarnateSkillTree` at all, button style preference) before re-attempting Task 1.
Tasks 2-5 have no such ambiguity flagged and can likely proceed directly.
