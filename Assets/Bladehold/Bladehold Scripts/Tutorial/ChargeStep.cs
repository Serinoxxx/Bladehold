using UnityEngine;

/// <summary>
///     "Hold Sprint to charge": tops the horse's charge stamina up on begin, then completes once the
///     ridden horse has charged itself winded (<see cref="HorseMotor.IsExhausted" />), so the player
///     sees the bar drain and is set up for the refill steps after it (carrots, kills). The counter
///     shows the stamina left. The hint's glyph should be the Sprint action. If the player is on foot,
///     the secondary line points back at the summon.
/// </summary>
public class ChargeStep : TutorialStep
{
    [SerializeField] private TutorialHint dismountedHint = TutorialHint.Of("tutorial.charge_mount", "Summon your warhorse first", "SummonMount");

    private PlayerMount mount;
    private bool showingDismounted;
    private int shownPercent = -1;

    protected override void OnBegin()
    {
        // PlayerMount sits on the player root; Player.Instance is on the Synty character child.
        mount = Player.Instance != null ? Player.Instance.transform.root.GetComponentInChildren<PlayerMount>(true) : null;
        if (mount == null)
        {
            Debug.LogError($"[ChargeStep] {name}: no PlayerMount on the player.", this);
            return;
        }
        // A full bar to burn, whatever wave 1 left in the bank.
        HorseMotor horse = mount.CurrentHorse;
        mount.AddMountStamina(horse != null ? horse.MaxStamina : 100f);
    }

    private void Update()
    {
        if (!IsActive || mount == null) return;

        HorseMotor horse = mount.CurrentHorse;
        bool dismounted = horse == null;
        if (dismounted != showingDismounted)
        {
            showingDismounted = dismounted;
            Director.SetSecondaryHint(dismounted ? dismountedHint : null);
        }

        int percent = Mathf.RoundToInt(mount.MountStaminaFraction * 100f);
        if (percent != shownPercent)
        {
            shownPercent = percent;
            Director.SetCounter($"{percent}%");
        }

        if (horse != null && horse.IsExhausted) Complete();
    }
}
