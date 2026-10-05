using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
///     Pans the campaign map camera along the diorama (world X). The rig keeps its height and pitch; it
///     glides to whatever <see cref="FocusOn" /> asks for (keyboard/gamepad focus, the open nodes on load),
///     and the player can drag with any mouse button, scroll the wheel or tilt the right stick. A drag
///     swallows the click that ends it (<see cref="SuppressClick" />) so letting go over a castle doesn't deploy.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CampaignDioramaCamera : MonoBehaviour
{
    [SerializeField] private CampaignDioramaLookSO look;
    [Tooltip("Pan limits in world X, set by the diorama builder from the node extents.")]
    [SerializeField] private float minX;
    [SerializeField] private float maxX = 60f;
    [Tooltip("World point the camera looks at when panned to X = 0 (the builder sets it; X is replaced by the pan).")]
    [SerializeField] private Vector3 lookOffset = new Vector3(0f, -22f, 20f);

    private Camera cam;
    private float targetX;
    private float velocity;
    private Quaternion restRotation;
    private Vector2 pressPosition;
    private bool pressed;
    private bool dragging;
    private float suppressClickUntil;
    private bool anyError;

    public Camera Camera => cam;

    /// <summary>True for a moment after a drag ends, so the button under the cursor ignores that release.</summary>
    public bool SuppressClick => dragging || Time.unscaledTime < suppressClickUntil;

    private void OnValidate()
    {
        if (cam == null) cam = GetComponent<Camera>();
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
        restRotation = transform.rotation;
        targetX = transform.position.x;
    }

    private void Start()
    {
        if (look == null)
        {
            Debug.LogError("[CampaignDioramaCamera] No CampaignDioramaLookSO assigned.", this);
            anyError = true;
        }
    }

    /// <summary>Glide so the given world X sits in the middle of the screen.</summary>
    public void FocusOn(float worldX, bool instant = false)
    {
        targetX = Mathf.Clamp(worldX, minX, maxX);
        if (instant)
        {
            velocity = 0f;
            Vector3 p = transform.position;
            p.x = targetX;
            transform.position = p;
        }
    }

    /// <summary>The world X currently centred on screen (where the camera is heading).</summary>
    public float FocusX => targetX;

    private void Update()
    {
        if (anyError) return;
        float dt = Time.unscaledDeltaTime;

        if (!DevConsole.IsVisible)
        {
            ReadMouse();
            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                float stick = pad.rightStick.ReadValue().x;
                if (Mathf.Abs(stick) > 0.2f) targetX = Mathf.Clamp(targetX + stick * look.stickPanSpeed * dt, minX, maxX);
            }
        }

        Vector3 pos = transform.position;
        pos.x = dragging ? targetX : Mathf.SmoothDamp(pos.x, targetX, ref velocity, look.cameraSmoothTime, Mathf.Infinity, dt);
        transform.position = pos;

        // A touch of lean towards the cursor so the miniatures shift against each other.
        Quaternion lean = Quaternion.identity;
        Mouse mouse = Mouse.current;
        if (mouse != null && look.mouseParallaxDegrees > 0f && Screen.width > 0 && Screen.height > 0)
        {
            Vector2 m = mouse.position.ReadValue();
            float nx = Mathf.Clamp(m.x / Screen.width * 2f - 1f, -1f, 1f);
            float ny = Mathf.Clamp(m.y / Screen.height * 2f - 1f, -1f, 1f);
            lean = Quaternion.Euler(-ny * look.mouseParallaxDegrees * 0.5f, nx * look.mouseParallaxDegrees, 0f);
        }
        transform.rotation = Quaternion.Slerp(transform.rotation, lean * restRotation, 1f - Mathf.Exp(-dt * 4f));
    }

    private void ReadMouse()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        float wheel = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(wheel) > 0.01f)
        {
            targetX = Mathf.Clamp(targetX - Mathf.Sign(wheel) * look.wheelPanStep, minX, maxX);
        }

        bool down = mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed;
        Vector2 position = mouse.position.ReadValue();

        if (down && !pressed)
        {
            pressed = true;
            dragging = false;
            pressPosition = position;
        }
        else if (!down && pressed)
        {
            pressed = false;
            if (dragging) suppressClickUntil = Time.unscaledTime + 0.1f;
            dragging = false;
        }

        if (!pressed) return;

        if (!dragging && (position - pressPosition).magnitude > look.dragThreshold)
        {
            dragging = true;
        }

        if (dragging)
        {
            float delta = mouse.delta.ReadValue().x;
            targetX = Mathf.Clamp(targetX - delta * WorldUnitsPerPixel(), minX, maxX);
            velocity = 0f;
        }
    }

    // How far one screen pixel spans on the ground plane the camera is looking at.
    private float WorldUnitsPerPixel()
    {
        if (cam == null || Screen.height <= 0) return 0.02f;
        float distance = lookOffset.magnitude;
        return 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Screen.height;
    }

#if UNITY_EDITOR
    /// <summary>Editor-only wiring for <c>CampaignDioramaBuilder</c>.</summary>
    public void EditorSetup(CampaignDioramaLookSO lookAsset, float panMin, float panMax, Vector3 offsetToFocus)
    {
        look = lookAsset;
        minX = panMin;
        maxX = panMax;
        lookOffset = offsetToFocus;
    }
#endif
}
