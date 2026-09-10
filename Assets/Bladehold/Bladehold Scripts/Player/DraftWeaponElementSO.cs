using UnityEngine;

[CreateAssetMenu(fileName = "DraftWeaponElementSO", menuName = "Scriptable Objects/DraftWeaponElementSO")]
public class DraftWeaponElementSO : ScriptableObject
{
    [Header("Fire")]
    [Min(0f)] public float explosionDamagePercent = 0.35f;
    [Min(0f)] public float explosionRadius = 4f;
    public GameObject explosionVfxPrefab;
    [Min(0.1f)] public float explosionVfxLifetime = 5f;

    [Header("Lightning")]
    [Min(0)] public int lightningBounces = 2;
    [Min(0f)] public float lightningDamagePercent = 0.5f;
}
