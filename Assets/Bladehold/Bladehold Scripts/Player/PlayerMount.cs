using System;
using System.Collections.Generic;
using System.Reflection;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
///     The player's side of horse riding. Mounting happens when the player jumps into a riderless
///     horse's <see cref="HorseMountable" /> trigger (gated on <see cref="CanRide" /> — the gold
///     tree's "Saddle Up" node OR the Reincarnate "Cavalier" node, since Reincarnate wipes the gold
///     tree); the character is parented to the saddle, the vendored controller and friends are
///     disabled (the <see cref="PlayerDeath" /> inspector-list idiom), and <see cref="HorseMotor" />
///     takes over movement. Dismount on the Dismount action (X / gamepad East — with a direct
///     X-key fallback until the vendored input class is regenerated), when the horse dies, or when
///     the player dies.
///
///     While mounted the player takes NO damage: a <see cref="Health.TryBlockDamage" /> handler
///     (subscribed in <c>Awake</c>, ahead of <see cref="DamageBlocker" />/<see cref="Parry" /> which
///     subscribe in Start, so a mounted hit can't burn a Solid cooldown) negates every hit and
///     re-lands it on the horse's Health — the horse is the shield, and its death at 0 HP
///     auto-dismounts the rider beside the corpse.
///
///     Mounted combat: the sword keeps swinging (via <see cref="MountedCombat" />) with extra,
///     non-visual reach (<see cref="DamageTrigger.SetReachBonus" /> — saddle height would otherwise
///     put grounded enemies outside the arc) and never carves the horse
///     (<see cref="DamageTrigger.SetIgnoredTarget" /> / <see cref="PlayerBow.SetIgnoredTarget" />).
/// </summary>
public class PlayerMount : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private PlayerStats stats;
    [SerializeField] private InputReader inputReader;
    [SerializeField] private CharacterController characterController;
    [Tooltip("The player rig's Animator. Synty rigs keep it on a child.")]
    [SerializeField] private Animator animator;

    [Tooltip("Control components disabled while mounted (the PlayerDeath idiom): the vendored Synty controller, CombatFacing, AttackCancelsSprint, … Keep InputReader, PlayerAttack, and PlayerBow OUT of this list — they stay live for mounted combat.")]
    [SerializeField] private MonoBehaviour[] componentsToDisableWhileMounted;

    [Tooltip("The player's sword hitbox (explicit — the player has other DamageTriggers, the VampiricBlade precedent). Gets the mounted reach bonus and ignores the horse.")]
    [SerializeField] private DamageTrigger swordTrigger;

    [Tooltip("Optional: the player's bow, so mounted arrows fly through the horse.")]
    [SerializeField] private PlayerBow bow;

    [Tooltip("Extra sword reach while mounted, as a fraction of blade length (0.6 = 60% longer sweep). No visual change.")]
    [SerializeField] private float mountedReachBonus = 0.6f;

    [Header("Animator params (optional wiring)")]
    [SerializeField] private string isMountedBool = "IsMounted";
    [SerializeField] private string horseSpeedFloat = "HorseSpeed";
    [SerializeField] private string ridingLayerName = "Riding";

    /// <summary>Raised with true on mount, false on dismount — for cosmetic listeners (camera, UI).</summary>
    public event Action<bool> OnMountedChanged;

    /// <summary>True while seated on a horse.</summary>
    public bool IsMounted => currentHorse != null;

    /// <summary>The horse being ridden, or null.</summary>
    public HorseMotor CurrentHorse => currentHorse;

    /// <summary>The player's CharacterController, for <see cref="HorseMountable" />'s airborne check.</summary>
    public CharacterController CharacterController => characterController;

    /// <summary>
    ///     Riding is allowed with the gold tree's "Saddle Up" node OR the Reincarnate "Cavalier"
    ///     node — the code-side OR is what keeps Cavalier working after a Reincarnate wipes every
    ///     gold-tree modifier.
    /// </summary>
    public bool CanRide => !anyError
        && (stats.GetValue(StatType.HorseRidingUnlocked) >= 1f || stats.GetValue(StatType.StartMounted) >= 1f);

    private HorseMotor currentHorse;
    private Health currentHorseHealth;
    private HorseMountable currentMountable;
    private HorsePickupProxy currentProxy;

    private Transform originalParent;
    private Vector3 originalLocalScale;

    /// <summary>Horses whose max health already got the Barded Steed multiplier — applied once per horse, not per mount.</summary>
    private readonly HashSet<Health> scaledHorses = new HashSet<Health>();

    private int isMountedHash;
    private int horseSpeedHash;
    private bool hasIsMountedParam;
    private bool hasHorseSpeedParam;
    private int ridingLayerIndex = -1;

    private bool hasDismountAction;
    private bool anyError = false;

    /// <summary>
    ///     Re-points at the active class's melee DamageTrigger. Called by
    ///     <see cref="PlayerClassController" /> in Awake — mounting (which applies the reach bonus and
    ///     ignored-target to this trigger) can only happen later, during gameplay.
    /// </summary>
    public void SetSwordTrigger(DamageTrigger trigger)
    {
        swordTrigger = trigger;
    }

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (stats == null)
        {
            stats = GetComponent<PlayerStats>();
        }
        if (inputReader == null)
        {
            inputReader = GetComponentInChildren<InputReader>();
        }
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }
        if (animator == null)
        {
            // Synty rigs keep the Animator on a child model object.
            animator = GetComponentInChildren<Animator>();
        }
        if (bow == null)
        {
            bow = GetComponent<PlayerBow>();
        }
    }

    private void Awake()
    {
        // Subscribed in Awake so this handler sits FIRST in the invocation list, ahead of
        // DamageBlocker/Parry (which subscribe in Start) — a mounted hit must forward to the horse,
        // not burn a Solid block cooldown.
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (health != null)
        {
            health.TryBlockDamage += HandleIncomingDamage;
        }
    }

    private void Start()
    {
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (stats == null)
        {
            Debug.LogError("PlayerStats component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (inputReader == null)
        {
            Debug.LogError("InputReader is not assigned or found; the mount can't read dismount input.");
            anyError = true;
        }
        if (characterController == null)
        {
            Debug.LogError("CharacterController component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (animator == null)
        {
            Debug.LogError("Player Animator is not assigned or found on a child.");
            anyError = true;
        }
        if (swordTrigger == null)
        {
            Debug.LogError("Sword DamageTrigger is not assigned in the inspector (explicit — the player has other DamageTriggers).");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        // Riding/mount stats: unlock stats at base 0 (locked), multiplier stats at base 1 (the
        // MoveSpeed convention). HorseArcheryUnlocked is registered by PlayerBow with the other bow
        // bases; skill nodes layer modifiers on all of these.
        stats.SetBase(StatType.HorseRidingUnlocked, 0f);
        stats.SetBase(StatType.StartMounted, 0f);
        stats.SetBase(StatType.HorseMaxHealthMultiplier, 1f);
        stats.SetBase(StatType.HorseSpeedMultiplier, 1f);
        stats.SetBase(StatType.HorseHealFromPacks, 0f);

        isMountedHash = Animator.StringToHash(isMountedBool);
        horseSpeedHash = Animator.StringToHash(horseSpeedFloat);
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == isMountedHash)
            {
                hasIsMountedParam = true;
            }
            else if (parameter.nameHash == horseSpeedHash)
            {
                hasHorseSpeedParam = true;
            }
        }
        ridingLayerIndex = animator.GetLayerIndex(ridingLayerName);
        if (!hasIsMountedParam && ridingLayerIndex < 0)
        {
            Debug.LogWarning("PlayerMount: the player Animator has neither an IsMounted (Bool) parameter nor a Riding layer — the seated pose won't play until the animator is wired (see TODO.md).");
        }

        hasDismountAction = FindDismountAction();
        if (!hasDismountAction)
        {
            Debug.LogWarning("PlayerMount: no 'Dismount' action found on the vendored Controls asset (has its C# class been regenerated?) — falling back to a direct X-key read, keyboard only.");
        }

        health.OnDied += HandlePlayerDied;
        inputReader.onDismountPerformed += HandleDismountPressed;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.TryBlockDamage -= HandleIncomingDamage;
            health.OnDied -= HandlePlayerDied;
        }
        if (inputReader != null)
        {
            inputReader.onDismountPerformed -= HandleDismountPressed;
        }
        if (currentHorseHealth != null)
        {
            currentHorseHealth.OnDied -= HandleHorseDied;
        }
    }

    /// <summary>
    ///     Checks the live input asset for a Dismount action, reaching the vendored InputReader's
    ///     private <c>_controls</c> by reflection (the <see cref="InputSettingsBinder" /> precedent).
    ///     False until the Controls C# class is regenerated from the edited .inputactions.
    /// </summary>
    private bool FindDismountAction()
    {
        FieldInfo controlsField = typeof(InputReader).GetField("_controls", BindingFlags.Instance | BindingFlags.NonPublic);
        if (controlsField == null)
        {
            return false;
        }
        if (controlsField.GetValue(inputReader) is IInputActionCollection2 controls)
        {
            return controls.FindAction("Dismount") != null;
        }
        return false;
    }

    private void Update()
    {
        if (anyError || !IsMounted) return;

        // Keyboard fallback until the regenerated Controls class routes the Dismount action
        // through InputReader (see FindDismountAction).
        if (!hasDismountAction && Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame)
        {
            Dismount();
            return;
        }

        if (hasHorseSpeedParam && currentHorse != null)
        {
            animator.SetFloat(horseSpeedHash, currentHorse.NormalizedSpeed);
        }
    }

    private void HandleDismountPressed()
    {
        if (anyError || !IsMounted) return;
        Dismount();
    }

    /// <summary>
    ///     While mounted the player takes nothing — the hit re-lands on the horse (which dies,
    ///     eventually, in the player's place). Health snapshots the invocation list, so the
    ///     dismount a lethal forwarded hit triggers (via the horse's OnDied) can safely run inside
    ///     this handler.
    /// </summary>
    private bool HandleIncomingDamage(Damage damage)
    {
        if (!IsMounted)
        {
            return false;
        }

        if (currentHorseHealth != null && !currentHorseHealth.IsDead)
        {
            currentHorseHealth.ReceiveDamage(damage);
        }
        return true;
    }

    /// <summary>
    ///     Seats the player on <paramref name="horse" />. Called by <see cref="HorseMountable" />
    ///     (jump-mount) and <see cref="StartMountedSpawner" /> (the Cavalier node). Returns false if
    ///     riding is locked, the horse is dead/occupied-by-knight, or it has no seat.
    /// </summary>
    public bool TryMount(HorseMotor horse)
    {
        if (anyError || IsMounted || !CanRide || horse == null) return false;

        Health horseHealth = horse.Health;
        if (horseHealth == null || horseHealth.IsDead) return false;

        Transform seat = horse.RiderSeat;
        if (seat == null)
        {
            Debug.LogWarning("PlayerMount: the horse has no RiderSeat transform; cannot mount.", horse);
            return false;
        }

        currentHorse = horse;
        currentHorseHealth = horseHealth;
        currentMountable = horse.GetComponentInChildren<HorseMountable>(true);
        currentProxy = horse.GetComponent<HorsePickupProxy>();

        // Park the on-foot body: controls off (the PlayerDeath idiom), controller off, snap to the
        // saddle. The camera follows for free — PlayerCameraPivot tracks a child of this transform.
        foreach (MonoBehaviour component in componentsToDisableWhileMounted)
        {
            if (component != null)
            {
                component.enabled = false;
            }
        }
        characterController.enabled = false;

        originalParent = transform.parent;
        originalLocalScale = transform.localScale;
        transform.SetParent(seat, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        if (hasIsMountedParam)
        {
            animator.SetBool(isMountedHash, true);
        }
        if (ridingLayerIndex >= 0)
        {
            animator.SetLayerWeight(ridingLayerIndex, 1f);
        }

        // Hand the horse over: reins to this player's input, saddle occupied, pickups redirected,
        // trample must never hit the rider.
        horse.SetRider(inputReader, Player.Instance != null ? Player.Instance.Damageable : null);
        if (currentMountable != null)
        {
            currentMountable.SetOccupied(true);
        }
        if (currentProxy != null)
        {
            currentProxy.SetRider(gameObject);
        }

        // Barded Steed: once per horse, fraction-preserving (a wounded horse doesn't heal from
        // the multiplier, its ceiling just rises).
        if (scaledHorses.Add(horseHealth))
        {
            horseHealth.ScaleMaxHealth(stats.GetValue(StatType.HorseMaxHealthMultiplier));
        }

        currentHorseHealth.OnDied += HandleHorseDied;

        // Mounted sword: longer sweep (no visual), and neither blade nor arrows carve the mount.
        IDamageable horseDamageable = horseHealth;
        swordTrigger.SetReachBonus(mountedReachBonus);
        swordTrigger.SetIgnoredTarget(horseDamageable);
        if (bow != null)
        {
            bow.SetIgnoredTarget(horseDamageable);
        }

        horse.TriggerRear();
        OnMountedChanged?.Invoke(true);
        return true;
    }

    /// <summary>
    ///     Unseats the player (X, horse death, or player death) beside the horse. Controls are only
    ///     re-enabled while alive — a dead player's dismount must never undo PlayerDeath's disable
    ///     list, whatever the OnDied listener order.
    /// </summary>
    public void Dismount()
    {
        if (!IsMounted) return;

        HorseMotor horse = currentHorse;
        Health horseHealth = currentHorseHealth;

        currentHorseHealth.OnDied -= HandleHorseDied;

        horse.ClearRider();
        if (currentProxy != null)
        {
            currentProxy.ClearRider();
        }
        // A dead horse stays unmountable through its own IsDead check; only free a live saddle.
        if (currentMountable != null && !horseHealth.IsDead)
        {
            currentMountable.SetOccupied(false);
        }

        swordTrigger.SetReachBonus(0f);
        swordTrigger.SetIgnoredTarget(null);
        if (bow != null)
        {
            bow.SetIgnoredTarget(null);
        }

        transform.SetParent(originalParent, true);
        transform.localScale = originalLocalScale;
        transform.position = ResolveDismountPosition(horse);
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        if (hasIsMountedParam)
        {
            animator.SetBool(isMountedHash, false);
        }
        if (ridingLayerIndex >= 0)
        {
            animator.SetLayerWeight(ridingLayerIndex, 0f);
        }
        if (hasHorseSpeedParam)
        {
            animator.SetFloat(horseSpeedHash, 0f);
        }

        if (!health.IsDead)
        {
            characterController.enabled = true;
            foreach (MonoBehaviour component in componentsToDisableWhileMounted)
            {
                if (component != null)
                {
                    component.enabled = true;
                }
            }
        }

        currentHorse = null;
        currentHorseHealth = null;
        currentMountable = null;
        currentProxy = null;

        OnMountedChanged?.Invoke(false);
    }

    /// <summary>Lands beside the horse, trying the off side / behind when the preferred side is blocked.</summary>
    private Vector3 ResolveDismountPosition(HorseMotor horse)
    {
        Vector3 offset = horse.DismountLocalOffset;
        Vector3[] candidates =
        {
            horse.transform.TransformPoint(offset),
            horse.transform.TransformPoint(new Vector3(-offset.x, offset.y, offset.z)),
            horse.transform.TransformPoint(new Vector3(0f, offset.y, -Mathf.Max(2f, Mathf.Abs(offset.x)))),
        };

        float radius = characterController.radius;
        float height = Mathf.Max(characterController.height, radius * 2f);
        foreach (Vector3 candidate in candidates)
        {
            Vector3 bottom = candidate + Vector3.up * radius;
            Vector3 top = candidate + Vector3.up * (height - radius);
            if (!Physics.CheckCapsule(bottom, top, radius * 0.9f, ~0, QueryTriggerInteraction.Ignore))
            {
                return candidate;
            }
        }
        return candidates[0];
    }

    private void HandleHorseDied()
    {
        // The horse's own HorseAnimation plays the death; the rider just gets off the corpse.
        Dismount();
    }

    private void HandlePlayerDied()
    {
        if (IsMounted)
        {
            Dismount();
        }
    }
}
