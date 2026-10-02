using TMPro;
using UnityEngine;

/// <summary>
///     HUD counters for the three elemental crystals (plan 17), beside supply. Listens to
///     <see cref="RunSession.OnCrystalsChanged" />. Hidden while the player has none of any kind, so
///     scenes and runs that never touch crystals stay uncluttered.
/// </summary>
public class CrystalCounterUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI fireLabel;
    [SerializeField] private TextMeshProUGUI iceLabel;
    [SerializeField] private TextMeshProUGUI lightningLabel;
    [Tooltip("Shown only once the player owns at least one crystal.")]
    [SerializeField] private GameObject root;

    private void Start()
    {
        if (fireLabel == null || iceLabel == null || lightningLabel == null)
        {
            Debug.LogError($"[CrystalCounterUI] {name}: a crystal label is not assigned.", this);
        }
        RunSession.OnCrystalsChanged += HandleChanged;
        Refresh();
    }

    private void OnDestroy()
    {
        RunSession.OnCrystalsChanged -= HandleChanged;
    }

    private void HandleChanged(StructureElement element, int amount)
    {
        Refresh();
    }

    private void Refresh()
    {
        int fire = RunSession.GetCrystals(StructureElement.Fire);
        int ice = RunSession.GetCrystals(StructureElement.Ice);
        int storm = RunSession.GetCrystals(StructureElement.Lightning);
        if (fireLabel != null) fireLabel.text = fire.ToString();
        if (iceLabel != null) iceLabel.text = ice.ToString();
        if (lightningLabel != null) lightningLabel.text = storm.ToString();
        if (root != null) root.SetActive(fire + ice + storm > 0);
    }
}
