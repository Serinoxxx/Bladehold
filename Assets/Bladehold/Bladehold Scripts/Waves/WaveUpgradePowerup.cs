using System;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Interactable world powerup that materializes in the arena between waves.
///     When interacted with via [E], opens the 3-card draft system filtered to a randomly selected
///     DraftCategory (Weapon or Elemental). Selecting a card consumes the powerup and
///     signals the game loop to proceed to the next wave.
/// </summary>
[RequireComponent(typeof(Interactable))]
public class WaveUpgradePowerup : MonoBehaviour
{
    [Header("Category Configuration")]
    [SerializeField] private BannerBountyType bountyType = BannerBountyType.WeaponDraft;

    [Header("Visual Effects")]
    [SerializeField] private float bobAmplitude = 0.25f;
    [SerializeField] private float bobSpeed = 2.5f;
    [SerializeField] private float rotationSpeed = 60f;
    [SerializeField] private Transform visualTransform;
    [Tooltip("If true, overrides child renderer material colors with the bounty type color. Recommended false for prefabs with their own textures.")]
    [SerializeField] private bool tintMaterialsWithBountyColor = false;

    [Header("Feedback")]
    [Tooltip("Optional: played at the chest when it appears. Nothing is authored yet.")]
    [SerializeField] private MMF_Player spawnFeedback;
    [Tooltip("Optional: played at the chest when it is claimed. Nothing is authored yet.")]
    [SerializeField] private MMF_Player claimFeedback;

    private Interactable interactable;
    private Vector3 initialVisualPosition;
    private bool isCollected = false;

    public BannerBountyType Bounty => bountyType;
    public string BountyName => GetBountyName(bountyType);
    public bool IsCollected => isCollected;

    public event Action<WaveUpgradePowerup> OnClaimed;

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        if (visualTransform == null)
        {
            Transform meshChild = transform.Find("Visual");
            visualTransform = meshChild != null ? meshChild : transform;
        }
        initialVisualPosition = visualTransform.localPosition;
    }

    private void Start()
    {
        InitializeBounty(bountyType);

        if (spawnFeedback != null)
        {
            spawnFeedback.PlayFeedbacks(transform.position);
        }
    }

    private void Update()
    {
        if (visualTransform != null && !isCollected)
        {
            // Gentle hovering bob & rotate
            float newY = initialVisualPosition.y + Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            visualTransform.localPosition = new Vector3(initialVisualPosition.x, newY, initialVisualPosition.z);
            visualTransform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        }
    }

    private void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.OnInteractedEvent -= HandleInteracted;
        }
    }

    /// <summary>
    ///     Initializes the powerup with a specific bounty type and updates prompt text and visual tint.
    /// </summary>
    public void InitializeBounty(BannerBountyType newBounty)
    {
        bountyType = newBounty;
        if (interactable == null)
        {
            interactable = GetComponent<Interactable>();
        }

        if (interactable != null)
        {
            interactable.PromptText = $"Claim Reward ({BountyName})";
            interactable.OnInteractedEvent -= HandleInteracted;
            interactable.OnInteractedEvent += HandleInteracted;
        }

        ApplyBountyColor(bountyType);
    }

    private string GetBountyName(BannerBountyType type)
    {
        return type switch
        {
            BannerBountyType.WeaponDraft => "Weapon Upgrade",
            BannerBountyType.ElementDraft => "Elemental Upgrade",
            BannerBountyType.FortressDraft => "Supply Cache",
            BannerBountyType.GoldCache => "Gold Cache",
            BannerBountyType.OrcishMetal => "Orcish Metal",
            BannerBountyType.GoblinBlood => "Goblin Blood",
            BannerBountyType.TrollHeart => "Troll Heart",
            _ => "Reward"
        };
    }

    private void ApplyBountyColor(BannerBountyType type)
    {
        Color color = type switch
        {
            BannerBountyType.WeaponDraft => new Color(1f, 0.4f, 0.1f, 1f),       // Fiery Orange
            BannerBountyType.ElementDraft => new Color(0.2f, 0.8f, 1f, 1f),    // Cyan / Ice Lightning
            BannerBountyType.FortressDraft => new Color(0.9f, 0.8f, 0.2f, 1f),   // Golden Amber
            BannerBountyType.GoldCache => new Color(1f, 0.9f, 0.2f, 1f),
            BannerBountyType.OrcishMetal => new Color(0.6f, 0.6f, 0.7f, 1f),
            BannerBountyType.GoblinBlood => new Color(0.8f, 0.1f, 0.1f, 1f),
            BannerBountyType.TrollHeart => new Color(0.2f, 0.8f, 0.3f, 1f),
            _ => Color.white
        };

        // Tint renderers if enabled
        if (tintMaterialsWithBountyColor)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (Renderer rend in renderers)
            {
                if (rend != null && rend.material != null)
                {
                    rend.material.color = color;
                    if (rend.material.HasProperty("_EmissionColor"))
                    {
                        rend.material.SetColor("_EmissionColor", color * 1.5f);
                    }
                }
            }
        }

        Light ptLight = GetComponentInChildren<Light>();
        if (ptLight != null)
        {
            ptLight.color = color;
        }
    }

    private void HandleInteracted(Player player)
    {
        if (isCollected) return;

        isCollected = true;
        if (interactable != null)
        {
            interactable.CanInteract = false;
        }

        if (claimFeedback != null)
        {
            claimFeedback.PlayFeedbacks(transform.position);
        }

        OnClaimed?.Invoke(this);
    }

    public void DestroyPowerup()
    {
        Destroy(gameObject);
    }

    /// <summary>
    ///     Instantiates an authored powerup prefab (a WaveUpgradePowerup variant: Interactable, trigger collider,
    ///     point Light and a "Visual" child) and sets its bounty. Returns null if the prefab is missing or wrong.
    /// </summary>
    public static WaveUpgradePowerup Spawn(Vector3 position, BannerBountyType bounty, GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError($"[WaveUpgradePowerup] No powerup prefab for bounty {bounty}: assign WarBannerRewardSO.rewardPrefab (or GameLoopManager.upgradePowerupPrefab).");
            return null;
        }

        if (prefab.GetComponent<WaveUpgradePowerup>() == null)
        {
            Debug.LogError($"[WaveUpgradePowerup] Prefab '{prefab.name}' has no WaveUpgradePowerup: point the reward at a Powerups/WaveReward_* variant.", prefab);
            return null;
        }

        GameObject go = Instantiate(prefab, position, Quaternion.identity);
        WaveUpgradePowerup powerup = go.GetComponent<WaveUpgradePowerup>();
        powerup.InitializeBounty(bounty);
        return powerup;
    }
}
