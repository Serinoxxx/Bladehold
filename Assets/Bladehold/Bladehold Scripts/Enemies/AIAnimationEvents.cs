using MoreMountains.Feedbacks;
using UnityEngine;

public class AIAnimationEvents : MonoBehaviour
{

    [SerializeField] MMF_Player leftFootStomp;
    [SerializeField] MMF_Player rightFootStomp;


    public void LeftFootStomp()
    {
        if (leftFootStomp != null)
        {
            leftFootStomp.PlayFeedbacks();
        }
    }

    public void RightFootStomp()
    {
        if (rightFootStomp != null)
        {
            rightFootStomp.PlayFeedbacks();
        }
    }
}
