using UnityEngine;
using UnityEngine.UI;

/// <summary>An enemy spawn point (or a cluster of close ones) on the minimap; pulses while a wave is spawning.</summary>
public class MinimapSpawnMarker : MinimapMarker
{
    [SerializeField] private Image icon;
    [Tooltip("Ring that pulses outward while enemies are spawning.")]
    [SerializeField] private Image pulse;

    private Vector3 position;
    private int pointCount = 1;

    public override Vector3 WorldPosition => position;

    public void Bind(Vector3 worldPosition, int points)
    {
        position = worldPosition;
        pointCount = Mathf.Max(1, points);
    }

    private void Start()
    {
        if (icon == null) Debug.LogError($"[MinimapSpawnMarker] {name}: icon is not assigned.", this);
    }

    public override void Refresh(MinimapConfigSO config)
    {
        if (pulse == null) return;
        bool active = SurvivorsSpawner.Instance != null && SurvivorsSpawner.Instance.AliveCount > 0
                      && GameLoopManager.Instance != null && !GameLoopManager.Instance.IsPrepPhase;
        pulse.enabled = active;
        if (!active) return;
        float t = Mathf.Repeat(Time.unscaledTime * 0.8f, 1f);
        pulse.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.8f, t);
        Color c = pulse.color;
        c.a = 1f - t;
        pulse.color = c;
    }

    public override void GetTooltip(out string title, out string body)
    {
        title = Loc.Get("minimap.spawn", "Enemy Spawn");
        body = pointCount > 1
            ? string.Format(Loc.Get("minimap.spawn_body_many", "{0} spawn points. The arrows show the route the horde will take to the gate."), pointCount)
            : Loc.Get("minimap.spawn_body", "The arrows show the route the horde will take to the gate.");
    }
}
