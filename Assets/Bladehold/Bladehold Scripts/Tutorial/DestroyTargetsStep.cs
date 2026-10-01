using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Completes when every target <see cref="Health" /> has died (banners, the door, the ropes), with a
///     "1/3" counter on the hint. Optionally watches the quiver: when the player has fewer arrows than
///     targets left, the secondary line points at <see cref="ammoCrate" /> and it gets a waypoint.
/// </summary>
public class DestroyTargetsStep : TutorialStep
{
    [Header("Targets")]
    [SerializeField] private List<Health> targets = new List<Health>();
    [Tooltip("Show a 1/3 counter on the hint. Off for single targets (the door).")]
    [SerializeField] private bool showCounter = true;

    [Header("Ammo helper (optional, bow step)")]
    [Tooltip("Optional: free ammo crate pointed at when the quiver can't finish the job. Leave empty for none.")]
    [SerializeField] private AmmoChest ammoCrate;
    [SerializeField] private TutorialHint lowAmmoHint = TutorialHint.Of("tutorial.bow_ammo", "Out of arrows? Take more from the crate", "Interact");

    private bool anyError;
    private bool showingLowAmmo;

    private void Start()
    {
        if (targets.Count == 0)
        {
            Debug.LogError($"[DestroyTargetsStep] {name}: no targets assigned.", this);
            anyError = true;
        }
        foreach (Health h in targets)
        {
            if (h == null)
            {
                Debug.LogError($"[DestroyTargetsStep] {name}: a target is not assigned.", this);
                anyError = true;
            }
        }
    }

    protected override void OnBegin()
    {
        if (anyError) return;
        foreach (Health h in targets) h.OnDied += HandleTargetDied;
        Refresh();
    }

    protected override void OnEnd()
    {
        foreach (Health h in targets)
        {
            if (h != null) h.OnDied -= HandleTargetDied;
        }
    }

    private void Update()
    {
        if (!IsActive || ammoCrate == null) return;
        bool low = Player.Instance != null && Player.Instance.Ammo != null && !Player.Instance.Ammo.InfiniteAmmo
                   && Player.Instance.Ammo.CurrentAmmo < Remaining;
        if (low == showingLowAmmo) return;
        showingLowAmmo = low;
        Director.SetSecondaryHint(low ? lowAmmoHint : null);
    }

    private void HandleTargetDied() => Refresh();

    private int Remaining
    {
        get
        {
            int n = 0;
            foreach (Health h in targets)
            {
                if (h != null && !h.IsDead) n++;
            }
            return n;
        }
    }

    private void Refresh()
    {
        int remaining = Remaining;
        if (showCounter) Director.SetCounter($"{targets.Count - remaining}/{targets.Count}");
        if (remaining == 0) Complete();
    }

    public override void GetExtraWaypoints(List<ObjectiveWaypointTarget> results)
    {
        if (showingLowAmmo && ammoCrate != null)
        {
            results.Add(new ObjectiveWaypointTarget(ammoCrate.transform, new Vector3(0f, 1.5f, 0f), null, new Color(0.6f, 0.9f, 1f, 1f), "ammo"));
        }
    }
}
