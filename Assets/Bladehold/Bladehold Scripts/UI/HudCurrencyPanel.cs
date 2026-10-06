using UnityEngine;

/// <summary>
///     The HUD's currency panel (gold, supply, Goblin Blood, metal, crystals) on the right edge. It sits
///     under the corner minimap in scenes that have one and moves up into the corner in scenes that don't
///     (Meta Area, Rest Area), so there's never an empty gap above it. The values come from each entry's
///     own view (<see cref="CoinUI" />, <see cref="SupplyUI" />, <see cref="GoblinBloodUI" />,
///     <see cref="OrcishMetalUI" />, <c>CrystalCounterUI</c>); this only places the panel.
/// </summary>
public class HudCurrencyPanel : MonoBehaviour
{
    [Tooltip("The minimap's corner frame. The panel docks under it while it's showing. The minimap hides it in scenes without a map.")]
    [SerializeField] private GameObject minimapFrame;
    [Tooltip("Anchored Y while docked under the minimap (its top + height + a gap).")]
    [SerializeField] private float underMinimapY = -668f;
    [Tooltip("Anchored Y with no minimap: the top-right corner.")]
    [SerializeField] private float cornerY = -44f;

    private RectTransform rect;
    private bool anyError;

    private void Awake()
    {
        rect = (RectTransform)transform;
    }

    private void Start()
    {
        if (minimapFrame == null)
        {
            Debug.LogError($"[HudCurrencyPanel] {name}: minimapFrame is not assigned.", this);
            anyError = true;
        }
    }

    private void LateUpdate()
    {
        if (anyError) return;
        // While the map is expanded the frame stays active (it moves to the centre), so the panel keeps its place.
        float y = minimapFrame.activeInHierarchy ? underMinimapY : cornerY;
        Vector2 pos = rect.anchoredPosition;
        if (!Mathf.Approximately(pos.y, y)) rect.anchoredPosition = new Vector2(pos.x, y);
    }
}
