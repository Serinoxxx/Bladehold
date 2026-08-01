---
name: mm-progress-bars
description: Use when working with MoreMountains Feel MMProgressBar or MMHealthBar to properly configure UI progress bars, avoiding common issues with Unity Sliders, anchored images, and delayed bars.
---

# MMProgressBar & MMHealthBar Configuration

This skill outlines the necessary steps and pitfalls when working with `MMProgressBar` and `MMHealthBar` from the MoreMountains Feel package, specifically within the Bladehold project.

## 1. Remove Interfering Components (The Slider Problem)
When adapting third-party UI prefabs (like Synty) to work with `MMProgressBar`, they often come with a built-in Unity `Slider` component. 
**CRITICAL:** You must completely remove the `Slider` component from the GameObject. If a `Slider` is present, it will fight for control over the `Fill Rect`'s width or fill amount, causing the `MMProgressBar` to appear broken, stuck, or unresponsive.

## 2. Choosing the Right FillMode
`MMProgressBar` supports different ways to scale the visual bar. The most common are `Width` and `FillAmount`.

*   **FillAmount (Recommended for Stretched/Anchored UI):** 
    *   Set the `MMProgressBar`'s `FillMode` to `FillAmount`.
    *   Set the target `Image` component's `Image Type` to `Filled`.
    *   Set the `Fill Method` to `Horizontal` and `Fill Origin` to `Left` (or as appropriate).
    *   This is the safest method if your UI images are anchored to stretch with their parent container, as `fillAmount` works independently of the RectTransform's `sizeDelta`.

*   **Width (For Fixed-Size Rects):**
    *   Set the `MMProgressBar`'s `FillMode` to `Width`.
    *   The target `Image` MUST NOT be anchored to stretch horizontally (i.e., its anchor Min X and Max X should be the same, usually 0 or 0.5).
    *   If the image is anchored to stretch, its `sizeDelta.x` will be 0 (or a negative margin value), and `MMProgressBar`'s multiplication logic will fail to scale it properly.

## 3. Configuring the Delayed Bar (Trailing Effect)
`MMProgressBar` supports a `DelayedBarDecreasing` transform to create a trailing effect when health is lost.
*   **Do not use the background image:** Assigning the UI background to the delayed bar will cause the entire background track to shrink when the player takes damage.
*   **Correct Setup:**
    1.  Duplicate the main foreground `Fill` image.
    2.  Rename it to `DelayedBar` and place it in the hierarchy directly *above* the `Fill` image so it renders behind it.
    3.  Set its color to white (or another appropriate trail color).
    4.  Assign this new `DelayedBar` object to the `DelayedBarDecreasing` field in `MMProgressBar`.
    5.  Ensure it uses the same `FillMode` logic (e.g., if using `FillAmount`, make sure the `DelayedBar` Image is also set to `Filled`).

## 4. MMHealthBar vs MMProgressBar
*   **MMProgressBar** is the visual UI component that animates a bar from 0 to 1 based on an external value.
*   **MMHealthBar** is an adapter script that automatically reads a target's `Health` component and updates an associated `MMProgressBar`.
*   If you are creating custom UI elements (like the Player Health Bar or Gate Health Bar), you generally want the UI script to manage the `MMProgressBar` directly (e.g. `PlayerHealthBarUI.cs`) rather than relying on `MMHealthBar`, so that you can hook into custom events or handle edge cases. 
*   If using `MMHealthBar`, ensure both it and the `MMProgressBar` are correctly linked.
