---
name: mm-progress-bars
description: Use when adding or fixing a Bladehold UI bar (health, boss, gate, charge, cast, stamina, ultimate) built on Feel's MMProgressBar or MMHealthBar — fill mode, delayed bar, Slider conflicts, and UpdateBar vs SetBar.
---

# MMProgressBar / MMHealthBar

Every Bladehold bar is Feel's `MoreMountains.Tools.MMProgressBar` (`Assets/Third Party/Feel/MMTools/Core/MMUI/MMProgressBar.cs`). Ignore the `Lofelt.NiceVibrations.MMProgressBar` demo class with the same name.

## Which pattern

| Bar | Pattern | Precedent |
|---|---|---|
| HUD bar fed by events | UI script holds a serialized `MMProgressBar`, subscribes, calls `UpdateBar` on change | `UI/PlayerHealthBarUI.cs` (`Health.OnHealthChanged`), `BossHealthBarUI`, `FortressGateHealthBarUI`, `HorseHealthBarUI` |
| Bar polled every frame | Same, but `SetBar`/`SetBar01` | `UI/SummonCastBarUI.cs`, `UltimateBarUI` (fill-up animation) |
| World-space bar over an enemy, chest, cage | `MMHealthBar` + `UI/HealthBarUI.cs` on the same object, bound to a `Health` | `Chests/Chest.cs`, enemy prefabs |

Validate the bar in `Start` (LogError + `anyError`, early-return from `Update`), per CLAUDE.md. `HorseStaminaUI` is a clean example of that part.

## UpdateBar vs SetBar (the big one)

- `UpdateBar(current, min, max)` is for **discrete changes** (damage, a spent chunk). Each call that changes the value stops and restarts the lerp coroutine and triggers a `Bump()`.
- Called **every frame** with a moving value (charge, cast timer, cooldown, stamina), `UpdateBar` restarts the lerp each frame, so the fill lags or looks frozen and it bumps constantly. Use **`SetBar(current, min, max)` / `SetBar01(t)`** for per-frame updates: they set the fill directly.
- `MMProgressBar.TimeScale` defaults to `UnscaledTime`, so bars keep animating on paused screens. Switch it to `Time` only if the bar must freeze with the game.

## Prefab setup

- **Remove any Unity `Slider`** from bars adapted from Synty prefabs (`Assets/Synty/InterfaceFantasyWarriorHUD/Prefabs/Player_Health_Equipment/`, `NPC_HealthBars_EnemyData/`). A Slider fights `MMProgressBar` for the fill rect, and the bar looks stuck.
- **FillMode** (default `LocalScale`; also `FillAmount`, `Width`, `Height`, `Anchor`):
  - `FillAmount` for stretched/anchored images: set the `Image` to `Filled`, Horizontal, origin Left. Safest choice.
  - `Width` only when the fill rect is **not** horizontally stretched (anchor min X = max X); a stretched rect has `sizeDelta.x` ≈ 0 and won't scale.
- **Delayed (trailing) bar:** duplicate the foreground fill, name it `DelayedBar`, put it above `Fill` in the hierarchy (renders behind), tint it, assign it to `DelayedBarDecreasing` (and `DelayedBarIncreasing` if wanted). Same fill mode/Image type as the foreground. Never assign the background track, or the whole track shrinks on damage.

Wire prefab changes via Unity MCP (`/unity-editor-mcp`) or list them in the plan's `plans/editor/` checklist (`/editor-wiring-todo`). Bar feedback beyond the built-in bump (shake on hit, pulse when full) is an `MMF_Player` per `/feel-integration`.
