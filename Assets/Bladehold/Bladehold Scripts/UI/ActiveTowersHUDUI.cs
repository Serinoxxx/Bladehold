using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     HUD overlay widget that displays all currently active defense towers on the battlefield
///     and their real-time supply levels via dynamic progress bar sliders.
///     Highlights low supply in amber and 0 supply in blinking alert red ("NO SUPPLY").
/// </summary>
public class ActiveTowersHUDUI : MonoBehaviour
{
    public static ActiveTowersHUDUI Instance { get; private set; }

    [Header("Layout Settings")]
    [Tooltip("Container holding tower supply row cards.")]
    [SerializeField] private RectTransform container;

    [Tooltip("Anchor position offset from screen edge.")]
    [SerializeField] private Vector2 screenOffset = new Vector2(24f, -140f);

    [Header("Color Styling")]
    [SerializeField] private Color normalColor = new Color(0.2f, 0.85f, 0.35f, 1f);
    [SerializeField] private Color warningColor = new Color(1f, 0.75f, 0.15f, 1f);
    [SerializeField] private Color depletedColor = new Color(1f, 0.25f, 0.25f, 1f);

    private readonly List<TowerEntryUI> activeEntries = new List<TowerEntryUI>();
    private Canvas rootCanvas;
    private float nextRefreshTime = 0f;

    private class TowerEntryUI
    {
        public DefenseStructure Structure;
        public GameObject RootObject;
        public TMP_Text TitleText;
        public Slider SupplySlider;
        public Image FillImage;
        public TMP_Text SupplyText;
        public Image BackgroundImage;
    }

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

