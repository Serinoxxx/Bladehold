using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
///     Animates the rigged bow prop (string draw, limb bend) in sync with the character's Bow
///     animator layer. Lives on the bow model (<see cref="PlayerBow" />'s <c>bowModel</c>), which the
///     bow activates while aiming, and plays the Synty bow-prop clips — the Generic-rig
///     <c>Rcv</c>/<c>Lng</c>/<c>Cmp</c> variants matching the equipped bow type, e.g.
///     <c>A_POLY_BOW_Rcv_Stand_Aiming_ToDrawn_Neut</c> for the recurve — through a small Playables
///     graph on the bow's own Animator. Synty ships no AnimatorController for the props (the prefab's
///     controller reference is dangling), so the graph replaces it outright: no controller asset to
///     author, just three clips assigned in the inspector.
///
///     Sequencing mirrors the character layer: activation (aim start) plays the draw clip once, then
///     the drawn loop; <see cref="PlayerBow.OnFired" /> plays the release/reload clip once, then back
///     to the loop. Crossfade times default to the character layer's transition durations so the two
///     rigs never visibly drift.
/// </summary>
public class BowPropAnimator : MonoBehaviour
{
    [Tooltip("The player's bow, for aim state and the fired event. Auto-found in parents.")]
    [SerializeField] private PlayerBow bow;
    [Tooltip("The bow prop's own Animator (its controller slot is ignored — a Playables graph drives it). Auto-found on this object.")]
    [SerializeField] private Animator animator;

    [Header("Bow-prop clips (the Generic-rig Rcv/Lng/Cmp variants, NOT the Humanoid character clips)")]
    [Tooltip("Played once when aiming starts, e.g. Aim/Rcv/A_POLY_BOW_Rcv_Stand_Aiming_ToDrawn_Neut.")]
    [SerializeField] private AnimationClip drawClip;
    [Tooltip("Looped while the bow stays drawn, e.g. Aim/Rcv/A_POLY_BOW_Rcv_Stand_Aiming_Drawn_Neut (looped here manually — the FBX Loop Time setting doesn't matter).")]
    [SerializeField] private AnimationClip aimLoopClip;
    [Tooltip("Played once per shot, e.g. Shoot/Rcv/A_POLY_BOW_Rcv_Stand_Shoot_Reload_Neut.")]
    [SerializeField] private AnimationClip fireClip;

    [Header("Blending")]
    [Tooltip("Crossfade seconds between draw/loop states (the character Bow layer uses 0.15/0.25).")]
    [SerializeField] private float crossfadeSeconds = 0.15f;
    [Tooltip("Crossfade seconds into the fire clip — snappy, matching the character layer's 0.05.")]
    [SerializeField] private float fireCrossfadeSeconds = 0.05f;

    private enum PropState
    {
        Idle,
        Draw,
        AimLoop,
        Fire,
    }

    private PlayableGraph graph;
    private AnimationMixerPlayable mixer;
    private AnimationClipPlayable drawPlayable;
    private AnimationClipPlayable aimPlayable;
    private AnimationClipPlayable firePlayable;
    private bool graphBuilt;

    private PropState state = PropState.Idle;
    private int currentInput = -1;
    private int previousInput = -1;
    private float fadeElapsed;
    private float fadeDuration;
    private bool subscribed;
    private bool anyError = false;

