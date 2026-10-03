using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One row of the expanded minimap's legend, filled from <see cref="MinimapConfigSO.legend" />.</summary>
public class MinimapLegendRow : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;

    public void Bind(MinimapConfigSO.LegendEntry entry)
    {
        if (icon == null || label == null)
        {
            Debug.LogError($"[MinimapLegendRow] {name}: icon/label are not assigned.", this);
            return;
        }
        icon.sprite = entry.icon;
        icon.color = entry.color;
        icon.enabled = entry.icon != null;
        icon.rectTransform.sizeDelta = new Vector2(entry.iconSize, entry.iconSize);
        label.text = Loc.Get(entry.locKey, entry.english);
    }
}
