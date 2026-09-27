using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillTreeIconsSO", menuName = "Scriptable Objects/SkillTreeIconsSO")]
public class SkillTreeIconsSO : ScriptableObject
{
    [Tooltip("Central list of icons for all skill trees.")]
    [SerializeField] private Sprite[] icons;

    [Header("Elemental Icon Tints")]
    [Tooltip("Icon colour for Fire elemental draft cards (card UI and the acquired-skills sidebar).")]
    [SerializeField] private Color fireIconTint = new Color(1f, 0.55f, 0.1f, 1f);
    [Tooltip("Icon colour for Ice elemental draft cards.")]
    [SerializeField] private Color iceIconTint = new Color(0.55f, 0.85f, 1f, 1f);
    [Tooltip("Icon colour for Lightning elemental draft cards.")]
    [SerializeField] private Color lightningIconTint = new Color(0.65f, 0.35f, 1f, 1f);

    [System.NonSerialized] private Dictionary<string, Sprite> iconsByName;

    private void OnEnable()
    {
        iconsByName = null;
    }

    public void Reload()
    {
        iconsByName = null;
    }

    public Sprite GetIcon(string iconName)
    {
        if (string.IsNullOrEmpty(iconName))
        {
            return null;
        }

        if (iconsByName == null)
        {
            iconsByName = new Dictionary<string, Sprite>();
            if (icons != null)
            {
                foreach (Sprite sprite in icons)
                {
                    if (sprite != null)
                    {
                        iconsByName[sprite.name] = sprite;
                    }
                }
            }
        }

        return iconsByName.TryGetValue(iconName, out Sprite found) ? found : null;
    }

    /// <summary>
    ///     The icon tint for a draft card's element (<c>Fire</c>, <c>Ice</c>, <c>Lightning</c>, any case).
    ///     False for no element (weapon cards, duos), so the caller keeps its default icon colour.
    /// </summary>
    public bool TryGetElementTint(string element, out Color tint)
    {
        switch (element?.Trim().ToUpperInvariant())
        {
            case "FIRE": tint = fireIconTint; return true;
            case "ICE": tint = iceIconTint; return true;
            case "LIGHTNING": tint = lightningIconTint; return true;
            default: tint = Color.white; return false;
        }
    }
}
