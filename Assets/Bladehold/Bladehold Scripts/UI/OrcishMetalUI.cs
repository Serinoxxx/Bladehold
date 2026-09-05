using TMPro;
using UnityEngine;

public class OrcishMetalUI : MonoBehaviour
{
    public TMP_Text label;
    private bool anyError = false;

    private void Start()
    {
        if (label == null) label = GetComponent<TMP_Text>();
        if (label == null) return;

        RunSession.OnOrcishMetalChanged -= UpdateLabel;
        RunSession.OnOrcishMetalChanged += UpdateLabel;

        SaveData data = SaveSystem.Load();
        UpdateLabel(data != null ? data.orcishMetal : 0);
    }

    private void OnDestroy()
    {
        RunSession.OnOrcishMetalChanged -= UpdateLabel;
    }

    private void UpdateLabel(int amount)
    {
        if (label != null) label.text = amount.ToString();
    }
}
