using UnityEngine;

/// <summary>
///     Completes when the player is riding (<see cref="PlayerMount.OnMountedChanged" />): the tutorial's
///     "summon your horse" step between waves. The hint's glyph should be the SummonMount action. Needs
///     the scene's <see cref="SceneAbilityRules" /> to allow the mount.
/// </summary>
public class MountStep : TutorialStep
{
    private PlayerMount mount;

    protected override void OnBegin()
    {
        // PlayerMount sits on the player root; Player.Instance is on the Synty character child.
        mount = Player.Instance != null ? Player.Instance.transform.root.GetComponentInChildren<PlayerMount>(true) : null;
        if (mount == null)
        {
            Debug.LogError($"[MountStep] {name}: no PlayerMount on the player.", this);
            return;
        }
        if (!SceneAbilityRules.MountAllowed)
        {
            Debug.LogError($"[MountStep] {name}: this scene's SceneAbilityRules block the mount, so the step can never finish.", this);
        }
        if (mount.IsMounted)
        {
            Complete();
            return;
        }
        mount.OnMountedChanged += HandleMountedChanged;
    }

    protected override void OnEnd()
    {
        if (mount != null) mount.OnMountedChanged -= HandleMountedChanged;
    }

    private void HandleMountedChanged(bool mounted)
    {
        if (mounted) Complete();
    }
}
