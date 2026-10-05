using UnityEngine;

/// <summary>
///     Default cues and fade timings for the <see cref="MusicDirector" />. All times are unscaled seconds,
///     because music keeps fading while the game is frozen (pause, death screen, gate fall).
/// </summary>
[CreateAssetMenu(fileName = "MusicSettings", menuName = "Scriptable Objects/Audio/Music Settings")]
public class MusicSettingsSO : ScriptableObject
{
    [Header("Default cues")]
    [Tooltip("Calm music between waves, unless the scene's SceneMusic overrides it.")]
    public MusicCueSO prep;
    [Tooltip("Wave music, unless the scene's SceneMusic overrides it.")]
    public MusicCueSO battle;
    [Tooltip("Plays while a clan captain is alive during a wave.")]
    public MusicCueSO captain;
    [Tooltip("Plays during the Necromancer and Princess fights.")]
    public MusicCueSO boss;
    [Tooltip("Calm bed under the victory screen.")]
    public MusicCueSO victoryBed;

    [Header("Stings (one-shot, played over ducked music)")]
    public MusicCueSO objectiveSting;
    public MusicCueSO victorySting;
    [Tooltip("Played instead of the victory sting when the finished node ends the campaign.")]
    public MusicCueSO campaignCompleteSting;
    public MusicCueSO defeatSting;

    [Header("Scene transitions")]
    public float sceneFadeOut = 1.2f;
    public float sceneFadeIn = 2f;

    [Header("Crossfades")]
    public float toBattle = 1.5f;
    public float toPrep = 3f;
    public float toCaptain = 1f;
    public float toBoss = 1.5f;
    [Tooltip("How long the music takes to stop when the player dies or the gate falls.")]
    public float defeatFadeOut = 1.5f;

    [Header("Sting ducking")]
    [Tooltip("Music level (0..1) while a sting plays.")]
    [Range(0f, 1f)] public float stingDuckTo = 0.25f;
    public float stingDuckIn = 0.3f;
    public float stingDuckOut = 1.5f;

    [Header("Pause ducking")]
    [Tooltip("Music level (0..1) while the pause menu is open, about -8 dB at 0.4.")]
    [Range(0f, 1f)] public float pauseDuckTo = 0.4f;
    public float pauseDuckFade = 0.25f;
}
