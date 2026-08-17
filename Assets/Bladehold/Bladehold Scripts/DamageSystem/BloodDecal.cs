using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
///     Attached to spawned blood decal GameObjects (which carry a <see cref="DecalProjector"/>).
///     Handles lifetime, smooth alpha/fade transition, and unregistering from <see cref="BloodDecalManager"/> upon despawn.
/// </summary>
[RequireComponent(typeof(DecalProjector))]
public class BloodDecal : MonoBehaviour
{
    private DecalProjector projector;
    private float lifetime;
    private float fadeDuration;
    private float spawnTime;
    private bool isEvicting;

    public void Init(float totalLifetime, float fadeTime)
    {
        projector = GetComponent<DecalProjector>();
        lifetime = totalLifetime;
        fadeDuration = Mathf.Min(fadeTime, totalLifetime);
        spawnTime = Time.time;
        if (projector != null)
        {
            projector.fadeFactor = 1f;
        }
        StartCoroutine(DecalRoutine());
    }

    /// <summary>
    ///     Forces an immediate fast fade-out when evicted by the global decal cap.
    /// </summary>
    public void EvictEarly()
    {
        if (isEvicting) return;
        isEvicting = true;
        StopAllCoroutines();
        StartCoroutine(EvictRoutine());
    }

    private IEnumerator DecalRoutine()
    {
        float solidTime = lifetime - fadeDuration;
        if (solidTime > 0f)
        {
            yield return new WaitForSeconds(solidTime);
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            if (projector != null)
            {
                projector.fadeFactor = Mathf.Clamp01(1f - (elapsed / fadeDuration));
            }
            yield return null;
        }

        Despawn();
    }

    private IEnumerator EvictRoutine()
    {
        float evictDuration = 0.5f;
        float startFade = projector != null ? projector.fadeFactor : 1f;
        float elapsed = 0f;

        while (elapsed < evictDuration)
        {
            elapsed += Time.deltaTime;
            if (projector != null)
            {
                projector.fadeFactor = Mathf.Lerp(startFade, 0f, elapsed / evictDuration);
            }
            yield return null;
        }

        Despawn();
    }

    private void Despawn()
    {
        BloodDecalManager.Unregister(this);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        BloodDecalManager.Unregister(this);
    }
}
