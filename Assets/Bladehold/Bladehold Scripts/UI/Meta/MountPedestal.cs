using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Diegetic mount pedestal in the Meta Area.
///     Displays a 3D preview model of the mount with its material, scale, and subtle bobbing,
///     accompanied by a world-space UI showing stats (Speed, Trample Damage, Knockback, Duration, Cooldown, Cast Time).
///     Allows the player to purchase mount variations using Orcish Metal and equip them.
/// </summary>
[RequireComponent(typeof(Interactable))]
public class MountPedestal : MonoBehaviour
{
    [Header("Mount Data")]
    [SerializeField] private MountDefinitionSO mountData;

    [Header("Display Transforms")]
    [Tooltip("Anchor point where the 3D horse preview model is mounted.")]
    [SerializeField] private Transform modelMountPoint;

    [Tooltip("Rotation speed of the display model in degrees per second.")]
    [SerializeField] private float rotationSpeed = 25f;

    [Tooltip("Bobbing height amplitude for the display model.")]
    [SerializeField] private float bobAmplitude = 0.04f;

    [Header("World UI Panel")]
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text costLabel;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private TMP_Text statsLabel;
    [SerializeField] private TMP_Text descriptionLabel;

    [Header("Proximity / Focus Settings")]
    [SerializeField] private float focusDistance = 5.0f;
    [SerializeField] private float fadeSpeed = 8f;

    private Interactable interactable;
    private Vector3 baseMountPosition;
    private GameObject spawnedModel;
    private Transform playerTransform;
    private PlayerInteraction playerInteraction;

    public MountDefinitionSO MountData => mountData;

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

