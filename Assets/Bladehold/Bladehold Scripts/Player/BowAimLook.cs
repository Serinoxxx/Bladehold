using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using UnityEngine;

/// <summary>
///     Bends the character's spine toward the camera's aim point while the bow is drawn, so the
///     upper body (and the bow) visibly points where the crosshair does. Yaw needs no help — the
///     vendored Synty controller switches to strafe mode while aiming, turning the whole character
///     to the camera heading — so this only adds the missing pitch: in <c>LateUpdate</c> (after the
///     Animator has posed the rig) the camera's pitch angle is distributed additively across the
///     Humanoid spine/chest/upper-chest bones (found via <c>Animator.GetBoneTransform</c>, the
///     <see cref="EnemyRagdoll" /> idiom), blended in and out over <see cref="BowSO.aimBlendSeconds" />
///     in step with <see cref="BowAimCamera" />. Purely cosmetic — arrows already fly to the camera
///     centre (<see cref="PlayerBow" />'s hitscan), this makes the pose agree with them.
/// </summary>
public class BowAimLook : MonoBehaviour
{
    [SerializeField] private PlayerBow bow;
    [SerializeField] private BowSO config;
    [Tooltip("The player rig's Humanoid Animator. Synty rigs keep it on a child.")]
    [SerializeField] private Animator animator;
    [Tooltip("Camera whose pitch the spine follows. Defaults to Camera.main.")]
    [SerializeField] private Camera aimCamera;
    [SerializeField] private InputReader inputReader;

    [Tooltip("Seconds after an attack press the pitch lingers, so a quick click still covers the swing.")]
    [SerializeField] private float attackLingerSeconds = 0.8f;

    /// <summary>Fraction of the pitch each spine bone absorbs, root-most first (spine, chest, upper chest) — renormalized over the bones the rig actually has.</summary>
    private static readonly float[] BoneWeights = { 0.25f, 0.35f, 0.4f };
    private static readonly HumanBodyBones[] Bones = { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest };

    private Transform[] spineBones;
    private float[] spineWeights;

    private float blend;
    private bool attackHeld;
    private float lastAttackPressTime = Mathf.NegativeInfinity;
    private bool subscribed;

    private bool anyError = false;

    private void OnValidate()
    {
        if (bow == null)
        {
            bow = GetComponent<PlayerBow>();
        }
        if (animator == null)
        {
            // Synty rigs keep the Animator on a child model object.
            animator = GetComponentInChildren<Animator>();
        }
        if (inputReader == null)
        {
            inputReader = GetComponentInChildren<InputReader>();
        }
    }

    private void Start()
    {
        if (animator == null || !animator.isHuman)
        {
            Debug.LogError("A Humanoid Animator is not assigned or found; the aim look needs spine bones to bend.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        if (aimCamera == null)
        {
            aimCamera = Camera.main;
        }
        if (aimCamera == null)
        {
            Debug.LogError("No aim Camera assigned and no Camera.main found; the aim look has no pitch to follow.");
            anyError = true;
            return;
        }

        // Not every Humanoid rig maps chest/upper chest — keep what exists and renormalize.
        int found = 0;
        float totalWeight = 0f;
        Transform[] bones = new Transform[Bones.Length];
        float[] weights = new float[Bones.Length];
        for (int i = 0; i < Bones.Length; i++)
        {
            Transform bone = animator.GetBoneTransform(Bones[i]);
            if (bone == null)
            {
                continue;
            }
            bones[found] = bone;
            weights[found] = BoneWeights[i];
            totalWeight += BoneWeights[i];
            found++;
        }

        if (found == 0 || totalWeight <= 0f)
        {
            Debug.LogError("The Animator's avatar maps no spine/chest bones; the aim look can't bend anything.");
            anyError = true;
            return;
        }

        spineBones = new Transform[found];
        spineWeights = new float[found];
        for (int i = 0; i < found; i++)
        {
            spineBones[i] = bones[i];
            spineWeights[i] = weights[i] / totalWeight;
        }

        Subscribe();
    }

    private void OnEnable()
    {
        if (!anyError && spineBones != null)
        {
            Subscribe();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
        attackHeld = false;
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (subscribed || inputReader == null)
        {
            return;
        }
        inputReader.onAttackActivated += HandleAttackPressed;
        inputReader.onAttackDeactivated += HandleAttackReleased;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || inputReader == null)
        {
            return;
        }
        inputReader.onAttackActivated -= HandleAttackPressed;
        inputReader.onAttackDeactivated -= HandleAttackReleased;
        subscribed = false;
    }

    private void HandleAttackPressed()
    {
        attackHeld = true;
        lastAttackPressTime = Time.time;
    }

    private void HandleAttackReleased()
    {
        attackHeld = false;
    }

    private void LateUpdate()
    {
        if (anyError)
        {
            return;
        }

        IChargedAimWeapon weapon = AimWeaponResolver.Resolve(bow);
        bool isAiming = weapon != null ? weapon.IsAiming : (bow != null && bow.IsAiming);
        bool attacking = attackHeld || Time.time - lastAttackPressTime < attackLingerSeconds;
        float blendSeconds = weapon != null ? weapon.AimBlendSeconds : (config != null ? config.aimBlendSeconds : 0.2f);
        float maxPitch = config != null ? config.aimLookMaxPitchDegrees : 60f;

        float target = (isAiming || attacking) ? 1f : 0f;
        float speed = blendSeconds > 0f ? Time.deltaTime / blendSeconds : 1f;
        blend = Mathf.MoveTowards(blend, target, speed);
        if (blend <= 0f)
        {
            return;
        }

        // Positive = camera (and so the spine) pitching down; clamped so extreme angles don't fold
        // the character in half.
        Vector3 forward = aimCamera.transform.forward;
        float pitchDown = -Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg;
        pitchDown = Mathf.Clamp(pitchDown, -maxPitch, maxPitch);

        // Ease like BowAimCamera so the bend arrives with the zoom. Rotating about the player's
        // right axis works because the strafing controller already faces the camera heading.
        float eased = Mathf.SmoothStep(0f, 1f, blend);
        Vector3 axis = transform.right;
        for (int i = 0; i < spineBones.Length; i++)
        {
            spineBones[i].Rotate(axis, pitchDown * spineWeights[i] * eased, Space.World);
        }
    }
}
