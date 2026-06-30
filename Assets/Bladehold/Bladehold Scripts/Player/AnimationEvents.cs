using UnityEngine;

public class AnimationEvents : MonoBehaviour
{
    [SerializeField] DamageTrigger oneHandedSwordDamageTrigger;
    public void OneHandedSwordAttack()
    {
        oneHandedSwordDamageTrigger.Activate();
    }
}
