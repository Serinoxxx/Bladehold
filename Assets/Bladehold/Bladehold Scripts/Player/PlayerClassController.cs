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

    /// <summary>True if the active class's aim weapon exists and has been unlocked in the skill tree / progression.</summary>
    public bool IsAimWeaponUnlocked => ActiveAimWeapon != null && ActiveAimWeapon.IsUnlocked;

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

        var periodicImbuements = GetComponent<PeriodicImbuementController>();
        if (periodicImbuements != null && active.meleeTrigger != null)
        {
            periodicImbuements.SetMeleeTrigger(active.meleeTrigger);
        }

        if (ActiveClass != null)
        {
            if (ActiveClass.animatorOverride != null && animator != null)
            {
                animator.runtimeAnimatorController = ActiveClass.animatorOverride;
            }
            if (ActiveClass.characterModelPrefab != null && animator != null)
            {
                SwapCharacterModel(ActiveClass.characterModelPrefab);
            }
            if (playerAttack != null)
            {
                playerAttack.SetChargeTimePerLevel(ActiveClass.chargeTimePerLevel);
            }
            var animController = GetComponentInChildren<Synty.AnimationBaseLocomotion.Samples.SamplePlayerAnimationController>();
            if (animController != null && ActiveClass.meleeAttackCooldown > 0f)
            {
                animController.AttackCooldown = ActiveClass.meleeAttackCooldown;
            }
            ApplyMeleeWeaponType();
        }
    }

    private void ApplyMeleeWeaponType()
    {
        if (ActiveClass == null || animator == null)
        {
            return;
        }

        int meleeWeaponTypeHash = Animator.StringToHash("MeleeWeaponType");
        int weaponTypeHash = Animator.StringToHash("WeaponType");

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if ((parameter.nameHash == meleeWeaponTypeHash || parameter.nameHash == weaponTypeHash) && parameter.type == AnimatorControllerParameterType.Int)
            {
                animator.SetInteger(parameter.nameHash, ActiveClass.meleeWeaponType);
            }
        }
    }

    /// <summary>
    ///     Swaps the visible character onto the shared rig: every SkinnedMeshRenderer in the class's
    ///     model prefab is re-bound onto the existing skeleton by bone name (Synty Sidekicks share the
    ///     base rig; outfit-only bones like cape danglers are grafted on under their same-named parent)
    ///     and parented under the Animator, then the authored model's renderers are disabled. Nothing else moves — the Animator, animation events, weapon bones,
    ///     and camera targets all stay exactly as wired. Runs in Awake; the class is fixed for the
    ///     scene's lifetime, so the swap is never undone (the reload-based switching rule).
    /// </summary>
    private void SwapCharacterModel(GameObject modelPrefab)
    {
        Transform rigRoot = animator.transform;

        var bonesByName = new Dictionary<string, Transform>();
        foreach (Transform bone in rigRoot.GetComponentsInChildren<Transform>(true))
        {
            bonesByName[bone.name] = bone;
        }

        // Captured before the new renderers arrive so only the authored model gets hidden.
        SkinnedMeshRenderer[] authoredRenderers = rigRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);

        GameObject instance = Instantiate(modelPrefab);
        int swapped = 0;
        foreach (SkinnedMeshRenderer renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Transform[] sourceBones = renderer.bones;
            Transform[] mappedBones = new Transform[sourceBones.Length];
            bool allBonesFound = true;
            for (int i = 0; i < sourceBones.Length; i++)
            {
                if (sourceBones[i] == null)
                {
                    allBonesFound = false;
                    break;
                }
                if (!bonesByName.TryGetValue(sourceBones[i].name, out mappedBones[i]))
                {
                    // Outfit-specific bones (cape/armour danglers like abac_dyn_*) don't exist on
                    // the base Sidekick rig — graft the missing subtree onto its same-named parent
                    // bone. Ungrafted, they just ride along with that parent (no Animator input).
                    if (!TryGraftBone(sourceBones[i], bonesByName))
                    {
                        allBonesFound = false;
                        break;
                    }
                    mappedBones[i] = bonesByName[sourceBones[i].name];
                }
            }
            if (!allBonesFound)
            {
                Debug.LogWarning($"PlayerClassController: renderer '{renderer.name}' on class model '{modelPrefab.name}' references bones the player rig doesn't have (and can't graft) — skipped. Class models must share the rig's base skeleton (Synty Sidekicks do).");
                continue;
            }

            renderer.bones = mappedBones;
            if (renderer.rootBone != null && bonesByName.TryGetValue(renderer.rootBone.name, out Transform mappedRoot))
            {
                renderer.rootBone = mappedRoot;
            }

            Transform rendererTransform = renderer.transform;
            rendererTransform.SetParent(rigRoot, false);
            rendererTransform.localPosition = Vector3.zero;
            rendererTransform.localRotation = Quaternion.identity;
            rendererTransform.localScale = Vector3.one;
            renderer.gameObject.SetActive(true);
            swapped++;
        }
        // Whatever's left of the instantiated prefab is just its now-meshless skeleton.
        Destroy(instance);

        if (swapped == 0)
        {
            Debug.LogError($"PlayerClassController: no SkinnedMeshRenderer in class model '{modelPrefab.name}' could bind to the player rig — the authored model stays visible.");
            return;
        }

        // Disabled rather than destroyed: the prefab instance keeps its authored state, and other
        // components on those GameObjects are untouched.
        foreach (SkinnedMeshRenderer renderer in authoredRenderers)
        {
            renderer.enabled = false;
        }
    }

    /// <summary>
    ///     Moves a bone subtree the rig is missing onto the rig, under its same-named parent bone,
    ///     preserving local transforms — the skeleton proportions are identical, so the grafted
    ///     bones land in exactly their authored pose. Grafts the topmost missing ancestor so a
    ///     whole dangler chain moves as one piece. Registers every grafted transform in the map.
    /// </summary>
    private static bool TryGraftBone(Transform missing, Dictionary<string, Transform> bonesByName)
    {
        Transform top = missing;
        while (top.parent != null && !bonesByName.ContainsKey(top.parent.name))
        {
            top = top.parent;
        }
        if (top.parent == null)
        {
            return false;
        }

        top.SetParent(bonesByName[top.parent.name], false);
        foreach (Transform grafted in top.GetComponentsInChildren<Transform>(true))
        {
            if (!bonesByName.ContainsKey(grafted.name))
            {
                bonesByName[grafted.name] = grafted;
            }
        }
        return true;
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

        ApplyMeleeWeaponType();
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
