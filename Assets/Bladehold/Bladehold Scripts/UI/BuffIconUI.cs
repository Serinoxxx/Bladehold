using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MoreMountains.Feedbacks;

/// <summary>
///     A single active buff icon. Displays the buff's sprite, name, and a radial fill/timer for its remaining duration.
/// </summary>
public class BuffIconUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Image radialFillImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI stackText;
    
    [Header("Optional Styling")]
    [Tooltip("Color of the text when the timer is low (e.g., under 3 seconds).")]
    [SerializeField] private Color lowTimeColor = Color.red;
    [SerializeField] private Color normalTimeColor = Color.white;
    [SerializeField] private float lowTimeThreshold = 3f;

    [Header("Feedbacks")]
    [SerializeField] private MMF_Player appearFeedback;
    [SerializeField] private MMF_Player pulseFeedback;
    [SerializeField] private MMF_Player expireFeedback;

    private int lastSecond = -1;
    private string baseBuffName = "";

    public void Setup(Sprite icon, string buffName, int stackCount = 0)
    {
        baseBuffName = buffName;
        if (iconImage != null) iconImage.sprite = icon;
        UpdateStacks(stackCount);

        if (appearFeedback != null)
        {
            appearFeedback.PlayFeedbacks();
        }
    }

    public void UpdateStacks(int stackCount)
    {
        if (stackText != null)
        {
            if (stackCount > 0)
            {
                stackText.text = stackCount > 1 ? "x" + stackCount : stackCount.ToString();
                stackText.gameObject.SetActive(true);
            }
            else
            {
                stackText.gameObject.SetActive(false);
            }
        }

        if (nameText != null)
        {
            if (stackText == null && stackCount > 1)
            {
                nameText.text = baseBuffName + " x" + stackCount;
            }
            else
            {
                nameText.text = baseBuffName;
            }
        }
    }

    public void UpdateTime(float remainingSeconds, float maxSeconds)
    {
        if (radialFillImage != null)
        {
            radialFillImage.fillAmount = maxSeconds > 0f ? remainingSeconds / maxSeconds : 0f;
        }

        if (timerText != null)
        {
            timerText.text = remainingSeconds.ToString("0.0");
            timerText.color = remainingSeconds <= lowTimeThreshold ? lowTimeColor : normalTimeColor;
        }

        int currentSecond = Mathf.CeilToInt(remainingSeconds);
        if (lastSecond == -1)
        {
            lastSecond = currentSecond;
        }
        else if (currentSecond != lastSecond)
        {
            lastSecond = currentSecond;
            if (pulseFeedback != null)
            {
                pulseFeedback.PlayFeedbacks();
            }
        }
    }

    public void Expire()
    {
        if (expireFeedback != null)
        {
            expireFeedback.PlayFeedbacks();
            Destroy(gameObject, expireFeedback.TotalDuration);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
