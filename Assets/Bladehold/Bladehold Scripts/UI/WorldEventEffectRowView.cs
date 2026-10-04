using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     One boon or peril row on the world-event banner (<c>WorldEventEffectRow.prefab</c>): a coloured icon chip
///     and a short line of text. Display only; <see cref="WorldEventBannerUI" /> instantiates and binds one per
///     <see cref="WorldEventEffectLine" />.
/// </summary>
public class WorldEventEffectRowView : MonoBehaviour
{
    [SerializeField] private Image chip;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Color boonColor = new Color(0.30f, 0.72f, 0.36f, 1f);
    [SerializeField] private Color perilColor = new Color(0.82f, 0.22f, 0.18f, 1f);

    public void Bind(Sprite iconSprite, string text, bool positive)
    {
        if (chip != null) chip.color = positive ? boonColor : perilColor;
        if (icon != null)
        {
            icon.sprite = iconSprite;
            icon.enabled = iconSprite != null;
        }
        if (label != null) label.text = text;
    }
}
