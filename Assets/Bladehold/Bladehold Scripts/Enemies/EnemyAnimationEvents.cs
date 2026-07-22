using UnityEngine;

/// <summary>
///     Receives animation events fired by third-party enemy clips (like FootL, FootR, Hit)
///     so Unity doesn't log missing-receiver warnings. Placed on the rig child next to the Animator.
/// </summary>
public class EnemyAnimationEvents : MonoBehaviour
{
    public void FootL() { }
    public void FootR() { }
    public void Footstep() { }
    
    // The player uses AnimationEvents -> DamageTrigger for frame-perfect hits, but enemies currently
    // use AIAttack.cs which handles its own damage application via a wind-up coroutine.
    // This empty placeholder suppresses the warning.
    public void Hit() { }
}
