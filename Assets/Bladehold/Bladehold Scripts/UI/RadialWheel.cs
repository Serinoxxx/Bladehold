using System.Collections.Generic;
using UnityEngine;

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
}
