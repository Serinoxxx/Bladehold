using System;
using System.Collections.Generic;
using Synty.AnimationBaseLocomotion.Samples;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Manages the player's equipped melee and ranged weapons.
///     Applies the equipped loadout (from SaveData or run override) at scene load in Awake,
///     activating the correct weapon meshes, damage triggers, aim weapons, and animator parameters.
/// </summary>
public class PlayerWeaponManager : MonoBehaviour
{
    public static PlayerWeaponManager Instance { get; private set; }

    [Serializable]
    public struct MeleeWeaponSlot
    {
        public WeaponDefinitionSO definition;
        public GameObject weaponObject;
        public DamageTrigger damageTrigger;
        public SwordHitFeedback hitFeedback;
    }

    [Serializable]
    public struct RangedWeaponSlot
    {
        public WeaponDefinitionSO definition;
        public GameObject weaponObject;
        public Behaviour aimWeaponComponent; // Must implement IChargedAimWeapon
    }

    [Header("Weapon Loadout Slots")]
    public MeleeWeaponSlot[] meleeWeapons;
    public RangedWeaponSlot[] rangedWeapons;

    [Header("Shared Components")]
    [SerializeField] private AnimationEvents animationEvents;
    [SerializeField] private PlayerAttack playerAttack;
    [SerializeField] private Animator animator;
    [SerializeField] private VampiricBlade vampiricBlade;
    [SerializeField] private ChainLightning chainLightning;
    [SerializeField] private ImpulseHitFeedback impulseHitFeedback;
    [SerializeField] private PlayerMount playerMount;
    [SerializeField] private PlayerBow playerBow;
    [SerializeField] private PlayerThrownAxe playerThrownAxe;

    public static event Action OnWeaponLoadoutChanged;
    public event Action<WeaponDefinitionSO> OnMeleeChanged;
    public event Action<WeaponDefinitionSO> OnRangedChanged;

    [Header("Elemental VFX Prefabs")]
    [SerializeField] private GameObject fireWeaponVfxPrefab;
    [SerializeField] private GameObject iceWeaponVfxPrefab;
    [SerializeField] private GameObject lightningWeaponVfxPrefab;
    [SerializeField] private GameObject poisonWeaponVfxPrefab;

    private string currentMeleeId = "sword";
    private string currentRangedId = "bow";

    public string CurrentMeleeId => currentMeleeId;
    public string CurrentRangedId => currentRangedId;
    public DamageTrigger ActiveMeleeTrigger { get; private set; }
    public IChargedAimWeapon ActiveAimWeapon { get; private set; }
    public WeaponDefinitionSO ActiveMeleeDefinition { get; private set; }
    public WeaponDefinitionSO ActiveRangedDefinition { get; private set; }

    /// <summary>
    /// The melee weapon type animator parameter value currently active.
    /// Ranged weapons poll this to revert back to the correct animation stance after aiming.
    /// </summary>
    public int CurrentMeleeWeaponType => ActiveMeleeDefinition != null ? ActiveMeleeDefinition.animatorWeaponType : 0;

    private GameObject activeMeleeVfxInstance;
    private GameObject activeRangedVfxInstance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        AutoFindReferences();
        ApplySavedLoadout();
        
