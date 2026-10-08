using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
///     The ring layout and modal plumbing shared by the radial wheels (<see cref="BuildWheelUI" /> and
///     <see cref="UltimateWheelUI" />). Slices are <see cref="BuildWheelButton" />s: the first authored one is
///     cloned as needed and every slice is laid out evenly round the ring the authored ones sit on, so the
///     option count is free. While open, a wheel unlocks the cursor and suspends the pause toggle (Esc/B
///     close the wheel instead).
/// </summary>
public class RadialWheel
{
    private readonly List<BuildWheelButton> buttons;
    private readonly string ownerKey;

    // The authored ring the slices sit on (captured once from the authored buttons).
    private bool ringCaptured;
    private Vector2 ringCentre;
    private float ringRadius;
    private float ringStartAngle;

    /// <param name="buttons">The wheel's slice list; clones are added to it.</param>
    /// <param name="ownerKey">Cursor-unlock owner key (unique per wheel).</param>
    public RadialWheel(List<BuildWheelButton> buttons, string ownerKey)
    {
        this.buttons = buttons;
        this.ownerKey = ownerKey;
    }

    /// <summary>Unlocks the cursor and suspends the pause toggle while open; restores both on close.</summary>
    public void SetModal(bool open)
    {
        CursorLockManager.SetUnlock(ownerKey, open);
        PauseMenuController.Instance?.SetToggleEnabled(!open);
    }

    /// <summary>Clones the first authored slice until there are <paramref name="count" /> buttons.</summary>
    public void EnsureButtonCount(int count)
    {
        if (buttons == null || buttons.Count == 0 || buttons[0] == null) return;
        CaptureRing();
        BuildWheelButton template = buttons[0];
        while (buttons.Count < count)
        {
            BuildWheelButton clone = Object.Instantiate(template, template.transform.parent);
            clone.name = $"Slice_{buttons.Count}_Runtime";
            buttons.Add(clone);
        }
    }

    /// <summary>Slot <paramref name="slot" /> of <paramref name="count" /> evenly round the ring, clockwise from the first authored slice.</summary>
    public void PlaceOnRing(BuildWheelButton button, int slot, int count)
    {
        CaptureRing();
        if (!ringCaptured || count <= 0 || !(button.transform is RectTransform rt)) return;
        rt.anchoredPosition = ringCentre + DirectionOf(slot, count) * ringRadius;
    }

