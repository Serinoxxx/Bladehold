using UnityEngine;

/// <summary>
///     Hoofbeat sound while the horse moves: a single looping gait bed (real recorded hoofbeats,
///     not a synthesized clop) that stays playing the whole time and is shaped by
///     <see cref="HorseMotor.NormalizedSpeed" /> each frame (the <see cref="HorseAnimation" /> idiom —
///     HorseMotor raises no speed-changed event) — volume fades in/out at the walk/stop threshold and
///     pitch rises with speed so the same loop reads as a brisker gait at a gallop, instead of
///     retriggering discrete one-shot clops on a timer (which reads as spaced-out taps rather than a
///     rolling gait, especially at low speed where the interval is longest).
/// </summary>
public class HorseHoofbeatAudio : MonoBehaviour
{
    [SerializeField] private HorseMotor horseMotor;
    [SerializeField] private AudioSource audioSource;

    [Header("Clip")]
    [Tooltip("Looping gait bed, e.g. a recorded multi-horse walk/trot cycle. Played continuously; speed only changes its volume and pitch.")]
    [SerializeField] private AudioClip gallopLoop;

    [Header("Volume")]
    [Tooltip("Volume once fully cross-faded in at full charge speed.")]
    [SerializeField] private float maxVolume = 1f;

    [Tooltip("Seconds for volume to fade fully in/out when crossing the moving/stopped threshold.")]
    [SerializeField] private float volumeFadeTime = 0.35f;

    [Header("Pitch")]
    [Tooltip("Playback pitch at a bare walk (NormalizedSpeed near Min Speed Fraction).")]
    [SerializeField] private float minPitch = 0.85f;

    [Tooltip("Playback pitch at full charge speed (NormalizedSpeed = 1) — raised so the same loop reads as a quicker gait.")]
    [SerializeField] private float maxPitch = 1.35f;

    [Tooltip("Fraction of (stat-scaled) charge speed below which the horse is considered stopped — the loop fades out and pauses.")]
    [Range(0f, 1f)]
    [SerializeField] private float minSpeedFraction = 0.05f;

    private bool anyError = false;

    private void OnValidate()
    {
        if (horseMotor == null)
        {
            horseMotor = GetComponent<HorseMotor>();
        }
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void Start()
    {
        if (horseMotor == null)
        {
            Debug.LogError("HorseMotor component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (audioSource == null)
        {
            Debug.LogError("AudioSource component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (gallopLoop == null)
        {
            Debug.LogError("Gallop Loop clip is not assigned.");
            anyError = true;
        }

        if (anyError) return;

        audioSource.clip = gallopLoop;
        audioSource.loop = true;
        audioSource.volume = 0f;
        audioSource.Play();
    }

    private void Update()
    {
        if (anyError) return;

        float normalizedSpeed = horseMotor.NormalizedSpeed;
        bool moving = normalizedSpeed >= minSpeedFraction;

        float targetVolume = moving ? maxVolume : 0f;
        float fadeStep = volumeFadeTime > 0f ? Time.deltaTime / volumeFadeTime : 1f;
        audioSource.volume = Mathf.MoveTowards(audioSource.volume, targetVolume, fadeStep);

        if (moving)
        {
            audioSource.pitch = Mathf.Lerp(minPitch, maxPitch, normalizedSpeed);
        }
    }
}
