using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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

    private const string CursorOwner = "FishingTally";

    private bool hasConsumedBuffFishThisVisit = false;

    private void Awake()
    {
        if (modalPanel != null) modalPanel.SetActive(false);
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(HandleContinueClicked);
        }
    }

    private void Start()
    {
        if (modalPanel == null) Debug.LogError("[FishingTallyUI] modalPanel is not assigned.", this);
        if (continueButton == null) Debug.LogError("[FishingTallyUI] continueButton is not assigned: the pond can't be left.", this);
        if (buffFishButtonContainer == null) Debug.LogError("[FishingTallyUI] buffFishButtonContainer is not assigned.", this);
        if (buffFishButtonPrefab == null) Debug.LogError("[FishingTallyUI] buffFishButtonPrefab is not assigned: buff fish can't be eaten.", this);
    }

    private void OnDestroy()
    {
        CursorLockManager.SetUnlock(CursorOwner, false);
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
        CursorLockManager.SetUnlock(CursorOwner, true);

        if (totalFishText != null) totalFishText.text = $"Total Fish Caught: {totalFish}";
        if (goldRewardText != null) goldRewardText.text = $"+{gold} Gold";
        if (bloodRewardText != null) bloodRewardText.text = $"+{blood} Goblin Blood";
        if (metalRewardText != null) metalRewardText.text = $"+{metal} Orcish Metal";
        if (diamondBonesRewardText != null) diamondBonesRewardText.text = $"+{diamondBones} Diamond Fish Bones";

        SetupBuffFishFeast(killedBuffs);

        // Gamepad: land on the first buff fish if there's a choice, else on Continue.
        if (EventSystem.current != null)
        {
            Button first = buffFishButtonContainer != null ? buffFishButtonContainer.GetComponentInChildren<Button>() : null;
            GameObject target = first != null ? first.gameObject : continueButton != null ? continueButton.gameObject : null;
            EventSystem.current.SetSelectedGameObject(target);
        }
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

        // Populate buff fish choice buttons (one prefab per fish; logged in Start if unwired)
        if (buffFishButtonContainer != null && buffFishButtonPrefab != null)
        {
            foreach (Transform child in buffFishButtonContainer)
            {
                Destroy(child.gameObject);
            }

            foreach (BuffFishType buff in killedBuffs)
            {
                GameObject btnObj = Instantiate(buffFishButtonPrefab, buffFishButtonContainer);

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

        if (!RunSession.TryConsumeBuffFish(type)) return;

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

        if (EventSystem.current != null && continueButton != null)
        {
            EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
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
        if (continueButton != null) continueButton.interactable = false;
        CursorLockManager.SetUnlock(CursorOwner, false);

        if (FishingManager.Instance != null)
        {
            FishingManager.Instance.CommitRewardsAndReturnToCampaign();
        }
    }
}
