using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class UltimateBarUI : MonoBehaviour
{
    [SerializeField] private PlayerUltimateController ultimateController;
    [SerializeField] private MoreMountains.Tools.MMProgressBar progressBar;
    [SerializeField] private Image glowImage;
    [SerializeField] private TextMeshProUGUI inputKeyText;
    [SerializeField] private TextMeshProUGUI chargeText;
    
    [Header("Feedbacks")]
    [SerializeField] private MoreMountains.Feedbacks.MMF_Player fullFeedback;
    [SerializeField] private MoreMountains.Feedbacks.MMF_Player activatedFeedback;
    
    [Header("Colors & Animation")]
    [SerializeField] private Color fullColor = Color.yellow;
    [SerializeField] private float glowSpeed = 2f;
    [SerializeField] private float glowAlphaMin = 0.2f;
    [SerializeField] private float glowAlphaMax = 0.8f;

    private bool isFull;
    private bool isSubscribed;

    private void Awake()
    {
        TryBindController();
    }

    private void OnEnable()
    {
        TryBindController();
        UpdateInputText();
    }

    private void Start()
    {
        TryBindController();
        UpdateInputText();
    }

    private void OnDisable()
    {
        UnbindController();
    }

    private void TryBindController()
    {
        if (isSubscribed) return;

        if (ultimateController == null)
        {
            if (Player.Instance != null)
            {
                ultimateController = Player.Instance.GetComponent<PlayerUltimateController>();
            }
            if (ultimateController == null)
            {
                ultimateController = FindFirstObjectByType<PlayerUltimateController>();
            }
        }

        if (ultimateController != null)
        {
            ultimateController.OnChargeChanged -= UpdateBar;
            ultimateController.OnChargeChanged += UpdateBar;
            ultimateController.OnUltimateActivated -= HandleActivated;
            ultimateController.OnUltimateActivated += HandleActivated;
            isSubscribed = true;
            Debug.Log($"[UltimateBarUI] Successfully bound to PlayerUltimateController on '{ultimateController.gameObject.name}'. Initial charge={ultimateController.CurrentCharge}, progressBar={(progressBar != null ? progressBar.name : "NULL")}");
            UpdateBar(ultimateController.CurrentCharge);
        }
    }

    private void UnbindController()
    {
        if (ultimateController != null && isSubscribed)
        {
            ultimateController.OnChargeChanged -= UpdateBar;
            ultimateController.OnUltimateActivated -= HandleActivated;
            isSubscribed = false;
        }
    }

    private void UpdateBar(float charge)
    {
        float fraction = charge / PlayerUltimateController.MaxCharge;
        Debug.Log($"[UltimateBarUI] UpdateBar(charge={charge:F1}) -> fraction={fraction:P0}, progressBar={(progressBar != null ? progressBar.name : "NULL")}");
        
        if (progressBar != null)
        {
            progressBar.UpdateBar(charge, 0f, PlayerUltimateController.MaxCharge);
        }

        if (chargeText != null)
        {
            chargeText.text = $"{Mathf.FloorToInt(fraction * 100f)}%";
        }

        bool wasFull = isFull;
        isFull = fraction >= 1f;

        if (isFull && !wasFull)
        {
            if (glowImage != null) glowImage.gameObject.SetActive(true);
            if (inputKeyText != null) inputKeyText.gameObject.SetActive(true);
            if (fullFeedback != null) fullFeedback.PlayFeedbacks();
        }
        else if (!isFull && wasFull)
        {
            if (glowImage != null) glowImage.gameObject.SetActive(false);
            if (inputKeyText != null) inputKeyText.gameObject.SetActive(false);
        }
    }

    private void HandleActivated()
    {
        // Flash or reset
        UpdateBar(0f);
        if (activatedFeedback != null) activatedFeedback.PlayFeedbacks();
    }

    private void Update()
    {
        if (!isSubscribed)
        {
            TryBindController();
        }

        if (isFull && glowImage != null && glowImage.gameObject.activeSelf)
        {
            float alpha = Mathf.Lerp(glowAlphaMin, glowAlphaMax, (Mathf.Sin(Time.time * glowSpeed) + 1f) / 2f);
            Color c = glowImage.color;
            c.a = alpha;
            glowImage.color = c;
        }
    }

    private void UpdateInputText()
    {
        if (inputKeyText == null) return;
        
        // This is a naive way to get the primary binding for the Ultimate action.
        var player = Player.Instance;
        if (player != null && player.InputSettings != null)
        {
            var map = player.InputSettings.GetRebindableActionMap();
            if (map != null)
            {
                var action = map.FindAction("Ultimate");
                if (action != null)
                {
                    int bindIndex = action.GetBindingIndexForControl(action.controls.Count > 0 ? action.controls[0] : null);
                    if (bindIndex >= 0)
                    {
                        inputKeyText.text = action.GetBindingDisplayString(bindIndex);
                    }
                    else
                    {
                        inputKeyText.text = "Q/Y";
                    }
                    return;
                }
            }
        }
        
        inputKeyText.text = "Q";
    }
}
