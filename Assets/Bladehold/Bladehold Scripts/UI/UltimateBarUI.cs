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
    
    [Header("Colors & Animation")]
    [SerializeField] private Color fullColor = Color.yellow;
    [SerializeField] private float glowSpeed = 2f;
    [SerializeField] private float glowAlphaMin = 0.2f;
    [SerializeField] private float glowAlphaMax = 0.8f;

    private bool isFull;

    private void Awake()
    {
        if (ultimateController == null)
        {
            ultimateController = FindFirstObjectByType<PlayerUltimateController>();
        }
    }

    private void OnEnable()
    {
        if (ultimateController != null)
        {
            ultimateController.OnChargeChanged += UpdateBar;
            ultimateController.OnUltimateActivated += HandleActivated;
            UpdateBar(ultimateController.CurrentCharge);
        }
        UpdateInputText();
    }

    private void OnDisable()
    {
        if (ultimateController != null)
        {
            ultimateController.OnChargeChanged -= UpdateBar;
            ultimateController.OnUltimateActivated -= HandleActivated;
        }
    }

    private void UpdateBar(float charge)
    {
        float fraction = charge / PlayerUltimateController.MaxCharge;
        
        if (progressBar != null)
        {
            progressBar.UpdateBar(charge, 0f, PlayerUltimateController.MaxCharge);
        }

        bool wasFull = isFull;
        isFull = fraction >= 1f;

        if (isFull && !wasFull)
        {
            if (glowImage != null) glowImage.gameObject.SetActive(true);
            if (inputKeyText != null) inputKeyText.gameObject.SetActive(true);
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
    }

    private void Update()
    {
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
