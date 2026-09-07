using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Diegetic armour set pedestal in the Meta Area.
///     Displays a 3D preview model of the knight armour set with idle animation,
///     and a world-space UI panel showing armour name, status, cost, and perks when
///     approached or looked at. Allows the player to unlock and equip armour sets.
/// </summary>
[RequireComponent(typeof(Interactable))]
public class ArmourPedestal : MonoBehaviour
{
    [Header("Armour Data")]
    [SerializeField] private ArmourSetSO armourData;

    [Header("Display Transforms")]
    [Tooltip("Anchor point where the 3D armour preview model is mounted.")]
    [SerializeField] private Transform modelMountPoint;

    [Tooltip("Rotation speed of the display model in degrees per second.")]
    [SerializeField] private float rotationSpeed = 25f;

    [Tooltip("Bobbing height amplitude for the display model.")]
    [SerializeField] private float bobAmplitude = 0.04f;

    [Tooltip("Animator controller applied to the preview model to play an idle pose.")]
    [SerializeField] private RuntimeAnimatorController previewAnimatorController;

    [Header("World UI Panel")]
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text costLabel;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private TMP_Text perksLabel;
    [SerializeField] private TMP_Text descriptionLabel;

    [Header("Proximity / Focus Settings")]
    [SerializeField] private float focusDistance = 4.5f;
    [SerializeField] private float fadeSpeed = 8f;

    private Interactable interactable;
    private Vector3 baseMountPosition;
    private GameObject spawnedModel;
    private Transform playerTransform;
    private PlayerInteraction playerInteraction;

    public ArmourSetSO ArmourData => armourData;

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

    private void Start()
    {
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

    private void Update()
    {
        // 1. Gentle rotation and bobbing for the preview model
        if (modelMountPoint != null)
        {
            modelMountPoint.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            modelMountPoint.localPosition = baseMountPosition + Vector3.up * (Mathf.Sin(Time.time * 2.0f) * bobAmplitude);
        }

        // 2. Billboard world canvas towards camera
        Camera cam = Camera.main;
        if (cam != null && worldCanvas != null)
        {
            worldCanvas.transform.rotation = cam.transform.rotation;
        }

        // 3. Proximity / Look-at check to reveal the perks panel
        UpdatePanelVisibility();
    }

    private void UpdatePanelVisibility()
    {
        if (panelCanvasGroup == null) return;

        if (playerTransform == null)
        {
            ResolvePlayerReferences();
        }

        bool isFocused = false;

        // Condition A: It is the active target of PlayerInteraction
        if (playerInteraction != null && playerInteraction.CurrentTarget == (IInteractable)interactable)
        {
            isFocused = true;
        }
        // Condition B: Player is close and facing towards this pedestal
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
        if (armourData == null || armourData.characterModelPrefab == null || modelMountPoint == null) return;
        if (spawnedModel != null) SafeDestroy(spawnedModel);

        spawnedModel = Instantiate(armourData.characterModelPrefab, modelMountPoint);
        spawnedModel.transform.localPosition = Vector3.zero;
        spawnedModel.transform.localRotation = Quaternion.identity;
        spawnedModel.transform.localScale = Vector3.one;

        // Apply idle animator controller if available
        Animator anim = spawnedModel.GetComponentInChildren<Animator>();
        if (anim != null && previewAnimatorController != null)
        {
            anim.runtimeAnimatorController = previewAnimatorController;
            anim.applyRootMotion = false;
        }
    }

    public void RefreshPedestal()
    {
        Initialize();
        if (armourData == null) return;

        SaveData data = SaveSystem.Load();
        bool isUnlocked = (armourData.orcishMetalUnlockCost == 0) || 
                          (data != null && data.unlockedArmourSets != null && data.unlockedArmourSets.Contains(armourData.id));
        bool isEquipped = string.Equals(data?.equippedArmourSet, armourData.id, StringComparison.OrdinalIgnoreCase);

        if (nameLabel != null) nameLabel.text = armourData.displayName;
        if (descriptionLabel != null) descriptionLabel.text = armourData.description;
        if (perksLabel != null) perksLabel.text = armourData.GetFormattedModifiersText();

        if (isEquipped)
        {
            if (statusLabel != null)
            {
                statusLabel.text = "EQUIPPED";
                statusLabel.color = new Color(0.3f, 1f, 0.4f);
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
            interactable.PromptText = $"Equip {armourData.displayName}";
            interactable.CanInteract = true;
        }
        else
        {
            int currentMetal = data != null ? data.orcishMetal : 0;
            bool canAfford = currentMetal >= armourData.orcishMetalUnlockCost;

            if (statusLabel != null)
            {
                statusLabel.text = "LOCKED";
                statusLabel.color = new Color(1f, 0.65f, 0.2f);
            }
            if (costLabel != null)
            {
                costLabel.text = $"{armourData.orcishMetalUnlockCost} Metal";
                costLabel.color = canAfford ? new Color(1f, 0.9f, 0.3f) : new Color(1f, 0.35f, 0.35f);
            }

            interactable.PromptText = $"Unlock {armourData.displayName} ({armourData.orcishMetalUnlockCost} Metal)";
            interactable.CanInteract = canAfford;
        }
    }

    private void HandleInteract(Player player)
    {
        if (armourData == null) return;

        SaveData data = SaveSystem.Load();
        bool isUnlocked = (armourData.orcishMetalUnlockCost == 0) || 
                          (data != null && data.unlockedArmourSets != null && data.unlockedArmourSets.Contains(armourData.id));

        PlayerArmourManager pam = null;
        if (player != null)
        {
            pam = player.GetComponent<PlayerArmourManager>() ?? player.GetComponentInChildren<PlayerArmourManager>();
        }
        if (pam == null && Player.Instance != null)
        {
            pam = Player.Instance.GetComponent<PlayerArmourManager>() ?? Player.Instance.GetComponentInChildren<PlayerArmourManager>();
        }
        if (pam == null)
        {
            pam = FindAnyObjectByType<PlayerArmourManager>();
        }

        if (!isUnlocked)
        {
            if (data != null && data.orcishMetal >= armourData.orcishMetalUnlockCost)
            {
                data.orcishMetal -= armourData.orcishMetalUnlockCost;
                if (!data.unlockedArmourSets.Contains(armourData.id))
                {
                    data.unlockedArmourSets.Add(armourData.id);
                }
                SaveSystem.Save(data);

                Debug.Log($"[ArmourPedestal] Unlocked armour: {armourData.displayName}!");

                // Auto-equip on purchase
                if (pam != null)
                {
                    pam.EquipArmour(armourData, playEffect: true);
                }

                RefreshAllPedestals();
            }
        }
        else
        {
            // Equip unlocked armour
            if (pam != null)
            {
                pam.EquipArmour(armourData, playEffect: true);
            }
            else
            {
                if (data != null)
                {
                    data.equippedArmourSet = armourData.id;
                    SaveSystem.Save(data);
                }
            }

            Debug.Log($"[ArmourPedestal] Equipped armour: {armourData.displayName}!");
            RefreshAllPedestals();
        }
    }

    public static void RefreshAllPedestals()
    {
        ArmourPedestal[] all = FindObjectsByType<ArmourPedestal>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            all[i].RefreshPedestal();
        }
    }

    private static void SafeDestroy(UnityEngine.Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }
}