    /// <summary>
    ///     The slot (of <paramref name="count" />) a stick pointing along <paramref name="stick" /> selects, or -1
    ///     inside the dead zone. Uses the same angles as <see cref="PlaceOnRing" />.
    /// </summary>
    public int SlotForDirection(Vector2 stick, int count, float deadZone)
    {
        if (count <= 0 || stick.magnitude < deadZone) return -1;
        CaptureRing();
        Vector2 dir = stick.normalized;
        int best = -1;
        float bestDot = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            float dot = Vector2.Dot(dir, DirectionOf(i, count));
            if (dot > bestDot)
            {
                bestDot = dot;
                best = i;
            }
        }
        return best;
    }

    private Vector2 DirectionOf(int slot, int count)
    {
        float angle = ringStartAngle - slot * (Mathf.PI * 2f / count);
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    /// <summary>Remembers the ring the authored slices sit on: their centroid, mean radius and first angle.</summary>
    private void CaptureRing()
    {
        if (ringCaptured || buttons == null) return;
        Vector2 sum = Vector2.zero;
        int n = 0;
        foreach (BuildWheelButton b in buttons)
        {
            if (b == null || !(b.transform is RectTransform rt)) continue;
            sum += rt.anchoredPosition;
            n++;
        }
        if (n == 0) return;
        ringCentre = sum / n;
        float r = 0f;
        foreach (BuildWheelButton b in buttons)
        {
            if (b == null || !(b.transform is RectTransform rt)) continue;
            r += (rt.anchoredPosition - ringCentre).magnitude;
        }
        ringRadius = r / n;
        Vector2 first = ((RectTransform)buttons[0].transform).anchoredPosition - ringCentre;
        ringStartAngle = Mathf.Atan2(first.y, first.x);
        ringCaptured = ringRadius > 1f;
    }

    // ---- Gamepad: gameplay-input suppression and the stick pointer -----------------------------

    // Gameplay actions switched off while a wheel is open, so the stick picks a slice instead of walking
    // the player and A/X/triggers don't jump, interact or swing underneath it.
    private static readonly string[] SuppressedActions = { "Move", "Look", "Attack", "Aim", "Jump", "Crouch", "Interact", "SummonMount", "Dismount", "StartWave", "LockOn", "Sprint" };
    private readonly List<InputAction> disabledActions = new List<InputAction>();
    private RectTransform pointer;

    /// <summary>
    ///     Disables the player's gameplay actions while the wheel is open (only the ones that were enabled,
    ///     and only those are re-enabled on close).
    /// </summary>
    public void SuppressGameplayInput(bool suppress)
    {
        if (!suppress)
        {
            foreach (InputAction action in disabledActions)
            {
                action?.Enable();
            }
            disabledActions.Clear();
            return;
        }

        if (disabledActions.Count > 0) return;
        InputActionMap map = Player.Instance != null && Player.Instance.InputSettings != null
            ? Player.Instance.InputSettings.GetRebindableActionMap()
            : null;
        if (map == null) return;
        foreach (string name in SuppressedActions)
        {
            InputAction action = map.FindAction(name);
            if (action == null || !action.enabled) continue;
            action.Disable();
            disabledActions.Add(action);
        }
    }

    /// <summary>
    ///     Points the stick indicator (a small arrow built at runtime between the hub and the slices) along
    ///     <paramref name="direction" />; hidden when <paramref name="visible" /> is false.
    /// </summary>
    public void SetPointer(Vector2 direction, bool visible)
    {
        if (visible && pointer == null) CreatePointer();
        if (pointer == null) return;
        pointer.gameObject.SetActive(visible);
        if (!visible || direction.sqrMagnitude < 0.0001f) return;
        Vector2 dir = direction.normalized;
        pointer.anchoredPosition = ringCentre + dir * (ringRadius * PointerRadius);
        pointer.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
        pointer.SetAsLastSibling();
    }

    // Arrow distance from the centre as a fraction of the ring radius: in the gap between the hub's gold ornaments and the slices.
    private const float PointerRadius = 0.64f;
    private static Sprite pointerSprite;

    private void CreatePointer()
    {
        CaptureRing();
        if (!ringCaptured || buttons[0] == null) return;
        var go = new GameObject("StickPointer", typeof(RectTransform));
        pointer = (RectTransform)go.transform;
        pointer.SetParent(buttons[0].transform.parent, false);
        pointer.anchorMin = pointer.anchorMax = ((RectTransform)buttons[0].transform).anchorMin;
        pointer.pivot = new Vector2(0.5f, 0.5f);
        float size = Mathf.Clamp(ringRadius * 0.2f, 24f, 64f);
        pointer.sizeDelta = new Vector2(size, size);
        var image = go.AddComponent<Image>();
        image.sprite = PointerSprite();
        // White with a dark rim: a gold arrow vanished against the wheel's gold art.
        image.color = Color.white;
        image.raycastTarget = false;
        var rim = go.AddComponent<Outline>();
        rim.effectColor = new Color(0.1f, 0.06f, 0.02f, 0.9f);
        rim.effectDistance = new Vector2(3f, -3f);
        go.SetActive(false);
    }

    /// <summary>A soft-edged upward triangle, drawn once (no art asset needed).</summary>
    private static Sprite PointerSprite()
    {
        if (pointerSprite != null) return pointerSprite;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            float v = (y + 0.5f) / n;            // 0 bottom (base) .. 1 top (tip)
            float halfWidth = 0.5f * (1f - v);    // triangle narrows to the tip
            for (int x = 0; x < n; x++)
            {
                float u = Mathf.Abs((x + 0.5f) / n - 0.5f);
                float edge = Mathf.Min(halfWidth - u, v - 0.05f, 0.97f - v) * n;
                byte a = (byte)(Mathf.Clamp01(edge / 1.5f) * 255f);
                px[y * n + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        pointerSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return pointerSprite;
    }
}
