using UnityEngine;

/// <summary>
///     Dims a <see cref="Light" /> from its authored intensity to zero over <see cref="fadeDuration" />,
///     then destroys the GameObject. Used for the bright flash when an enemy is hit so hard it goes flying
///     (spawned by the enemy's fling MMF_Player). Colour, peak intensity and range are the Light's own.
/// </summary>
public class FlashLightDimmer : MonoBehaviour
{
    [SerializeField] private Light lightComponent;
    [Tooltip("Seconds from peak intensity to dark.")]
    [SerializeField] private float fadeDuration = 0.2f;

    private float elapsed;
    private float startIntensity;
    private bool anyError = false;

    private void OnValidate()
    {
        if (lightComponent == null)
        {
            lightComponent = GetComponent<Light>();
        }
    }

    private void Start()
    {
        if (lightComponent == null)
        {
            Debug.LogError($"FlashLightDimmer on {name}: lightComponent is not assigned.", this);
            anyError = true;
            Destroy(gameObject);
            return;
        }
        startIntensity = lightComponent.intensity;
    }

    private void Update()
    {
        if (anyError) return;

        elapsed += Time.deltaTime;
        float t = elapsed / Mathf.Max(0.01f, fadeDuration);
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        lightComponent.intensity = Mathf.Lerp(startIntensity, 0f, t);
    }
}
