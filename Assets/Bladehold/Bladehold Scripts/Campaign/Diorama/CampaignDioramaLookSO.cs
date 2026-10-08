using UnityEngine;

/// <summary>
///     Runtime look of the campaign map diorama: how node status reads on the castles (banners, glow ring,
///     greying out), the route overlay colours, and how the camera pans. The terrain and castles themselves
///     are baked by <c>CampaignDioramaBuilder</c> from its theme asset; this only covers what changes at runtime.
/// </summary>
[CreateAssetMenu(fileName = "CampaignDioramaLook", menuName = "Scriptable Objects/Campaign/Campaign Diorama Look")]
public class CampaignDioramaLookSO : ScriptableObject
{
    [Header("Castle banners")]
    [Tooltip("Banner colour over a sector the horde still holds.")]
    public Color hordeBannerColor = new Color(0.62f, 0.12f, 0.1f);
    [Tooltip("Banner colour once the player has liberated the sector.")]
    public Color liberatedBannerColor = new Color(0.16f, 0.36f, 0.78f);
    [Tooltip("Banner colour for sectors past the demo cutoff.")]
    public Color demoLockedBannerColor = new Color(0.25f, 0.25f, 0.28f);

    [Header("Castle tint by status")]
    [Range(0f, 1.5f)] public float bypassedSaturation = 0.15f;
    [Range(0f, 2f)] public float bypassedBrightness = 0.55f;
    [Range(0f, 1.5f)] public float demoLockedSaturation = 0.3f;
    [Range(0f, 2f)] public float demoLockedBrightness = 0.6f;
    [Range(0f, 1.5f)] public float completedSaturation = 0.9f;

    [Header("Open-sector ground ring")]
    [ColorUsage(true, true)] public Color availableRingColor = new Color(1.5f, 1.1f, 0.35f, 0.75f);

    [Header("Inspected node (keyboard/gamepad focus or mouse hover)")]
    [Tooltip("Wide pulsing ground ring around the castle whose tooltip is showing.")]
    [ColorUsage(true, true)] public Color inspectedRingColor = new Color(2.6f, 2.3f, 1.6f, 1f);
    [Tooltip("Inspected ring size relative to the open-sector ring.")]
    public float inspectedRingScale = 1.45f;
    [Tooltip("Ring pulse speed (the open-sector ring's material default is 2.2).")]
    public float inspectedRingPulseSpeed = 5f;
    [Tooltip("Rising light column over the inspected castle.")]
    [ColorUsage(true, true)] public Color inspectedBeamColor = new Color(2.2f, 1.8f, 0.9f, 0.55f);
    [Tooltip("Beam height and width as multiples of the open-sector ring's radius.")]
    public float inspectedBeamHeight = 3.5f;
    public float inspectedBeamWidth = 0.22f;
    [Tooltip("Castle brightness multiplier while inspected.")]
    [Range(1f, 2f)] public float inspectedBrightness = 1.25f;

    [Header("Route overlay")]
    [ColorUsage(true, true)] public Color roadAvailableColor = new Color(2.4f, 1.75f, 0.45f, 0.95f);
    [ColorUsage(true, true)] public Color roadCompletedColor = new Color(0.5f, 1.2f, 0.6f, 0.7f);
    [ColorUsage(true, true)] public Color roadLockedColor = new Color(0.85f, 0.8f, 0.7f, 0.12f);
    [ColorUsage(true, true)] public Color roadBypassedColor = new Color(0.5f, 0.5f, 0.55f, 0.08f);
    [Tooltip("Dash march speed on roads open from the current location (0 = still).")]
    public float roadAvailableDashSpeed = 0.9f;

    [Header("Camera")]
    [Tooltip("Seconds the camera takes to settle on a new focus.")]
    public float cameraSmoothTime = 0.35f;
    [Tooltip("World units panned per mouse-wheel notch.")]
    public float wheelPanStep = 3f;
    [Tooltip("Screen pixels the mouse must move with the button held before it counts as a drag (so clicks still deploy).")]
    public float dragThreshold = 8f;
    [Tooltip("Degrees the camera leans towards the mouse, for a little parallax on the miniatures.")]
    public float mouseParallaxDegrees = 1.2f;
}
