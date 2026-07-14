using UnityEngine;

[CreateAssetMenu(fileName = "ArmorPlatingSO", menuName = "Scriptable Objects/ArmorPlatingSO")]
public class ArmorPlatingSO : ScriptableObject
{
    [Tooltip("Hits below this damage value count as 'light' and are scaled down. Heavy/charged hits at or above it pass through untouched — the counter is 'charge your swings'.")]
    public float lightHitThreshold = 15f;

    [Tooltip("Multiplier applied to light hits (0.4 = they deal 40% damage).")]
    [Range(0f, 1f)] public float lightHitMultiplier = 0.4f;
}