        rootCanvas = GetComponentInParent<Canvas>();
        if (container == null)
        {
            EnsureContainer();
        }
    }

    private void Start()
    {
        EnsureContainer();
        RefreshEntries();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // Periodic check for new or removed towers
        if (Time.unscaledTime >= nextRefreshTime)
        {
            nextRefreshTime = Time.unscaledTime + 0.5f;
            CheckForRosterChanges();
        }

        // Smooth pulse on depleted entries
        for (int i = 0; i < activeEntries.Count; i++)
        {
            var entry = activeEntries[i];
            if (entry == null || entry.Structure == null) continue;

            if (entry.Structure.IsDepleted)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
                if (entry.SupplyText != null)
                {
                    entry.SupplyText.color = Color.Lerp(depletedColor, Color.white, pulse);
                }
            }
        }
    }

    private void EnsureContainer()
    {
        if (container != null) return;

        // Try find under Bladehold HUD
        GameObject hudCanvas = GameObject.Find("Bladehold HUD");
        Transform parentTransform = hudCanvas != null ? hudCanvas.transform : (rootCanvas != null ? rootCanvas.transform : transform);

        GameObject holder = new GameObject("ActiveTowersPanel", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        holder.transform.SetParent(parentTransform, false);

        RectTransform rt = holder.GetComponent<RectTransform>();
        // Anchor to Top-Left, below standard currencies (y = -140)
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = screenOffset;
        rt.sizeDelta = new Vector2(220f, 0f);

        VerticalLayoutGroup vlg = holder.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 6f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = holder.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        container = rt;
    }

    private void CheckForRosterChanges()
    {
        var currentActive = DefenseStructure.AllActive;
        bool changed = currentActive.Count != activeEntries.Count;

        if (!changed)
        {
            for (int i = 0; i < currentActive.Count; i++)
            {
                if (activeEntries[i].Structure != currentActive[i])
                {
                    changed = true;
                    break;
                }
            }
        }

        if (changed)
        {
            RefreshEntries();
        }
    }

    public void RefreshEntries()
    {
        // Clean up old entries
        foreach (var entry in activeEntries)
        {
            if (entry.RootObject != null)
            {
                Destroy(entry.RootObject);
            }
        }
        activeEntries.Clear();

        if (container == null) EnsureContainer();
        if (container == null) return;

        var allDefenses = DefenseStructure.AllActive;
        foreach (DefenseStructure def in allDefenses)
        {
            if (def == null) continue;
            CreateEntryForDefense(def);
        }

        // Show container only when at least one tower exists
        container.gameObject.SetActive(activeEntries.Count > 0);
    }

    private void CreateEntryForDefense(DefenseStructure def)
    {
        GameObject rowObj = new GameObject($"TowerRow_{def.DefenseType}_{def.PlotIndex}", typeof(RectTransform), typeof(Image));
        rowObj.transform.SetParent(container, false);

        RectTransform rowRt = rowObj.GetComponent<RectTransform>();
        rowRt.sizeDelta = new Vector2(220f, 44f);

        Image bg = rowObj.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.09f, 0.11f, 0.88f);

        // Header text (Name + Level)
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(rowObj.transform, false);
        RectTransform titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 0.55f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.offsetMin = new Vector2(8f, 0f);
        titleRt.offsetMax = new Vector2(-8f, -2f);

        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.fontSize = 13f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = new Color(0.92f, 0.92f, 0.95f, 1f);
        titleTmp.alignment = TextAlignmentOptions.Left;
        titleTmp.text = FormatTitle(def);

        // Slider Root
        GameObject sliderObj = new GameObject("SupplySlider", typeof(RectTransform), typeof(Slider));
        sliderObj.transform.SetParent(rowObj.transform, false);
        RectTransform sliderRt = sliderObj.GetComponent<RectTransform>();
        sliderRt.anchorMin = new Vector2(0f, 0f);
        sliderRt.anchorMax = new Vector2(1f, 0.5f);
        sliderRt.offsetMin = new Vector2(8f, 6f);
        sliderRt.offsetMax = new Vector2(-8f, 0f);

        Slider slider = sliderObj.GetComponent<Slider>();
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;

        // Slider Background
        GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgObj.transform.SetParent(sliderObj.transform, false);
        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;
        Image bgImg = bgObj.GetComponent<Image>();
        bgImg.color = new Color(0.18f, 0.18f, 0.20f, 0.9f);

        // Fill Area
        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform faRt = fillArea.GetComponent<RectTransform>();
        faRt.anchorMin = Vector2.zero;
        faRt.anchorMax = Vector2.one;
        faRt.sizeDelta = Vector2.zero;

        // Fill Image
        GameObject fillObj = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObj.transform.SetParent(fillArea.transform, false);
        RectTransform fillRt = fillObj.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.sizeDelta = Vector2.zero;
        Image fillImg = fillObj.GetComponent<Image>();
        fillImg.color = normalColor;

        slider.fillRect = fillRt;
        slider.targetGraphic = fillImg;

        // Numeric / Status Overlay Text
        GameObject valObj = new GameObject("SupplyText", typeof(RectTransform), typeof(TextMeshProUGUI));
        valObj.transform.SetParent(sliderObj.transform, false);
        RectTransform valRt = valObj.GetComponent<RectTransform>();
        valRt.anchorMin = Vector2.zero;
        valRt.anchorMax = Vector2.one;
        valRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI valTmp = valObj.GetComponent<TextMeshProUGUI>();
        valTmp.fontSize = 11f;
        valTmp.fontStyle = FontStyles.Bold;
        valTmp.color = Color.white;
        valTmp.alignment = TextAlignmentOptions.Center;

        var entry = new TowerEntryUI
        {
            Structure = def,
            RootObject = rowObj,
            TitleText = titleTmp,
            SupplySlider = slider,
            FillImage = fillImg,
            SupplyText = valTmp,
            BackgroundImage = bg
        };

        UpdateEntryDisplay(entry, def.CurrentSupply, def.MaxSupply);

        // Subscribe to structure events
        def.OnSupplyChanged += (curr, max) =>
        {
            if (entry.RootObject != null) UpdateEntryDisplay(entry, curr, max);
        };
        def.OnLevelChanged += (newLevel) =>
        {
            if (entry.TitleText != null) entry.TitleText.text = FormatTitle(def);
        };

        activeEntries.Add(entry);
    }

    private void UpdateEntryDisplay(TowerEntryUI entry, int current, int max)
    {
        if (entry.SupplySlider != null)
        {
            entry.SupplySlider.maxValue = Mathf.Max(1, max);
            entry.SupplySlider.value = Mathf.Clamp(current, 0, max);
        }

        float pct = max > 0 ? (float)current / max : 0f;

        if (current <= 0)
        {
            if (entry.FillImage != null) entry.FillImage.color = depletedColor;
            if (entry.SupplyText != null)
            {
                entry.SupplyText.text = "NO SUPPLY";
                entry.SupplyText.color = depletedColor;
            }
        }
        else
        {
            Color barColor = pct <= 0.35f ? warningColor : normalColor;
            if (entry.FillImage != null) entry.FillImage.color = barColor;
            if (entry.SupplyText != null)
            {
                entry.SupplyText.text = $"{current} / {max}";
                entry.SupplyText.color = Color.white;
            }
        }
    }

    private string FormatTitle(DefenseStructure def)
    {
        string name = def.DefenseType switch
        {
            FortDefenseType.ArrowSlits => "Arrow Tower",
            FortDefenseType.Catapult => "Catapult",
            FortDefenseType.Ballista => "Ballista",
            FortDefenseType.NetThrower => "Net Thrower",
            FortDefenseType.Spikes => "Spike Trap",
            FortDefenseType.BurningOil => "Oil Vat",
            _ => def.DefenseType.ToString()
        };

        return $"{name}  <size=80%><color=#E0BB50>Lv {def.Level}</color></size>";
    }
}
