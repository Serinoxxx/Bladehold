using UnityEngine;

/// <summary>
///     A pool of music clips the <see cref="MusicDirector" /> treats as one "cue" (for example the battle
///     music). The director picks a clip at random, never repeating the last pick for that cue.
/// </summary>
[CreateAssetMenu(fileName = "Music_", menuName = "Scriptable Objects/Audio/Music Cue")]
public class MusicCueSO : ScriptableObject
{
    [Tooltip("Pool of clips to pick from.")]
    public AudioClip[] clips;

    [Tooltip("Cue volume, 0..1. The Music slider and ducking multiply on top of it.")]
    [Range(0f, 1f)]
    public float volume = 1f;

    [Tooltip("On for seamless LOOP files (the clip repeats forever). Off for FULL tracks: the director crossfades to another pool pick shortly before the clip ends.")]
    public bool loopClip = true;

    [Tooltip("Only when Loop Clip is off: seconds before the clip ends at which the next pick starts crossfading in.")]
    public float rotateCrossfade = 4f;
}
