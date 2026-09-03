using System;
using System.Collections.Generic;
using Synty.AnimationBaseLocomotion.Samples;
using UnityEngine;

/// <summary>
///     Manages the player's equipped melee and ranged weapons.
///     Applies the equipped loadout (from SaveData or run override) at scene load in Awake,
///     activating the correct weapon meshes, damage triggers, aim weapons, and animator parameters.
/// </summary>
public class PlayerWeaponManager : MonoBehaviour
{
    public static PlayerWeaponManager Instance { get; private set; }

    [Header("Melee Weapons")]
    [SerializeField] private GameObject swordObject;
    [SerializeField] private DamageTrigger swordTrigger;
    [SerializeField] private SwordHitFeedback swordHitFeedback;

    [SerializeField] private GameObject axeObject;
    [SerializeField] private DamageTrigger axeTrigger;
    [SerializeField] private SwordHitFeedback axeHitFeedback;

    [Header("Ranged Weapons")]
    [SerializeField] private PlayerBow playerBow;
    [SerializeField] private PlayerThrownAxe playerThrownAxe;

    [Header("Shared Components")]
    [SerializeField] private AnimationEvents animationEvents;
    [SerializeField] private PlayerAttack playerAttack;
    [SerializeField] private Animator animator;
    [SerializeField] private VampiricBlade vampiricBlade;
    [SerializeField] private ChainLightning chainLightning;
    [SerializeField] private ImpulseHitFeedback impulseHitFeedback;

    private string currentMeleeId = "sword";
    private string currentRangedId = "bow";

    public string CurrentMeleeId => currentMeleeId;
    public string CurrentRangedId => currentRangedId;
    public DamageTrigger ActiveMeleeTrigger { get; private set; }
    public IChargedAimWeapon ActiveAimWeapon { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        AutoFindReferences();
        ApplySavedLoadout();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
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

        if (playerBow == null) playerBow = GetComponentInChildren<PlayerBow>(true);
        if (playerThrownAxe == null) playerThrownAxe = GetComponentInChildren<PlayerThrownAxe>(true);

        if (swordObject == null)
        {
            Transform t = transform.Find("SidekickSyntyCharacter/Root/Hips/Spine_01/Spine_02/Spine_03/Clavicle_R/Shoulder_R/Elbow_R/Hand_R/1H_Sword");
            if (t != null)
            {
                swordObject = t.gameObject;
                swordTrigger = swordObject.GetComponent<DamageTrigger>();
                swordHitFeedback = swordObject.GetComponent<SwordHitFeedback>();
            }
        }

        if (axeObject == null)
        {
            Transform t = transform.Find("SidekickSyntyCharacter/Root/Hips/Spine_01/Spine_02/Spine_03/Clavicle_R/Shoulder_R/Elbow_R/Hand_R/2H_Axe");
            if (t != null)
            {
                axeObject = t.gameObject;
                axeTrigger = axeObject.GetComponent<DamageTrigger>();
                axeHitFeedback = axeObject.GetComponent<SwordHitFeedback>();
            }
        }
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
        bool isAxe = string.Equals(weaponId, "axe", StringComparison.OrdinalIgnoreCase);

        if (swordObject != null) swordObject.SetActive(!isAxe);
        if (axeObject != null) axeObject.SetActive(isAxe);

        ActiveMeleeTrigger = isAxe ? axeTrigger : swordTrigger;
        SwordHitFeedback activeFeedback = isAxe ? axeHitFeedback : swordHitFeedback;

        // Re-point shared listeners onto the active melee trigger
        if (animationEvents != null)
        {
            if (ActiveMeleeTrigger != null) animationEvents.SetMeleeTrigger(ActiveMeleeTrigger);
            if (activeFeedback != null) animationEvents.SetHitFeedback(activeFeedback);
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

        // Configure PlayerAttack charge time and animator weapon type
        if (playerAttack != null)
        {
            playerAttack.SetChargeTimePerLevel(isAxe ? 0.45f : 0.33f);
        }

        if (animator != null)
        {
            int paramHash = Animator.StringToHash("MeleeWeaponType");
            foreach (var p in animator.parameters)
            {
                if (p.nameHash == paramHash)
                {
                    animator.SetInteger(paramHash, isAxe ? 1 : 0);
                    break;
                }
            }
        }
    }

    public void EquipRanged(string weaponId)
    {
        currentRangedId = weaponId;
        bool isAxe = string.Equals(weaponId, "throwing_axe", StringComparison.OrdinalIgnoreCase);

        if (playerBow != null)
        {
            playerBow.enabled = !isAxe;
        }
        if (playerThrownAxe != null)
        {
            playerThrownAxe.enabled = isAxe;
        }

        ActiveAimWeapon = isAxe ? (IChargedAimWeapon)playerThrownAxe : playerBow;
    }
}
