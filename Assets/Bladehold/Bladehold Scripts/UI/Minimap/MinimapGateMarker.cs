using UnityEngine;
using UnityEngine.UI;

/// <summary>The main gate on the minimap: its icon with an HP ring that flashes while it's under attack.</summary>
public class MinimapGateMarker : MinimapMarker
{
    [SerializeField] private Image icon;
    [Tooltip("Radial fill showing gate HP.")]
    [SerializeField] private Image healthRing;

    private Gate gate;
    private float lastHealth = -1f;
    private float lastHitTime = -999f;

    public override Vector3 WorldPosition => gate != null ? gate.TargetPosition : transform.position;

    public void Bind(Gate target)
    {
        gate = target;
    }

    private void Start()
    {
        if (icon == null) Debug.LogError($"[MinimapGateMarker] {name}: icon is not assigned.", this);
        if (healthRing == null) Debug.LogError($"[MinimapGateMarker] {name}: healthRing is not assigned.", this);
    }

    public override void Refresh(MinimapConfigSO config)
    {
        if (gate == null || config == null || healthRing == null || icon == null) return;
        Health h = gate.Health;
        float frac = h != null && h.MaxHealth > 0f ? Mathf.Clamp01(h.CurrentHealth / h.MaxHealth) : 0f;
        if (h != null)
        {
            if (lastHealth >= 0f && h.CurrentHealth < lastHealth) lastHitTime = Time.unscaledTime;
            lastHealth = h.CurrentHealth;
        }
        healthRing.fillAmount = frac;
        Color c = config.WallColor(frac);
        float t = Time.unscaledTime - lastHitTime;
        if (t < config.hitFlashSeconds && Mathf.Repeat(t * 6f, 1f) < 0.5f) c = config.hitFlash;
        healthRing.color = c;
        icon.color = gate.IsDestroyed ? new Color(0.5f, 0.5f, 0.5f, 0.8f) : Color.white;
    }

    public override void GetTooltip(out string title, out string body)
    {
        title = Loc.Get("minimap.gate", "Main Gate");
        Health h = gate != null ? gate.Health : null;
        if (h == null || gate.IsDestroyed)
        {
            body = Loc.Get("minimap.gate_fallen", "The gate has fallen.");
            return;
        }
        body = string.Format(Loc.Get("minimap.gate_body", "{0} / {1} HP. If it falls, the run is over."),
            Mathf.CeilToInt(h.CurrentHealth), Mathf.CeilToInt(h.MaxHealth));
    }
}
