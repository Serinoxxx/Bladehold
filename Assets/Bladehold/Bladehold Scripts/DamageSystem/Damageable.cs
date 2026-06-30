using UnityEngine;

public interface IDamageable
{
    public void ReceiveDamage(Damage damage);
}

public class Damage
{
    public float value;
    public DamageType type;

}

public enum DamageType
{
    sharp = 0,
    blunt = 1,
    elemental = 2
}
