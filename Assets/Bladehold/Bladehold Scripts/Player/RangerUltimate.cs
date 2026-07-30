using System;
using UnityEngine;

public class RangerUltimate : MonoBehaviour, IUltimateHandler
{
    private Player player;
    private PlayerBow bow;
    private float ultimateEndTime;
    private float nextFireTime;
    
    private PlayerUltimateController controller;
    private bool isRunning;

    private void Awake()
    {
        player = GetComponent<Player>();
        bow = GetComponentInChildren<PlayerBow>();
    }

    public void Activate(PlayerUltimateController controller)
    {
        this.controller = controller;
        float duration = player.Stats.GetValue(StatType.UltimateDurationSeconds);
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
