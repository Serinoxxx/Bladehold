using UnityEngine;

/// <summary>
///     Tells the <see cref="MusicDirector" /> what this scene sounds like. Place one per scene, like
///     <see cref="SceneAbilityRules" />. A scene without one leaves the current music playing.
///     Combat scenes set <see cref="sceneCue" /> to their prep cue, since it only plays before the
///     game loop has a phase.
/// </summary>
public class SceneMusic : MonoBehaviour
{
    [Tooltip("Required. Plays in the scene when no wave phase applies (menus, hubs, the fishing pond).")]
    [SerializeField] private MusicCueSO sceneCue;
    [Tooltip("Optional: replaces the default prep music between waves in this scene.")]
    [SerializeField] private MusicCueSO prepOverride;
    [Tooltip("Optional: replaces the default battle music during waves in this scene.")]
    [SerializeField] private MusicCueSO battleOverride;

    public static SceneMusic Current { get; private set; }

    public MusicCueSO SceneCue => sceneCue;
    public MusicCueSO PrepOverride => prepOverride;
    public MusicCueSO BattleOverride => battleOverride;

    private void Awake()
    {
        if (Current != null && Current != this)
        {
            Debug.LogError("[SceneMusic] More than one in the scene; keeping the first.", this);
            return;
        }
        Current = this;
    }

    private void Start()
    {
        if (sceneCue == null)
        {
            Debug.LogError($"{name}: sceneCue is not assigned; this scene has no music.", this);
        }
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
    }
}
