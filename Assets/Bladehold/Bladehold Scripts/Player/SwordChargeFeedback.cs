using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Escalating charge-up feedback for the sword's charged attack. <see cref="PlayerAttack" /> already
///     owns the charge timing (<see cref="PlayerAttack.IsCharging" />/<see cref="PlayerAttack.ChargeLevel" />);
///     this component just polls it (same style as <see cref="PlayerMoveSpeedBinder" /> polls stats) and
///     plays the next <see cref="MMF_Player" /> in <see cref="chargeStages" /> each time the hold gains
///     another charge level, so the spark/SFX gets bigger the longer the attack is held. Stage N plays when
///     level N+1 is reached; levels beyond the array just keep the last stage's look.
/// </summary>
public class SwordChargeFeedback : MonoBehaviour
{
    [SerializeField] private PlayerAttack playerAttack;

    [Tooltip("Played in order as the charge crosses each stage's threshold (evenly split across 0..1).")]
    [SerializeField] private MMF_Player[] chargeStages;

    private int lastPlayedStage = -1;
    private bool anyError = false;

    private void OnValidate()
    {
        if (playerAttack == null)
        {
            playerAttack = GetComponent<PlayerAttack>();
        }
    }

    private void Start()
    {
        if (playerAttack == null)
        {
            Debug.LogError("PlayerAttack is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (chargeStages == null || chargeStages.Length == 0)
        {
            Debug.LogError("No charge stage MMF_Players assigned; charge feedback disabled.");
            anyError = true;
        }
    }

    private void Update()
    {
        if (anyError) return;

        if (!playerAttack.IsCharging)
        {
            lastPlayedStage = -1;
            return;
        }

        // Level 1 plays stage 0, level 2 plays stage 1, ... (level 0 = nothing yet).
        int stage = Mathf.Min(playerAttack.ChargeLevel, chargeStages.Length) - 1;
        while (lastPlayedStage < stage)
        {
            lastPlayedStage++;
            chargeStages[lastPlayedStage]?.PlayFeedbacks();
        }
    }
}