        RunSession.OnElementalSlotChanged -= HandleElementalSlotChanged;
        RunSession.OnElementalSlotChanged += HandleElementalSlotChanged;
    }

    private void Start()
    {
        ApplySavedLoadout();
        // Apply initial from session
        HandleElementalSlotChanged("SLOT_MELEE", RunSession.ElementalSlots.GetValueOrDefault("SLOT_MELEE", ""));
        HandleElementalSlotChanged("SLOT_RANGED", RunSession.ElementalSlots.GetValueOrDefault("SLOT_RANGED", ""));

        if (GameLoopManager.Instance != null)
        {
            GameLoopManager.Instance.OnEnemyKilledEvent += HandleEnemyKilled;
        }

        PlayerStats stats = GetComponent<PlayerStats>();
        if (stats != null)
        {
            stats.SetBase(StatType.AxeCelebratorySpinDuration, 0f);
            stats.SetBase(StatType.AxeFearDuration, 0f);
        }
    }

    private void HandleEnemyKilled(Health enemyHealth)
    {
        if (Player.Instance == null || Player.Instance.Stats == null) return;
        
        float duration = Player.Instance.Stats.GetValue(StatType.AxeCelebratorySpinDuration);
        if (duration > 0f && ActiveMeleeTrigger != null && !ActiveMeleeTrigger.IsWhirlwindActive)
        {
            StartCoroutine(CelebratorySpinRoutine(duration));
        }

        float fearDuration = Player.Instance.Stats.GetValue(StatType.AxeFearDuration);
        if (fearDuration > 0f && enemyHealth != null)
        {
            if (CurrentMeleeId.Equals("axe", StringComparison.OrdinalIgnoreCase))
            {
                Collider[] hits = Physics.OverlapSphere(enemyHealth.transform.position, 6f);
                HashSet<Health> affected = new HashSet<Health>();
                foreach (Collider hit in hits)
                {
                    Health targetHealth = hit.GetComponentInParent<Health>();
                    if (targetHealth != null && targetHealth != enemyHealth && !targetHealth.IsDead && (targetHealth.transform.root != Player.Instance.transform.root))
                    {
                        if (affected.Add(targetHealth))
                        {
                            SlowStatus.GetOrAdd(targetHealth)?.ApplySlow(1.0f, fearDuration);
                            if (targetHealth.TryGetComponent<NavMeshAgent>(out var agent) && agent.isOnNavMesh)
                            {
                                agent.velocity = Vector3.zero;
                            }
                        }
                    }
                }
            }
        }
    }

    private System.Collections.IEnumerator CelebratorySpinRoutine(float duration)
    {
        ActiveMeleeTrigger.StartWhirlwind();
        
        if (animator != null)
        {
            animator.ResetTrigger("StopWhirlwind");
            animator.SetTrigger("StartWhirlwind");
        }

        yield return new WaitForSeconds(duration);

        if (ActiveMeleeTrigger != null && ActiveMeleeTrigger.IsWhirlwindActive)
        {
            // Only stop if the ultimate didn't take over
            ActiveMeleeTrigger.StopWhirlwind();
            if (animator != null)
            {
                animator.ResetTrigger("StartWhirlwind");
                animator.SetTrigger("StopWhirlwind");
            }
        }
    }

    private void OnDestroy()
    {
        RunSession.OnElementalSlotChanged -= HandleElementalSlotChanged;
        if (GameLoopManager.Instance != null)
        {
            GameLoopManager.Instance.OnEnemyKilledEvent -= HandleEnemyKilled;
        }
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private GameObject GetVfxPrefabForElement(string elementId)
    {
        switch (elementId?.ToUpper())
        {
            case "FIRE": return fireWeaponVfxPrefab;
            case "ICE": return iceWeaponVfxPrefab;
            case "LIGHTNING": return lightningWeaponVfxPrefab;
            case "POISON": return poisonWeaponVfxPrefab;
            default: return null;
        }
    }

    private void HandleElementalSlotChanged(string slotName, string elementId)
    {
        if (slotName == "SLOT_MELEE")
        {
            if (activeMeleeVfxInstance != null) Destroy(activeMeleeVfxInstance);
            GameObject prefab = GetVfxPrefabForElement(elementId);
            if (prefab != null)
            {
                Transform parent = null;
                foreach (var slot in meleeWeapons)
                {
                    if (slot.definition != null && slot.definition.id == currentMeleeId && slot.weaponObject != null)
                    {
                        parent = slot.weaponObject.transform;
                        break;
                    }
                }
                
                if (parent != null)
                {
                    activeMeleeVfxInstance = Instantiate(prefab, parent);
                    activeMeleeVfxInstance.transform.localPosition = Vector3.zero;
                    activeMeleeVfxInstance.transform.localRotation = Quaternion.identity;
                }
            }
        }
        else if (slotName == "SLOT_RANGED")
        {
            if (activeRangedVfxInstance != null) Destroy(activeRangedVfxInstance);
            GameObject prefab = GetVfxPrefabForElement(elementId);
            if (prefab != null)
            {
                Transform parent = null;
                foreach (var slot in rangedWeapons)
                {
                    if (slot.definition != null && slot.definition.id == currentRangedId && slot.weaponObject != null)
                    {
                        parent = slot.weaponObject.transform;
                        break;
                    }
                }

                if (parent != null)
                {
                    activeRangedVfxInstance = Instantiate(prefab, parent);
                    activeRangedVfxInstance.transform.localPosition = Vector3.zero;
                    activeRangedVfxInstance.transform.localRotation = Quaternion.identity;
                }
            }
        }
    }

    private void AutoFindReferences()
    {
        if (animationEvents == null) animationEvents = GetComponentInChildren<AnimationEvents>();
        if (playerAttack == null) playerAttack = GetComponentInChildren<PlayerAttack>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (vampiricBlade == null) vampiricBlade = GetComponentInChildren<VampiricBlade>();
        if (chainLightning == null) chainLightning = GetComponentInChildren<ChainLightning>();
        if (impulseHitFeedback == null) impulseHitFeedback = GetComponentInChildren<ImpulseHitFeedback>();
        if (playerMount == null) playerMount = GetComponentInChildren<PlayerMount>();
        if (playerBow == null) playerBow = GetComponentInChildren<PlayerBow>(true);
        if (playerThrownAxe == null) playerThrownAxe = GetComponentInChildren<PlayerThrownAxe>(true);
    }

    /// <summary>
    ///     Reads equipped weapons from SaveData and configures player weapon components.
    /// </summary>
    public void ApplySavedLoadout()
    {
        SaveData save = SaveSystem.Load();
        string meleeId = save != null && !string.IsNullOrEmpty(save.equippedMeleeWeapon) ? save.equippedMeleeWeapon.ToLower() : "sword";
        string rangedId = save != null && !string.IsNullOrEmpty(save.equippedRangedWeapon) ? save.equippedRangedWeapon.ToLower() : "bow";

        EquipMelee(meleeId);
        EquipRanged(rangedId);

        if (!string.IsNullOrEmpty(RunSession.ActiveUltimateId))
        {
            DraftUpgradeService.ConfigureUltimateHandler(GetComponent<Player>() ?? Player.Instance, RunSession.ActiveUltimateId);
        }
    }

    public void EquipMelee(string weaponId)
    {
        currentMeleeId = weaponId;
        AutoFindReferences();
        
        foreach (var slot in meleeWeapons)
        {
            bool isActive = slot.definition != null && string.Equals(slot.definition.id, weaponId, StringComparison.OrdinalIgnoreCase);
            
            if (slot.weaponObject != null)
            {
                slot.weaponObject.SetActive(isActive);
            }

            if (isActive)
            {
                ActiveMeleeDefinition = slot.definition;
                ActiveMeleeTrigger = slot.damageTrigger;
                
                // Re-point shared listeners onto the active melee trigger
                if (animationEvents != null)
                {
                    if (ActiveMeleeTrigger != null) animationEvents.SetMeleeTrigger(ActiveMeleeTrigger);
                    if (slot.hitFeedback != null) animationEvents.SetHitFeedback(slot.hitFeedback);
                }
                if (vampiricBlade != null && ActiveMeleeTrigger != null)
                {
                    vampiricBlade.SetSwordTrigger(ActiveMeleeTrigger);
                }
                if (chainLightning != null && ActiveMeleeTrigger != null)
                {
                    chainLightning.SetSwordTrigger(ActiveMeleeTrigger);
                }
                if (impulseHitFeedback != null && ActiveMeleeTrigger != null)
                {
                    impulseHitFeedback.SetDamageTrigger(ActiveMeleeTrigger);
                }
                if (playerMount != null && ActiveMeleeTrigger != null)
                {
                    playerMount.SetSwordTrigger(ActiveMeleeTrigger);
                }
                
                var periodicImbuements = GetComponent<PeriodicImbuementController>();
                if (periodicImbuements != null && ActiveMeleeTrigger != null)
                {
                    periodicImbuements.SetMeleeTrigger(ActiveMeleeTrigger);
                }

                // Configure PlayerAttack charge time and animator weapon type
                if (playerAttack != null)
                {
                    playerAttack.SetChargeTimePerLevel(ActiveMeleeDefinition.chargeTimePerLevel);
                }

                var animController = GetComponentInChildren<Synty.AnimationBaseLocomotion.Samples.SamplePlayerAnimationController>();
                if (animController != null && ActiveMeleeDefinition.attackCooldown > 0f)
                {
                    animController.AttackCooldown = ActiveMeleeDefinition.attackCooldown;
                }
                
                ApplyAnimatorWeaponType(ActiveMeleeDefinition.animatorWeaponType);

                if (playerBow != null)
                {
                    playerBow.SetMeleeWeaponModel(slot.weaponObject);
                }
                if (playerThrownAxe != null)
                {
                    playerThrownAxe.SetMeleeWeaponModel(slot.weaponObject);
                }
            }
        }

        HandleElementalSlotChanged("SLOT_MELEE", RunSession.ElementalSlots.GetValueOrDefault("SLOT_MELEE", ""));
        OnMeleeChanged?.Invoke(ActiveMeleeDefinition);
        OnWeaponLoadoutChanged?.Invoke();
    }

    private void ApplyAnimatorWeaponType(int typeInt)
    {
        if (animator == null) return;
        
        int meleeWeaponTypeHash = Animator.StringToHash("MeleeWeaponType");
        int weaponTypeHash = Animator.StringToHash("WeaponType");

        foreach (var p in animator.parameters)
        {
            if (p.nameHash == meleeWeaponTypeHash || p.nameHash == weaponTypeHash)
            {
                animator.SetInteger(p.nameHash, typeInt);
            }
        }
    }

    public void EquipRanged(string weaponId)
    {
        currentRangedId = weaponId;
        AutoFindReferences();
        
        foreach (var slot in rangedWeapons)
        {
            bool isActive = slot.definition != null && string.Equals(slot.definition.id, weaponId, StringComparison.OrdinalIgnoreCase);
            
            // The weapon component itself handles its own visuals/mesh via OnEnable/OnDisable
            // We just need to enable/disable the script (Behaviour) that drives it.
            if (slot.aimWeaponComponent != null)
            {
                slot.aimWeaponComponent.enabled = isActive;
            }

            if (isActive)
            {
                ActiveRangedDefinition = slot.definition;
                ActiveAimWeapon = slot.aimWeaponComponent as IChargedAimWeapon;
            }
        }

        HandleElementalSlotChanged("SLOT_RANGED", RunSession.ElementalSlots.GetValueOrDefault("SLOT_RANGED", ""));
        OnRangedChanged?.Invoke(ActiveRangedDefinition);
        OnWeaponLoadoutChanged?.Invoke();
    }
}
