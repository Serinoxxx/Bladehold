using UnityEngine;

/// <summary>
///     Tutorial-wide tunables (plan 16). Loaded from <c>Resources/TutorialConfig</c> so the Main Menu can
///     route New Game into the tutorial without a scene reference. Per-scene details (targets, spawn
///     points, hint text) live on the scene's <see cref="TutorialDirector" /> and steps.
/// </summary>
[CreateAssetMenu(fileName = "TutorialConfig", menuName = "Scriptable Objects/Tutorial/Tutorial Config")]
public class TutorialConfigSO : ScriptableObject
{
    private const string ResourcePath = "TutorialConfig";

    [Header("Scenes")]
    [Tooltip("First tutorial scene, loaded by New Game while SaveData.tutorialCompleted is false.")]
    public string firstSceneName = "Bladehold Tutorial Dungeon";
    public string metaAreaSceneName = "Bladehold Meta Area Scene";

    [Header("Heavy attack gate")]
    [Tooltip("Minimum PlayerAttack.AttackDamageMultiplier (latched on release) that counts as a heavy attack. " +
             "The sword runs 0.1 (tap) to 2.0 (full charge), so 1.6 is 80% charge (~8 damage on the stock sword).")]
    [Min(0f)] public float heavyMinChargeMultiplier = 1.6f;

    [Tooltip("Weak hits on a heavy-only target before the hint nudges the player to hold the attack.")]
    [Min(1)] public int heavyNudgeAfterBlocks = 2;

    [Header("Death in the arena")]
    [Tooltip("Seconds between the player's death and the arena reloading (lets the death animation play).")]
    [Min(0f)] public float reloadAfterDeathDelay = 2f;

    [Header("Steps")]
    [Tooltip("Pause between a step completing and the next one starting, so the completion chime lands.")]
    [Min(0f)] public float stepAdvanceDelay = 0.75f;

    private static TutorialConfigSO cached;

    public static TutorialConfigSO Load()
    {
        if (cached == null)
        {
            cached = Resources.Load<TutorialConfigSO>(ResourcePath);
        }
        return cached;
    }
}