    private void Update()
    {
        if (modelMountPoint != null)
        {
            modelMountPoint.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            modelMountPoint.localPosition = baseMountPosition + Vector3.up * (Mathf.Sin(Time.time * 2.0f) * bobAmplitude);
        }

        Camera cam = Camera.main;
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

                if (Vector3.Dot(playerFwd.normalized, toPedestal) > 0.15f)
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
        if (mountData == null || modelMountPoint == null) return;
        if (spawnedModel != null) Destroy(spawnedModel);

        GameObject horseSource = Resources.Load<GameObject>("Horse") ??
                                 Resources.Load<GameObject>("Prefabs/Horse/Horse");

        if (horseSource != null)
        {
            spawnedModel = Instantiate(horseSource, modelMountPoint);
            spawnedModel.transform.localPosition = Vector3.zero;
            spawnedModel.transform.localRotation = Quaternion.identity;

            // Remove gameplay logic scripts on the display pedestal preview
            var motor = spawnedModel.GetComponentInChildren<HorseMotor>();
            if (motor != null) Destroy(motor);
            var col = spawnedModel.GetComponentInChildren<Collider>();
            if (col != null) col.enabled = false;
            var cc = spawnedModel.GetComponentInChildren<CharacterController>();
            if (cc != null) Destroy(cc);

            float scale = mountData.scaleMultiplier > 0f ? mountData.scaleMultiplier : 1.0f;
            spawnedModel.transform.localScale = Vector3.one * scale;

            if (mountData.material != null)
            {
                var renderers = spawnedModel.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
                    Material[] mats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++)
                    {
                        mats[i] = mountData.material;
                    }
                    r.materials = mats;
                }
            }
        }
    }

    public void RefreshPedestal()
    {
        Initialize();
        if (mountData == null) return;

        SaveData data = SaveSystem.Load();
        bool isUnlocked = (mountData.orcishMetalUnlockCost == 0) ||
                          (data != null && data.unlockedMounts != null && data.unlockedMounts.Contains(mountData.id));
        bool isEquipped = string.Equals(data?.equippedMount, mountData.id, StringComparison.OrdinalIgnoreCase);

        if (nameLabel != null) nameLabel.text = mountData.displayName;

        if (statsLabel != null)
        {
            statsLabel.text = $"<b>Speed:</b> {mountData.maxSpeed:F1} m/s (Charge {mountData.chargeSpeed:F1})\n" +
                              $"<b>Trample Dmg:</b> {mountData.chargeDamage:F0}  |  <b>Knockback:</b> {mountData.knockbackForce:F0}\n" +
                              $"<b>Duration:</b> {mountData.mountDuration:F0}s  |  <b>Cooldown:</b> {mountData.mountCooldown:F0}s\n" +
                              $"<b>Cast Time:</b> {mountData.castTime:F1}s";
        }

        if (descriptionLabel != null)
        {
            descriptionLabel.text = mountData.description;
        }

        if (DemoConfigSO.IsMountLocked(mountData))
        {
            if (statusLabel != null) statusLabel.text = "";
            if (costLabel != null)
            {
                costLabel.text = DemoConfigSO.LockedLabel;
                costLabel.color = Color.white;
            }
            if (interactable != null)
            {
                interactable.PromptText = DemoConfigSO.LockedPrompt;
                interactable.CanInteract = false;
            }
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
            if (interactable != null)
            {
                interactable.PromptText = "Equipped";
                interactable.CanInteract = false;
            }
        }
        else if (isUnlocked)
        {
            if (statusLabel != null)
            {
                statusLabel.text = "UNLOCKED";
                statusLabel.color = Color.white;
            }
            if (costLabel != null) costLabel.text = "";
            if (interactable != null)
            {
                interactable.PromptText = $"Equip {mountData.displayName}";
                interactable.CanInteract = true;
            }
        }
        else
        {
            int currentMetal = data != null ? data.orcishMetal : 0;
            bool canAfford = currentMetal >= mountData.orcishMetalUnlockCost;

            if (statusLabel != null)
            {
                statusLabel.text = "LOCKED";
                statusLabel.color = new Color(0.9f, 0.4f, 0.4f);
            }
            if (costLabel != null)
            {
                costLabel.text = $"{mountData.orcishMetalUnlockCost} Metal";
                costLabel.color = canAfford ? Color.white : Color.red;
            }
            if (interactable != null)
            {
                interactable.PromptText = $"Unlock {mountData.displayName} ({mountData.orcishMetalUnlockCost} Metal)";
                interactable.CanInteract = canAfford;
            }
        }
    }

    public void OnPedestalInteracted(Player player)
    {
        HandleInteract(player);
    }

    private void HandleInteract(Player player)
    {
        if (mountData == null || DemoConfigSO.IsMountLocked(mountData)) return;

        SaveData data = SaveSystem.Load();
        if (data == null) data = new SaveData();

        bool isUnlocked = (mountData.orcishMetalUnlockCost == 0) ||
                          (data.unlockedMounts != null && data.unlockedMounts.Contains(mountData.id));
        bool isEquipped = string.Equals(data.equippedMount, mountData.id, StringComparison.OrdinalIgnoreCase);

        if (isEquipped) return;

        if (isUnlocked)
        {
            data.equippedMount = mountData.id;
            SaveSystem.Save(data);
            Debug.Log($"[MountPedestal] Equipped mount: {mountData.displayName}!");
            RefreshAllPedestals();
        }
        else
        {
            if (data.orcishMetal >= mountData.orcishMetalUnlockCost)
            {
                data.orcishMetal -= mountData.orcishMetalUnlockCost;
                if (data.unlockedMounts == null) data.unlockedMounts = new System.Collections.Generic.List<string>();
                if (!data.unlockedMounts.Contains(mountData.id))
                {
                    data.unlockedMounts.Add(mountData.id);
                }
                data.equippedMount = mountData.id;
                SaveSystem.Save(data);
                Debug.Log($"[MountPedestal] Unlocked and equipped mount: {mountData.displayName} for {mountData.orcishMetalUnlockCost} Metal!");
                RefreshAllPedestals();
            }
            else
            {
                Debug.LogWarning($"[MountPedestal] Cannot afford {mountData.displayName}. Costs {mountData.orcishMetalUnlockCost}, has {data.orcishMetal}.");
            }
        }
    }

    private static void RefreshAllPedestals()
    {
        MountPedestal[] all = FindObjectsByType<MountPedestal>(FindObjectsSortMode.None);
        foreach (var p in all)
        {
            p.RefreshPedestal();
        }
    }
}