    private void OnValidate()
    {
        if (bow == null)
        {
            bow = GetComponentInParent<PlayerBow>();
        }
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    private void Start()
    {
        if (bow == null)
        {
            Debug.LogError("PlayerBow is not assigned or found in parents; the bow prop can't follow the aim state.");
            anyError = true;
        }
        if (animator == null)
        {
            Debug.LogError("Animator is not assigned or found on the bow prop.");
            anyError = true;
        }
        if (drawClip == null || aimLoopClip == null || fireClip == null)
        {
            Debug.LogError("BowPropAnimator is missing one or more clips (draw/aim loop/fire) — assign the Generic-rig bow-prop variants (see TODO.md).");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        Subscribe();
    }

    private void OnEnable()
    {
        // The bow object is activated by PlayerBow at aim start — restart the sequence fresh.
        state = PropState.Idle;
        if (!anyError)
        {
            Subscribe();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (graph.IsValid())
        {
            graph.Destroy();
        }
    }

    private void Subscribe()
    {
        if (subscribed || bow == null)
        {
            return;
        }
        bow.OnFired += HandleFired;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || bow == null)
        {
            return;
        }
        bow.OnFired -= HandleFired;
        subscribed = false;
    }

    private void Update()
    {
        if (anyError)
        {
            return;
        }

        // The prop only animates while the bow is actually drawn — being active but un-aimed (e.g. a
        // prefab left active in the Editor) just holds whatever pose the rig is in.
        if (!bow.IsAiming)
        {
            state = PropState.Idle;
            return;
        }

        BuildGraphIfNeeded();

        if (state == PropState.Idle)
        {
            // Aim just started: snap straight into the draw (the object usually appeared this frame).
            drawPlayable.SetTime(0);
            SwitchTo(0, 0f);
            state = PropState.Draw;
        }
        else if (state == PropState.Draw && drawPlayable.GetTime() >= drawClip.length - crossfadeSeconds)
        {
            EnterAimLoop(crossfadeSeconds);
        }
        else if (state == PropState.Fire && firePlayable.GetTime() >= fireClip.length - crossfadeSeconds)
        {
            EnterAimLoop(crossfadeSeconds);
        }

        // The drawn loop wraps manually so the clip's FBX Loop Time import setting is irrelevant.
        if (state == PropState.AimLoop && aimPlayable.GetTime() >= aimLoopClip.length)
        {
            aimPlayable.SetTime(aimPlayable.GetTime() % aimLoopClip.length);
        }

        UpdateFadeWeights();
    }

    private void HandleFired()
    {
        if (anyError || !isActiveAndEnabled)
        {
            return;
        }

        BuildGraphIfNeeded();
        firePlayable.SetTime(0);
        SwitchTo(2, fireCrossfadeSeconds);
        state = PropState.Fire;
    }

    private void EnterAimLoop(float fadeSeconds)
    {
        aimPlayable.SetTime(0);
        SwitchTo(1, fadeSeconds);
        state = PropState.AimLoop;
    }

    /// <summary>Starts a crossfade to mixer input <paramref name="input" /> (0 = draw, 1 = aim loop, 2 = fire).</summary>
    private void SwitchTo(int input, float fadeSeconds)
    {
        if (input == currentInput)
        {
            return;
        }
        previousInput = currentInput;
        currentInput = input;
        fadeElapsed = 0f;
        fadeDuration = fadeSeconds;
        UpdateFadeWeights();
    }

    private void UpdateFadeWeights()
    {
        if (currentInput < 0)
        {
            return;
        }

        fadeElapsed += Time.deltaTime;
        float weight = fadeDuration > 0f ? Mathf.Clamp01(fadeElapsed / fadeDuration) : 1f;
        for (int i = 0; i < 3; i++)
        {
            float target = i == currentInput ? weight
                : i == previousInput ? 1f - weight
                : 0f;
            mixer.SetInputWeight(i, target);
        }
    }

    /// <summary>
    ///     Lazily builds the Playables graph on first aim (the <see cref="EnemyRagdoll" /> lazy-build
    ///     idiom — an unused bow costs nothing at load).
    /// </summary>
    private void BuildGraphIfNeeded()
    {
        if (graphBuilt)
        {
            return;
        }

        graph = PlayableGraph.Create("BowPropAnimator");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        mixer = AnimationMixerPlayable.Create(graph, 3);
        drawPlayable = AnimationClipPlayable.Create(graph, drawClip);
        aimPlayable = AnimationClipPlayable.Create(graph, aimLoopClip);
        firePlayable = AnimationClipPlayable.Create(graph, fireClip);
        graph.Connect(drawPlayable, 0, mixer, 0);
        graph.Connect(aimPlayable, 0, mixer, 1);
        graph.Connect(firePlayable, 0, mixer, 2);

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Bow", animator);
        output.SetSourcePlayable(mixer);

        graph.Play();
        graphBuilt = true;
    }
}
