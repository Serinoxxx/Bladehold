using UnityEngine;

/// <summary>
///     Tunables for the Berserker's rage meter, consumed by <see cref="RageBuff" />. Rage is
///     <b>innate</b> to the class — the effect magnitudes below are authored here rather than being
///     base-0-locked stats — while how fast rage builds and how long it lingers go through
///     <see cref="StatType.RageGainMultiplier" />/<see cref="StatType.RageRetentionMultiplier" />
///     (base 1.0) so skill nodes can improve them.
/// </summary>
[CreateAssetMenu(fileName = "RageSO", menuName = "Scriptable Objects/RageSO")]
public class RageSO : ScriptableObject
{
    [Header("Meter")]
    [Tooltip("Rage points at a full meter.")]
    public float maxRage = 100f;

    [Tooltip("Rage gained per point of damage the Berserker deals (melee and thrown axe).")]
    public float ragePerDamageDealt = 1f;

    [Tooltip("Rage gained per point of damage the Berserker takes — higher than dealing, the take-damage-to-deal-damage fantasy.")]
    public float ragePerDamageTaken = 4f;

    [Header("Decay")]
    [Tooltip("Seconds without gaining rage before it starts draining. Scaled up by RageRetentionMultiplier.")]
    public float decayDelaySeconds = 3f;

    [Tooltip("Rage points drained per second once decay starts. Scaled down by RageRetentionMultiplier.")]
    public float decayPerSecond = 10f;

    [Header("Effects at a full meter (scale linearly with the meter)")]
    [Tooltip("Extra damage fraction at full rage (0.5 = +50%). Applied to melee and thrown-axe damage.")]
    public float damageBonusAtFullRage = 0.5f;

    [Tooltip("Extra move speed fraction at full rage (0.2 = +20%), applied as a MoveSpeed percent modifier.")]
    public float moveSpeedBonusAtFullRage = 0.2f;

    [Tooltip("Fraction of incoming damage prevented at full rage (0.3 = 30% reduction), via Health.ScaleDamageTaken.")]
    public float damageReductionAtFullRage = 0.3f;
}
