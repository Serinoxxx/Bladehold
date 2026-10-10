using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     One ember spark on the centre charge-stamina bar (<see cref="ChargeStaminaCentreUI" />): drifts up and
///     sideways from where it was emitted, shrinks and fades, then deactivates itself so the bar can reuse it.
///     A UI image rather than a particle system because the HUD is a Screen Space Overlay canvas.
/// </summary>
[RequireComponent(typeof(Image))]
public class ChargeEmberUI : MonoBehaviour
{
    [SerializeField] private Image image;
    [SerializeField] private Vector2 riseSpeedRange = new Vector2(60f, 140f);
    [SerializeField] private float sideSpeed = 50f;
    [SerializeField] private Vector2 lifetimeRange = new Vector2(0.35f, 0.7f);
    [SerializeField] private Gradient colorOverLife;

    private RectTransform rect;
    private Vector2 velocity;
    private float lifetime;
    private float age;
    private float startScale;

    private void OnValidate()
    {
        if (image == null) image = GetComponent<Image>();
    }

    public void Emit(Vector2 anchoredPosition)
    {
        if (rect == null) rect = (RectTransform)transform;
        rect.anchoredPosition = anchoredPosition;
        velocity = new Vector2(Random.Range(-sideSpeed, sideSpeed), Random.Range(riseSpeedRange.x, riseSpeedRange.y));
        lifetime = Random.Range(lifetimeRange.x, lifetimeRange.y);
        startScale = Random.Range(0.6f, 1.2f);
        age = 0f;
        rect.localScale = Vector3.one * startScale;
        gameObject.SetActive(true);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        age += dt;
        float t = lifetime > 0f ? age / lifetime : 1f;
        if (t >= 1f)
        {
            gameObject.SetActive(false);
            return;
        }
        rect.anchoredPosition += velocity * dt;
        rect.localScale = Vector3.one * (startScale * (1f - t));
        if (image != null && colorOverLife != null) image.color = colorOverLife.Evaluate(t);
    }
}
