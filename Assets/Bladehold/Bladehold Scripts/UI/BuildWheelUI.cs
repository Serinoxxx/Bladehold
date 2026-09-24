using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
///     Radial / Circular build wheel UI for constructing battlefield defenses on TowerPlots.
///     Displays 6 defense options with costs in Supply, description, and affordability state.
/// </summary>
public class BuildWheelUI : MonoBehaviour
{
    private static BuildWheelUI instance;
    public static BuildWheelUI Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<BuildWheelUI>(FindObjectsInactive.Include);
            }
            return instance;
        }
        private set => instance = value;
    }

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

    [Header("Build Feedback")]
    [SerializeField] private DamageNumbersPro.DamageNumber supplyPopupPrefab;
    [SerializeField] private AudioClip buildSfx;
    [SerializeField] private GameObject buildVfxPrefab;

    [Header("Defense Slices / Buttons")]
    [SerializeField] private GameObject sliceButtonPrefab;
    [SerializeField] private List<BuildWheelButton> wheelButtons = new List<BuildWheelButton>();
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
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }

        if (!isOpen)
        {
            if (wheelPanel != null && wheelPanel != gameObject) wheelPanel.SetActive(false);
            gameObject.SetActive(false);
        }

        ResolveFallbacks();
    }

    private void ResolveFallbacks()
    {
#if UNITY_EDITOR
        if (supplyPopupPrefab == null)
        {
            var goldGo = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Third Party/DamageNumbersPro/Demo/Prefabs/3D/Gold.prefab");
            if (goldGo != null) supplyPopupPrefab = goldGo.GetComponent<DamageNumbersPro.DamageNumber>();
        }
        if (buildSfx == null)
        {
            buildSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Bladehold Audio/SFX/Impacts/HAMMER_Hit_Wood_Shield_stereo.wav");
        }
        if (buildVfxPrefab == null)
        {
            buildVfxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonParticleFX/Prefabs/FX_Impact_Wood_01.prefab");
        }
        if (sliceButtonPrefab == null)
        {
            sliceButtonPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bladehold/Bladehold Prefabs/UI/BuildWheelSliceButton.prefab");
        }
#endif
    }

    private void Start()
    {
        SetupButtons();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        CursorLockManager.SetUnlock("BuildWheel", false);
        PauseMenuController.Instance?.SetToggleEnabled(true);
    }

    private void Update()
    {
        if (!isOpen) return;

        // Cancel on Escape or B (Keyboard or Gamepad)
        bool cancelPressed = false;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.bKey.wasPressedThisFrame))
        {
            cancelPressed = true;
        }

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null && gamepad.buttonEast.wasPressedThisFrame)
        {
            cancelPressed = true;
        }

        if (cancelPressed)
        {
            Close();
        }
    }

    public void Open(TowerPlot plot)
    {
        activePlot = plot;
        isOpen = true;

        gameObject.SetActive(true);
        if (wheelPanel != null && wheelPanel != gameObject) wheelPanel.SetActive(true);

        CursorLockManager.SetUnlock("BuildWheel", true);
        PauseMenuController.Instance?.SetToggleEnabled(false);

        SetupButtons();
        RefreshUI();
    }

    public void Close()
    {
        isOpen = false;
        activePlot = null;

        if (wheelPanel != null && wheelPanel != gameObject) wheelPanel.SetActive(false);
        gameObject.SetActive(false);

        CursorLockManager.SetUnlock("BuildWheel", false);
        PauseMenuController.Instance?.SetToggleEnabled(true);
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

        // 1. If dedicated BuildWheelButtons are present, configure them
        if (wheelButtons != null && wheelButtons.Count > 0)
        {
            for (int i = 0; i < defenseOptions.Count && i < wheelButtons.Count; i++)
            {
                BuildWheelButton btn = wheelButtons[i];
                if (btn == null) continue;

                DefenseOption opt = defenseOptions[i];
                bool canAfford = playerSupply >= opt.supplyCost;
                int index = i;

                btn.Setup(
                    opt.displayName,
                    opt.supplyCost,
                    opt.icon,
                    canAfford,
                    () => OnSelectSlice(index),
                    () => OnHoverSlice(index),
                    () => OnUnhoverSlice()
                );
            }
        }

        // 2. Also refresh legacy sliceButtons if wired
        if (sliceButtons != null && sliceButtons.Count > 0)
        {
            for (int i = 0; i < defenseOptions.Count && i < sliceButtons.Count; i++)
            {
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
    }

    private void SetupButtons()
    {
        if (wheelButtons != null && wheelButtons.Count > 0)
        {
            for (int i = 0; i < defenseOptions.Count && i < wheelButtons.Count; i++)
            {
                int index = i;
                BuildWheelButton btn = wheelButtons[i];
                if (btn != null)
                {
                    DefenseOption opt = defenseOptions[i];
                    bool canAfford = RunSession.InRunSupply >= opt.supplyCost;
                    btn.Setup(
                        opt.displayName,
                        opt.supplyCost,
                        opt.icon,
                        canAfford,
                        () => OnSelectSlice(index),
                        () => OnHoverSlice(index),
                        () => OnUnhoverSlice()
                    );
                }
            }
        }

        if (sliceButtons != null && sliceButtons.Count > 0)
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
    }

    public void OnUnhoverSlice()
    {
        if (descriptionLabel != null)
        {
            descriptionLabel.text = "Choose a defense structure to protect the gates.";
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
            Vector3 plotPos = targetPlot != null ? targetPlot.BuildPosition : transform.position;
            Close();

            if (buildSfx != null)
            {
                AudioSource.PlayClipAtPoint(buildSfx, plotPos, 1.0f);
            }

            if (buildVfxPrefab != null)
            {
                GameObject vfx = Instantiate(buildVfxPrefab, plotPos + Vector3.up * 0.2f, Quaternion.identity);
                Destroy(vfx, 2.5f);
            }

            if (supplyPopupPrefab != null)
            {
                supplyPopupPrefab.Spawn(plotPos + Vector3.up * 1.8f, $"-{opt.supplyCost} Supply");
            }

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
