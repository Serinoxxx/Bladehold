using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Applies the player's chosen class (persisted in <see cref="SaveData.playerClassId" />) at scene
///     load. Each class is a <see cref="ClassSlot" />: the weapon GameObjects to activate, the melee
///     <see cref="DamageTrigger" />/<see cref="SwordHitFeedback" /> the shared listeners get re-pointed
///     at, and the class-specific player components to enable.
///
///     Everything happens in <c>Awake</c>, strictly before any <c>Start</c>, because:
///     - only the active class's weapon may run <see cref="DamageTrigger" />'s Start — an inactive
///       GameObject never registers its melee stat bases, so two readsPlayerStats triggers can't
///       clobber each other's <see cref="StatType.SwordDamage" />/range/crit bases;
///     - listeners that subscribe to the melee trigger in Start (<see cref="VampiricBlade" />,
///       <see cref="ChainLightning" />, <see cref="ImpulseHitFeedback" />, <see cref="AnimationEvents" />,
///       <see cref="PlayerMount" />) must be re-pointed before they subscribe;
///     - <see cref="SkillTreeService" /> reads <see cref="ActiveClass" />'s skill tree in its own Start.
///
///     Class switching is reload-based — the reincarnate class select and the DevConsole cheat write
///     the save field via <see cref="SetSavedClass" /> and reload the scene. Never hot-swapped: stat
///     bases, event subscriptions, and animator state all assume the class is fixed for the scene's
///     lifetime.
/// </summary>
public class PlayerClassController : MonoBehaviour
{
    [Serializable]
    public class ClassSlot
    {
        [Tooltip("The class this slot wires up.")]
        public ClassDefinitionSO definition;

        [Tooltip("Weapon GameObjects activated for this class and deactivated for every other (e.g. the sword prefab instance under the hand bone). An inactive weapon never runs Start, so only the active one registers the shared melee stat bases.")]
        public GameObject[] weaponObjects;

        [Tooltip("This class's melee readsPlayerStats DamageTrigger — re-pointed onto AnimationEvents, VampiricBlade, ChainLightning, ImpulseHitFeedback, and PlayerMount.")]
        public DamageTrigger meleeTrigger;

        [Tooltip("This class's weapon SwordHitFeedback — re-pointed onto AnimationEvents.")]
        public SwordHitFeedback hitFeedback;

        [Tooltip("Class-specific player components enabled for this class and disabled for every other (Swordsman: PlayerBow, FreezingDraw; Berserker: PlayerThrownAxe, RageBuff, PainIntoPower). A disabled component never runs Start, so its stat bases stay unregistered — base 0 = locked.")]
        public Behaviour[] classComponents;
    }

    [SerializeField] private ClassSlot[] slots;

    [Header("Shared components re-pointed at the active class's weapon")]
    [SerializeField] private AnimationEvents animationEvents;
    [SerializeField] private PlayerAttack playerAttack;
    [Tooltip("Optional — only re-pointed when present.")]
    [SerializeField] private VampiricBlade vampiricBlade;
    [Tooltip("Optional — only re-pointed when present.")]
    [SerializeField] private ChainLightning chainLightning;
    [Tooltip("Optional — only re-pointed when present.")]
    [SerializeField] private ImpulseHitFeedback impulseHitFeedback;
    [Tooltip("Optional — only re-pointed when present.")]
    [SerializeField] private PlayerMount playerMount;
    [Tooltip("The player rig's Animator. Synty rigs keep it on a child.")]
    [SerializeField] private Animator animator;

    private string savedClassId;
    private bool usedFallbackSlot;
    private bool anyError = false;

    /// <summary>The class definition applied this scene load (null until Awake, or if no slots are wired).</summary>
    public ClassDefinitionSO ActiveClass { get; private set; }

    /// <summary>The active class's melee DamageTrigger.</summary>
    public DamageTrigger ActiveMeleeTrigger { get; private set; }

    /// <summary>
    ///     The active class's hold-aim weapon (the Swordsman's bow, the Berserker's thrown axe), if
    ///     any — the first <see cref="IChargedAimWeapon" /> in the active slot's classComponents. The
    ///     shared aim UI/camera fall back to this when their serialized PlayerBow is benched.
    /// </summary>
    public IChargedAimWeapon ActiveAimWeapon { get; private set; }

    /// <summary>All wired class slots, for the DevConsole class-switch cheat and the class-select UI.</summary>
    public IReadOnlyList<ClassSlot> Slots => slots ?? Array.Empty<ClassSlot>();

    /// <summary>
    ///     Persists the chosen class id; it takes effect on the next scene load. Used by the reincarnate
    ///     class select (just before the reload) and the DevConsole class-switch cheat.
    /// </summary>
    public static void SetSavedClass(string classId)
    {
        SaveData data = SaveSystem.Load();
        data.playerClassId = classId;
        SaveSystem.Save(data);
    }

