using TMPro;
using UnityEngine;

public class GoblinBloodUI : MonoBehaviour
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
            Debug.LogError("[GoblinBloodUI] No TMP_Text label assigned or found in children!");
            return;
        }

        RunSession.OnGoblinBloodChanged -= UpdateLabel;
        RunSession.OnGoblinBloodChanged += UpdateLabel;

        Refresh();
    }

    private void OnDestroy()
    {
        RunSession.OnGoblinBloodChanged -= UpdateLabel;
    }

    public void Refresh()
    {
        SaveData data = SaveSystem.Load();
        UpdateLabel(data != null ? data.goblinBlood : 0);
    }

    private void UpdateLabel(int amount)
    {
        if (label != null) label.text = amount.ToString();
    }
}
