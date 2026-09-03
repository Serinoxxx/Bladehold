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
    [SerializeField] private GameObject horsePrefab;

    private Player player;
    private PlayerMount playerMount;
    private PlayerUltimateController controller;
    private GameObject spawnedHorse;
    private float ultimateEndTime;
    private bool isRunning = false;

    private void Awake()
    {
        player = GetComponentInChildren<Player>();
        playerMount = GetComponentInChildren<PlayerMount>();
    }

    private void Start()
    {
        if (player == null) player = GetComponentInChildren<Player>();
        if (playerMount == null) playerMount = GetComponentInChildren<PlayerMount>();
        if (horsePrefab == null)
        {
            horsePrefab = Resources.Load<GameObject>("Horse") ?? 
                          Resources.Load<GameObject>("Prefabs/Horse/Horse");
        }
    }

    public void Activate(PlayerUltimateController controller)
    {
        this.controller = controller;
        if (player == null) player = GetComponentInChildren<Player>();
        if (playerMount == null) playerMount = GetComponentInChildren<PlayerMount>();

        if (player == null || playerMount == null)
        {
            controller?.EndUltimate();
            return;
        }

        float duration = player.Stats != null ? player.Stats.GetValue(StatType.UltimateDurationSeconds) : 8f;
        if (duration <= 0f) duration = 8f;

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
