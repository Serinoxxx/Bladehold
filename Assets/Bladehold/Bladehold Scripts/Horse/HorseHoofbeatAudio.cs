using UnityEngine;

/// <summary>
///     Hoofbeat sound while the horse moves: polls <see cref="HorseMotor.NormalizedSpeed" /> each
///     frame (the <see cref="HorseAnimation" /> idiom — HorseMotor raises no speed-changed event) and
///     fires a random clip from <see cref="hoofbeatSounds" /> at a random pitch on a timer whose
///     interval shortens with speed, so a walk clops slower than a gallop. Silent below
///     <see cref="minSpeedFraction" /> (standing still / barely moving).
/// </summary>
public class HorseHoofbeatAudio : MonoBehaviour
{
    [SerializeField] private HorseMotor horseMotor;
    [SerializeField] private AudioSource audioSource;

    [Header("Clips")]
    [SerializeField] private AudioClip[] hoofbeatSounds;

    [Header("Pitch")]
    [Tooltip("Random pitch is picked in this range on every play, for variation.")]
    [SerializeField] private float minPitch = 0.9f;
    [SerializeField] private float maxPitch = 1.1f;

    [Header("Timing")]
    [Tooltip("Fraction of (stat-scaled) charge speed below which the horse is considered stopped — no hoofbeats play.")]
    [Range(0f, 1f)]
    [SerializeField] private float minSpeedFraction = 0.05f;

    [Tooltip("Seconds between hoofbeats at a bare walk (NormalizedSpeed near Min Speed Fraction).")]
    [SerializeField] private float slowInterval = 0.5f;

    [Tooltip("Seconds between hoofbeats at full charge speed (NormalizedSpeed = 1).")]
    [SerializeField] private float fastInterval = 0.18f;

    private float timer;
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
    }

    private void Update()
    {
        if (anyError) return;

        float normalizedSpeed = horseMotor.NormalizedSpeed;
        if (normalizedSpeed < minSpeedFraction)
        {
            timer = 0f;
            return;
        }

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        PlayRandomHoofbeat();
        timer = Mathf.Lerp(slowInterval, fastInterval, normalizedSpeed);
    }

    private void PlayRandomHoofbeat()
    {
        if (hoofbeatSounds == null || hoofbeatSounds.Length == 0) return;

        audioSource.pitch = Random.Range(minPitch, maxPitch);
        audioSource.PlayOneShot(hoofbeatSounds[Random.Range(0, hoofbeatSounds.Length)]);
    }
}
