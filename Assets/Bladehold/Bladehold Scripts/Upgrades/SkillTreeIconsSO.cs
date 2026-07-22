using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillTreeIconsSO", menuName = "Scriptable Objects/SkillTreeIconsSO")]
public class SkillTreeIconsSO : ScriptableObject
{
    [Tooltip("Central list of icons for all skill trees.")]
    [SerializeField] private Sprite[] icons;

    [System.NonSerialized] private Dictionary<string, Sprite> iconsByName;

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
}
