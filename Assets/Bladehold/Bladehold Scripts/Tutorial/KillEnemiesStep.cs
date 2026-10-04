using UnityEngine;

/// <summary>
///     Spawns a <see cref="TutorialEncounter" /> on begin and completes when all of it is dead.
///     <see cref="keepMountStaminaFull" /> turns it into the "charge through the warband" beat: the
///     horse's charge stamina is held full while the step runs, so the lesson is the charge itself.
/// </summary>
public class KillEnemiesStep : TutorialStep
{
    [SerializeField] private TutorialEncounter encounter;

    [Tooltip("Hold the ridden horse's charge stamina full while this step runs (the charge-through-the-warband step).")]
    [SerializeField] private bool keepMountStaminaFull;

    private PlayerMount mount;
    private bool anyError;

    private void Start()
    {
        if (encounter == null)
        {
            Debug.LogError($"[KillEnemiesStep] {name}: encounter is not assigned.", this);
            anyError = true;
        }
    }

    protected override void OnBegin()
    {
        if (anyError) return;
        if (keepMountStaminaFull)
        {
            // PlayerMount sits on the player root; Player.Instance is on the Synty character child.
            mount = Player.Instance != null ? Player.Instance.transform.root.GetComponentInChildren<PlayerMount>(true) : null;
            if (mount == null) Debug.LogError($"[KillEnemiesStep] {name}: no PlayerMount on the player to keep stamina full.", this);
        }
        encounter.OnAliveCountChanged += HandleAliveChanged;
        encounter.OnAllDead += HandleAllDead;
        encounter.Spawn();
    }

    protected override void OnEnd()
    {
        if (encounter == null) return;
        encounter.OnAliveCountChanged -= HandleAliveChanged;
        encounter.OnAllDead -= HandleAllDead;
    }

    private void Update()
    {
        if (!IsActive || mount == null || mount.MountStaminaFraction >= 1f) return;
        HorseMotor horse = mount.CurrentHorse;
        mount.AddMountStamina(horse != null ? horse.MaxStamina : 100f);
    }

    private void HandleAliveChanged(int alive, int total)
    {
        Director.SetCounter($"{total - alive}/{total}");
    }

    private void HandleAllDead() => Complete();
}
