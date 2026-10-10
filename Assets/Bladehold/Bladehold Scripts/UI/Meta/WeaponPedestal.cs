using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Diegetic weapon pedestal in the Meta Area.
///     Displays a floating, gently spinning 3D model of a weapon and a floating world-space UI.
///     Allows the player to unlock the weapon with Orcish Metal or equip it for future runs.
///     An unlock gets a fanfare: <see cref="unlockFeedback" /> (sting, level-up burst and sparks at the
///     floating model) and a "New weapon unlocked" banner on the HUD (<see cref="EnemyIntroUI" />).
/// </summary>
[RequireComponent(typeof(Interactable))]
public class WeaponPedestal : MonoBehaviour
{
    [Header("Weapon Data")]
    [SerializeField] private WeaponDefinitionSO weaponData;

    [Header("Display Transforms")]
    [Tooltip("Anchor point where the 3D floating weapon model is mounted and rotated.")]
    [SerializeField] private Transform modelMountPoint;

    [Tooltip("Optional rotation speed in degrees per second.")]
    [SerializeField] private float rotationSpeed = 35f;

    [Tooltip("Optional bobbing height amplitude.")]
    [SerializeField] private float bobAmplitude = 0.08f;

    [Header("World UI Elements")]
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text costLabel;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private TMP_Text descriptionLabel;
    [SerializeField] private TMP_Text combatStatsLabel;
    [SerializeField] private TMP_Text upgradesLabel;
    [SerializeField] private TMP_Text ultimateLabel;
    [SerializeField] private Image currencyIcon;

    [Header("Unlock Fanfare")]
    [Tooltip("Played at the floating model when this weapon is unlocked: fanfare sting, level-up burst, sparks.")]
    [SerializeField] private MMF_Player unlockFeedback;
    [Tooltip("Real seconds the \"New weapon unlocked\" banner stays up.")]
    [SerializeField] private float unlockBannerDuration = 3.5f;

    [Header("Proximity / Focus Settings")]
    [SerializeField] private float focusDistance = 4.5f;
    [SerializeField] private float fadeSpeed = 8f;

    private Interactable interactable;
    private Camera cachedCamera;
    private Vector3 baseMountPosition;
    private GameObject spawnedModel;
    private Transform playerTransform;
    private PlayerInteraction playerInteraction;

    private static readonly List<WeaponPedestal> all = new List<WeaponPedestal>();

    /// <summary>The Pedestals menu theme (scope on the pedestal root) for state colours.</summary>
    private UIThemeSO Theme => UITheme.For(this);

    /// <summary>Every enabled pedestal in the scene (the Meta Area's weapon rack).</summary>
    public static IReadOnlyList<WeaponPedestal> All => all;

    public WeaponDefinitionSO WeaponData => weaponData;

    /// <summary>True when this weapon is still locked, allowed in the demo, and the player has the Orcish Metal for it.</summary>
    public bool CanAffordUnlock
    {
        get
        {
            if (weaponData == null || DemoConfigSO.IsWeaponLocked(weaponData)) return false;
            SaveData data = SaveSystem.Load();
            bool isUnlocked = data.unlockedWeapons != null && data.unlockedWeapons.Contains(weaponData.id);
            return !isUnlocked && data.orcishMetal >= weaponData.orcishMetalUnlockCost;
        }
    }

    private void OnEnable()
    {
        all.Add(this);
    }

    private void OnDisable()
    {
        all.Remove(this);
    }

    public void Initialize()
    {
        if (interactable == null)
        {
            interactable = GetComponent<Interactable>();
            if (modelMountPoint != null)
            {
                baseMountPosition = modelMountPoint.localPosition;
            }
            if (interactable != null)
            {
                interactable.OnInteractedEvent += HandleInteract;
            }
        }
    }

    private void Awake()
    {
        Initialize();
    }

    public void OnPedestalInteracted(Player player)
    {
        HandleInteract(player);
    }

    private void Start()
    {
        // Feedback is required but never gameplay-breaking: log only.
        if (unlockFeedback == null) Debug.LogError($"[WeaponPedestal] {name}: unlockFeedback is not assigned.", this);

        ResolvePlayerReferences();
        SpawnModel();
        RefreshPedestal();
    }

