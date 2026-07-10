using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using UnityEngine;

/// <summary>
///     Twists the mounted player's upper body toward the camera while fighting — the
///     <see cref="BowAimLook" /> sibling for horseback. On foot, the strafing Synty controller
///     turns the whole character to the camera heading, but in the saddle the body faces where the
///     HORSE faces; while the attack is held (with a short linger so a single click covers the
///     swing) or the bow aims, this distributes the camera-vs-body yaw additively across the spine
///     bones in <c>LateUpdate</c>, plus camera pitch — except while the bow aims, where
///     <see cref="BowAimLook" /> already owns the pitch (yaw-only here avoids a double bend).
///     Purely cosmetic; hits already fly toward the camera (bow hitscan) or the blade arc.
/// </summary>
public class MountedCombatLook : MonoBehaviour
{
    [SerializeField] private PlayerMount mount;
    [SerializeField] private PlayerBow bow;
    [SerializeField] private InputReader inputReader;
    [Tooltip("The player rig's Humanoid Animator. Synty rigs keep it on a child.")]
    [SerializeField] private Animator animator;
    [Tooltip("Camera the upper body turns toward. Defaults to Camera.main.")]
    [SerializeField] private Camera aimCamera;

    [Tooltip("Max degrees the upper body twists left/right of the horse's facing.")]
    [SerializeField] private float yawClampDegrees = 70f;
    [Tooltip("Max degrees the upper body pitches up/down (sword only — BowAimLook owns aim pitch).")]
    [SerializeField] private float pitchClampDegrees = 45f;
    [Tooltip("Seconds to blend the twist in and out.")]
    [SerializeField] private float blendSeconds = 0.25f;
    [Tooltip("Seconds after an attack press the twist lingers, so a quick click still covers the swing.")]
    [SerializeField] private float attackLingerSeconds = 0.8f;

    /// <summary>Fraction of the twist each spine bone absorbs, root-most first — the BowAimLook weights, renormalized over the bones the rig actually has.</summary>
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
        if (mount == null)
        {
            mount = GetComponent<PlayerMount>();
        }
        if (bow == null)
        {
            bow = GetComponent<PlayerBow>();
        }
        if (inputReader == null)
        {
            inputReader = GetComponentInChildren<InputReader>();
        }
        if (animator == null)
        {
            // Synty rigs keep the Animator on a child model object.
            animator = GetComponentInChildren<Animator>();
        }
    }

    private void Start()
    {
        if (mount == null)
        {
            Debug.LogError("PlayerMount component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (inputReader == null)
        {
            Debug.LogError("InputReader is not assigned or found; the mounted look can't track attacks.");
            anyError = true;
        }
        if (animator == null || !animator.isHuman)
        {
            Debug.LogError("A Humanoid Animator is not assigned or found; the mounted look needs spine bones to twist.");
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
            Debug.LogError("No aim Camera assigned and no Camera.main found; the mounted look has nothing to face.");
            anyError = true;
            return;
        }

        // Not every Humanoid rig maps chest/upper chest — keep what exists and renormalize (the
        // BowAimLook bone walk).
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
            Debug.LogError("The Animator's avatar maps no spine/chest bones; the mounted look can't twist anything.");
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

        bool aiming = bow != null && bow.IsAiming;
        bool attacking = attackHeld || Time.time - lastAttackPressTime < attackLingerSeconds;
        bool active = mount.IsMounted && (attacking || aiming);

        float speed = blendSeconds > 0f ? Time.deltaTime / blendSeconds : 1f;
        blend = Mathf.MoveTowards(blend, active ? 1f : 0f, speed);
        if (blend <= 0f)
        {
            return;
        }

        Vector3 cameraForward = aimCamera.transform.forward;

        Vector3 flatCamera = cameraForward;
        flatCamera.y = 0f;
        Vector3 flatBody = transform.forward;
        flatBody.y = 0f;
        float yaw = 0f;
        if (flatCamera.sqrMagnitude > 0.0001f && flatBody.sqrMagnitude > 0.0001f)
        {
            yaw = Mathf.Clamp(Vector3.SignedAngle(flatBody.normalized, flatCamera.normalized, Vector3.up),
                -yawClampDegrees, yawClampDegrees);
        }

        // While the bow aims, BowAimLook already pitches the spine — adding ours would double-bend.
        float pitchDown = 0f;
        if (!aiming)
        {
            pitchDown = -Mathf.Asin(Mathf.Clamp(cameraForward.y, -1f, 1f)) * Mathf.Rad2Deg;
            pitchDown = Mathf.Clamp(pitchDown, -pitchClampDegrees, pitchClampDegrees);
        }

        float eased = Mathf.SmoothStep(0f, 1f, blend);
        Vector3 pitchAxis = transform.right;
        for (int i = 0; i < spineBones.Length; i++)
        {
            float weight = spineWeights[i] * eased;
            spineBones[i].Rotate(Vector3.up, yaw * weight, Space.World);
            if (pitchDown != 0f)
            {
                spineBones[i].Rotate(pitchAxis, pitchDown * weight, Space.World);
            }
        }
    }
}
