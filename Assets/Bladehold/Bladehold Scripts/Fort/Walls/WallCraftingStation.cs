using UnityEngine;

/// <summary>
///     The workbench behind a <see cref="WallPlot" /> (plan 17, castle side). [E] builds the wall during
///     prep (or rebuilds it from rubble) and, once it stands, opens the upgrade wheel on it: material,
///     Repair, Spikes, one element, Deconstruct. The door is a separate interactable on the wall.
/// </summary>
public class WallCraftingStation : MonoBehaviour, IInteractable
{
    [SerializeField] private float interactionRadius = 3f;

    private WallPlot plot;

    public void Init(WallPlot owner)
    {
        plot = owner;
    }

    private void OnEnable()
    {
        InteractableRegistry.Register(this);
    }

    private void OnDisable()
    {
        InteractableRegistry.Unregister(this);
    }

    private static bool IsPrep => GameLoopManager.Instance == null || GameLoopManager.Instance.IsPrepPhase;

    public string PromptText
    {
        get
        {
            if (plot == null) return "";
            if (plot.HasStandingWall) return "Upgrade Wall";
            if (!IsPrep) return plot.HasRubble ? "Rebuild Wall (Locked: Wave Active)" : "Build Wall (Locked: Wave Active)";
            return $"{(plot.HasRubble ? "Rebuild" : "Build")} Wall ({WallPlot.BuildCost} Supply)";
        }
    }

    public bool CanInteract => plot != null && (plot.HasStandingWall || IsPrep);
    public Vector3 InteractionPosition => transform.position + Vector3.up;
    public float InteractionRadius => interactionRadius;

    public void Interact(Player player)
    {
        if (plot == null) return;
        if (plot.HasStandingWall)
        {
            BuildWheelUI wheel = BuildWheelUI.Instance;
            if (wheel != null) wheel.OpenUpgrades(plot.Wall);
            else Debug.LogWarning("[WallCraftingStation] BuildWheelUI.Instance is not found in the scene.");
            return;
        }
        plot.TryBuild();
    }
}
