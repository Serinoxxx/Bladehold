---
name: feel-integration
description: Master router skill for using MoreMountains Feel feedbacks. Contains the categorized index of all 194 feedbacks.
---

# MoreMountains Feel Integration

This skill serves as the master index for utilizing MoreMountains Feel feedbacks in Bladehold. 

## Best Practices in Bladehold
- Can be added to an MMF_Player component to trigger these effects.
- Keep in mind Bladehold's component-based reactive architecture when triggering feedbacks. Feedbacks should generally be triggered by event listeners (e.g., listening to Health.OnDied or Health.OnDamaged or AnimationEvents) rather than hardcoded in core logic.
- When adding feedbacks via the Unity Editor, make sure to test it via the MMF_Player's "Play" button in edit mode, or use the unity-mcp bridge to configure it programmatically.
- Record manual Editor wiring tasks in TODO.md if the configuration cannot be done purely in C#.

## Documentation References
To learn about specific feedbacks, use the iew_file tool to read the corresponding reference file below:

- **Feedbacks**: eferences/feedbacks.md (3 feedbacks)
- **Events**: eferences/events.md (3 feedbacks)
- **Renderer**: eferences/renderer.md (15 feedbacks)
- **Debug**: eferences/debug.md (3 feedbacks)
- **Springs**: eferences/springs.md (5 feedbacks)
- **HDRP Volume**: eferences/hdrp-volume.md (11 feedbacks)
- **Animation**: eferences/animation.md (5 feedbacks)
- **Audio**: eferences/audio.md (19 feedbacks)
- **Particles**: eferences/particles.md (4 feedbacks)
- **Time**: eferences/time.md (2 feedbacks)
- **TextMesh Pro**: eferences/textmesh-pro.md (15 feedbacks)
- **Nice Vibrations**: eferences/nice-vibrations.md (5 feedbacks)
- **Various**: eferences/various.md (2 feedbacks)
- **Scene**: eferences/scene.md (2 feedbacks)
- **UI**: eferences/ui.md (22 feedbacks)
- **Transform**: eferences/transform.md (14 feedbacks)
- **Pause**: eferences/pause.md (2 feedbacks)
- **Camera**: eferences/camera.md (12 feedbacks)
- **UI Toolkit**: eferences/ui-toolkit.md (17 feedbacks)
- **URP Volume**: eferences/urp-volume.md (10 feedbacks)
- **Post Processing**: eferences/post-processing.md (8 feedbacks)
- **GameObject**: eferences/gameobject.md (13 feedbacks)
- **Loop**: eferences/loop.md (2 feedbacks)