    private void OnValidate()
    {
        if (animationEvents == null)
        {
            animationEvents = GetComponentInChildren<AnimationEvents>();
        }
        if (playerAttack == null)
        {
            playerAttack = GetComponentInChildren<PlayerAttack>();
        }
        if (vampiricBlade == null)
        {
            vampiricBlade = GetComponentInChildren<VampiricBlade>();
        }
        if (chainLightning == null)
        {
            chainLightning = GetComponentInChildren<ChainLightning>();
        }
        if (impulseHitFeedback == null)
        {
            impulseHitFeedback = GetComponentInChildren<ImpulseHitFeedback>();
        }
        if (playerMount == null)
        {
            playerMount = GetComponentInChildren<PlayerMount>();
        }
        if (animator == null)
        {
            // Synty rigs keep the Animator on a child model object.
            animator = GetComponentInChildren<Animator>();
        }
    }

    private void Awake()
    {
        if (slots == null || slots.Length == 0)
        {
            // Start reports the error; the prefab-authored (Swordsman) state stays as-is.
            return;
        }

        savedClassId = SaveSystem.Load().playerClassId;
        ClassSlot active = FindSlot(savedClassId);
        if (active == null)
        {
            active = slots[0];
            usedFallbackSlot = true;
        }

        ActiveClass = active.definition;
        ActiveMeleeTrigger = active.meleeTrigger;

        if (active.classComponents != null)
        {
            foreach (Behaviour component in active.classComponents)
            {
                if (component is IChargedAimWeapon aimWeapon)
                {
                    ActiveAimWeapon = aimWeapon;
                    break;
                }
            }
        }

        foreach (ClassSlot slot in slots)
        {
            if (slot == null)
            {
                continue;
            }
            bool isActive = slot == active;
            if (slot.weaponObjects != null)
            {
                foreach (GameObject weapon in slot.weaponObjects)
                {
                    if (weapon != null)
                    {
                        weapon.SetActive(isActive);
                    }
                }
            }
            if (slot.classComponents != null)
            {
                foreach (Behaviour component in slot.classComponents)
                {
                    if (component != null)
                    {
                        component.enabled = isActive;
                    }
                }
            }
        }

        // Re-point every shared melee-trigger listener before its Start subscribes.
        if (animationEvents != null && active.meleeTrigger != null)
        {
            animationEvents.SetMeleeTrigger(active.meleeTrigger);
        }
        if (animationEvents != null && active.hitFeedback != null)
        {
            animationEvents.SetHitFeedback(active.hitFeedback);
        }
        if (vampiricBlade != null && active.meleeTrigger != null)
        {
            vampiricBlade.SetSwordTrigger(active.meleeTrigger);
        }
        if (chainLightning != null && active.meleeTrigger != null)
        {
            chainLightning.SetSwordTrigger(active.meleeTrigger);
        }
        if (impulseHitFeedback != null && active.meleeTrigger != null)
        {
            impulseHitFeedback.SetDamageTrigger(active.meleeTrigger);
        }
        if (playerMount != null && active.meleeTrigger != null)
        {
            playerMount.SetSwordTrigger(active.meleeTrigger);
        }

        if (ActiveClass != null)
        {
            if (ActiveClass.animatorOverride != null && animator != null)
            {
                animator.runtimeAnimatorController = ActiveClass.animatorOverride;
            }
            if (playerAttack != null)
            {
                playerAttack.SetChargeTimePerLevel(ActiveClass.chargeTimePerLevel);
            }
        }
    }

    private void Start()
    {
        if (slots == null || slots.Length == 0)
        {
            Debug.LogError("PlayerClassController has no class slots wired; the prefab-authored weapon state is left untouched.");
            anyError = true;
            return;
        }

        if (usedFallbackSlot)
        {
            Debug.LogWarning($"PlayerClassController: saved class id '{savedClassId}' matches no slot; fell back to slot 0 ('{(slots[0]?.definition != null ? slots[0].definition.id : "<no definition>")}').");
        }

        if (ActiveClass == null)
        {
            Debug.LogError("PlayerClassController: the active slot has no ClassDefinitionSO assigned.");
            anyError = true;
        }
        if (ActiveMeleeTrigger == null)
        {
            Debug.LogError("PlayerClassController: the active slot has no melee DamageTrigger assigned — melee stat listeners are still pointing wherever the prefab left them.");
            anyError = true;
        }
        if (animationEvents == null)
        {
            Debug.LogError("PlayerClassController could not find AnimationEvents (set it or keep it on the player root).");
            anyError = true;
        }
        if (playerAttack == null)
        {
            Debug.LogError("PlayerClassController could not find PlayerAttack (set it or keep it on the player root).");
            anyError = true;
        }
    }

    private ClassSlot FindSlot(string classId)
    {
        if (string.IsNullOrEmpty(classId))
        {
            return null;
        }
        foreach (ClassSlot slot in slots)
        {
            if (slot != null && slot.definition != null && slot.definition.id == classId)
            {
                return slot;
            }
        }
        return null;
    }
}
