using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Contextual HUD element displayed while aiming ranged weapons (Bow, Thrown Axe, Wand).
///     Shows the current and maximum ammunition under the crosshairs (e.g. '10/20 [arrow icon]')
///     and displays a bold red 'OUT OF AMMO' warning center-screen when ammunition is depleted.
///     Fades in/out with weapon aim states matching BowCrosshairUI.
/// </summary>
public class BowAmmoUI : MonoBehaviour
{
    [Tooltip("The player's bow or ranged weapon. Resolved automatically if unassigned.")]
    [SerializeField] private PlayerBow bow;

    [Tooltip("CanvasGroup controlling visibility fade during aiming. Usually on this object or root container.")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Ammo Counter")]
    [Tooltip("Text element positioned directly beneath the crosshair showing '10/20'.")]
    [SerializeField] private TMP_Text ammoCountText;

    [Tooltip("Icon image displayed next to or after the ammo text (e.g. arrow icon).")]
    [SerializeField] private Image arrowIcon;

    [Header("Out of Ammo Warning")]
    [Tooltip("Center-screen text element in bold red displayed when aiming with 0 ammunition.")]
    [SerializeField] private TMP_Text outOfAmmoText;

    [Header("Tuning & Colors")]
    [Tooltip("Seconds to fade the UI in and out when aiming starts or ends.")]
    [SerializeField] private float fadeSeconds = 0.15f;

    [SerializeField] private Color normalAmmoColor = Color.white;
    [SerializeField] private Color outOfAmmoColor = new Color(1f, 0.25f, 0.25f, 1f);

    private IChargedAimWeapon weapon;
    private PlayerAmmo playerAmmo;

    private void Awake()
    {
        EnsureVisualElements();
    }

    private void Start()
    {
        weapon = AimWeaponResolver.Resolve(bow);

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        ResolveAmmoComponent();
        PlayerAmmo.OnAnyAmmoChanged += HandleAmmoChanged;

        RefreshDisplay(GetEffectiveCurrentAmmo(), GetEffectiveMaxAmmo());
    }

    private void OnDestroy()
    {
        PlayerAmmo.OnAnyAmmoChanged -= HandleAmmoChanged;
    }

    private void ResolveAmmoComponent()
    {
        if (playerAmmo == null && Player.Instance != null)
        {
            playerAmmo = Player.Instance.Ammo;
        }
        if (playerAmmo == null)
        {
            playerAmmo = PlayerAmmo.Instance;
        }
    }

    private int GetEffectiveCurrentAmmo()
    {
        return RunSession.CurrentAmmo;
    }

    private int GetEffectiveMaxAmmo()
    {
        if (playerAmmo != null)
        {
            return playerAmmo.MaxAmmo;
        }
        if (Player.Instance != null && Player.Instance.Stats != null)
        {
            float val = Player.Instance.Stats.GetValue(StatType.MaxAmmo);
            if (val > 0f) return Mathf.RoundToInt(val);
        }
        return RunSession.HasMetaPerk("deep_quiver") ? 25 : 20;
    }

    private void HandleAmmoChanged(int current, int max)
    {
        RefreshDisplay(current, max);
    }

    public void RefreshDisplay(int current, int max)
    {
        if (ammoCountText != null)
        {
            ammoCountText.text = $"{current}/{max}";
            ammoCountText.color = current <= 0 ? outOfAmmoColor : normalAmmoColor;
        }

        if (outOfAmmoText != null)
        {
            outOfAmmoText.gameObject.SetActive(current <= 0);
        }
    }

    private void Update()
    {
        if (weapon == null)
        {
            weapon = AimWeaponResolver.Resolve(bow);
            if (weapon == null) return;
        }

        bool isAiming = weapon.IsAiming;
        float targetAlpha = isAiming ? 1f : 0f;

        if (canvasGroup != null)
        {
            float fadeStep = fadeSeconds > 0f ? Time.deltaTime / fadeSeconds : 1f;
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeStep);
        }

        int curr = GetEffectiveCurrentAmmo();
        int max = GetEffectiveMaxAmmo();

        if (outOfAmmoText != null)
        {
            bool showWarning = isAiming && (curr <= 0);
            if (outOfAmmoText.gameObject.activeSelf != showWarning)
            {
                outOfAmmoText.gameObject.SetActive(showWarning);
            }
        }
    }

    private void EnsureVisualElements()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        // If all essential visual components are already wired via Inspector/Prefab, skip re-creating them
        if (ammoCountText != null && arrowIcon != null && outOfAmmoText != null)
        {
            return;
        }

