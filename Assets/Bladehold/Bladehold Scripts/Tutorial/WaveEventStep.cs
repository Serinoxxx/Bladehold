using UnityEngine;

/// <summary>
///     Completes on a <see cref="GameLoopManager" /> event: <see cref="Trigger.WaveStarted" /> (the player
///     held Ready) or <see cref="Trigger.Victory" /> (the wave is beaten; the normal victory flow takes
///     over from there) or <see cref="Trigger.WaveCleared" /> (any wave's rout is over and prep has begun).
/// </summary>
public class WaveEventStep : TutorialStep
{
    public enum Trigger
    {
        WaveStarted,
        Victory,
        WaveCleared
    }

    [SerializeField] private Trigger trigger = Trigger.WaveStarted;

    private GameLoopManager loop;

    // The wave itself completes this step (and a Victory step spans the waves after Ready2), so it never holds Ready shut.
    public override bool BlocksWaveStart => false;

    // Not a lesson of its own (Ready, or the waves themselves): every BUILD marker stays up.
    public override bool GetBuildMarkerFocus(System.Collections.Generic.List<Object> allowedPlots) => false;

    protected override void OnBegin()
    {
        loop = GameLoopManager.Instance;
        if (loop == null)
        {
            Debug.LogError($"[WaveEventStep] {name}: no GameLoopManager in the scene.", this);
            return;
        }
        if (trigger == Trigger.WaveStarted)
        {
            // Already under way (Ready was held during an earlier step): nothing to wait for.
            if (!loop.IsPrepPhase)
            {
                Complete();
                return;
            }
            loop.OnWaveStarted += HandleWaveStarted;
        }
        else if (trigger == Trigger.WaveCleared) loop.OnWaveCleared += HandleWaveCleared;
        else loop.OnVictory += HandleVictory;
    }

    protected override void OnEnd()
    {
        if (loop == null) return;
        loop.OnWaveStarted -= HandleWaveStarted;
        loop.OnVictory -= HandleVictory;
        loop.OnWaveCleared -= HandleWaveCleared;
    }

    private void HandleWaveStarted(int wave) => Complete();

    private void HandleVictory() => Complete();

    private void HandleWaveCleared(int wave, string title) => Complete();
}
