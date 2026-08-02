using System.Collections;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;

using MoreMountains.Tools;

/// <summary>
///     Listens for wave clear events and pops in a banner displaying the gold earned and enemies killed
///     during that specific wave. Uses <see cref="MMF_Player"/> to animate the banner in and out.
/// </summary>
public class WaveClearedBannerUI : MonoBehaviour
{
    [SerializeField] private WaveSpawner spawner;

    [Header("References")]
    [Tooltip("The parent GameObject containing the banner visual elements. Used to hide it completely when not active.")]
    [SerializeField] private GameObject bannerRoot;
    [SerializeField] private TMP_Text waveClearedText;
    [SerializeField] private TMP_Text goldEarnedText;
    [SerializeField] private TMP_Text enemiesKilledText;

    [Header("Animation & Juiciness")]
    [Tooltip("Played when the banner appears. Should handle its own reset/outro or be paired with a separate outro if needed.")]
    [SerializeField] private MMF_Player bannerAnimationFeedback;
    [SerializeField] private AudioClip[] waveClearedSounds;
    [Tooltip("How long the banner stays on screen before hiding itself.")]
    [SerializeField] private float displayDuration = 3f;

    private int goldAtWaveStart;
    private int killsAtWaveStart;
    private Coroutine hideRoutine;
    private bool anyError;

    private void OnValidate()
    {
        if (spawner == null)
        {
            spawner = FindObjectOfType<WaveSpawner>();
        }
    }

    private void Start()
    {
        if (spawner == null)
        {
            Debug.LogError("WaveClearedBannerUI has no WaveSpawner assigned.");
            anyError = true;
        }

        if (anyError) return;

        if (bannerRoot != null)
        {
            bannerRoot.SetActive(false);
        }

        spawner.WaveStarted += HandleWaveStarted;
        spawner.WaveCleared += HandleWaveCleared;
    }

    private void OnDestroy()
    {
        if (spawner != null)
        {
            spawner.WaveStarted -= HandleWaveStarted;
            spawner.WaveCleared -= HandleWaveCleared;
        }
    }

    private void HandleWaveStarted(int wave)
    {
        // Snapshot the stats at the beginning of the wave to calculate the delta when it clears.
        goldAtWaveStart = GameStats.Instance != null ? GameStats.Instance.GoldEarnedThisRun : 0;
        killsAtWaveStart = GameStats.Instance != null ? GameStats.Instance.GoblinsKilled : 0;
    }

    private void HandleWaveCleared(int wave)
    {
        int goldEarned = (GameStats.Instance != null ? GameStats.Instance.GoldEarnedThisRun : 0) - goldAtWaveStart;
        int kills = (GameStats.Instance != null ? GameStats.Instance.GoblinsKilled : 0) - killsAtWaveStart;

        goldEarned = Mathf.Max(0, goldEarned);
        kills = Mathf.Max(0, kills);

        if (waveClearedText != null)
        {
            // For now, hardcode or string format if Loc missing, but let's assume Loc.Format is available since WaveUI uses it.
            waveClearedText.text = $"WAVE {wave} CLEARED";
        }

        if (goldEarnedText != null)
        {
            goldEarnedText.text = goldEarned.ToString();
        }

        if (enemiesKilledText != null)
        {
            enemiesKilledText.text = kills.ToString();
        }

        if (bannerRoot != null)
        {
            bannerRoot.SetActive(true);
        }

        if (bannerAnimationFeedback != null)
        {
            bannerAnimationFeedback.Initialization();
            bannerAnimationFeedback.PlayFeedbacks();
        }

        AudioClip clipToPlay = null;
        if (waveClearedSounds != null && waveClearedSounds.Length > 0)
        {
            clipToPlay = waveClearedSounds[Random.Range(0, waveClearedSounds.Length)];
        }
#if UNITY_EDITOR
        if (clipToPlay == null)
        {
            clipToPlay = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Bladehold/Audio/Bells/chime_bell_10.wav");
        }
#endif
        if (clipToPlay != null)
        {
            MMSoundManagerPlayOptions options = MMSoundManagerPlayOptions.Default;
            options.MmSoundManagerTrack = MMSoundManager.MMSoundManagerTracks.UI;
            options.Location = transform.position;
            options.Volume = 0.9f;
            options.Pitch = Random.Range(0.95f, 1.05f);
            MMSoundManagerSoundPlayEvent.Trigger(clipToPlay, options);
        }

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
        }
        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSecondsRealtime(displayDuration);
        
        // If there is an outro animation, it would ideally be played here. 
        // For now we simply hide the root object.
        if (bannerRoot != null)
        {
            bannerRoot.SetActive(false);
        }
        hideRoutine = null;
    }
}
