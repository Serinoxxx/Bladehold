using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Radial / Circular build wheel UI for constructing battlefield defenses on TowerPlots.
///     Displays 6 defense options with costs in Supply, description, and affordability state.
/// </summary>
public class BuildWheelUI : MonoBehaviour
{
    public static BuildWheelUI Instance { get; private set; }

    [System.Serializable]
    public class DefenseOption
    {
        public FortDefenseType defenseType;
        public string displayName;
        public int supplyCost;
        [TextArea(2, 3)] public string description;
        public Sprite icon;
    }

    [Header("UI References")]
    [SerializeField] private GameObject wheelPanel;
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private TMP_Text supplyLabel;
    [SerializeField] private TMP_Text descriptionLabel;
    [SerializeField] private Button closeButton;

    [Header("Defense Slices / Buttons")]
    [SerializeField] private List<Button> sliceButtons = new List<Button>();
    [SerializeField] private List<DefenseOption> defenseOptions = new List<DefenseOption>
    {
        new DefenseOption
        {
            defenseType = FortDefenseType.ArrowSlits,
            displayName = "Arrow Tower",
            supplyCost = 30,
            description = "Rapidly fires light piercing arrows at the closest incoming enemy."
        },
        new DefenseOption
        {
            defenseType = FortDefenseType.Catapult,
            displayName = "Catapult",
            supplyCost = 45,
            description = "Lobs fiery boulders that detonate with massive area splash fire damage."
        },
        new DefenseOption
        {
            defenseType = FortDefenseType.Ballista,
            displayName = "Ballista",
            supplyCost = 50,
            description = "Punches through multiple foes in a straight line with heavy damage and knockback."
        },
        new DefenseOption
        {
            defenseType = FortDefenseType.NetThrower,
            displayName = "Net Thrower",
            supplyCost = 35,
            description = "Launches heavy rope nets that immobilize and root groups of enemies in place."
        },
        new DefenseOption
        {
            defenseType = FortDefenseType.Spikes,
            displayName = "Spike Trap",
            supplyCost = 25,
            description = "Concealed ground spikes that impale passing enemies for devastating damage."
        },
        new DefenseOption
        {
            defenseType = FortDefenseType.BurningOil,
            displayName = "Oil Vat",
            supplyCost = 30,
            description = "Spills a pool of boiling oil that slows foes by 50% and scorches them."
        }
    };

    private TowerPlot activePlot;
    private bool isOpen = false;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (wheelPanel != null) wheelPanel.SetActive(false);

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }
    }

    private void Start()
    {
        SetupButtons();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        CursorLockManager.SetUnlock("BuildWheel", false);
    }

    private void Update()
    {
        if (!isOpen) return;

        // Cancel on Escape or B
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
    }

    public void Open(TowerPlot plot)
    {
        activePlot = plot;
        isOpen = true;

        if (wheelPanel != null) wheelPanel.SetActive(true);

        CursorLockManager.SetUnlock("BuildWheel", true);

        RefreshUI();
    }

    public void Close()
    {
        isOpen = false;
        activePlot = null;

        if (wheelPanel != null) wheelPanel.SetActive(false);

        CursorLockManager.SetUnlock("BuildWheel", false);
    }

    public void RefreshUI()
    {
        int playerSupply = RunSession.InRunSupply;

        if (supplyLabel != null)
        {
            supplyLabel.text = $"Supply: <color=#FFD700>{playerSupply}</color>";
        }

        if (headerText != null)
        {
            headerText.text = "SELECT DEFENCE TO CONSTRUCT";
        }

        if (descriptionLabel != null)
        {
            descriptionLabel.text = "Choose a structure to build at this plot.";
        }

        for (int i = 0; i < defenseOptions.Count; i++)
        {
            if (i >= sliceButtons.Count) break;

            Button btn = sliceButtons[i];
            if (btn == null) continue;

            DefenseOption opt = defenseOptions[i];
            bool canAfford = playerSupply >= opt.supplyCost;

            btn.interactable = canAfford;

            TMP_Text txt = btn.GetComponentInChildren<TMP_Text>();
            if (txt != null)
            {
                string costColor = canAfford ? "#FFD700" : "#FF4444";
                txt.text = $"{opt.displayName}\n<color={costColor}>{opt.supplyCost} Supply</color>";
            }

            int index = i;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnSelectSlice(index));
        }
    }

    private void SetupButtons()
    {
        for (int i = 0; i < defenseOptions.Count && i < sliceButtons.Count; i++)
        {
            int index = i;
            Button btn = sliceButtons[i];
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnSelectSlice(index));
            }
        }
    }

    public void OnSelectSlice(int index)
    {
        if (!isOpen || activePlot == null) return;
        if (index < 0 || index >= defenseOptions.Count) return;

        DefenseOption opt = defenseOptions[index];
        if (RunSession.InRunSupply < opt.supplyCost)
        {
            Debug.Log($"[BuildWheelUI] Cannot afford {opt.displayName} (Cost: {opt.supplyCost}, Supply: {RunSession.InRunSupply}).");
            return;
        }

        if (RunSession.TrySpendInRunSupply(opt.supplyCost))
        {
            TowerPlot targetPlot = activePlot;
            Close();

            targetPlot.BuildDefense(opt.defenseType);
            Debug.Log($"[BuildWheelUI] Constructed {opt.displayName} on plot {targetPlot.PlotIndex}!");
        }
    }

    public void OnHoverSlice(int index)
    {
        if (index >= 0 && index < defenseOptions.Count && descriptionLabel != null)
        {
            descriptionLabel.text = defenseOptions[index].description;
        }
    }
}
