using UnityEngine;

/// <summary>
///     Lightly dims a <see cref="Light" /> from peak intensity to zero over a given duration,
///     then destroys the GameObject. Used for bright flash lights when enemies are hit so hard they go flying.
/// </summary>
public class FlashLightDimmer : MonoBehaviour
{
    private float duration;
    private float elapsed;
    private Light lightComponent;
    private float startIntensity;

    public void Initialize(float peakIntensity, float fadeDuration)
    {
        lightComponent = GetComponent<Light>();
        startIntensity = peakIntensity;
        duration = Mathf.Max(0.01f, fadeDuration);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        if (lightComponent != null)
        {
            lightComponent.intensity = Mathf.Lerp(startIntensity, 0f, t);
        }
    }
}
