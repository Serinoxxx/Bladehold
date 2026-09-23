using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Results tally modal at the conclusion of the 60-second Fishing Frenzy.
///     Displays fish caught, resources earned, and allows choosing 1 buff fish feast (max 3 per run).
/// </summary>
public class FishingTallyUI : MonoBehaviour
{
    [Header("Modal Panels")]
    [SerializeField] private GameObject modalPanel;
    [SerializeField] private GameObject buffFishSection;

    [Header("Tally Labels")]
    [SerializeField] private TMP_Text totalFishText;
    [SerializeField] private TMP_Text goldRewardText;
    [SerializeField] private TMP_Text bloodRewardText;
    [SerializeField] private TMP_Text metalRewardText;
    [SerializeField] private TMP_Text diamondBonesRewardText;

    [Header("Buff Fish Feast")]
    [SerializeField] private TMP_Text buffFishStatusText;
    [SerializeField] private Transform buffFishButtonContainer;
    [SerializeField] private GameObject buffFishButtonPrefab;

    [Header("Navigation")]
    [SerializeField] private Button continueButton;

    private bool hasConsumedBuffFishThisVisit = false;

    private void Awake()
    {
        if (modalPanel != null) modalPanel.SetActive(false);
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(HandleContinueClicked);
        }
    }

    public void OpenTally(
        int gold,
        int blood,
        int metal,
        int diamondBones,
        int totalFish,
        IReadOnlyCollection<BuffFishType> killedBuffs)
    {
        if (modalPanel != null) modalPanel.SetActive(true);

        if (totalFishText != null) totalFishText.text = $"Total Fish Caught: {totalFish}";
        if (goldRewardText != null) goldRewardText.text = $"+{gold} Gold";
        if (bloodRewardText != null) bloodRewardText.text = $"+{blood} Goblin Blood";
        if (metalRewardText != null) metalRewardText.text = $"+{metal} Orcish Metal";
        if (diamondBonesRewardText != null) diamondBonesRewardText.text = $"+{diamondBones} Diamond Fish Bones";

        SetupBuffFishFeast(killedBuffs);
    }

    private void SetupBuffFishFeast(IReadOnlyCollection<BuffFishType> killedBuffs)
    {
        hasConsumedBuffFishThisVisit = false;
        int runBuffCount = RunSession.ConsumedBuffFish.Count;

        if (runBuffCount >= 3)
        {
            if (buffFishStatusText != null) buffFishStatusText.text = "Buff Fish Eaten: 3/3 (Run Maximum Reached)";
            return;
        }

        if (killedBuffs == null || killedBuffs.Count == 0)
        {
            if (buffFishStatusText != null) buffFishStatusText.text = $"Buff Fish Eaten: {runBuffCount}/3 (No Buff Fish Caught This Session)";
            return;
        }

        if (buffFishStatusText != null)
        {
            buffFishStatusText.text = $"CHOOSE A BUFF FISH TO EAT ({runBuffCount}/3 Eaten This Run - Choose 1):";
        }

        // Populate buff fish choice buttons
        if (buffFishButtonContainer != null)
        {
            foreach (Transform child in buffFishButtonContainer)
            {
                Destroy(child.gameObject);
            }

            foreach (BuffFishType buff in killedBuffs)
            {
                GameObject btnObj;
                if (buffFishButtonPrefab != null)
                {
                    btnObj = Instantiate(buffFishButtonPrefab, buffFishButtonContainer);
                }
                else
                {
                    btnObj = new GameObject($"BuffButton_{buff}");
                    btnObj.transform.SetParent(buffFishButtonContainer, false);
                    btnObj.AddComponent<Image>().color = new Color(0.2f, 0.4f, 0.5f, 0.9f);
                    btnObj.AddComponent<Button>();
                    var txtGo = new GameObject("Text");
                    txtGo.transform.SetParent(btnObj.transform, false);
                    var t = txtGo.AddComponent<TextMeshProUGUI>();
                    t.fontSize = 18;
                    t.alignment = TextAlignmentOptions.Center;
                }

                TMP_Text btnText = btnObj.GetComponentInChildren<TMP_Text>();
                if (btnText != null)
                {
                    btnText.text = $"{buff} Fish: {GetBuffDescription(buff)}";
                }

                Button btn = btnObj.GetComponent<Button>();
                if (btn != null)
                {
                    BuffFishType captured = buff;
                    btn.onClick.AddListener(() => OnBuffFishSelected(captured));
                }
            }
        }
    }

    private void OnBuffFishSelected(BuffFishType type)
    {
        if (hasConsumedBuffFishThisVisit) return;
        hasConsumedBuffFishThisVisit = true;

        RunSession.TryConsumeBuffFish(type);

        if (buffFishStatusText != null)
        {
            buffFishStatusText.text = $"Feasted on {type} Fish! ({RunSession.ConsumedBuffFish.Count}/3 Eaten)";
        }

        if (buffFishButtonContainer != null)
        {
            foreach (Transform child in buffFishButtonContainer)
            {
                Button b = child.GetComponent<Button>();
                if (b != null) b.interactable = false;
            }
        }
    }

    public static string GetBuffDescription(BuffFishType type) => type switch
    {
        BuffFishType.Speedy => "+10% Move Speed",
        BuffFishType.Armored => "+10 Max HP",
        BuffFishType.Fire => "+10% Fire Damage",
        BuffFishType.Frost => "+10% Frost Damage",
        BuffFishType.Spark => "+10% Lightning Damage",
        BuffFishType.Savage => "+5% ALL Damage",
        _ => ""
    };

    private void HandleContinueClicked()
    {
        if (FishingManager.Instance != null)
        {
            FishingManager.Instance.CommitRewardsAndReturnToCampaign();
        }
    }
}
