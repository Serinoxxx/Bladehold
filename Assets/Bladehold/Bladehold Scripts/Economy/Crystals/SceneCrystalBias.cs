using UnityEngine;

/// <summary>
///     Which elemental crystals a scene favours (plan 17): Desert leans Fire, Alpine (Outer Gate) leans
///     Ice, Graveyard / enchanted forest leans Storm, grassy scenes are an even mix. Every random crystal
///     roll (<see cref="Roll" />) reads it. No component in the scene means an even mix, the same
///     "absent = default" convention as <see cref="SceneAbilityRules" />. The defense-scene generator
///     copies the weights from the biome palette.
/// </summary>
public class SceneCrystalBias : MonoBehaviour
{
    private static SceneCrystalBias instance;

    [Min(0f)] [SerializeField] private float fireWeight = 1f;
    [Min(0f)] [SerializeField] private float iceWeight = 1f;
    [Min(0f)] [SerializeField] private float lightningWeight = 1f;

    private void Awake()
    {
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public void SetWeights(float fire, float ice, float lightning)
    {
        fireWeight = Mathf.Max(0f, fire);
        iceWeight = Mathf.Max(0f, ice);
        lightningWeight = Mathf.Max(0f, lightning);
    }

    /// <summary>A random element, weighted by this scene's bias (even when there's no bias component).</summary>
    public static StructureElement Roll()
    {
        float fire = instance != null ? instance.fireWeight : 1f;
        float ice = instance != null ? instance.iceWeight : 1f;
        float lightning = instance != null ? instance.lightningWeight : 1f;
        float total = fire + ice + lightning;
        if (total <= 0f) return StructureElements.All[Random.Range(0, 3)];

        float r = Random.value * total;
        if (r < fire) return StructureElement.Fire;
        if (r < fire + ice) return StructureElement.Ice;
        return StructureElement.Lightning;
    }
}
