using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DraftUpgradeDefinition
{
    public string id;
    public string displayName;
    public DraftCategory category;
    public string weapon;       // sword, axe, bow, throwing_axe, or empty
    public string element;      // fire, lightning, ice, or empty
    public bool isUltimate;
    public int maxLevel = 1;
    public string description;
    public string upgradeText;
    public List<SkillEffect> effects = new List<SkillEffect>();
    public string iconName;
    
    [Header("Elemental Synergy System")]
    public string targetSlot;
    public bool isDuo;
    public List<string> prerequisiteElements = new List<string>();

    public string GetDescriptionForLevel(int currentLevel)
    {
        if (currentLevel > 0 && !string.IsNullOrEmpty(upgradeText))
        {
            return upgradeText;
        }
        return description;
    }
}
