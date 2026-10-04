using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     "Ride over the carrots": switches on a set of hand-placed <see cref="CarrotPickup" />s (keep them
///     inactive in the scene so wave 1 can't eat them early) and completes once they are all eaten,
///     with a "1/3" counter and a HUD waypoint on each carrot left. The placed carrots should have
///     Collect When Full on, so a full bar can't stall the step. The bar starts the step at
///     <see cref="startMountStamina" /> so every carrot shows a gain.
/// </summary>
public class CollectCarrotsStep : TutorialStep
{
    [SerializeField] private List<CarrotPickup> carrots = new List<CarrotPickup>();
    [SerializeField] private Color carrotWaypointTint = new Color(1f, 0.6f, 0.15f, 1f);

    [Tooltip("Charge stamina (0-1) set when the step begins, so each carrot visibly fills the bar (the passive trickle refills it during earlier steps). Negative = leave it alone.")]
    [Range(-1f, 1f)] [SerializeField] private float startMountStamina = 0f;

    private int eaten;
    private bool anyError;

    private void Start()
    {
        if (carrots.Count == 0)
        {
            Debug.LogError($"[CollectCarrotsStep] {name}: no carrots assigned.", this);
            anyError = true;
        }
        foreach (CarrotPickup carrot in carrots)
        {
            if (carrot == null)
            {
                Debug.LogError($"[CollectCarrotsStep] {name}: a carrot is not assigned.", this);
                anyError = true;
            }
        }
    }

    protected override void OnBegin()
    {
        if (anyError) return;
        if (startMountStamina >= 0f) SetMountStamina(startMountStamina);
        foreach (CarrotPickup carrot in carrots)
        {
            carrot.gameObject.SetActive(true);
            carrot.OnCollected += HandleCollected;
        }
        Refresh();
    }

    protected override void OnEnd()
    {
        foreach (CarrotPickup carrot in carrots)
        {
            if (carrot != null) carrot.OnCollected -= HandleCollected;
        }
    }

    private static void SetMountStamina(float fraction)
    {
        // PlayerMount sits on the player root; Player.Instance is on the Synty character child.
        PlayerMount mount = Player.Instance != null ? Player.Instance.transform.root.GetComponentInChildren<PlayerMount>(true) : null;
        if (mount == null || RunSession.MountLost) return;
        if (mount.CurrentHorse != null) mount.CurrentHorse.SetNormalizedStamina(fraction);
        RunSession.MountStaminaFraction = fraction;
    }

    private void HandleCollected(CarrotPickup carrot)
    {
        carrot.OnCollected -= HandleCollected;
        eaten++;
        Refresh();
    }

    private void Refresh()
    {
        Director.SetCounter($"{eaten}/{carrots.Count}");
        if (eaten >= carrots.Count) Complete();
    }

    public override void GetExtraWaypoints(List<ObjectiveWaypointTarget> results)
    {
        foreach (CarrotPickup carrot in carrots)
        {
            if (carrot != null && !carrot.IsCollected && carrot.gameObject.activeInHierarchy)
            {
                results.Add(new ObjectiveWaypointTarget(carrot.transform, new Vector3(0f, 1f, 0f), null, carrotWaypointTint, "carrot"));
            }
        }
    }
}
