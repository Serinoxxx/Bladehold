using System;
using UnityEngine;

public class RangerUltimate : MonoBehaviour, IUltimateHandler
{
    [Header("Config")]
    [Tooltip("Configuration ScriptableObject defining base duration and metadata.")]
    [SerializeField] private UltimateConfigSO config;

    public float BaseDuration => config != null && config.baseDuration > 0f ? config.baseDuration : 5f;

    private Player player;
    private PlayerBow bow;
    private float ultimateEndTime;
    private float nextFireTime;
    
    private PlayerUltimateController controller;
    private bool isRunning;

    private void Awake()
    {
        FindDependencies();
    }

    private void Start()
    {
        FindDependencies();
    }

    private void FindDependencies()
    {
        Transform rootTr = transform.root;
        if (player == null) player = rootTr.GetComponentInChildren<Player>(true) ?? GetComponentInParent<Player>() ?? GetComponentInChildren<Player>(true) ?? Player.Instance;
        if (bow == null) bow = rootTr.GetComponentInChildren<PlayerBow>(true) ?? GetComponentInParent<PlayerBow>() ?? GetComponentInChildren<PlayerBow>(true);
    }

    public void Activate(PlayerUltimateController controller)
    {
        this.controller = controller;
        FindDependencies();
        float duration = player != null && player.Stats != null ? player.Stats.GetValue(StatType.UltimateDurationSeconds) : BaseDuration;
        if (duration <= 0f) duration = BaseDuration;
        ultimateEndTime = Time.time + duration;
        isRunning = true;
        
        if (bow != null)
        {
            bow.IsUltimateLocked = true;
            bow.ForceStartAim();
        }
    }

    private void Update()
    {
        if (!isRunning) return;

        if (Time.time >= ultimateEndTime)
        {
            End();
            return;
        }

        if (bow != null && Time.time >= nextFireTime)
        {
            float fireRate = player.Stats.GetValue(StatType.UltimateRangerFireRate);
            if (fireRate <= 0f) fireRate = 0.05f; // Fallback
            
            nextFireTime = Time.time + fireRate;
            bow.ForceFire();
        }
    }

    private void End()
    {
        isRunning = false;
        if (bow != null)
        {
            bow.IsUltimateLocked = false;
            bow.ForceEndAim();
        }
        controller?.EndUltimate();
    }
}
