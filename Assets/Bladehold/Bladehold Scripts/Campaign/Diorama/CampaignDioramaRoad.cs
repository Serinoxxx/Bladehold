using UnityEngine;

/// <summary>
///     The glowing route overlay along one graph edge of the campaign map diorama (the dirt road itself is
///     painted into the terrain). <see cref="CampaignDiorama" /> sets its look from the edge's status: marching
///     gold dashes for roads open from the current location, green for the route travelled, faint otherwise.
/// </summary>
public class CampaignDioramaRoad : MonoBehaviour
{
    public enum RoadStatus
    {
        Locked,
        Available,
        Completed,
        Bypassed
    }

    [SerializeField] private string fromNodeId;
    [SerializeField] private string toNodeId;
    [SerializeField] private Renderer overlay;

    private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
    private static readonly int DashSpeedId = Shader.PropertyToID("_DashSpeed");

    private MaterialPropertyBlock block;

    public string FromNodeId => fromNodeId;
    public string ToNodeId => toNodeId;

    private void Start()
    {
        if (overlay == null)
        {
            Debug.LogError($"[CampaignDioramaRoad] '{name}' has no overlay renderer; rebuild the diorama.", this);
        }
    }

    public void ApplyStatus(RoadStatus status, CampaignDioramaLookSO look)
    {
        if (overlay == null || look == null) return;
        if (block == null) block = new MaterialPropertyBlock();

        Color color;
        float speed = 0f;
        switch (status)
        {
            case RoadStatus.Available:
                color = look.roadAvailableColor;
                speed = look.roadAvailableDashSpeed;
                break;
            case RoadStatus.Completed:
                color = look.roadCompletedColor;
                break;
            case RoadStatus.Bypassed:
                color = look.roadBypassedColor;
                break;
            default:
                color = look.roadLockedColor;
                break;
        }

        overlay.GetPropertyBlock(block);
        block.SetColor(GlowColorId, color);
        block.SetFloat(DashSpeedId, speed);
        overlay.SetPropertyBlock(block);
    }

#if UNITY_EDITOR
    /// <summary>Editor-only wiring for <c>CampaignDioramaBuilder</c>.</summary>
    public void EditorSetup(string from, string to, Renderer overlayRenderer)
    {
        fromNodeId = from;
        toNodeId = to;
        overlay = overlayRenderer;
    }
#endif
}
