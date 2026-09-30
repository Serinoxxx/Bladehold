using System;
using UnityEngine;

/// <summary>
///     One baked crowd rig: the idle / run / attack poses of a body sampled into a bone-matrix texture,
///     plus the static mesh and instanced material <see cref="BakedCrowdRenderer" /> draws it with.
///     Written by <c>Bladehold/Crowd/Bake Goblin Crowd Animation</c> (<c>Editor/BakedCrowdBaker</c>):
///     re-run the bake rather than editing the baked fields by hand. The tuning fields below the bake
///     output are designer-editable and survive a re-bake.
///
///     Texture layout: one row per baked frame, three texels per bone (the top three rows of the
///     bone's skin matrix, which maps a bind-pose mesh vertex into the rig's Animator space). Clips
///     are stacked vertically in <see cref="clips" /> order.
/// </summary>
[CreateAssetMenu(fileName = "BakedCrowdAnimation", menuName = "Scriptable Objects/Enemies/Baked Crowd Animation")]
public class BakedCrowdAnimationSO : ScriptableObject
{
    public const int ClipIdle = 0;
    public const int ClipRun = 1;
    public const int ClipAttack = 2;

    // The upper-body attack layered over the run legs, for goblins that swing while chasing.
    public const int ClipAttackMoving = 3;

    [Serializable]
    public class Clip
    {
        public string name;
        public int startFrame;
        public int frameCount;
        public float length;
        public bool loop;

        [Tooltip("Ground speed (m/s) the clip was authored at; run playback is scaled by agent speed / this.")]
        public float referenceSpeed;
    }

    [Header("Bake output (written by the baker)")]
    public Mesh mesh;
    public Material material;
    public Texture2D boneTexture;
    public int boneCount;
    public float bakeFps = 30f;
    public Clip[] clips;

    [Header("Playback tuning")]
    [Tooltip("Agent speed (m/s) above which the run clip plays instead of idle.")]
    [Min(0f)] public float runSpeedThreshold = 0.4f;

    [Tooltip("Lowest run playback rate; slower agents still play at this rate so the loop never crawls.")]
    [Min(0.05f)] public float minRunPlaybackRate = 0.6f;

    [Tooltip("Crossfade between clips, in seconds.")]
    [Min(0f)] public float crossfadeSeconds = 0.15f;

    [Tooltip("How often (in frames) each baked goblin re-checks for things only the real Animator can show " +
             "(an added HighlightEffect outline, a late golden/impulse swap).")]
    [Min(1)] public int visualCheckInterval = 15;

    // CPU copy of the (readable) bone texture, fetched on first promotion.
    [NonSerialized] private Color[] cachedPixels;

    /// <summary>The baked skin matrix of <paramref name="bone" /> at absolute frame <paramref name="frame" />.</summary>
    public Matrix4x4 SkinMatrix(int frame, int bone)
    {
        if (cachedPixels == null)
        {
            cachedPixels = boneTexture.GetPixels();
        }
        int i = frame * boneTexture.width + bone * 3;
        Color r0 = cachedPixels[i], r1 = cachedPixels[i + 1], r2 = cachedPixels[i + 2];
        Matrix4x4 m = Matrix4x4.identity;
        m.SetRow(0, new Vector4(r0.r, r0.g, r0.b, r0.a));
        m.SetRow(1, new Vector4(r1.r, r1.g, r1.b, r1.a));
        m.SetRow(2, new Vector4(r2.r, r2.g, r2.b, r2.a));
        return m;
    }

    public int FrameOf(int clipIndex, float time)
    {
        Clip clip = clips[clipIndex];
        int local = Mathf.FloorToInt(time * bakeFps);
        local = clip.loop
            ? ((local % clip.frameCount) + clip.frameCount) % clip.frameCount
            : Mathf.Clamp(local, 0, clip.frameCount - 1);
        return clip.startFrame + local;
    }
}
