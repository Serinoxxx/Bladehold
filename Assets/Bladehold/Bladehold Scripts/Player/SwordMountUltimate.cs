using System;
using UnityEngine;

/// <summary>
///     Sword Melee Ultimate: Warhorse Mount / Cavalry Charge.
///     Summons a warhorse on command and immediately mounts it.
///     The horse absorbs all incoming damage, tramples enemies with high speed,
///     and boosts sword reach for the duration of the ultimate.
/// </summary>
public class SwordMountUltimate : MonoBehaviour, IUltimateHandler
{
    [Header("Config")]
    [Tooltip("Configuration ScriptableObject defining base duration and metadata.")]
    [SerializeField] private UltimateConfigSO config;

    public float BaseDuration => config != null && config.baseDuration > 0f ? config.baseDuration : 15f;

    [SerializeField] private GameObject horsePrefab;

    private Player player;
    private PlayerMount playerMount;
    private PlayerUltimateController controller;
    private GameObject spawnedHorse;
    private float ultimateEndTime;
    private bool isRunning = false;

    private void Awake()
    {
        FindDependencies();
    }

    private void Start()
    {
        FindDependencies();
        if (horsePrefab == null)
        {
            horsePrefab = Resources.Load<GameObject>("Horse") ?? 
                          Resources.Load<GameObject>("Prefabs/Horse/Horse");
        }
    }

    private void FindDependencies()
    {
        Transform rootTr = transform.root;
        if (player == null) player = rootTr.GetComponentInChildren<Player>(true) ?? GetComponentInParent<Player>() ?? GetComponentInChildren<Player>(true) ?? Player.Instance;
        if (playerMount == null) playerMount = rootTr.GetComponentInChildren<PlayerMount>(true) ?? GetComponentInParent<PlayerMount>() ?? GetComponentInChildren<PlayerMount>(true);
    }

    public void Activate(PlayerUltimateController controller)
    {
        this.controller = controller;
        FindDependencies();

        if (player == null || playerMount == null)
        {
            Debug.LogError($"[SwordMountUltimate] Missing player ({player}) or playerMount ({playerMount})!");
            controller?.EndUltimate();
            return;
        }

        float duration = player.Stats != null ? player.Stats.GetValue(StatType.UltimateDurationSeconds) : BaseDuration;
        if (duration <= 0f) duration = BaseDuration;

        ultimateEndTime = Time.time + duration;
        ultimateStartTime = Time.time;
        isRunning = true;

        // Spawn horse right at player
        Vector3 spawnPos = player.transform.position;
        Quaternion spawnRot = player.transform.rotation;

        if (horsePrefab != null)
        {
            spawnedHorse = Instantiate(horsePrefab, spawnPos, spawnRot);
        }
        else
        {
            // Fallback load via Resources or AssetDatabase
            GameObject fallbackPrefab = Resources.Load<GameObject>("Horse");
            if (fallbackPrefab != null)
            {
                spawnedHorse = Instantiate(fallbackPrefab, spawnPos, spawnRot);
            }
            else
            {
                // Create a basic horse GameObject structure if no prefab is found
                Debug.LogWarning("[SwordMountUltimate] Horse prefab not assigned; finding scene horse or creating fallback.");
                HorseMotor sceneHorse = FindAnyObjectByType<HorseMotor>();
                if (sceneHorse != null)
                {
                    playerMount.TryMount(sceneHorse);
                    return;
                }
            }
        }

        if (spawnedHorse != null)
        {
            HorseMotor horseMotor = spawnedHorse.GetComponentInChildren<HorseMotor>();
            if (horseMotor != null)
            {
                playerMount.TryMount(horseMotor);
            }
        }

        Debug.Log("[SwordMountUltimate] Warhorse Cavalry Charge activated!");
    }

    private float ultimateStartTime;

    private void Update()
    {
        if (!isRunning) return;

        if (Time.time >= ultimateEndTime || (Time.time > ultimateStartTime + 1.0f && playerMount != null && !playerMount.IsMounted))
        {
            End();
        }
    }

    private void End()
    {
        if (!isRunning) return;
        isRunning = false;

        if (playerMount != null && playerMount.IsMounted)
        {
            playerMount.Dismount();
        }

        if (spawnedHorse != null)
        {
            Destroy(spawnedHorse, 0.5f);
            spawnedHorse = null;
        }

        controller?.EndUltimate();
        Debug.Log("[SwordMountUltimate] Warhorse Cavalry Charge ended.");
    }

    private void OnDisable()
    {
        if (isRunning) End();
    }

    private void OnDestroy()
    {
        if (isRunning) End();
    }
}
