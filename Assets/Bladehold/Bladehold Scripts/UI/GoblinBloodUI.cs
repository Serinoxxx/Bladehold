using TMPro;
using UnityEngine;

public class GoblinBloodUI : MonoBehaviour
{
    public TMP_Text label;
    private bool anyError = false;

    private void Start()
    {
        if (label == null) label = GetComponent<TMP_Text>();
        if (label == null) return;

        RunSession.OnGoblinBloodChanged -= UpdateLabel;
        RunSession.OnGoblinBloodChanged += UpdateLabel;

        SaveData data = SaveSystem.Load();
        UpdateLabel(data != null ? data.goblinBlood : 0);
    }

    private void OnDestroy()
    {
        RunSession.OnGoblinBloodChanged -= UpdateLabel;
    }

    private void UpdateLabel(int amount)
    {
        if (label != null) label.text = amount.ToString();
    }
}
