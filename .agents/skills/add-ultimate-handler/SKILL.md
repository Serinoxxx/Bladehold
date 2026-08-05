---
name: add-ultimate-handler
description: Use when adding a new ultimate ability (IUltimateHandler) for a player class in Bladehold — ensures proper player component resolution, stat configuration, dependency validation, and lifecycle management.
---

# Add a Player Ultimate Handler

When creating a new ultimate ability (e.g. `MageUltimate`, `RangerUltimate`, `BerserkerUltimate`), you must follow specific lifecycle and hierarchy rules to integrate cleanly with `PlayerUltimateController`.

## Step 1 — Implementation Requirements

1. **Implement `IUltimateHandler`**: Your component must implement the `IUltimateHandler` interface and provide the `Activate(PlayerUltimateController controller)` method.
2. **Never assume the component hierarchy**: Ultimate handler scripts are typically placed alongside `PlayerUltimateController`, but the core `Player` component may be located on a child GameObject. 
   - **DO NOT** use `GetComponent<Player>()`.
   - **ALWAYS** use `GetComponentInChildren<Player>()` to find the player reference.
3. **Validate dependencies**: In your `Start` method, explicitly check that the `Player` and `PlayerStats` components were found. If they are null, log an error (`Debug.LogError`) and set an `anyError` flag. All `Update` loops and the `Activate` method must early-return if `anyError` is true. Do not allow silent `NullReferenceException` failures.

## Step 2 — Configuration via Stats

Values like duration, damage, or specific mechanical numbers must be read from the `PlayerStats` system (`StatType`), rather than hardcoded or exposed only as simple serialized fields, so they can be modified by the Skill Tree / Reincarnate systems.

```csharp
float duration = player.Stats.GetValue(StatType.UltimateDurationSeconds);
```

If adding a completely new mechanical stat (e.g., `UltimateMageLandingExplosionRadius`), register its base value in `PlayerUltimateController.RegisterDefaultStats()` so it can be modified later.

## Step 3 — Lifecycle Management

1. **Keep a reference to the controller**: When `Activate(PlayerUltimateController controller)` is called, save the `controller` reference locally.
2. **End the ultimate**: When your ultimate's effect concludes (e.g., the duration expires), you **MUST** call `controller.EndUltimate()` to properly reset the UI and internal state.
3. **Clean up**: Ensure that any stat modifiers added (via `player.Stats.AddModifier`) or event listeners subscribed to (e.g., `Health.ScaleDamageTaken`) during the ultimate are properly removed or reverted when `EndUltimate()` is called or if the component is disabled (`OnDisable`).

## Step 4 — Editor Wiring

Record any Unity Editor manual setup steps in `TODO.md` using the `editor-wiring-todo` skill:
- Adding the new component to the specific class prefab variant.
- Configuring any `[SerializeField]` particle effects, VFX, or sound clips.
- Adding specific animator states or triggers required by the ultimate.
