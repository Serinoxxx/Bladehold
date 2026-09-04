using UnityEngine;

public class EnemyBuffController : MonoBehaviour
{
    private BannerBuffType activeBuff = BannerBuffType.None;
    private Health health;
    private float regenTimer = 0f;
    private float maxHp = 0f;

    public void Initialize(BannerBuffType buff)
    {
        activeBuff = buff;
        health = GetComponent<Health>();

        if (health == null) return;

        maxHp = health.MaxHealth;

        switch (buff)
        {
            case BannerBuffType.Shield:
                health.SetMaxHealth(health.MaxHealth * 1.25f);
                health.Heal(health.MaxHealth * 0.25f); // Simplified "Shield" as extra health.
                break;
            case BannerBuffType.Haste:
                AIMovement move = GetComponent<AIMovement>();
                if (move != null) move.SetSpeedMultiplier(1.35f);
                
                Animator anim = GetComponentInChildren<Animator>();
                if (anim != null) anim.speed *= 1.35f;
                break;
            case BannerBuffType.Berserk:
                health.SetMaxHealth(health.MaxHealth * 0.7f);
                AIAttack attack = GetComponent<AIAttack>();
                if (attack != null) attack.SetDamageMultiplier(1.5f);
                break;
            case BannerBuffType.Regen:
                // Passive regen 3% per sec handled in Update
                break;
            case BannerBuffType.Armor:
                // Handled in a damage hook if possible, otherwise we hook into Health.
                health.ScaleDamageTaken += HandleDamageTaken_Armor;
                break;
        }
    }

    private void Update()
    {
        if (activeBuff == BannerBuffType.Regen && health != null && !health.IsDead)
        {
            regenTimer += Time.deltaTime;
            if (regenTimer >= 1f)
            {
                regenTimer = 0f;
                health.Heal(maxHp * 0.03f);
            }
        }
    }

    private float HandleDamageTaken_Armor(Damage damage)
    {
        return 0.75f; // Take 25% reduced damage
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.ScaleDamageTaken -= HandleDamageTaken_Armor;
        }
    }
}
