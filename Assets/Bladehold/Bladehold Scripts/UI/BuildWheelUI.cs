using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
///     Radial / Circular build wheel UI for constructing battlefield defenses on TowerPlots.
///     Displays the defense options with costs in Supply, description, and affordability state.
///
///     It doubles as the plan-17 upgrade wheel (<see cref="OpenUpgrades" />): a tower or a wall's
///     crafting station hands it an <see cref="IUpgradeable" />, whose <see cref="UpgradeOption" />
///     list becomes the slices (supply and/or crystal prices, blocked reasons). Slices are cloned from
///     the first authored <see cref="BuildWheelButton" /> as needed and laid out round the authored
///     ring, so the option count is free. The wheel stays open after a purchase (so you can refill and
///     upgrade in one visit) and refreshes; Deconstruct closes it.
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

    [Header("Range Summary (hover line under the description)")]
    [Tooltip("Max range below this reads as Short; below Long reads as Medium; at or above Long reads as Long.")]
    [SerializeField] private float mediumRangeFrom = 12f;
    [SerializeField] private float longRangeFrom = 20f;
    [Tooltip("Blind spot below this reads as Small; below Large reads as Medium; at or above Large reads as Large.")]
    [SerializeField] private float mediumBlindSpotFrom = 5f;
    [SerializeField] private float largeBlindSpotFrom = 7f;

    [Header("Build Feedback")]
    [SerializeField] private DamageNumbersPro.DamageNumber supplyPopupPrefab;
    [Tooltip("Played at the plot when a tower is bought (hammer sound + wood burst).")]
    [SerializeField] private MMF_Player buildFeedback;

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
        }
    };

    private TowerPlot activePlot;
    private bool isOpen = false;

    private IUpgradeable upgradeTarget;
    private readonly List<UpgradeOption> upgradeOptions = new List<UpgradeOption>();
    private bool IsUpgradeMode => upgradeTarget != null;

    // The authored ring the slices sit on (captured once from the authored buttons).
    private bool ringCaptured;
    private Vector2 ringCentre;
    private float ringRadius;
    private float ringStartAngle;

    public bool IsOpen => isOpen;

    /// <summary>
    ///     While set, the wheel offers only this defence and hides the rest (the tutorial's first build is
    ///     an Arrow Tower, see <see cref="BuildDefenseStep" />). Null offers everything. Cleared when the
    ///     wheel is destroyed, so it never leaks into the next scene.
    /// </summary>
    public static FortDefenseType? OnlyAllowed { get; set; }

    private static bool IsOffered(FortDefenseType type) => OnlyAllowed == null || OnlyAllowed.Value == type;

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

        ValidateFeedbackReferences();
    }

    private void ValidateFeedbackReferences()
    {
        if (supplyPopupPrefab == null) Debug.LogError("BuildWheelUI: supplyPopupPrefab is not assigned.", this);
        if (buildFeedback == null) Debug.LogError("BuildWheelUI: buildFeedback is not assigned.", this);
    }

    private void Start()
    {
        SetupButtons();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            OnlyAllowed = null;
        }
        CursorLockManager.SetUnlock("BuildWheel", false);
        PauseMenuController.Instance?.SetToggleEnabled(true);
    }

    private void Update()
    {
        if (!isOpen) return;

        if (upgradeTarget != null && !upgradeTarget.IsUpgradeTargetAlive)
        {
            Close();
            return;
        }

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
        upgradeTarget = null;
        isOpen = true;

        gameObject.SetActive(true);
        if (wheelPanel != null && wheelPanel != gameObject) wheelPanel.SetActive(true);

        CursorLockManager.SetUnlock("BuildWheel", true);
        PauseMenuController.Instance?.SetToggleEnabled(false);

        SetupButtons();
        RefreshUI();
    }

    /// <summary>Opens the wheel as an upgrade wheel for a tower or wall (plan 17).</summary>
    public void OpenUpgrades(IUpgradeable target)
    {
        if (target == null) return;
        activePlot = null;
        upgradeTarget = target;
        isOpen = true;

        gameObject.SetActive(true);
        if (wheelPanel != null && wheelPanel != gameObject) wheelPanel.SetActive(true);

        CursorLockManager.SetUnlock("BuildWheel", true);
        PauseMenuController.Instance?.SetToggleEnabled(false);

        RefreshUI();
    }

    public void Close()
    {
        isOpen = false;
        activePlot = null;
        upgradeTarget = null;
        upgradeOptions.Clear();

        if (wheelPanel != null && wheelPanel != gameObject) wheelPanel.SetActive(false);
        gameObject.SetActive(false);

        CursorLockManager.SetUnlock("BuildWheel", false);
        PauseMenuController.Instance?.SetToggleEnabled(true);
    }

    public void RefreshUI()
    {
        if (IsUpgradeMode)
        {
            RefreshUpgradeUI();
            return;
        }

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
            int offered = 0;
            foreach (DefenseOption o in defenseOptions)
            {
                if (IsOffered(o.defenseType)) offered++;
            }
            EnsureButtonCount(defenseOptions.Count);
            for (int i = defenseOptions.Count; i < wheelButtons.Count; i++)
            {
                if (wheelButtons[i] != null) wheelButtons[i].gameObject.SetActive(false);
            }
            int slot = 0;
            for (int i = 0; i < defenseOptions.Count && i < wheelButtons.Count; i++)
            {
                if (wheelButtons[i] != null && IsOffered(defenseOptions[i].defenseType))
                {
                    PlaceOnRing(wheelButtons[i], slot++, offered);
                }
            }

            for (int i = 0; i < defenseOptions.Count && i < wheelButtons.Count; i++)
            {
                BuildWheelButton btn = wheelButtons[i];
                if (btn == null) continue;

                DefenseOption opt = defenseOptions[i];
                btn.gameObject.SetActive(IsOffered(opt.defenseType));
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
                btn.gameObject.SetActive(IsOffered(opt.defenseType));
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
            descriptionLabel.text = IsUpgradeMode ? "Choose an upgrade." : "Choose a defense structure to protect the gates.";
        }
    }

    public void OnSelectSlice(int index)
    {
        if (!isOpen || activePlot == null) return;
        if (index < 0 || index >= defenseOptions.Count) return;

        DefenseOption opt = defenseOptions[index];
        if (!IsOffered(opt.defenseType)) return;
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

            if (buildFeedback != null)
            {
                buildFeedback.PlayFeedbacks(plotPos);
            }

            if (supplyPopupPrefab != null)
            {
                supplyPopupPrefab.Spawn(plotPos + Vector3.up * 1.8f, $"-{opt.supplyCost} Supply");
            }

            targetPlot.BuildDefense(opt.defenseType, buildCost: opt.supplyCost);
            Debug.Log($"[BuildWheelUI] Constructed {opt.displayName} on plot {targetPlot.PlotIndex}!");
        }
    }

    public void OnHoverSlice(int index)
    {
        if (IsUpgradeMode)
        {
            if (descriptionLabel != null && index >= 0 && index < upgradeOptions.Count)
            {
                descriptionLabel.text = upgradeOptions[index].description;
            }
            return;
        }

        if (index >= 0 && index < defenseOptions.Count && descriptionLabel != null)
        {
            DefenseOption opt = defenseOptions[index];
            string rangeLine = GetRangeSummary(opt.defenseType);
            descriptionLabel.text = string.IsNullOrEmpty(rangeLine)
                ? opt.description
                : $"{opt.description}\n<size=85%>{rangeLine}</size>";
        }
    }

    // ---- Upgrade wheel (plan 17) ---------------------------------------------------------------

    private void RefreshUpgradeUI()
    {
        upgradeOptions.Clear();
        upgradeTarget.BuildUpgradeOptions(upgradeOptions);

        if (supplyLabel != null) supplyLabel.text = CurrencySummary();
        if (headerText != null) headerText.text = upgradeTarget.UpgradeTitle.ToUpperInvariant();
        if (descriptionLabel != null) descriptionLabel.text = "Choose an upgrade.";

        EnsureButtonCount(upgradeOptions.Count);
        for (int i = 0; i < wheelButtons.Count; i++)
        {
            BuildWheelButton btn = wheelButtons[i];
            if (btn == null) continue;
            bool used = i < upgradeOptions.Count;
            btn.gameObject.SetActive(used);
            if (!used) continue;

            UpgradeOption opt = upgradeOptions[i];
            int index = i;
            PlaceOnRing(btn, i, upgradeOptions.Count);
            btn.Setup(opt.label, opt.CostLabel, opt.icon, opt.IsAvailable,
                () => OnSelectUpgrade(index),
                () => OnHoverSlice(index),
                () => OnUnhoverSlice());
        }

        // Legacy slice buttons have no upgrade layout.
        foreach (Button b in sliceButtons)
        {
            if (b != null) b.gameObject.SetActive(false);
        }
    }

    private void OnSelectUpgrade(int index)
    {
        if (!isOpen || upgradeTarget == null || index < 0 || index >= upgradeOptions.Count) return;
        UpgradeOption opt = upgradeOptions[index];
        Vector3 anchor = upgradeTarget.UpgradeAnchor;
        if (!opt.TryPurchase()) return;

        if (opt.supplyCost > 0 || opt.crystalCost > 0)
        {
            if (buildFeedback != null) buildFeedback.PlayFeedbacks(anchor);
            if (supplyPopupPrefab != null)
            {
                string spent = opt.supplyCost > 0 ? $"-{opt.supplyCost} Supply" : $"-{opt.crystalCost} {opt.crystalElement.CrystalName()}";
                supplyPopupPrefab.Spawn(anchor + Vector3.up * 0.4f, spent);
            }
        }

        if (opt.closesWheel || upgradeTarget == null || !upgradeTarget.IsUpgradeTargetAlive) Close();
        else RefreshUI();
    }

    /// <summary>"Supply: 120   Fire 2  Ice 0  Storm 1" with element colours.</summary>
    private static string CurrencySummary()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"Supply: <color=#FFD700>{RunSession.InRunSupply}</color>");
        foreach (StructureElement e in StructureElements.All)
        {
            string name = e == StructureElement.Lightning ? "Storm" : e.ToString();
            sb.Append($"   <color={e.Hex()}>{name} {RunSession.GetCrystals(e)}</color>");
        }
        return sb.ToString();
    }

    /// <summary>Clones the first authored slice until there are <paramref name="count" /> buttons.</summary>
    private void EnsureButtonCount(int count)
    {
        if (wheelButtons == null || wheelButtons.Count == 0 || wheelButtons[0] == null) return;
        CaptureRing();
        BuildWheelButton template = wheelButtons[0];
        while (wheelButtons.Count < count)
        {
            BuildWheelButton clone = Instantiate(template, template.transform.parent);
            clone.name = $"Slice_{wheelButtons.Count}_Runtime";
            wheelButtons.Add(clone);
        }
    }

    /// <summary>Remembers the ring the authored slices sit on: their centroid, mean radius and first angle.</summary>
    private void CaptureRing()
    {
        if (ringCaptured) return;
        Vector2 sum = Vector2.zero;
        int n = 0;
        foreach (BuildWheelButton b in wheelButtons)
        {
            if (b == null || !(b.transform is RectTransform rt)) continue;
            sum += rt.anchoredPosition;
            n++;
        }
        if (n == 0) return;
        ringCentre = sum / n;
        float r = 0f;
        foreach (BuildWheelButton b in wheelButtons)
        {
            if (b == null || !(b.transform is RectTransform rt)) continue;
            r += (rt.anchoredPosition - ringCentre).magnitude;
        }
        ringRadius = r / n;
        Vector2 first = ((RectTransform)wheelButtons[0].transform).anchoredPosition - ringCentre;
        ringStartAngle = Mathf.Atan2(first.y, first.x);
        ringCaptured = ringRadius > 1f;
    }

    /// <summary>Slot <paramref name="slot" /> of <paramref name="count" /> evenly round the ring, clockwise from the first authored slice.</summary>
    private void PlaceOnRing(BuildWheelButton button, int slot, int count)
    {
        CaptureRing();
        if (!ringCaptured || count <= 0 || !(button.transform is RectTransform rt)) return;
        float angle = ringStartAngle - slot * (Mathf.PI * 2f / count);
        rt.anchoredPosition = ringCentre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ringRadius;
    }

    /// <summary>
    ///     "Range: Medium  |  Blind spot: Small", read from the prefab the active plot would build,
    ///     so it stays in step with whatever minRange / range the prefab is tuned to.
    /// </summary>
    private string GetRangeSummary(FortDefenseType type)
    {
        if (activePlot == null) return null;
        GameObject prefab = activePlot.GetPrefabForType(type);
        DefenseStructure defense = prefab != null ? prefab.GetComponent<DefenseStructure>() : null;
        if (defense == null) return null;

        if (defense.MaxRange <= 0f) return "Trap: hits what walks over it";

        float max = defense.MaxRange;
        string range = max < mediumRangeFrom ? "Short" : max < longRangeFrom ? "Medium" : "Long";

        float min = defense.MinRange;
        string blindSpot = min <= 0f ? "None"
            : min < mediumBlindSpotFrom ? "Small"
            : min < largeBlindSpotFrom ? "Medium"
            : "Large";

        return $"Range: {range}  |  Blind spot: {blindSpot}";
    }
}
