using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Escalating charge-up feedback for the sword's charged attack. <see cref="PlayerAttack" /> already
///     owns the charge timing (<see cref="PlayerAttack.IsCharging" />/<see cref="PlayerAttack.ChargeLevel" />);
///     this component just polls it (same style as <see cref="PlayerMoveSpeedBinder" /> polls stats) and
///     plays the next <see cref="MMF_Player" /> in <see cref="chargeStages" /> each time the hold gains
///     another charge level, so the spark/SFX gets bigger the longer the attack is held. Stage N plays when
///     level N+1 is reached; levels beyond the array just keep the last stage's look.
///
///     In parallel it toggles <see cref="chargeEffects" />: while charging it <c>SetActive</c>s only the
///     GameObject for the current charge level (level 1 → element 0, hiding the rest; levels beyond the array
///     keep the last one), and it disables them all the moment the hold ends — i.e. when the player actually
///     swings (or the charge is otherwise released).
/// </summary>
public class SwordChargeFeedback : MonoBehaviour
{
    [SerializeField] private PlayerAttack playerAttack;

    [Tooltip("Played in order as the charge crosses each stage's threshold (evenly split across 0..1).")]
    [SerializeField] private MMF_Player[] chargeStages;

    [Tooltip("One GameObject per charge level. Only the current level's object is active while charging; all off once the swing fires.")]
    [SerializeField] private GameObject[] chargeEffects;

    private int lastPlayedStage = -1;
    private int activeEffect = -1;
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

        // Start from a known state — nothing showing until a hold gains its first level.
        HideAllChargeEffects();
    }

    private void Update()
    {
        if (anyError) return;

        if (!playerAttack.IsCharging)
        {
            lastPlayedStage = -1;
            // Charging ended (the swing fired, or the hold was cancelled): turn every effect off.
            HideAllChargeEffects();
            return;
        }

        // Level 1 plays stage 0, level 2 plays stage 1, ... (level 0 = nothing yet).
        int stage = Mathf.Min(playerAttack.ChargeLevel, chargeStages.Length) - 1;
        while (lastPlayedStage < stage)
        {
            lastPlayedStage++;
            chargeStages[lastPlayedStage]?.PlayFeedbacks();
        }

        // Show only the GameObject for the current level (level 1 -> element 0), hiding the others;
        // levels beyond the array keep the last element. Level 0 (still winding up) shows nothing.
        int level = playerAttack.ChargeLevel;
        if (level >= 1 && chargeEffects != null && chargeEffects.Length > 0)
        {
            ShowOnlyChargeEffect(Mathf.Min(level - 1, chargeEffects.Length - 1));
        }
        else
        {
            HideAllChargeEffects();
        }
    }

    private void ShowOnlyChargeEffect(int index)
    {
        if (activeEffect == index) return;

        for (int i = 0; i < chargeEffects.Length; i++)
        {
            if (chargeEffects[i] != null)
            {
                chargeEffects[i].SetActive(i == index);
            }
        }
        activeEffect = index;
    }

    private void HideAllChargeEffects()
    {
        if (activeEffect == -1 || chargeEffects == null) return;

        for (int i = 0; i < chargeEffects.Length; i++)
        {
            if (chargeEffects[i] != null)
            {
                chargeEffects[i].SetActive(false);
            }
        }
        activeEffect = -1;
    }
}