    private void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.OnInteractedEvent -= HandleInteract;
        }
    }

    private void Update()
    {
        if (modelMountPoint != null)
        {
            modelMountPoint.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            modelMountPoint.localPosition = baseMountPosition + Vector3.up * (Mathf.Sin(Time.time * 2.0f) * bobAmplitude);
        }

        if (cachedCamera == null) cachedCamera = Camera.main;
        Camera cam = cachedCamera;
        if (cam != null && worldCanvas != null)
        {
            worldCanvas.transform.rotation = cam.transform.rotation;
        }

        UpdatePanelVisibility();
    }

    private void ResolvePlayerReferences()
    {
        if (playerTransform == null)
        {
            Player player = Player.Instance ?? FindAnyObjectByType<Player>();
            if (player != null)
            {
                playerTransform = player.transform;
                playerInteraction = player.GetComponent<PlayerInteraction>() ?? player.GetComponentInChildren<PlayerInteraction>();
            }
        }
    }

    private void UpdatePanelVisibility()
    {
        if (panelCanvasGroup == null) return;

        if (playerTransform == null)
        {
            ResolvePlayerReferences();
        }

        bool isFocused = false;

        if (playerInteraction != null && playerInteraction.CurrentTarget == (IInteractable)interactable)
        {
            isFocused = true;
        }
        else if (playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist <= focusDistance)
            {
                Vector3 toPedestal = (transform.position - playerTransform.position).normalized;
                toPedestal.y = 0f;
                Vector3 playerFwd = playerTransform.forward;
                playerFwd.y = 0f;

                if (Vector3.Dot(playerFwd.normalized, toPedestal) > 0.2f)
                {
                    isFocused = true;
                }
            }
        }

        float targetAlpha = isFocused ? 1f : 0f;
        panelCanvasGroup.alpha = Mathf.MoveTowards(panelCanvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
    }

    private void SpawnModel()
    {
        if (weaponData == null || weaponData.modelPrefab == null || modelMountPoint == null) return;
        if (spawnedModel != null) Destroy(spawnedModel);

        spawnedModel = Instantiate(weaponData.modelPrefab, modelMountPoint);
        spawnedModel.transform.localPosition = Vector3.zero;
        spawnedModel.transform.localRotation = Quaternion.identity;

        // modelPrefab is the equippable weapon, so it carries combat components that expect a wielder.
        // Switch them off before their Start runs; the pedestal only needs the renderers.
        foreach (DamageTrigger trigger in spawnedModel.GetComponentsInChildren<DamageTrigger>(true)) trigger.enabled = false;
        foreach (SwordChargeFeedback charge in spawnedModel.GetComponentsInChildren<SwordChargeFeedback>(true)) charge.enabled = false;
        foreach (Collider col in spawnedModel.GetComponentsInChildren<Collider>(true)) col.enabled = false;
    }

    public void RefreshPedestal()
    {
        Initialize();
        if (weaponData == null) return;

        SaveData data = SaveSystem.Load();
        bool isUnlocked = data != null && data.unlockedWeapons != null && data.unlockedWeapons.Contains(weaponData.id);
        bool isEquipped = false;

        if (weaponData.category == WeaponCategory.Melee)
        {
            isEquipped = string.Equals(data?.equippedMeleeWeapon, weaponData.id, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            isEquipped = string.Equals(data?.equippedRangedWeapon, weaponData.id, StringComparison.OrdinalIgnoreCase);
        }

        if (nameLabel != null) nameLabel.text = weaponData.displayName;
        RefreshWeaponDetails();

        if (DemoConfigSO.IsWeaponLocked(weaponData))
        {
            if (statusLabel != null) statusLabel.text = "";
            if (costLabel != null)
            {
                costLabel.text = DemoConfigSO.LockedLabel;
                costLabel.color = Theme.Get(UIColorRole.TextDim);
            }
            interactable.PromptText = DemoConfigSO.LockedPrompt;
            interactable.CanInteract = false;
            interactable.CanAfford = true;
            return;
        }

        if (isEquipped)
        {
            if (statusLabel != null)
            {
                statusLabel.text = "EQUIPPED";
                statusLabel.color = Theme.Get(UIColorRole.Success);
            }
            if (costLabel != null) costLabel.text = "";
            interactable.PromptText = "Equipped";
            interactable.CanInteract = false;
            interactable.CanAfford = true;
        }
        else if (isUnlocked)
        {
            if (statusLabel != null)
            {
                statusLabel.text = "UNLOCKED";
                statusLabel.color = Theme.Get(UIColorRole.Text);
            }
            if (costLabel != null) costLabel.text = "";
            interactable.PromptText = $"Equip {weaponData.displayName}";
            interactable.CanInteract = true;
            interactable.CanAfford = true;
        }
        else
        {
            // Locked: requires Orcish Metal to unlock
            int currentMetal = data != null ? data.orcishMetal : 0;
            bool canAfford = currentMetal >= weaponData.orcishMetalUnlockCost;

            if (statusLabel != null)
            {
                statusLabel.text = "LOCKED";
                statusLabel.color = Theme.Get(UIColorRole.TextDim);
            }
            if (costLabel != null)
            {
                costLabel.text = $"{weaponData.orcishMetalUnlockCost} Metal";
                costLabel.color = Theme.Get(canAfford ? UIColorRole.Cost : UIColorRole.Danger);
            }

            interactable.PromptText = $"Unlock {weaponData.displayName} ({weaponData.orcishMetalUnlockCost} Metal)";
            interactable.CanInteract = true;
            interactable.CanAfford = canAfford;
        }
    }

    private void RefreshWeaponDetails()
    {
        if (descriptionLabel != null)
        {
            descriptionLabel.text = weaponData.pedestalDescription;
        }

        if (combatStatsLabel != null)
        {
            combatStatsLabel.text = weaponData.pedestalCombatStats;
        }

        System.Text.StringBuilder upgrades = new System.Text.StringBuilder("<b>UPGRADES</b>");
        if (weaponData.pedestalUpgrades != null)
        {
            for (int i = 0; i < weaponData.pedestalUpgrades.Length && i < 3; i++)
            {
                WeaponPedestalUpgrade upgrade = weaponData.pedestalUpgrades[i];
                if (string.IsNullOrWhiteSpace(upgrade.name) && string.IsNullOrWhiteSpace(upgrade.description)) continue;
                upgrades.Append($"\n• <b>{upgrade.name}</b>");
                if (!string.IsNullOrWhiteSpace(upgrade.description))
                {
                    upgrades.Append($" — {upgrade.description}");
                }
            }
        }

        if (upgradesLabel != null)
        {
            upgradesLabel.text = upgrades.ToString();
        }

        if (ultimateLabel != null)
        {
            ultimateLabel.text = string.IsNullOrWhiteSpace(weaponData.pedestalUltimateName)
                ? ""
                : $"<b>ULTIMATE ABILITY: {weaponData.pedestalUltimateName}</b>\n{weaponData.pedestalUltimateDescription}";
        }
    }

    private void HandleInteract(Player player)
    {
        if (weaponData == null || DemoConfigSO.IsWeaponLocked(weaponData)) return;

        SaveData data = SaveSystem.Load();
        bool isUnlocked = data != null && data.unlockedWeapons != null && data.unlockedWeapons.Contains(weaponData.id);

        if (!isUnlocked)
        {
            // Unlock weapon with Orcish Metal
            if (data != null && data.orcishMetal >= weaponData.orcishMetalUnlockCost)
            {
                RunSession.SpendOrcishMetal(data, weaponData.orcishMetalUnlockCost);
                data.unlockedWeapons.Add(weaponData.id);
                // Also auto-equip newly unlocked weapon so player can test it immediately
                if (weaponData.category == WeaponCategory.Melee)
                {
                    data.equippedMeleeWeapon = weaponData.id;
                }
                else
                {
                    data.equippedRangedWeapon = weaponData.id;
                }
                SaveSystem.Save(data);

                ApplyEquippedToPlayer();

                Debug.Log($"[WeaponPedestal] Unlocked and equipped weapon: {weaponData.displayName}!");
                RefreshAllPedestals();
                PlayUnlockFanfare();
            }
        }
        else
        {
            // Equip weapon
            if (weaponData.category == WeaponCategory.Melee)
            {
                data.equippedMeleeWeapon = weaponData.id;
            }
            else
            {
                data.equippedRangedWeapon = weaponData.id;
            }
            SaveSystem.Save(data);

            ApplyEquippedToPlayer();

            Debug.Log($"[WeaponPedestal] Equipped weapon: {weaponData.displayName}!");
            RefreshAllPedestals();
        }
    }

    private void PlayUnlockFanfare()
    {
        if (unlockFeedback != null)
        {
            unlockFeedback.PlayFeedbacks(modelMountPoint != null ? modelMountPoint.position : transform.position);
        }
        if (EnemyIntroUI.Instance != null)
        {
            string subtitle = string.IsNullOrWhiteSpace(weaponData.pedestalUltimateName)
                ? ""
                : string.Format(Loc.Get("weapon_unlock.ultimate", "Ultimate: {0}"), weaponData.pedestalUltimateName);
            EnemyIntroUI.Instance.ShowIntro(Loc.Get("weapon_unlock.eyebrow", "New weapon unlocked"), weaponData.displayName, 0, subtitle, unlockBannerDuration, false);
        }
    }

    private void ApplyEquippedToPlayer()
    {
        PlayerWeaponManager pwm = PlayerWeaponManager.GetInstance();

        if (pwm == null)
        {
            Debug.LogError("[WeaponPedestal] Could not find a populated PlayerWeaponManager to apply the equipped weapon.");
            return;
        }

        if (weaponData.category == WeaponCategory.Melee)
        {
            pwm.EquipMelee(weaponData.id);
        }
        else
        {
            pwm.EquipRanged(weaponData.id);
        }
    }

    public static void RefreshAllPedestals()
    {
        WeaponPedestal[] all = FindObjectsByType<WeaponPedestal>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            all[i].RefreshPedestal();
        }
    }
}