        // 1. Build or locate Ammo Counter container directly under crosshairs
        Transform existingCounter = transform.Find("AmmoCounter");
        GameObject counterObj;
        if (existingCounter != null)
        {
            counterObj = existingCounter.gameObject;
        }
        else
        {
            counterObj = new GameObject("AmmoCounter", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            counterObj.transform.SetParent(transform, false);
        }

        RectTransform rt = counterObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -95f);
        rt.sizeDelta = new Vector2(240f, 60f);

        HorizontalLayoutGroup hlg = counterObj.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null) hlg = counterObj.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 10f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        ContentSizeFitter csf = counterObj.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = counterObj.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Ammo Text
        if (ammoCountText == null)
        {
            Transform textChild = counterObj.transform.Find("AmmoText");
            GameObject textObj;
            if (textChild != null)
            {
                textObj = textChild.gameObject;
            }
            else
            {
                textObj = new GameObject("AmmoText", typeof(RectTransform), typeof(TextMeshProUGUI));
                textObj.transform.SetParent(counterObj.transform, false);
            }

            TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 46f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = normalAmmoColor;
            tmp.raycastTarget = false;
            ApplySafeOutline(tmp, 0.25f, Color.black);
            ammoCountText = tmp;
        }

        // Arrow Icon
        if (arrowIcon == null)
        {
            Transform iconChild = counterObj.transform.Find("ArrowIcon");
            GameObject iconObj;
            if (iconChild != null)
            {
                iconObj = iconChild.gameObject;
            }
            else
            {
                iconObj = new GameObject("ArrowIcon", typeof(RectTransform), typeof(Image));
                iconObj.transform.SetParent(counterObj.transform, false);
            }

            RectTransform iconRt = iconObj.GetComponent<RectTransform>();
            iconRt.sizeDelta = new Vector2(52f, 52f);

            Image img = iconObj.GetComponent<Image>();
            if (img == null) img = iconObj.AddComponent<Image>();
            img.raycastTarget = false;
#if UNITY_EDITOR
            if (img.sprite == null)
            {
                img.sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceFantasyWarriorHUD/Sprites/Icons_Weapons/ICON_SM_Prop_Arrow_01.png")
                    ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Bladehold/Art/Icons/Skills/Base/swiftarrow.png");
            }
#endif
            arrowIcon = img;
        }

        // 2. Build or locate Out Of Ammo warning in center screen
        if (outOfAmmoText == null)
        {
            Transform existingWarning = transform.Find("OutOfAmmoWarning");
            GameObject warnObj;
            if (existingWarning != null)
            {
                warnObj = existingWarning.gameObject;
            }
            else
            {
                warnObj = new GameObject("OutOfAmmoWarning", typeof(RectTransform), typeof(TextMeshProUGUI));
                warnObj.transform.SetParent(transform, false);
            }

            RectTransform wrt = warnObj.GetComponent<RectTransform>();
            wrt.anchorMin = new Vector2(0.5f, 0.5f);
            wrt.anchorMax = new Vector2(0.5f, 0.5f);
            wrt.pivot = new Vector2(0.5f, 0.5f);
            wrt.anchoredPosition = new Vector2(0f, 90f);
            wrt.sizeDelta = new Vector2(500f, 80f);

            TextMeshProUGUI wTmp = warnObj.GetComponent<TextMeshProUGUI>();
            if (wTmp == null) wTmp = warnObj.AddComponent<TextMeshProUGUI>();
            wTmp.text = "OUT OF AMMO";
            wTmp.fontSize = 64f;
            wTmp.fontStyle = FontStyles.Bold;
            wTmp.alignment = TextAlignmentOptions.Center;
            wTmp.color = outOfAmmoColor;
            wTmp.raycastTarget = false;
            ApplySafeOutline(wTmp, 0.3f, Color.black);
            outOfAmmoText = wTmp;
            warnObj.SetActive(false);
        }
    }

    private static void ApplySafeOutline(TextMeshProUGUI tmp, float width, Color color)
    {
        if (tmp == null) return;
        try
        {
            if (tmp.fontSharedMaterial != null)
            {
                tmp.outlineWidth = width;
                tmp.outlineColor = color;
            }
            else if (tmp.font != null && tmp.font.material != null)
            {
                tmp.fontSharedMaterial = tmp.font.material;
                tmp.outlineWidth = width;
                tmp.outlineColor = color;
            }
        }
        catch (System.Exception)
        {
            // Silently fall back if material cannot be instanced on uninitialized or inactive TMP
        }
    }
}
