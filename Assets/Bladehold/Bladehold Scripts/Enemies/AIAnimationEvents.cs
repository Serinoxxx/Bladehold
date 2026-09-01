using MoreMountains.Feedbacks;
using System;
using UnityEngine;

public class AIAnimationEvents : MonoBehaviour
{

    [SerializeField] MMF_Player leftFootStomp;
    [SerializeField] MMF_Player rightFootStomp;

    public Action OnLeftFootStomp;
    public Action OnRightFootStomp;

    public void LeftFootStomp()
    {
        if (leftFootStomp != null)
        {
            leftFootStomp.PlayFeedbacks();
        }
        OnLeftFootStomp?.Invoke();
    }

    public void RightFootStomp()
    {
        if (rightFootStomp != null)
        {
            rightFootStomp.PlayFeedbacks();
        }
        OnRightFootStomp?.Invoke();
    }
}
