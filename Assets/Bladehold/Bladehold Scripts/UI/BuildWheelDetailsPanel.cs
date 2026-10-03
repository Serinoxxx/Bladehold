using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     The build / upgrade wheel's details box, beside the wheel. Shows the hovered (or pad-selected)
///     slice's icon, name, a cost breakdown against what the player holds, the description and a stats
///     line. With nothing hovered it shows a short prompt instead. <see cref="BuildWheelUI" /> drives it.
/// </summary>
public class BuildWheelDetailsPanel : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [Tooltip("Cost breakdown, one currency per line. Hidden when empty.")]
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text bodyText;
    [Tooltip("Range / blind spot for towers, the blocked reason for upgrades. Hidden when empty.")]
    [SerializeField] private TMP_Text statsText;
    [Tooltip("Divider above the cost block, hidden together with it.")]
    [SerializeField] private GameObject costDivider;

    private bool anyError;

    private void Start()
    {
        if (titleText == null || costText == null || bodyText == null || statsText == null)
        {
            Debug.LogError($"[BuildWheelDetailsPanel] {name}: a text reference is not assigned.", this);
            anyError = true;
        }
    }

    /// <summary>Nothing hovered: a heading and a one-line prompt.</summary>
    public void ShowPrompt(string title, string prompt)
    {
        Show(title, null, null, prompt, null);
    }

    public void Show(string title, Sprite icon, string costLines, string body, string stats)
    {
        if (anyError) return;

        titleText.text = title;
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.gameObject.SetActive(icon != null);
        }

        bool hasCost = !string.IsNullOrEmpty(costLines);
        costText.text = costLines;
        costText.gameObject.SetActive(hasCost);
        if (costDivider != null) costDivider.SetActive(hasCost);

        bodyText.text = body;

        bool hasStats = !string.IsNullOrEmpty(stats);
        statsText.text = stats;
        statsText.gameObject.SetActive(hasStats);
    }

    /// <summary>"30 Supply   (you have 20)": gold when affordable, red with the shortfall otherwise.</summary>
    public static string CostLine(int cost, int have, string currency, string affordableHex)
    {
        if (have >= cost) return $"<color={affordableHex}>{cost} {currency}</color>";
        string youHave = Loc.Get("buildwheel.you_have", "you have");
        return $"<color=#FF6A5A>{cost} {currency}</color>   <color=#B8AE9C><size=80%>({youHave} {have})</size></color>";
    }
}
