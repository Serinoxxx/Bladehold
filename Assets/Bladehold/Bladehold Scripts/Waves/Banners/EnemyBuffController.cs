using UnityEngine;

public class EnemyBuffController : MonoBehaviour
{
    private BannerBuffType activeBuff = BannerBuffType.None;
    private Health health;
    private float regenTimer = 0f;
    private float buffMagnitude = 1f;

    public void Initialize(BannerBuffType buff, float magnitude = -1f)
    {
        activeBuff = buff;
        health = GetComponent<Health>();

        if (health == null) return;

        // Resolve magnitude from Clan SO if available, or fall back to parameter/defaults
        if (magnitude > 0f)
        {
            buffMagnitude = magnitude;
        }
        else if (GameLoopManager.Instance != null && GameLoopManager.Instance.CurrentClanBuffSO != null &&
                 GameLoopManager.Instance.CurrentClanBuffSO.buffType == buff)
        {
            buffMagnitude = GameLoopManager.Instance.CurrentClanBuffSO.buffMagnitude;
        }
        else
        {
            buffMagnitude = GetDefaultMagnitude(buff);
        }

        switch (buff)
        {
            case BannerBuffType.Shield:
                // Buff magnitude e.g. 0.25 -> +25% max health as shield
                float shieldFraction = buffMagnitude > 0f ? buffMagnitude : 0.25f;
                health.SetMaxHealth(health.MaxHealth * (1f + shieldFraction));
                health.Heal(health.MaxHealth * shieldFraction);
                break;

            case BannerBuffType.Haste:
                // Buff magnitude e.g. 1.35 -> +35% move & attack speed
                float hasteMult = buffMagnitude > 1f ? buffMagnitude : (1f + buffMagnitude);
                AIMovement move = GetComponent<AIMovement>();
                if (move != null) move.SetSpeedMultiplier(hasteMult);
                
                Animator anim = GetComponentInChildren<Animator>();
                if (anim != null) anim.speed *= hasteMult;
                break;

            case BannerBuffType.Berserk:
                health.SetMaxHealth(health.MaxHealth * 0.7f);
                AIAttack attack = GetComponent<AIAttack>();
                float atkMult = buffMagnitude > 1f ? buffMagnitude : 1.5f;
                if (attack != null) attack.SetDamageMultiplier(atkMult);
                break;

            case BannerBuffType.Regen:
                // Passive flat regen per second (e.g. 2 HP/s)
                if (buffMagnitude <= 0f) buffMagnitude = 2f;
                break;

            case BannerBuffType.Armor:
                health.ScaleDamageTaken += HandleDamageTaken_Armor;
                break;
        }
    }

    private float GetDefaultMagnitude(BannerBuffType type)
    {
        return type switch
        {
            BannerBuffType.Shield => 0.25f,
            BannerBuffType.Haste => 1.35f,
            BannerBuffType.Berserk => 1.5f,
            BannerBuffType.Regen => 2.0f, // "Enemies heal 2hp/s"
            BannerBuffType.Armor => 0.25f, // 25% damage reduction
            _ => 1.0f
        };
    }

    private void Update()
    {
        if (activeBuff == BannerBuffType.Regen && health != null && !health.IsDead)
        {
            regenTimer += Time.deltaTime;
            if (regenTimer >= 1f)
            {
                regenTimer = 0f;
                health.Heal(buffMagnitude);
            }
        }
    }

    private float HandleDamageTaken_Armor(Damage damage)
    {
        float reduction = Mathf.Clamp(buffMagnitude > 0f && buffMagnitude < 1f ? buffMagnitude : 0.25f, 0f, 0.9f);
        return 1f - reduction;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.ScaleDamageTaken -= HandleDamageTaken_Armor;
        }
    }
}
