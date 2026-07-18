using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
///     Edit-mode animation sampling for the Enemy Manager's preview: a minimal PlayableGraph
///     (one <see cref="AnimationClipPlayable" /> → <see cref="AnimationPlayableOutput" /> on the
///     rig's Animator, the <see cref="BowPropAnimator" /> pattern) evaluated manually. Going through
///     the Animator is what makes Humanoid retargeting work — never <c>clip.SampleAnimation</c>.
///     The instance's root transform is pinned across every Evaluate so clips with root motion
///     can't walk the preview model away. Dispose (or let the owner's OnDisable do it) — a leaked
///     graph is a native leak.
/// </summary>
public class EnemyAnimSampler : IDisposable
{
    private PlayableGraph graph;
    private AnimationClipPlayable playable;
    private Animator animator;
    private AnimationClip clip;

    public AnimationClip Clip => clip;
    public bool IsOpen => graph.IsValid() && animator != null;

    /// <summary>Builds the graph against a rig. Any previous graph is destroyed first.</summary>
    public void Open(Animator rigAnimator)
    {
        Dispose();
        animator = rigAnimator;
        graph = PlayableGraph.Create("EnemyAnimSampler");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
    }

    public void SetClip(AnimationClip newClip)
    {
        if (!graph.IsValid() || newClip == null)
        {
            return;
        }

        if (playable.IsValid())
        {
            playable.Destroy();
        }
        clip = newClip;
        playable = AnimationClipPlayable.Create(graph, clip);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Preview", animator);
        output.SetSourcePlayable(playable);
    }

    /// <summary>Poses the rig at <paramref name="time" /> seconds into the clip, root pinned in place.</summary>
    public void Evaluate(double time)
    {
        if (!IsOpen || !playable.IsValid())
        {
            return;
        }

        Transform root = animator.transform;
        Vector3 position = root.position;
        Quaternion rotation = root.rotation;

        playable.SetTime(time);
        graph.Evaluate(0f);

        root.position = position;
        root.rotation = rotation;
    }

    public void Dispose()
    {
        if (graph.IsValid())
        {
            graph.Destroy();
        }
        animator = null;
        clip = null;
    }
}
