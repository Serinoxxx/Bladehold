using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Individual waypoint overlay widget on the HUD canvas.
///     Displays an icon, distance meter, and directional arrow for offscreen clamping.
/// </summary>
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class ObjectiveWaypointMarkerUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform markerRect;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image iconBackground;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image offscreenArrow;
    [SerializeField] private TMP_Text distanceText;

    [Header("Settings")]
    [SerializeField] private float fadeSpeed = 8f;
    [SerializeField] private float arrowDistanceOffset = 36f;
    [SerializeField] private bool showDistance = true;

    private Transform targetTransform;
    private Vector3 worldOffset;
    private float targetAlpha = 0f;

    public Transform TargetTransform => targetTransform;
    public bool IsActive => targetTransform != null;

    private void Awake()
    {
        if (markerRect == null) markerRect = GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    public void Bind(Transform target, Vector3 offset, Sprite icon, Color tint, string label)
    {
        targetTransform = target;
        worldOffset = offset;
        targetAlpha = 1f;

        gameObject.SetActive(true);

        if (iconImage != null && icon != null)
        {
            iconImage.sprite = icon;
            iconImage.color = tint;
            iconImage.gameObject.SetActive(true);
        }

        if (iconBackground != null)
        {
            iconBackground.color = new Color(tint.r * 0.25f, tint.g * 0.25f, tint.b * 0.25f, 0.85f);
        }

        if (offscreenArrow != null)
        {
            offscreenArrow.color = tint;
        }

        if (distanceText != null)
        {
            distanceText.gameObject.SetActive(showDistance);
        }
    }

    public void Unbind()
    {
        targetTransform = null;
        targetAlpha = 0f;
    }

    public void UpdatePosition(Vector2 canvasLocalPos, bool isOffScreen, float arrowAngle, float distanceMeters)
    {
        if (markerRect != null)
        {
            markerRect.anchoredPosition = canvasLocalPos;
        }

        if (offscreenArrow != null)
        {
            offscreenArrow.gameObject.SetActive(isOffScreen);
            if (isOffScreen)
            {
                offscreenArrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, arrowAngle);
                float rad = arrowAngle * Mathf.Deg2Rad;
                offscreenArrow.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * arrowDistanceOffset;
            }
        }

        if (distanceText != null && showDistance)
        {
            if (distanceMeters >= 3f)
            {
                distanceText.text = $"{Mathf.RoundToInt(distanceMeters)}m";
                distanceText.gameObject.SetActive(true);
            }
            else
            {
                distanceText.gameObject.SetActive(false);
            }
        }
    }

    private void Update()
    {
        if (canvasGroup != null && !Mathf.Approximately(canvasGroup.alpha, targetAlpha))
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * fadeSpeed);
            if (canvasGroup.alpha <= 0.001f && targetTransform == null)
            {
                gameObject.SetActive(false);
            }
        }
    }

    public Vector3 GetTargetWorldPosition()
    {
        if (targetTransform == null) return Vector3.zero;
        return targetTransform.position + worldOffset;
    }
}
