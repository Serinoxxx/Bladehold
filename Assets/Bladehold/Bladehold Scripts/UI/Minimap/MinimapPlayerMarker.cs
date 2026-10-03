using UnityEngine;

/// <summary>You on the minimap: an arrow turned to the player's facing, under a view cone turned to the camera.</summary>
public class MinimapPlayerMarker : MinimapMarker
{
    [SerializeField] private RectTransform arrow;
    [SerializeField] private RectTransform viewCone;

    public override Vector3 WorldPosition => Player.Instance != null ? Player.Instance.transform.position : transform.position;
    public override bool Hoverable => false;

    private void Start()
    {
        if (arrow == null) Debug.LogError($"[MinimapPlayerMarker] {name}: arrow is not assigned.", this);
    }

    public override void Refresh(MinimapConfigSO config)
    {
    }

    /// <summary>Turns the arrow and cone; called every frame by the owner.</summary>
    public void UpdateHeading(MinimapProjection projection)
    {
        if (Player.Instance == null) return;
        if (arrow != null) arrow.localEulerAngles = new Vector3(0f, 0f, projection.HeadingToUiAngle(Player.Instance.transform.forward));
        Camera cam = Camera.main;
        if (viewCone != null && cam != null) viewCone.localEulerAngles = new Vector3(0f, 0f, projection.HeadingToUiAngle(cam.transform.forward));
    }

    public override void GetTooltip(out string title, out string body)
    {
        title = string.Empty;
        body = string.Empty;
    }
}
