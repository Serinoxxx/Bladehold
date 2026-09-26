using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.InputSystem;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;

/// <summary>
///     Dedicated bow controller active during the Fishing Minigame.
///     Provides infinite arrows, overrides standard weapon loadout, and binds minigame upgrades.
/// </summary>
public class FishingBowController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private Camera aimCamera;
    [SerializeField] private Transform arrowSpawnPoint;
    [SerializeField] private GameObject arrowPrefab;
    [Tooltip("Optional: played on each shot. Nothing is authored yet.")]
    [SerializeField] private MMF_Player shootFeedback;

    [Header("Tuning")]
    [SerializeField] private float fireCooldown = 0.35f;
    private float lastFireTime = -10f;
    private bool isAiming = false;

    private void Awake()
    {
        if (aimCamera == null) aimCamera = Camera.main;
        if (inputReader == null) inputReader = GetComponentInParent<InputReader>();
    }

    private void OnEnable()
    {
        if (inputReader != null)
        {
            inputReader.onAimActivated += HandleAimActivated;
            inputReader.onAimDeactivated += HandleAimDeactivated;
            inputReader.onAttackActivated += HandleAttackActivated;
        }
    }

    private void OnDisable()
    {
        if (inputReader != null)
        {
            inputReader.onAimActivated -= HandleAimActivated;
            inputReader.onAimDeactivated -= HandleAimDeactivated;
            inputReader.onAttackActivated -= HandleAttackActivated;
        }
    }

    private void Update()
    {
        // Standalone fallback: mouse right click for aim, left click for shoot
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.rightButton.wasPressedThisFrame) isAiming = true;
            if (mouse.rightButton.wasReleasedThisFrame) isAiming = false;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                TryShoot();
            }
        }
    }

    private void HandleAimActivated() => isAiming = true;
    private void HandleAimDeactivated() => isAiming = false;
    private void HandleAttackActivated() => TryShoot();

    public void TryShoot()
    {
        // Don't shoot if minigame not active or paused
        if (Time.timeScale <= 0f) return;
        if (FishingManager.Instance != null && !FishingManager.Instance.IsFrenzyActive) return;

        if (Time.time - lastFireTime < fireCooldown) return;
        lastFireTime = Time.time;

        ShootArrow();
    }

    private void ShootArrow()
    {
        Vector3 aimPoint;
        Ray ray = aimCamera != null
            ? aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
            : new Ray(transform.position, transform.forward);

        Plane waterPlane = new Plane(Vector3.up, new Vector3(0f, 0f, 0f));
        if (waterPlane.Raycast(ray, out float enter))
        {
            aimPoint = ray.GetPoint(enter);
        }
        else
        {
            aimPoint = ray.GetPoint(30f);
        }

        Vector3 spawnPos = arrowSpawnPoint != null ? arrowSpawnPoint.position : transform.position + Vector3.up * 1.2f;
        Vector3 fireDir = (aimPoint - spawnPos).normalized;

        int pierces = FishingUpgradeManager.Instance != null ? FishingUpgradeManager.Instance.PierceCount : 0;
        int bounces = FishingUpgradeManager.Instance != null ? FishingUpgradeManager.Instance.BounceCount : 0;

        if (arrowPrefab == null)
        {
            Debug.LogError("[FishingBowController] arrowPrefab is not assigned (Player.prefab → SidekickSyntyCharacter → FishingBowController).", this);
            return;
        }

        GameObject arrowObj = Instantiate(arrowPrefab, spawnPos, Quaternion.LookRotation(fireDir));
        FishingBowArrow arrow = arrowObj.GetComponent<FishingBowArrow>();
        if (arrow != null)
        {
            arrow.Setup(fireDir, pierces, bounces);
        }
        else
        {
            Debug.LogError($"[FishingBowController] arrowPrefab '{arrowPrefab.name}' has no FishingBowArrow.", this);
        }

        if (shootFeedback != null)
        {
            shootFeedback.PlayFeedbacks(spawnPos);
        }
    }
}
