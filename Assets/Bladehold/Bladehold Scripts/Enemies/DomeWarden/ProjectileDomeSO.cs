using UnityEngine;

/// <summary>Tunables for <see cref="ProjectileDome" />.</summary>
[CreateAssetMenu(fileName = "ProjectileDomeSO", menuName = "Scriptable Objects/Enemies/Projectile Dome")]
public class ProjectileDomeSO : ScriptableObject
{
    [Tooltip("Dome radius in metres. Enemies inside are shielded from projectiles.")]
    public float radius = 5f;

    [Tooltip("Seconds between checks for enemies walking into or out of the dome.")]
    public float rescanInterval = 0.25f;

    [Header("Dome health")]
    [Tooltip("Damage the dome soaks up from blocked projectiles before it breaks.")]
    public float domeHealth = 150f;
    [Tooltip("Seconds a broken dome stays down before the Warden raises it again at full health.")]
    public float rebuildCooldown = 8f;

    [Header("Visuals")]
    [Tooltip("Authored dome visual: a unit-diameter sphere, scaled to the radius at runtime. Any colliders on it are switched off so it never catches hits itself.")]
    public GameObject domeVisualPrefab;
    [Tooltip("Height of the dome's centre above the caster's feet. 0 = a half-buried dome. Boulders burst where their arc enters this sphere.")]
    public float visualHeightOffset = 0f;
    [Tooltip("Colour of the damage number that pops where a projectile hits the dome.")]
    public Color hitNumberColor = new Color(0.72f, 0.35f, 1f);
}
