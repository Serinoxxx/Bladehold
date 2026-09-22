using System.Reflection;
using Synty.AnimationBaseLocomotion.Samples;
using UnityEngine;

/// <summary>
///     Handles player interaction with slippery ice zones created by Glacial Catapults.
///     When on ice, reduces movement friction (damping factor) and grants a move speed boost,
///     allowing the player to glide and drift across the ice with full rotational control.
/// </summary>
public class PlayerIceSlideController : MonoBehaviour
{
    private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.NonPublic;

    public static PlayerIceSlideController Instance { get; private set; }

    [SerializeField] private SamplePlayerAnimationController controller;
    [SerializeField] private PlayerStats stats;
    [SerializeField] private float iceDamping = 1.8f;
    [SerializeField] private float speedBoostPercent = 0.35f;

    private FieldInfo dampingField;
    private float originalDamping = 10f;
    private int activeZoneCount = 0;
    private bool isSlidingOnIce = false;
    private bool speedModifierApplied = false;

    public bool IsOnIce => isSlidingOnIce;

    public static PlayerIceSlideController GetOrAdd(Player player)
    {
        if (player == null) return null;
        PlayerIceSlideController comp = player.GetComponentInChildren<PlayerIceSlideController>(true);
        if (comp == null)
        {
            comp = player.gameObject.AddComponent<PlayerIceSlideController>();
        }
        return comp;
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (controller == null)
        {
            controller = GetComponentInChildren<SamplePlayerAnimationController>(true);
            if (controller == null && Player.Instance != null)
            {
                controller = Player.Instance.GetComponentInChildren<SamplePlayerAnimationController>(true);
            }
        }

        if (stats == null)
        {
            stats = transform.root.GetComponentInChildren<PlayerStats>(true);
            if (stats == null && Player.Instance != null)
            {
                stats = Player.Instance.Stats;
            }
        }

        if (controller != null && dampingField == null)
        {
            dampingField = controller.GetType().GetField("_speedChangeDamping", FieldFlags);
            if (dampingField != null)
            {
                originalDamping = (float)dampingField.GetValue(controller);
            }
        }
    }

    public void RegisterIceZone()
    {
        activeZoneCount++;
        UpdateIceState();
    }

    public void UnregisterIceZone()
    {
        activeZoneCount = Mathf.Max(0, activeZoneCount - 1);
        UpdateIceState();
    }

    private void UpdateIceState()
    {
        ResolveReferences();

        bool shouldSlide = activeZoneCount > 0;
        if (shouldSlide == isSlidingOnIce) return;

        isSlidingOnIce = shouldSlide;

        if (isSlidingOnIce)
        {
            // Lower damping -> reduces friction so player glides
            if (dampingField != null && controller != null)
            {
                dampingField.SetValue(controller, iceDamping);
            }

            // Speed boost
            if (stats != null && !speedModifierApplied)
            {
                stats.AddModifier(StatType.MoveSpeed, ModifierKind.Percent, speedBoostPercent);
                speedModifierApplied = true;
            }
        }
        else
        {
            // Restore normal friction
            if (dampingField != null && controller != null)
            {
                dampingField.SetValue(controller, originalDamping);
            }

            // Remove speed boost
            if (stats != null && speedModifierApplied)
            {
                stats.AddModifier(StatType.MoveSpeed, ModifierKind.Percent, -speedBoostPercent);
                speedModifierApplied = false;
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        // Ensure clean restoration on scene change
        if (dampingField != null && controller != null)
        {
            dampingField.SetValue(controller, originalDamping);
        }
        if (stats != null && speedModifierApplied)
        {
            stats.AddModifier(StatType.MoveSpeed, ModifierKind.Percent, -speedBoostPercent);
            speedModifierApplied = false;
        }
    }
}
