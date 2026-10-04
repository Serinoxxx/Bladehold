using UnityEngine;

/// <summary>
///     A lingering ground hazard left by a world event (the Eruption's magma pool): ticks
///     <see cref="WorldEventHazards.HitArea" /> on the player and enemies standing in it, then sinks away.
///     A gameplay spawn, not feedback: the prefab carries its own looping visual.
/// </summary>
public class WorldHazardZone : MonoBehaviour
{
    [Tooltip("Seconds between damage ticks.")]
    [SerializeField] private float tickInterval = 0.5f;
    [Tooltip("Seconds the visual takes to shrink away at the end.")]
    [SerializeField] private float shrinkSeconds = 0.6f;

    private float radius;
    private float lifetime;
    private WorldHazardDamage tickDamage;
    private string statusId;
    private float age;
    private float tickTimer;
    private Vector3 baseScale;

    public void Initialize(float zoneRadius, float seconds, WorldHazardDamage damagePerTick, string status)
    {
        radius = zoneRadius;
        lifetime = seconds;
        tickDamage = damagePerTick;
        statusId = status;
        baseScale = transform.localScale;
        transform.localScale = new Vector3(baseScale.x * radius, baseScale.y, baseScale.z * radius);
        baseScale = transform.localScale;
    }

    private void Update()
    {
        age += Time.deltaTime;
        tickTimer += Time.deltaTime;
        if (tickTimer >= tickInterval && age < lifetime)
        {
            tickTimer = 0f;
            WorldEventHazards.HitArea(transform.position, radius, tickDamage, statusId);
        }

        float shrink = Mathf.Clamp01((age - lifetime) / Mathf.Max(0.05f, shrinkSeconds));
        if (shrink > 0f) transform.localScale = Vector3.Lerp(baseScale, new Vector3(0f, baseScale.y, 0f), shrink);
        if (shrink >= 1f) Destroy(gameObject);
    }
}
