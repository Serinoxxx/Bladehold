using UnityEngine;

/// <summary>Spawns a <see cref="TutorialEncounter" /> on begin and completes when all of it is dead.</summary>
public class KillEnemiesStep : TutorialStep
{
    [SerializeField] private TutorialEncounter encounter;

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

    private void HandleAliveChanged(int alive, int total)
    {
        Director.SetCounter($"{total - alive}/{total}");
    }

    private void HandleAllDead() => Complete();
}
