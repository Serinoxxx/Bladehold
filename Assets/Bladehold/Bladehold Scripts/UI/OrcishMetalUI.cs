using TMPro;
using UnityEngine;

public class OrcishMetalUI : MonoBehaviour
{
    public TMP_Text label;
    private bool anyError = false;

    private void OnValidate()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>();
    }

    private void Awake()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>();
    }

    private void Start()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>();
        if (label == null)
        {
            Debug.LogError("[OrcishMetalUI] No TMP_Text label assigned or found in children!");
            return;
        }

        RunSession.OnOrcishMetalChanged -= UpdateLabel;
        RunSession.OnOrcishMetalChanged += UpdateLabel;

        Refresh();
    }

    private void OnDestroy()
    {
        RunSession.OnOrcishMetalChanged -= UpdateLabel;
    }

    public void Refresh()
    {
        SaveData data = SaveSystem.Load();
        UpdateLabel(data != null ? data.orcishMetal : 0);
    }

    private void UpdateLabel(int amount)
    {
        if (label != null) label.text = amount.ToString();
    }
}
