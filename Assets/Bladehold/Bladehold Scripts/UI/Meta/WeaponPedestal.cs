using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Diegetic weapon pedestal in the Meta Area.
///     Displays a floating, gently spinning 3D model of a weapon and a floating world-space UI.
///     Allows the player to unlock the weapon with Orcish Metal or equip it for future runs.
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
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text costLabel;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private Image currencyIcon;

    private Interactable interactable;
    private Vector3 baseMountPosition;
    private GameObject spawnedModel;

    public WeaponDefinitionSO WeaponData => weaponData;

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
    }

    private void SpawnModel()
    {
        if (weaponData == null || weaponData.modelPrefab == null || modelMountPoint == null) return;
        if (spawnedModel != null) Destroy(spawnedModel);

        spawnedModel = Instantiate(weaponData.modelPrefab, modelMountPoint);
        spawnedModel.transform.localPosition = Vector3.zero;
        spawnedModel.transform.localRotation = Quaternion.identity;
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

        if (weaponData.isLockedForDemo)
        {
            if (costLabel != null) costLabel.text = "LOCKED FOR DEMO";
            if (statusLabel != null) statusLabel.text = "";
            interactable.PromptText = "Locked for Demo";
            interactable.CanInteract = false;
            return;
        }

        if (isEquipped)
        {
            if (statusLabel != null)
            {
                statusLabel.text = "EQUIPPED";
                statusLabel.color = Color.green;
            }
            if (costLabel != null) costLabel.text = "";
            interactable.PromptText = "Equipped";
            interactable.CanInteract = false;
        }
        else if (isUnlocked)
        {
            if (statusLabel != null)
            {
                statusLabel.text = "UNLOCKED";
                statusLabel.color = Color.white;
            }
            if (costLabel != null) costLabel.text = "";
            interactable.PromptText = $"Equip {weaponData.displayName}";
            interactable.CanInteract = true;
        }
        else
        {
            // Locked: requires Orcish Metal to unlock
            int currentMetal = data != null ? data.orcishMetal : 0;
            bool canAfford = currentMetal >= weaponData.orcishMetalUnlockCost;

            if (statusLabel != null) statusLabel.text = "LOCKED";
            if (costLabel != null)
            {
                costLabel.text = $"{weaponData.orcishMetalUnlockCost} Metal";
                costLabel.color = canAfford ? Color.white : Color.red;
            }

            interactable.PromptText = $"Unlock {weaponData.displayName} ({weaponData.orcishMetalUnlockCost} Metal)";
            interactable.CanInteract = canAfford;
        }
    }

    private void HandleInteract(Player player)
    {
        if (weaponData == null || weaponData.isLockedForDemo) return;

        SaveData data = SaveSystem.Load();
        bool isUnlocked = data != null && data.unlockedWeapons != null && data.unlockedWeapons.Contains(weaponData.id);

        if (!isUnlocked)
        {
            // Unlock weapon with Orcish Metal
            if (data != null && data.orcishMetal >= weaponData.orcishMetalUnlockCost)
            {
                data.orcishMetal -= weaponData.orcishMetalUnlockCost;
                data.unlockedWeapons.Add(weaponData.id);
                SaveSystem.Save(data);

                Debug.Log($"[WeaponPedestal] Unlocked weapon: {weaponData.displayName}!");
                RefreshAllPedestals();
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

            Debug.Log($"[WeaponPedestal] Equipped weapon: {weaponData.displayName}!");
            RefreshAllPedestals();
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
