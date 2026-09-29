using UnityEngine;

/// <summary>Tunables for <see cref="ProjectileDome" />.</summary>
[CreateAssetMenu(fileName = "ProjectileDomeSO", menuName = "Scriptable Objects/Enemies/Projectile Dome")]
public class ProjectileDomeSO : ScriptableObject
{
    [Tooltip("Dome radius in metres (flat distance from the caster). Enemies inside are shielded from projectiles.")]
    public float radius = 5f;

    [Tooltip("Seconds between checks for enemies walking into or out of the dome.")]
    public float rescanInterval = 0.25f;

    [Tooltip("Authored dome visual: a unit-diameter sphere, scaled to the radius at runtime. Any colliders on it are switched off so it never catches hits itself.")]
    public GameObject domeVisualPrefab;

    [Tooltip("Height of the visual's centre above the caster's feet. 0 = a half-buried dome.")]
    public float visualHeightOffset = 0f;
}
