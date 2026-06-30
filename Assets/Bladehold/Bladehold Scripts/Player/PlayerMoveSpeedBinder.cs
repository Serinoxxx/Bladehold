using System.Reflection;
using Synty.AnimationBaseLocomotion.Samples;
using UnityEngine;

/// <summary>
///     Scales the vendored <see cref="SamplePlayerAnimationController" />'s movement speeds from
///     <see cref="PlayerStats" />. The controller derives its per-gait target speed from the private
///     <c>_walkSpeed</c>/<c>_runSpeed</c>/<c>_sprintSpeed</c> fields every frame, so we capture those authored
///     values once and write scaled values back whenever the relevant stats change. No vendored source is
///     edited; the private fields are reached by reflection (cached) and the binder degrades gracefully
///     (logs and disables itself) if Synty ever renames them.
///
///     <see cref="StatType.MoveSpeed" /> and <see cref="StatType.SprintSpeed" /> are treated as unitless
///     <em>multipliers</em> with base 1.0, so a "+5% Move Speed" upgrade is a +0.05 percent modifier giving
///     ×1.05. MoveSpeed scales all three gaits; SprintSpeed is an extra multiplier on sprint only.
/// </summary>
[RequireComponent(typeof(PlayerStats))]
public class PlayerMoveSpeedBinder : MonoBehaviour
{
    private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.NonPublic;

    [SerializeField] private SamplePlayerAnimationController controller;
    [SerializeField] private PlayerStats stats;

    private FieldInfo walkField;
    private FieldInfo runField;
    private FieldInfo sprintField;
    private float baseWalk;
    private float baseRun;
    private float baseSprint;

    private bool anyError = false;

    private void OnValidate()
    {
        if (controller == null)
        {
            controller = GetComponent<SamplePlayerAnimationController>();
            if (controller == null)
            {
                controller = GetComponentInChildren<SamplePlayerAnimationController>();
            }
        }
        if (stats == null)
        {
            stats = GetComponent<PlayerStats>();
        }
    }

    private void Start()
    {
        if (controller == null)
        {
            Debug.LogError("SamplePlayerAnimationController is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (stats == null)
        {
            Debug.LogError("PlayerStats component is not assigned or found on the GameObject.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        System.Type type = controller.GetType();
        walkField = type.GetField("_walkSpeed", FieldFlags);
        runField = type.GetField("_runSpeed", FieldFlags);
        sprintField = type.GetField("_sprintSpeed", FieldFlags);

        if (walkField == null || runField == null || sprintField == null)
        {
            Debug.LogError("PlayerMoveSpeedBinder could not find the controller's speed fields (_walkSpeed/_runSpeed/_sprintSpeed). Movement upgrades disabled.");
            anyError = true;
            return;
        }

        // Capture the controller's authored speeds as the bases the multipliers scale.
        baseWalk = (float)walkField.GetValue(controller);
        baseRun = (float)runField.GetValue(controller);
        baseSprint = (float)sprintField.GetValue(controller);

        // Multiplier stats: base 1.0 so "+5%" gives ×1.05.
        stats.SetBase(StatType.MoveSpeed, 1f);
        stats.SetBase(StatType.SprintSpeed, 1f);

        stats.OnStatChanged += HandleStatChanged;
        Apply();
    }

    private void OnDestroy()
    {
        if (stats != null)
        {
            stats.OnStatChanged -= HandleStatChanged;
        }
    }

    private void HandleStatChanged(StatType stat)
    {
        if (stat == StatType.MoveSpeed || stat == StatType.SprintSpeed)
        {
            Apply();
        }
    }

    private void Apply()
    {
        if (anyError) return;

        float move = stats.GetValue(StatType.MoveSpeed);
        float sprint = stats.GetValue(StatType.SprintSpeed);

        walkField.SetValue(controller, baseWalk * move);
        runField.SetValue(controller, baseRun * move);
        sprintField.SetValue(controller, baseSprint * move * sprint);
    }
}
