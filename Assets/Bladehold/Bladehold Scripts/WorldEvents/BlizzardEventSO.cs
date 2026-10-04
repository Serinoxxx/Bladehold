using UnityEngine;

/// <summary>Blizzard tunables (alpine / ice biome). Read by <see cref="BlizzardEvent" />.</summary>
[CreateAssetMenu(fileName = "BlizzardEvent", menuName = "Scriptable Objects/World Events/Blizzard")]
public class BlizzardEventSO : WorldEventSO
{
    [Header("Gusts")]
    [Tooltip("Seconds between gusts (random between x and y).")]
    public Vector2 gustInterval = new Vector2(6f, 9f);
    [Tooltip("Seconds of rising howl and streaks before a gust hits: the cue to brace or reposition.")]
    public float gustWarningSeconds = 1.5f;
    [Tooltip("Seconds a gust pushes.")]
    public float gustSeconds = 2.2f;
    [Tooltip("Metres per second a gust shoves the player (walk speed is ~5, so you can still fight it).")]
    public float playerShoveSpeed = 3.5f;
    [Tooltip("Metres per second a gust shoves enemies along the NavMesh.")]
    public float enemyShoveSpeed = 5f;
    [Tooltip("Only enemies within this distance of the player are shoved and chilled (keeps big hordes cheap).")]
    public float gustRadius = 45f;
    [Tooltip("Each gust swings the wind up to this many degrees from the last one.")]
    public float directionWobbleDegrees = 40f;
    [Tooltip("Gusts chill every enemy they shove (Ice status: slow, builds to a freeze with Deep Freeze).")]
    public bool gustsChillEnemies = true;
}
